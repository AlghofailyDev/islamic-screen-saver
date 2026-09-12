using static IslamicScreensaver.Interop.NativeMethods;

namespace IslamicScreensaver.Interop;

/// <summary>A physical display. <see cref="Bounds"/> is in device pixels, in the virtual-desktop coordinate space.</summary>
internal sealed record DisplayMonitor(nint Handle, RECT Bounds, bool IsPrimary, string DeviceName)
{
    public static IReadOnlyList<DisplayMonitor> All()
    {
        var monitors = new List<DisplayMonitor>();

        EnumDisplayMonitors(0, 0, (nint hMonitor, nint hdc, ref RECT rect, nint data) =>
        {
            var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new DisplayMonitor(
                    hMonitor,
                    info.rcMonitor,
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    info.szDevice));
            }
            return true;
        }, 0);

        // Primary first, so it is the window that receives focus and (later) the verse panel.
        return monitors.OrderByDescending(m => m.IsPrimary).ToList();
    }
}
