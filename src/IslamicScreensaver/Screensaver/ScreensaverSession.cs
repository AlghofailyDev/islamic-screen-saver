using System.Diagnostics;
using System.Windows;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Screensaver;
using IslamicScreensaver.Interop;

namespace IslamicScreensaver.Screensaver;

/// <summary>
/// The running screensaver: one window per monitor, one shared exit policy, one shutdown.
/// Input from any window funnels through here so movement across monitors is measured consistently.
/// </summary>
internal sealed class ScreensaverSession
{
    private readonly List<ScreensaverWindow> _windows = [];
    private readonly InputExitPolicy _exitPolicy = InputExitPolicy.Default;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _exiting;

    public void Start()
    {
        var monitors = DisplayMonitor.All();
        Log.Info($"Screensaver starting on {monitors.Count} monitor(s).");

        foreach (var monitor in monitors)
        {
            var window = new ScreensaverWindow(monitor, this);
            _windows.Add(window);
            window.Show();
        }

        // Keyboard input needs a focused window; the primary is first in the list.
        _windows.FirstOrDefault()?.Activate();
    }

    public void OnMouseMove(double screenX, double screenY)
    {
        if (_exitPolicy.ShouldExitOnMouseMove(_clock.Elapsed, screenX, screenY))
            Exit("mouse moved");
    }

    public void OnDiscreteInput()
    {
        if (_exitPolicy.ShouldExitOnDiscreteInput())
            Exit("key or button");
    }

    public void Exit(string reason)
    {
        if (_exiting)
            return;

        _exiting = true;
        Log.Info($"Screensaver exiting ({reason}) after {_clock.Elapsed:mm\\:ss}.");
        Application.Current.Shutdown();
    }
}
