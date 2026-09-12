using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Core.Timing;

namespace IslamicScreensaver.Player;

/// <summary>
/// Plays the playlist surah by surah and reports which verse and word are being recited.
/// Two <see cref="MediaPlayer"/>s alternate so the next surah is already opened when the current one ends.
/// Everything here runs on the dispatcher thread.
/// </summary>
internal sealed class PlaybackEngine : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);

    private readonly ContentCache _cache;
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<PlaylistItem> _playlist;
    private readonly Dictionary<int, Chapter> _chapters;
    private readonly Dictionary<int, string> _reciterNames;
    private readonly DispatcherTimer _tick;

    private MediaPlayer _player = new();
    private MediaPlayer _standby = new();
    private int _index;
    private int _resumeVerseIndex = -1;
    private TimeSpan? _pendingSeek;

    // Per-surah context.
    private VerseClock _clock = new([]);
    private IReadOnlyList<VerseText> _verses = [];
    private SurahTranslation? _translation;
    private VerseClock.Position _shown = new(int.MinValue, 0);
    private bool _disposed;

    public PlaybackEngine(ContentCache cache, AppSettings settings, IReadOnlyList<PlaylistItem> playlist, int startIndex, PlaybackState? resumeFrom)
    {
        _cache = cache;
        _settings = settings;
        _playlist = playlist;
        _index = Math.Clamp(startIndex, 0, Math.Max(0, playlist.Count - 1));
        if (resumeFrom is not null && playlist.Count > 0 && playlist[_index].Surah == resumeFrom.Surah)
            _resumeVerseIndex = resumeFrom.VerseIndex;

        _chapters = (cache.ReadChapters() ?? []).ToDictionary(c => c.Number);
        _reciterNames = (cache.ReadReciters() ?? []).ToDictionary(r => r.Id, r => r.DisplayName);

        _tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TickInterval };
        _tick.Tick += (_, _) => Poll();

        foreach (var p in new[] { _player, _standby })
        {
            p.Volume = settings.Volume;
            p.MediaOpened += OnMediaOpened;
            p.MediaEnded += OnMediaEnded;
            p.MediaFailed += OnMediaFailed;
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (!_disposed && ReferenceEquals(sender, _player))
            ApplyPendingSeek();
    }

    private void ApplyPendingSeek()
    {
        if (_pendingSeek is { } seek)
        {
            _pendingSeek = null;
            _player.Position = seek;
        }
    }

    /// <summary>Raised when the verse on screen should change (including the bismillah before verse 1).</summary>
    public event Action<VerseDisplay>? VerseChanged;

    /// <summary>Raised when the recited word within the current verse changes. 0 = none yet.</summary>
    public event Action<int>? WordChanged;

    public PlaylistItem? Current => _playlist.Count > 0 ? _playlist[_index] : null;

    public PlaybackState? CurrentState => Current is { } item
        ? new PlaybackState { ReciterId = item.ReciterId, Surah = item.Surah, VerseIndex = Math.Max(-1, _shown.VerseIndex) }
        : null;

    public void Start()
    {
        if (_playlist.Count == 0)
            return;

        PrepareStandby(_index);
        SwapInStandby();
        _tick.Start();
    }

    private void PrepareStandby(int index)
    {
        var item = _playlist[index];
        var path = _cache.AudioPath(item.ReciterId, item.Surah);
        if (!File.Exists(path))
        {
            Log.Error($"Audio missing for {item.ReciterId}/{item.Surah} despite manifest; skipping.");
            return;
        }
        _standby.Open(new Uri(path));
    }

    private void SwapInStandby()
    {
        (_player, _standby) = (_standby, _player);
        _standby.Close();

        var item = _playlist[_index];
        _clock = new VerseClock(_cache.ReadTimings(item.ReciterId, item.Surah) ?? []);
        _verses = _cache.ReadSurahText(item.Surah) ?? [];
        _translation = _settings.ShowTranslation ? _cache.ReadSurahTranslation(_settings.TranslationId, item.Surah) : null;
        _shown = new VerseClock.Position(int.MinValue, 0);

        if (_resumeVerseIndex >= 0)
        {
            // Open() is asynchronous: seek now if the media is already opened, otherwise once it is.
            _pendingSeek = TimeSpan.FromMilliseconds(_clock.StartOf(_resumeVerseIndex));
            Log.Info($"Resuming at verse index {_resumeVerseIndex} ({_pendingSeek.Value.TotalSeconds:0.0}s).");
            _resumeVerseIndex = -1;
            if (_player.NaturalDuration.HasTimeSpan)
                ApplyPendingSeek();
        }

        Log.Info($"Playing {item.ReciterId}/{item.Surah} ({ChapterFor(item.Surah).NameSimple}).");
        _player.Play();

        // Warm up the following surah while this one plays.
        if (_playlist.Count > 1)
            PrepareStandby((_index + 1) % _playlist.Count);

        Poll();
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (_disposed || !ReferenceEquals(sender, _player))
            return;

        _index = (_index + 1) % _playlist.Count;
        SwapInStandby();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (_disposed)
            return;

        Log.Error($"Playback failed for {(ReferenceEquals(sender, _player) ? "current" : "standby")} surah.", e.ErrorException);
        if (ReferenceEquals(sender, _player))
            OnMediaEnded(sender, EventArgs.Empty);
    }

    private void Poll()
    {
        if (_disposed || _playlist.Count == 0)
            return;

        var position = _clock.Lookup((int)_player.Position.TotalMilliseconds);
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
    }

    private VerseDisplay BuildDisplay(int verseIndex)
    {
        var item = _playlist[_index];
        var chapter = ChapterFor(item.Surah);
        var reciter = _reciterNames.GetValueOrDefault(item.ReciterId, $"Reciter {item.ReciterId}");

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

    private Chapter ChapterFor(int surah) =>
        _chapters.GetValueOrDefault(surah) ?? new Chapter(surah, string.Empty, $"Surah {surah}", string.Empty, _clock.VerseCount, surah is not (1 or 9));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _tick.Stop();
        _player.Close();
        _standby.Close();
    }
}
