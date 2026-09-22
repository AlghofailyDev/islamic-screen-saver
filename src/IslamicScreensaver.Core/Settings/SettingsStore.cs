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

    /// <summary>Steps an older file forward one version at a time. Never rewrites history in place.</summary>
    private static void Migrate(AppSettings settings)
    {
        if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
        {
            Log.Info($"Settings file is from a newer version ({settings.SchemaVersion}); reading what we understand.");
            return;
        }

        if (settings.SchemaVersion < 2)
        {
            // 1 → 2: bare numbers were always Quran.com reciters; qualify them.
            if (settings.LegacyReciterIds is { Count: > 0 } legacy)
            {
                settings.RecitationIds = legacy
                    .Where(id => id > 0)
                    .Select(id => Content.RecitationId.QuranCom(id).ToString())
                    .Distinct()
                    .ToList();
                Log.Info($"Migrated {settings.RecitationIds.Count} reciter id(s) to recitation ids.");
            }
            settings.LegacyReciterIds = null;
            settings.SchemaVersion = 2;
            settings.WasMigrated = true;
        }
    }
}
