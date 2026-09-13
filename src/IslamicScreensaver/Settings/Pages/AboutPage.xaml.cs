using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using IslamicScreensaver.Core;

namespace IslamicScreensaver.Settings.Pages;

public partial class AboutPage : UserControl
{
    private readonly SettingsContext _ctx;

    internal AboutPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";
        VersionText.Text = $"Islamic Screensaver {version} — Quran recitation with the verse on screen, as a Windows screensaver.";
        SettingsCard.Description = AppPaths.SettingsFile;
        _ctx.Downloads.Changed += (_, _) => UpdateCacheSize();
        UpdateCacheSize();
    }

    private void UpdateCacheSize()
    {
        try
        {
            var dir = new DirectoryInfo(_ctx.Cache.RootDirectory);
            var bytes = dir.Exists ? dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            CacheCard.Description = $"{bytes / 1024.0 / 1024.0:0.0} MB in {_ctx.Cache.RootDirectory}";
        }
        catch (Exception ex)
        {
            Log.Error("Could not measure cache size.", ex);
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void OpenCache_Click(object sender, RoutedEventArgs e) => OpenFolder(_ctx.Cache.RootDirectory);

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.SettingsDirectory);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.LogDirectory);

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Delete all downloaded recitations, text and translations? The screensaver will have nothing to play until you download again.",
            "Clear cache", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        _ctx.Downloads.CancelAll();
        _ctx.Samples.Stop();
        try
        {
            if (Directory.Exists(_ctx.Cache.RootDirectory))
                Directory.Delete(_ctx.Cache.RootDirectory, recursive: true);
            Log.Info("Cache cleared by user.");
            MessageBox.Show("Cache cleared. Close and reopen Settings to refresh the lists.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error("Clearing the cache failed.", ex);
            MessageBox.Show($"Could not clear the cache: {ex.Message}", "Clear cache", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        UpdateCacheSize();
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
