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

    /// <summary>
    /// Where the library goes when someone picks <paramref name="chosenFolder"/> for it: always a
    /// folder of our own inside it, "&lt;chosen&gt;\IslamicScreensaver\cache", never the chosen folder
    /// itself. That is what lets a reset, a move or an uninstall delete the library without ever being
    /// able to reach the user's own files beside it.
    ///
    /// Someone who browses to a folder that is already ours — "D:\IslamicScreensaver" or its cache —
    /// gets that, rather than another IslamicScreensaver nested inside it.
    /// </summary>
    public static string LibraryInside(string chosenFolder)
    {
        var full = Path.GetFullPath(chosenFolder);
        // Never trim a drive root: "D:\" without its slash is "D:", the current folder on drive D.
        if (full != Path.GetPathRoot(full))
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = Path.GetFileName(full);

        if (leaf.Equals("cache", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(Path.GetDirectoryName(full) ?? "").Equals(AppFolderName, StringComparison.OrdinalIgnoreCase))
            return full;
        if (leaf.Equals(AppFolderName, StringComparison.OrdinalIgnoreCase))
            return Path.Combine(full, "cache");
        return Path.Combine(full, AppFolderName, "cache");
    }
}
