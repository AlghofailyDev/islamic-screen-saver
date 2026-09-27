using IslamicScreensaver.Core;
using NAudio.CoreAudioApi;

namespace IslamicScreensaver.Player;

/// <summary>
/// The screensaver's place in the Windows volume mixer, and — only if asked — the device volume.
///
/// What a listener hears is our own gain × our entry in the mixer × the device's master level.
/// Windows remembers that mixer entry per application for ever, so a slider nudged once months ago
/// silently multiplied everything after it: a setting of 10% against a mixer entry of 10% is 1%.
/// Our session is therefore pinned wide open on every start, leaving the setting as the only gain
/// we apply and the device master as the only thing outside our control.
/// </summary>
internal sealed class OutputVolume : IDisposable
{
    private readonly MMDevice? _device;
    private float? _restoreLevel;
    private bool? _restoreMute;

    private OutputVolume(MMDevice? device) => _device = device;

    /// <summary>
    /// Opens the endpoint playback will use and clears anything left in our own mixer entry.
    /// Never throws: an audio policy we cannot read is not a reason to abandon the screensaver.
    /// </summary>
    public static OutputVolume For(string? deviceId)
    {
        MMDevice? device = null;
        try
        {
            device = AudioDevices.Resolve(deviceId);
            if (device is null)
            {
                using var enumerator = new MMDeviceEnumerator();
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not open the output device to read its volume.", ex);
        }
        return new OutputVolume(device);
    }

    /// <summary>
    /// Takes our own mixer entry to full and unmutes it, so the volume setting is not multiplied by
    /// a leftover from a previous run. Windows scopes this to this application alone.
    /// </summary>
    public void ResetOwnSession()
    {
        try
        {
            if (_device is null)
                return;

            var sessions = _device.AudioSessionManager.Sessions;
            var mine = Environment.ProcessId;
            for (var i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                if (session.GetProcessID != mine)
                    continue;

                var volume = session.SimpleAudioVolume;
                if (volume.Mute || volume.Volume < 0.999f)
                    Log.Info($"Mixer entry for this app was {(volume.Mute ? "muted" : $"{volume.Volume:P0}")}; opening it up so the volume setting is the only gain.");
                volume.Mute = false;
                volume.Volume = 1f;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not reset this app's mixer entry.", ex);
        }
    }

    /// <summary>
    /// Raises the device itself to <paramref name="floor"/> when it is muted or below it, remembering
    /// what it was. Only called when the user has explicitly asked for it — a muted PC is usually
    /// muted on purpose.
    /// </summary>
    public void RaiseDeviceTo(double floor)
    {
        try
        {
            if (_device is null)
                return;

            var volume = _device.AudioEndpointVolume;
            var level = volume.MasterVolumeLevelScalar;
            var muted = volume.Mute;
            if (!muted && level >= floor)
                return;

            _restoreLevel = level;
            _restoreMute = muted;
            volume.Mute = false;
            volume.MasterVolumeLevelScalar = (float)Math.Clamp(floor, 0.05, 1);
            Log.Info($"Speakers were {(muted ? "muted" : $"at {level:P0}")}; raised to {floor:P0} for this session and will be put back on exit.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not raise the device volume.", ex);
        }
    }

    /// <summary>Puts the device back exactly as it was. Safe to call when nothing was changed.</summary>
    public void Restore()
    {
        try
        {
            if (_device is null || _restoreLevel is not { } level || _restoreMute is not { } mute)
                return;

            _device.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            _device.AudioEndpointVolume.Mute = mute;
            _restoreLevel = null;
            _restoreMute = null;
            Log.Info("Speakers restored to how they were before the screensaver started.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not restore the device volume.", ex);
        }
    }

    public void Dispose()
    {
        Restore();
        _device?.Dispose();
    }
}
