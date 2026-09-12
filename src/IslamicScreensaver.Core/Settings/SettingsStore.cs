namespace IslamicScreensaver.Core.Settings;

public static class SettingsStore
{
    public static AppSettings Load(string? path = null)
    {
        var settings = JsonFileStore.Load<AppSettings>(path ?? AppPaths.SettingsFile);
        Migrate(settings);
        settings.Normalize();
        return settings;
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        settings.Normalize();
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        JsonFileStore.Save(path ?? AppPaths.SettingsFile, settings);
    }

    private static void Migrate(AppSettings settings)
    {
        // Nothing to migrate yet. When the schema changes, step older versions forward here,
        // one version at a time, and bump AppSettings.CurrentSchemaVersion.
        if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            Log.Info($"Settings file is from a newer version ({settings.SchemaVersion}); reading what we understand.");
    }
}
