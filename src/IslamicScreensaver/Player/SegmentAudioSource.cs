using IslamicScreensaver.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace IslamicScreensaver.Player;

/// <summary>
/// One endless sample stream made of the program's segments back to back, looping at
/// <paramref name="loopStart"/> so a session opener (the isti'ādhah) is not repeated. Every time a
/// segment starts, a marker records the stream sample it started at, so a "samples rendered by the
/// device" count maps back to (segment, position in that file) with sample accuracy — the sound
/// card's own clock being the only one that stays honest with variable-bitrate MP3s.
/// </summary>
/// <remarks><see cref="Read"/> runs on the output device's thread; everything else on the UI thread.</remarks>
internal sealed class SegmentAudioSource : ISampleProvider, IDisposable
{
    public const int SampleRate = 44100;
    public const int Channels = 2;

    private readonly record struct Marker(long StreamFrame, int SegmentIndex, long FileStartFrame);

    private readonly record struct Opened(ISampleProvider Samples, IDisposable Reader, TimeSpan Offset);

    private readonly IReadOnlyList<AudioSegment> _segments;
    private readonly int _loopStart;
    private readonly object _gate = new();
    private readonly List<Marker> _markers = [];

    /// <summary>Where to resume, and which segment it belongs to — used once, not on every lap.</summary>
    private readonly TimeSpan _resumeOffset;
    private readonly int _resumeSegment;
    private bool _resumeUsed;

    private ISampleProvider? _current;
    private IDisposable? _currentReader;
    private int _currentIndex;
    private long _streamFrames;
    private Task<Opened?>? _prefetch;
    private int _prefetchIndex = -1;
    private bool _disposed;

    /// <param name="resumeSegment">Index of the segment the offset applies to — the surah being resumed, not the opening isti'ādhah.</param>
    public SegmentAudioSource(IReadOnlyList<AudioSegment> segments, int loopStart, TimeSpan resumeOffset, int resumeSegment)
    {
        _segments = segments;
        _loopStart = Math.Clamp(loopStart, 0, Math.Max(0, segments.Count - 1));
        _resumeOffset = resumeOffset;
        _resumeSegment = resumeSegment;
        _resumeUsed = resumeOffset <= TimeSpan.Zero;
        _currentIndex = 0;

        if (segments.Count > 0 && Open(0) is { } opened)
        {
            _current = opened.Samples;
            _currentReader = opened.Reader;
            ConsumeResume(0);
            _markers.Add(new Marker(0, 0, FileFrame(segments[0], opened.Offset)));
        }
        else
        {
            AdvanceLocked();
        }
        StartPrefetch();
    }

    private static long FileFrame(AudioSegment segment, TimeSpan offset) =>
        (long)((segment.Start + offset).TotalSeconds * SampleRate);

    /// <summary>The resume offset, if this is the segment it belongs to and it has not been used yet.</summary>
    private TimeSpan PeekResume(int index) =>
        !_resumeUsed && index == _resumeSegment ? _resumeOffset : TimeSpan.Zero;

    private void ConsumeResume(int index)
    {
        if (index == _resumeSegment)
            _resumeUsed = true;
    }

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    /// <summary>Which segment, and how far into its file, the sample at <paramref name="streamFrame"/> belongs to.</summary>
    public (AudioSegment Segment, int SegmentIndex, TimeSpan Position)? Locate(long streamFrame)
    {
        lock (_gate)
        {
            for (var i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                if (m.StreamFrame <= streamFrame)
                {
                    var fileFrame = m.FileStartFrame + (streamFrame - m.StreamFrame);
                    return (_segments[m.SegmentIndex], m.SegmentIndex, TimeSpan.FromSeconds((double)fileFrame / SampleRate));
                }
            }
            return null;
        }
    }

    public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public int Read(Span<float> buffer)
    {
        var count = buffer.Length;
        var written = 0;
        while (written < count && !_disposed)
        {
            ISampleProvider? current;
            lock (_gate)
                current = _current;

            if (current is null)
            {
                // Nothing playable at all: emit silence rather than ending the stream.
                var silence = count - written;
                buffer[written..].Clear();
                lock (_gate)
                    _streamFrames += silence / Channels;
                written = count;
                break;
            }

            var read = current.Read(buffer[written..]);
            if (read > 0)
            {
                written += read;
                lock (_gate)
                    _streamFrames += read / Channels;
                continue;
            }

            lock (_gate)
                AdvanceLocked();
            StartPrefetch();
        }
        return written;
    }

    private void AdvanceLocked()
    {
        _currentReader?.Dispose();
        _current = null;
        _currentReader = null;

        if (_segments.Count == 0)
            return;

        // Try each segment once; if none opens, _current stays null and silence plays.
        for (var attempt = 0; attempt < _segments.Count; attempt++)
        {
            var next = NextIndex(_currentIndex);
            _currentIndex = next;

            var opened = TakePrefetch(next) ?? Open(next);
            if (opened is { } o)
            {
                _current = o.Samples;
                _currentReader = o.Reader;
                ConsumeResume(next);
                _markers.Add(new Marker(_streamFrames, next, FileFrame(_segments[next], o.Offset)));
                if (_markers.Count > 8)
                    _markers.RemoveAt(0);
                return;
            }
        }
    }

    /// <summary>After the last segment, go back to the loop point rather than to the session opener.</summary>
    private int NextIndex(int index) => index + 1 >= _segments.Count ? _loopStart : index + 1;

    private void StartPrefetch()
    {
        if (_segments.Count <= 1)
            return;
        var next = NextIndex(_currentIndex);
        if (_prefetchIndex == next)
            return;
        _prefetchIndex = next;
        // Peeked, not consumed: the offset is only spent when this segment actually starts playing.
        var offset = PeekResume(next);
        _prefetch = Task.Run(() => Open(next, offset));
    }

    private Opened? TakePrefetch(int index)
    {
        if (_prefetch is null || _prefetchIndex != index)
            return null;
        var task = _prefetch;
        _prefetch = null;
        _prefetchIndex = -1;
        try
        {
            return task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error($"Prefetch of segment {index} failed.", ex);
            return null;
        }
    }

    private Opened? Open(int index) => Open(index, PeekResume(index));

    /// <summary>Opens a segment and normalises it to the stream format (44.1 kHz stereo float).</summary>
    private Opened? Open(int index, TimeSpan extraOffset)
    {
        var segment = _segments[index];
        try
        {
            var reader = new MediaFoundationReader(segment.Path);
            var start = segment.Start + extraOffset;
            if (start > TimeSpan.Zero && start < reader.TotalTime)
                reader.CurrentTime = start;

            ISampleProvider samples = reader.ToSampleProvider();
            if (samples.WaveFormat.Channels == 1)
                samples = new MonoToStereoSampleProvider(samples);
            if (samples.WaveFormat.SampleRate != SampleRate)
                samples = new WdlResamplingSampleProvider(samples, SampleRate);

            // A sliced segment (the basmalah taken from Al-Fatihah) stops at its own end.
            if (segment.Duration is { } duration)
            {
                var remaining = duration - extraOffset;
                if (remaining <= TimeSpan.Zero)
                    return null;
                samples = new OffsetSampleProvider(samples) { Take = remaining };
            }

            if (Math.Abs(segment.Gain - 1f) > 0.001f)
                samples = new VolumeSampleProvider(samples) { Volume = segment.Gain };

            return new Opened(samples, reader, extraOffset);
        }
        catch (Exception ex)
        {
            Log.Error($"Cannot open {segment.Part} audio for segment {index} ({segment.Path}).", ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        lock (_gate)
        {
            _currentReader?.Dispose();
            _currentReader = null;
            _current = null;
        }
        try { _prefetch?.GetAwaiter().GetResult()?.Reader.Dispose(); } catch { /* best effort */ }
    }
}
