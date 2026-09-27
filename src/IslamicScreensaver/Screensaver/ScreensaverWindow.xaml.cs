using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Interop;
using static IslamicScreensaver.Interop.NativeMethods;

namespace IslamicScreensaver.Screensaver;

/// <summary>One fullscreen, topmost, cursor-less window covering exactly one monitor.</summary>
public partial class ScreensaverWindow : Window
{
    /// <summary>The overlay is designed against this height; it scales up or down to the monitor's.</summary>
    private const double DesignHeight = 1080;

    private readonly DisplayMonitor _monitor;
    private readonly ScreensaverSession _session;

    internal ScreensaverWindow(DisplayMonitor monitor, ScreensaverSession session)
    {
        _monitor = monitor;
        _session = session;
        InitializeComponent();
        ReciterCaption.Text = Tracked(Strings.Get("screensaver.reciter"));
        // The reciter caption and the hint text read right-to-left in Arabic; the verse panel has its own direction.
        Overlay.FlowDirection = Strings.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => ScaleOverlay();
        Panel.SetBackdrop(Backdrop);
    }

    internal DisplayMonitor Monitor => _monitor;

    /// <summary>
    /// The wide letter-spacing of a small caption label. WPF has no letter-spacing, so it is spaces —
    /// which only works for a script whose letters stand alone. Arabic letters join, and pulling them
    /// apart turns القارئ into ا ل ق ا ر ئ, so connected scripts are left exactly as written.
    /// </summary>
    private static string Tracked(string label) =>
        Strings.IsRightToLeftText(label) ? label : string.Join(' ', label.ToCharArray());

    /// <summary>Applies background and panel look. Call once before playback starts.</summary>
    /// <summary>True when the user has turned the panel off: the reciter name goes with it.</summary>
    private bool _panelHidden;

    internal void ApplyAppearance(AppSettings settings, bool showPanel)
    {
        Backdrop.Load(settings.Background);
        Panel.ApplyStyle(settings.Panel, Backdrop.SuggestedTint);
        Panel.ApplyPosition(settings.Panel.Position);

        _panelHidden = settings.Panel.Position == PanelPosition.Hidden;
        if (_panelHidden || !showPanel)
        {
            Panel.Visibility = Visibility.Collapsed;
            ReciterBlock.Visibility = Visibility.Collapsed;
        }
    }

    internal void SetReciter(string name)
    {
        // Hiding the panel hides the reciter with it — the user asked for the background alone.
        if (_panelHidden)
            return;

        ReciterName.Text = name;
        // A Latin name keeps its own direction, so "AbdulBaset AbdulSamad (Murattal)" is not mirrored.
        ReciterName.FlowDirection = Strings.IsRightToLeftText(name) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ReciterBlock.Visibility = string.IsNullOrEmpty(name) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ScaleOverlay()
    {
        if (ActualHeight <= 0)
            return;
        var scale = ActualHeight / DesignHeight;
        Overlay.LayoutTransform = new ScaleTransform(scale, scale);
    }

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
