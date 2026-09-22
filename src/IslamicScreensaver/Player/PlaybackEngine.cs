using System.IO;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Playlist;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Core.Timing;
using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlaybackState = IslamicScreensaver.Core.Playlist.PlaybackState;

namespace IslamicScreensaver.Player;

/// <summary>
/// Plays the playlist as one continuous stream and reports which verse and word are being recited.
/// The position used for lookups comes from the output device — frames it has actually rendered —
/// so text never runs ahead of what is heard, whatever the MP3's bitrate does.
/// Public members run on the dispatcher thread.
/// </summary>
internal sealed class PlaybackEngine : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);
    private const int OutputLatencyMs = 100;

    private readonly ContentCache _cache;
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<PlaylistItem> _playlist;
    private readonly Dictionary<int, Chapter> _chapters;
    private readonly Dictionary<int, string> _quranComNames;
    private readonly Catalog _catalog;
    private readonly DispatcherTimer _tick;
    private readonly int _startIndex;
    private readonly TimeSpan _startOffset;

    private SegmentAudioSource? _source;
    private IWavePlayer? _output;
    private IReadOnlyList<AudioSegment> _segments = [];
    private int _loopStart;
    private int _segmentIndex = -1;
    private int _index = -1;

    // Per-surah context.
    private VerseClock _clock = new([]);
    private IReadOnlyList<VerseText> _verses = [];
    private SurahTranslation? _translation;
    private VerseClock.Position _shown = new(int.MinValue, 0);
    private bool _disposed;

    public PlaybackEngine(ContentCache cache, AppSettings settings, IReadOnlyList<PlaylistItem> playlist, int startIndex, PlaybackState? resumeFrom, Catalog? catalog = null)
    {
        _cache = cache;
        _settings = settings;
        _playlist = playlist;
        _catalog = catalog ?? Catalog.Empty;
        _startIndex = Math.Clamp(startIndex, 0, Math.Max(0, playlist.Count - 1));

        if (resumeFrom is not null && playlist.Count > 0 && playlist[_startIndex].Surah == resumeFrom.Surah && resumeFrom.VerseIndex >= 0)
        {
            var item = playlist[_startIndex];
            var clock = new VerseClock(cache.ReadTimings(item.Recitation, item.Surah) ?? []);
            _startOffset = TimeSpan.FromMilliseconds(clock.StartOf(resumeFrom.VerseIndex));
        }

        _chapters = (cache.ReadChapters() ?? []).ToDictionary(c => c.Number);
        _quranComNames = (cache.ReadReciters() ?? []).ToDictionary(r => r.Id, r => r.DisplayName);

        _tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TickInterval };
        _tick.Tick += (_, _) => Poll();
    }

    /// <summary>Raised when the verse on screen should change (including the bismillah before verse 1).</summary>
    public event Action<VerseDisplay>? VerseChanged;

    /// <summary>Raised when the recited word within the current verse changes. 0 = none yet.</summary>
    public event Action<int>? WordChanged;

    /// <summary>Raised every tick with 0..1 progress through the current verse, for the panel's hairline.</summary>
    public event Action<double>? VerseProgress;

    public PlaylistItem? Current => _index >= 0 && _index < _playlist.Count ? _playlist[_index] : null;

    /// <summary>For diagnostics: the audio position the listener is hearing, if playing.</summary>
    public TimeSpan? HeardPosition => Heard()?.Position;

    public PlaybackState? CurrentState => Current is { } item
        ? new PlaybackState { RecitationId = item.Recitation.ToString(), Surah = item.Surah, VerseIndex = Math.Max(-1, _shown.VerseIndex) }
        : null;

    public void Start()
    {
        if (_playlist.Count == 0)
            return;

        MediaFoundationApi.Startup();

        // Asked of the surah's own timings rather than the catalog: whether a recording opens with
        // the basmalah varies surah by surah, and the file on disk is the only thing that knows.
        var steps = RecitationProgram.Build(
            _playlist, _startIndex,
            startsMidSurah: _startOffset > TimeSpan.Zero,
            audioHasBasmalah: item => RecordedBasmalah.IsInAudio(_cache, item.Recitation, item.Surah));
        _segments = SegmentBuilder.Build(steps, _cache);
        if (_segments.Count == 0)
        {
            Log.Error("No playable audio in the program; nothing to start.");
            return;
        }

        // The loop point is expressed in steps; a step with no audio (a missing isti'ādhah recording)
        // is dropped, so translate it into the segments that actually exist.
        _loopStart = LoopStartSegment(steps);

        // The resume offset belongs to the surah being resumed, which sits after the opening isti'ādhah.
        var resumeSegment = _startOffset > TimeSpan.Zero
            ? _segments.Select((s, i) => (s, i)).FirstOrDefault(x => x.s.IsSurah).i
            : 0;

        _source = new SegmentAudioSource(_segments, _loopStart, _startOffset, resumeSegment);
        var volume = new VolumeSampleProvider(_source) { Volume = (float)_settings.Volume };

        _output = CreateOutput(_settings.OutputDeviceId);
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(volume);
        _output.Play();

        var preludes = _segments.Count(s => !s.IsSurah);
        Log.Info($"Program: {_segments.Count} segment(s), {preludes} prelude(s)" +
                 (_startOffset > TimeSpan.Zero ? $", resuming {_playlist[_startIndex].Recitation}/{_playlist[_startIndex].Surah} at {_startOffset.TotalSeconds:0.0}s (segment {resumeSegment})" : "") + ".");

        _tick.Start();
        Poll();
    }

    private int LoopStartSegment(IReadOnlyList<RecitationStep> steps)
    {
        if (RecitationProgram.LoopStart(steps) == 0)
            return 0;

        // Skip a leading isti'ādhah segment if one was actually produced.
        return _segments.Count > 0 && _segments[0].Part == RecitationPart.Istiadhah && _segments.Count > 1 ? 1 : 0;
    }

    /// <summary>Builds the output, honouring the chosen device and falling back rather than going silent.</summary>
    internal static IWavePlayer CreateOutput(string? deviceId)
    {
        var device = AudioDevices.Resolve(deviceId);
        try
        {
            var builder = new WasapiPlayerBuilder()
                .WithSharedMode()
                .WithEventSync()
                .WithLatency(OutputLatencyMs)
                .WithCategory(AudioStreamCategory.Media);

            if (device is not null)
            {
                builder = builder.WithDevice(device);
                Log.Info($"Audio output: {device.FriendlyName}.");
            }
            else
            {
                // No device pinned (or the pinned one is gone): follow Windows, including later default changes.
                builder = builder.WithDefaultDeviceStreamRouting();
            }

            return builder.Build();
        }
        catch (Exception ex)
        {
            Log.Error("WASAPI output unavailable; falling back to WaveOut.", ex);
            device?.Dispose();
            return new WaveOut { BufferMilliseconds = OutputLatencyMs };
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!_disposed && e.Exception is not null)
            Log.Error("Audio output stopped unexpectedly.", e.Exception);
    }

    /// <summary>Playback position of the audio the listener is hearing right now.</summary>
    private (AudioSegment Segment, int SegmentIndex, TimeSpan Position)? Heard()
    {
        if (_output is not { } output || _source is null || output.OutputWaveFormat is not { } format)
            return null;

        var bytes = ((IWavePosition)output).GetPosition();
        var seconds = (double)bytes / format.AverageBytesPerSecond;
        return _source.Locate((long)(seconds * SegmentAudioSource.SampleRate));
    }

    private void Poll()
    {
        if (_disposed || Heard() is not { } heard)
            return;

        if (heard.SegmentIndex != _segmentIndex)
            EnterSegment(heard.SegmentIndex, heard.Segment);

        if (!heard.Segment.IsSurah)
        {
            // A prelude has no verses; keep the panel on its text and run the hairline across it.
            if (heard.Segment.Duration is { } duration && duration > TimeSpan.Zero)
                VerseProgress?.Invoke(Math.Clamp((heard.Position - heard.Segment.Start) / duration, 0, 1));
            return;
        }

        var ms = (int)heard.Position.TotalMilliseconds - _settings.SyncOffsetMs;
        var position = _clock.Lookup(ms);
        if (position.VerseIndex != _shown.VerseIndex)
        {
            _shown = position;
            VerseChanged?.Invoke(BuildDisplay(position.VerseIndex));
            WordChanged?.Invoke(position.WordPosition);
        }
        else if (position.WordPosition != _shown.WordPosition)
        {
            _shown = position;
            WordChanged?.Invoke(position.WordPosition);
        }

        VerseProgress?.Invoke(_clock.Progress(position.VerseIndex, ms));
    }

    private void EnterSegment(int segmentIndex, AudioSegment segment)
    {
        _segmentIndex = segmentIndex;
        var item = segment.Item;

        if (!segment.IsSurah)
        {
            var chapter = ChapterFor(item.Surah);
            Log.Info($"Playing the {(segment.Part == RecitationPart.Istiadhah ? "istiadhah" : "basmalah")} before {chapter.NameSimple}.");
            VerseChanged?.Invoke(VerseDisplay.Prelude(
                segment.Part == RecitationPart.Istiadhah,
                chapter.NameArabic, chapter.NameSimple, chapter.VersesCount,
                ReciterNameFor(item.Recitation),
                _settings.ShowTranslation));
            WordChanged?.Invoke(0);
            VerseProgress?.Invoke(0);
            return;
        }

        _index = segment.PlaylistIndex;
        _clock = new VerseClock(_cache.ReadTimings(item.Recitation, item.Surah) ?? []);
        _verses = _cache.ReadSurahText(item.Surah) ?? [];
        _translation = _settings.ShowTranslation ? _cache.ReadSurahTranslation(_settings.TranslationId, item.Surah) : null;
        _shown = new VerseClock.Position(int.MinValue, 0);
        Log.Info($"Playing {item.Recitation}/{item.Surah} ({ChapterFor(item.Surah).NameSimple}).");
    }

    private VerseDisplay BuildDisplay(int verseIndex)
    {
        var item = _playlist[_index];
        var chapter = ChapterFor(item.Surah);
        var reciter = ReciterNameFor(item.Recitation);

        if (verseIndex < 0 || verseIndex >= _clock.VerseCount)
        {
            return new VerseDisplay(chapter.NameArabic, chapter.NameSimple, 0, chapter.VersesCount,
                VerseDisplay.BismillahText, [], null, reciter, IsBismillah: true);
        }

        var key = _clock[verseIndex].Key;
        var verse = _verses.FirstOrDefault(v => v.Key == key);
        var arabic = verse?.TextUthmani ?? key.ToString();
        var words = verse?.Words ?? [];
        var translation = _translation is { } t && key.Ayah - 1 < t.Texts.Count ? t.Texts[key.Ayah - 1] : null;

        return new VerseDisplay(chapter.NameArabic, chapter.NameSimple, key.Ayah, chapter.VersesCount,
            arabic, words, translation, reciter, IsBismillah: false);
    }

    /// <summary>
    /// The name shown on screen. The catalog knows every reciter; the cached Quran.com list is the
    /// fallback for a library downloaded before the catalog shipped, and the id itself the last resort.
    /// </summary>
    private string ReciterNameFor(RecitationId recitation)
    {
        if (_catalog.ReciterOf(recitation) is { } reciter)
            return reciter.Name(Strings.IsArabic);

        return recitation.Source == RecitationSource.QuranCom && _quranComNames.TryGetValue(recitation.Number, out var name)
            ? name
            : recitation.ToString();
    }

    private Chapter ChapterFor(int surah) =>
        _chapters.GetValueOrDefault(surah) ?? new Chapter(surah, string.Empty, $"Surah {surah}", string.Empty, _clock.VerseCount, surah is not (1 or 9));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _tick.Stop();
        try
        {
            _output?.Stop();
            _output?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("Error stopping audio output.", ex);
        }
        _source?.Dispose();
    }
}
