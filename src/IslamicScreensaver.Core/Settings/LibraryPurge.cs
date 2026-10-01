namespace IslamicScreensaver.Core.Settings;

/// <summary>What a purge did, so the caller can say so — or say what it deliberately left.</summary>
public sealed record PurgeResult(IReadOnlyList<string> Removed, IReadOnlyList<string> LeftAlone, IReadOnlyList<string> Failed);

/// <summary>
/// Removes everything this app has written: settings, logs, playback state, and every downloaded
/// recitation — including a library the user moved to another drive.
///
/// It lives here, not in the uninstaller, because only the app can read its own settings to find a
/// moved library, and because it is the one place both the setup's uninstaller and uninstall.ps1 can
/// share. Two copies of "which folders is it safe to delete" would sooner or later disagree.
/// </summary>
public static class LibraryPurge
{
    /// <summary>The folder name the app always places a moved library inside.</summary>
    private const string AppFolder = AppPaths.AppFolderName;
    private const string CacheFolder = "cache";

    /// <summary>
    /// The folder to remove for a library the user moved, or <c>null</c> when there is nothing to
    /// remove or it is not a folder this app created.
    ///
    /// The app never moves a library into the folder the user picked; it always makes
    /// "&lt;picked&gt;\IslamicScreensaver\cache" so that resetting or purging can never reach the user's
    /// own files. Only that exact shape is accepted. A settings file edited by hand to point at
    /// "D:\Music" gets its library left alone, not its music deleted.
    /// </summary>
    public static string? MovedLibraryRoot(string? cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
            return null;

        string full;
        try
        {
            full = Path.GetFullPath(cacheDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return null;   // not a path at all
        }

        var parent = Path.GetDirectoryName(full);
        if (parent is null
            || !string.Equals(Path.GetFileName(full), CacheFolder, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(parent), AppFolder, StringComparison.OrdinalIgnoreCase))
            return null;

        // A drive root's "IslamicScreensaver" folder still has a parent, so it is still the app's own;
        // what must never come back is the drive root itself.
        return Path.GetPathRoot(parent) == parent ? null : parent;
    }

    /// <param name="settingsDirectory">Usually <c>%APPDATA%\IslamicScreensaver</c>.</param>
    /// <param name="localDirectory">Usually <c>%LOCALAPPDATA%\IslamicScreensaver</c> — the default library and the logs.</param>
    /// <param name="movedLibrary">The configured library location, if the user moved it.</param>
    public static PurgeResult Run(string settingsDirectory, string localDirectory, string? movedLibrary)
    {
        var removed = new List<string>();
        var leftAlone = new List<string>();
        var failed = new List<string>();

        // The moved library first: once the settings are gone, nothing records where it was.
        if (!string.IsNullOrWhiteSpace(movedLibrary))
        {
            if (MovedLibraryRoot(movedLibrary) is { } root)
                Remove(root, removed, failed);
            else
                leftAlone.Add(movedLibrary);
        }

        Remove(settingsDirectory, removed, failed);
        Remove(localDirectory, removed, failed);

        return new PurgeResult(removed, leftAlone, failed);
    }

    private static void Remove(string directory, List<string> removed, List<string> failed)
    {
        try
        {
            if (!Directory.Exists(directory))
                return;
            Directory.Delete(directory, recursive: true);
            removed.Add(directory);
        }
        catch (Exception)
        {
            // A file still open in another process. Reported rather than thrown: an uninstaller
            // must not stop half-way because one log file was busy.
            failed.Add(directory);
        }
    }
}
