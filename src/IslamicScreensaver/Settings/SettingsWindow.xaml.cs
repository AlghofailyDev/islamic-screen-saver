using System.Diagnostics;
using System.Windows;
using IslamicScreensaver.Interop;

namespace IslamicScreensaver.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        var monitors = DisplayMonitor.All();
        MonitorSummary.Text = string.Join(Environment.NewLine,
            monitors.Select(m => $"{m.DeviceName}: {m.Bounds.Width}×{m.Bounds.Height} at ({m.Bounds.Left}, {m.Bounds.Top}){(m.IsPrimary ? "  — primary" : "")}"));
    }

    private void RunScreensaver_Click(object sender, RoutedEventArgs e)
    {
        var self = Environment.ProcessPath;
        if (self is null)
            return;

        Process.Start(new ProcessStartInfo(self, "/s") { UseShellExecute = false });
    }
}
