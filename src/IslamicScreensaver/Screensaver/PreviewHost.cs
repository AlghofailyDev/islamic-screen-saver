using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using static IslamicScreensaver.Interop.NativeMethods;

namespace IslamicScreensaver.Screensaver;

/// <summary>
/// Hosts <see cref="PreviewView"/> as a Win32 child of the small preview box in Windows' Screen Saver
/// Settings dialog (<c>/p &lt;hwnd&gt;</c>). Windows does not always tell us when that box goes away,
/// so the parent is polled and the process exits once it is gone.
/// </summary>
internal sealed class PreviewHost
{
    private readonly HwndSource _source;
    private readonly DispatcherTimer _parentWatch;

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

        _parentWatch = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        _parentWatch.Tick += (_, _) =>
        {
            if (!IsWindow(parent))
            {
                _parentWatch.Stop();
                _source.Dispose();
                Application.Current.Shutdown();
            }
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
}
