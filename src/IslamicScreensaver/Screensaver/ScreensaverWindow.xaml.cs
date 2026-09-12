using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using IslamicScreensaver.Interop;
using static IslamicScreensaver.Interop.NativeMethods;

namespace IslamicScreensaver.Screensaver;

/// <summary>One fullscreen, topmost, cursor-less window covering exactly one monitor.</summary>
public partial class ScreensaverWindow : Window
{
    private readonly DisplayMonitor _monitor;
    private readonly ScreensaverSession _session;

    internal ScreensaverWindow(DisplayMonitor monitor, ScreensaverSession session)
    {
        _monitor = monitor;
        _session = session;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    internal DisplayMonitor Monitor => _monitor;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Position in device pixels straight from the monitor rect. Doing it at the Win32 level sidesteps
        // WPF's DIP conversion, which would otherwise assume the primary monitor's DPI for every window.
        var hwnd = new WindowInteropHelper(this).Handle;
        var b = _monitor.Bounds;
        SetWindowPos(hwnd, HWND_TOPMOST, b.Left, b.Top, b.Width, b.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        // Ask Win32 for the position rather than converting WPF's DIPs: with two monitors at different
        // scale factors the two answers disagree, and the session compares every sample against one origin.
        if (GetCursorPos(out var p))
            _session.OnMouseMove(p.X, p.Y);
        base.OnPreviewMouseMove(e);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e) => _session.OnDiscreteInput($"mouse {e.ChangedButton}");

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e) => _session.OnDiscreteInput("mouse wheel");

    protected override void OnPreviewKeyDown(KeyEventArgs e) => _session.OnDiscreteInput($"key {e.Key} (system key {e.SystemKey}, repeat {e.IsRepeat})");
}
