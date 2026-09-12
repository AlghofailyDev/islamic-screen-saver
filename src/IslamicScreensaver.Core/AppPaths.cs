namespace IslamicScreensaver.Core;

/// <summary>Where the app keeps its files. Settings roam with the profile; the cache and logs do not.</summary>
public static class AppPaths
{
    public const string AppFolderName = "IslamicScreensaver";

    public static string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static string SettingsFile { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public static string LocalDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public static string CacheDirectory { get; } = Path.Combine(LocalDirectory, "cache");

    public static string LogDirectory { get; } = Path.Combine(LocalDirectory, "logs");
}
