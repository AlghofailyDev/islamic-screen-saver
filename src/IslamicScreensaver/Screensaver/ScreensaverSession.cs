using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Screensaver;
using IslamicScreensaver.Interop;
using static IslamicScreensaver.Interop.NativeMethods;

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

        // When the grace period ends, take the cursor's resting position as the origin so the first
        // real event is compared against where the mouse actually is, not against a synthetic move.
        var originTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = _exitPolicy.GracePeriod };
        originTimer.Tick += (_, _) =>
        {
            originTimer.Stop();
            if (GetCursorPos(out var p))
                OnMouseMove(p.X, p.Y);
        };
        originTimer.Start();
    }

    public void OnMouseMove(double screenX, double screenY)
    {
        if (_exitPolicy.ShouldExitOnMouseMove(_clock.Elapsed, screenX, screenY))
            Exit($"mouse moved to ({screenX:0}, {screenY:0})");
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
        Log.Info($"Screensaver exiting ({reason}) after {_clock.Elapsed:mm\\:ss\\.fff}.");
        Application.Current.Shutdown();
    }
}
