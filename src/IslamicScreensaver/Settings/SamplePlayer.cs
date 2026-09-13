using IslamicScreensaver.Core;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace IslamicScreensaver.Settings;

/// <summary>Plays one cached file at a time for the "listen" buttons; a second call stops the first.</summary>
internal sealed class SamplePlayer : IDisposable
{
    private WaveOut? _output;
    private MediaFoundationReader? _reader;
    private string? _playingKey;

    public event Action<string?>? PlayingChanged;

    public string? PlayingKey => _playingKey;

    public void Toggle(string key, string path, float volume)
    {
        if (_playingKey == key)
        {
            Stop();
            return;
        }

        Stop();
        try
        {
            MediaFoundationApi.Startup();
            _reader = new MediaFoundationReader(path);
            _output = new WaveOut { Volume = volume };
            _output.PlaybackStopped += (_, _) => { if (_playingKey == key) Stop(); };
            _output.Init(_reader);
            _output.Play();
            _playingKey = key;
            PlayingChanged?.Invoke(key);
        }
        catch (Exception ex)
        {
            Log.Error($"Sample playback failed for {path}.", ex);
            Stop();
        }
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _reader?.Dispose();
        _output = null;
        _reader = null;
        if (_playingKey is not null)
        {
            _playingKey = null;
            PlayingChanged?.Invoke(null);
        }
    }

    public void Dispose() => Stop();
}
