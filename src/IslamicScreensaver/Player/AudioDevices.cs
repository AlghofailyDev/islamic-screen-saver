using IslamicScreensaver.Core;
using NAudio.CoreAudioApi;

namespace IslamicScreensaver.Player;

/// <summary>A playback endpoint the user can choose. A null <see cref="Id"/> means "follow the system default".</summary>
public sealed record AudioDevice(string? Id, string Name)
{
    public static AudioDevice SystemDefault { get; } = new(null, "System default");

    public bool IsSystemDefault => Id is null;

    public override string ToString() => Name;
}

/// <summary>Lists the machine's active playback devices and resolves a saved choice back to one.</summary>
internal static class AudioDevices
{
    /// <summary>System default first, then every active render endpoint by friendly name.</summary>
    public static IReadOnlyList<AudioDevice> All()
    {
        var devices = new List<AudioDevice> { AudioDevice.SystemDefault };
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device)
                    devices.Add(new AudioDevice(device.ID, device.FriendlyName));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not list audio devices; only the system default will be offered.", ex);
        }
        return devices;
    }

    /// <summary>
    /// The saved device if it is present and active, otherwise null (meaning: use the system default).
    /// Callers log the fallback so an unplugged speaker never leaves the screensaver silent.
    /// </summary>
    public static MMDevice? Resolve(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return null;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            if (device is not null && device.State == DeviceState.Active && device.DataFlow == DataFlow.Render)
                return device;

            device?.Dispose();
            Log.Info($"Saved audio device {deviceId} is not available; using the system default.");
        }
        catch (Exception ex)
        {
            Log.Error($"Saved audio device {deviceId} could not be opened; using the system default.", ex);
        }
        return null;
    }
}
