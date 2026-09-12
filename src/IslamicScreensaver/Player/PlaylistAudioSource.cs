using IslamicScreensaver.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace IslamicScreensaver.Player;

/// <summary>
/// One endless sample stream made of the playlist's surahs back to back (wrapping at the end).
/// Every time a new file starts, a marker records the stream sample it started at, so a "samples
/// rendered by the device" count from the output can be mapped back to (playlist index, position in
/// that file) with sample accuracy. That mapping is the whole reason this class exists: the sound
/// card's own clock is the only one that stays honest with variable-bitrate MP3s.
/// </summary>
/// <remarks><see cref="Read"/> runs on the output device's thread; everything else on the UI thread.</remarks>
internal sealed class PlaylistAudioSource : ISampleProvider, IDisposable
{
    public const int SampleRate = 44100;
    public const int Channels = 2;

    private readonly record struct Marker(long StreamFrame, int ItemIndex, long FileStartFrame);

    private readonly Func<int, string?> _pathForIndex;
    private readonly int _count;
    private readonly object _gate = new();
    private readonly List<Marker> _markers = [];

    private ISampleProvider? _current;
    private IDisposable? _currentReader;
    private int _currentIndex;
    private long _streamFrames;          // frames handed to the output so far
    private Task<(ISampleProvider, IDisposable)?>? _prefetch;
    private int _prefetchIndex = -1;
    private bool _disposed;

    public PlaylistAudioSource(Func<int, string?> pathForIndex, int count, int startIndex, TimeSpan startOffset)
    {
        _pathForIndex = pathForIndex;
        _count = count;
        _currentIndex = startIndex;

        var opened = Open(startIndex, startOffset);
        if (opened is { } o)
        {
            (_current, _currentReader) = o;
            _markers.Add(new Marker(0, startIndex, (long)(startOffset.TotalSeconds * SampleRate)));
        }
        else
        {
            // First file unreadable: start the stream at the next one instead.
            AdvanceLocked();
        }
        StartPrefetch();
    }

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    /// <summary>Which playlist item, and how far into its file, the sample at <paramref name="streamFrame"/> belongs to.</summary>
    public (int ItemIndex, TimeSpan Position)? Locate(long streamFrame)
    {
        lock (_gate)
        {
            for (var i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                if (m.StreamFrame <= streamFrame)
                {
                    var fileFrame = m.FileStartFrame + (streamFrame - m.StreamFrame);
                    return (m.ItemIndex, TimeSpan.FromSeconds((double)fileFrame / SampleRate));
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

            // Current file exhausted: the next marker starts exactly at the frames counted so far.
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

        // Try each item once; if none opens, _current stays null and silence plays.
        for (var attempt = 0; attempt < _count; attempt++)
        {
            var next = (_currentIndex + 1) % _count;
            _currentIndex = next;

            var opened = TakePrefetch(next) ?? Open(next, TimeSpan.Zero);
            if (opened is { } o)
            {
                (_current, _currentReader) = o;
                _markers.Add(new Marker(_streamFrames, next, 0));
                if (_markers.Count > 8)
                    _markers.RemoveAt(0);
                return;
            }
        }
    }

    private void StartPrefetch()
    {
        var next = (_currentIndex + 1) % _count;
        if (_count <= 1 || _prefetchIndex == next)
            return;
        _prefetchIndex = next;
        _prefetch = Task.Run(() => Open(next, TimeSpan.Zero));
    }

    private (ISampleProvider, IDisposable)? TakePrefetch(int index)
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
            Log.Error($"Prefetch of playlist item {index} failed.", ex);
            return null;
        }
    }

    /// <summary>Opens a file and normalises it to the stream format (44.1 kHz stereo float).</summary>
    private (ISampleProvider, IDisposable)? Open(int index, TimeSpan offset)
    {
        var path = _pathForIndex(index);
        if (path is null)
            return null;

        try
        {
            var reader = new MediaFoundationReader(path);
            if (offset > TimeSpan.Zero && offset < reader.TotalTime)
                reader.CurrentTime = offset;

            ISampleProvider samples = reader.ToSampleProvider();
            if (samples.WaveFormat.Channels == 1)
                samples = new MonoToStereoSampleProvider(samples);
            if (samples.WaveFormat.SampleRate != SampleRate)
                samples = new WdlResamplingSampleProvider(samples, SampleRate);
            return (samples, reader);
        }
        catch (Exception ex)
        {
            Log.Error($"Cannot open audio for playlist item {index} ({path}).", ex);
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
        try { _prefetch?.GetAwaiter().GetResult()?.Item2.Dispose(); } catch { /* best effort */ }
    }
}
