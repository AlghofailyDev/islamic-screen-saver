namespace IslamicScreensaver.Core.Settings;

/// <summary>What setup asked for, and what was done about it.</summary>
public enum LibraryChoiceResult
{
    /// <summary>The default location: nothing to record.</summary>
    UsedDefault,

    /// <summary>The folder the user picked, recorded in settings.</summary>
    UsedChosen,

    /// <summary>A library already existed and was left exactly where it was.</summary>
    KeptExisting,

    /// <summary>The folder could not be used, so the default stands.</summary>
    Refused,
}

/// <summary>
/// Applies the library folder chosen during setup. Kept apart from the UI and the installer so the
/// rules can be tested: setup can only ever choose where a library *starts*.
/// </summary>
public static class LibraryChoice
{
    /// <param name="settingsExisted">
    /// Whether settings were already on disk. If they were, this is not a first install, and there may
    /// be gigabytes of recitations somewhere — repointing the setting would orphan them rather than
    /// move them. Moving belongs to Library → Storage, which carries the files across with progress.
    /// </param>
    /// <param name="validate">Returns why a folder cannot be used, or null if it can.</param>
    public static LibraryChoiceResult Apply(
        AppSettings settings, bool settingsExisted, string? chosenFolder, string defaultLibrary, Func<string, string?> validate)
    {
        if (settingsExisted || settings.CacheDirectory is not null)
            return LibraryChoiceResult.KeptExisting;

        if (string.IsNullOrWhiteSpace(chosenFolder))
            return LibraryChoiceResult.UsedDefault;

        string target;
        try
        {
            target = AppPaths.LibraryInside(chosenFolder);
        }
        catch (Exception)
        {
            return LibraryChoiceResult.Refused;   // not a path
        }

        // Choosing the default's own parent is just the default; nothing needs recording.
        if (string.Equals(Path.GetFullPath(target).TrimEnd('\\'), Path.GetFullPath(defaultLibrary).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return LibraryChoiceResult.UsedDefault;

        if (validate(target) is not null)
            return LibraryChoiceResult.Refused;

        settings.CacheDirectory = target;
        return LibraryChoiceResult.UsedChosen;
    }
}
