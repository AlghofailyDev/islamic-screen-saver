using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using static IslamicScreensaver.Interop.NativeMethods;

namespace IslamicScreensaver.Screensaver;

/// <summary>
/// Hosts <see cref="PreviewView"/> as a Win32 child of the small preview box in Windows' Screen Saver
/// Settings dialog (<c>/p &lt;hwnd&gt;</c>).
/// </summary>
/// <remarks>
/// The dialog retires a preview by destroying the child window we created (it keeps its own preview
/// box alive and spawns a fresh process into it), so the signal to exit is our window's
/// <c>WM_DESTROY</c>. The parent is polled too, in case the dialog goes away without that.
/// </remarks>
internal sealed class PreviewHost
{
    private const int WM_DESTROY = 0x0002;

    private readonly HwndSource _source;
    private readonly DispatcherTimer _parentWatch;
    private bool _exiting;

    private PreviewHost(nint parent)
    {
        GetClientRect(parent, out var client);

        var parameters = new HwndSourceParameters("IslamicScreensaverPreview")
        {
            ParentWindow = parent,
            WindowStyle = WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
            PositionX = 0,
            PositionY = 0,
            Width = client.Width,
            Height = client.Height,
        };

        _source = new HwndSource(parameters) { RootVisual = new PreviewView() };
        _source.AddHook(WndProc);

        _parentWatch = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        _parentWatch.Tick += (_, _) =>
        {
            if (!IsWindow(parent))
                Exit("preview box gone");
        };
        _parentWatch.Start();
    }

    public static PreviewHost? TryStart(nint parent)
    {
        if (parent == 0 || !IsWindow(parent))
        {
            Log.Info("Preview requested without a live parent window; exiting.");
            return null;
        }

        return new PreviewHost(parent);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_DESTROY)
            Exit("preview window destroyed");
        return 0;
    }

    private void Exit(string reason)
    {
        if (_exiting)
            return;

        _exiting = true;
        Log.Info($"Preview exiting ({reason}).");
        _parentWatch.Stop();
        // Shutdown is dispatched so we never tear down the HwndSource from inside its own WndProc.
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (!_source.IsDisposed)
                _source.Dispose();
            Application.Current.Shutdown();
        });
    }
}
