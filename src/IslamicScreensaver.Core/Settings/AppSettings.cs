namespace IslamicScreensaver.Core.Settings;

public enum PlayOrder
{
    Sequential,
    Shuffled,
}

public enum BackgroundKind
{
    /// <summary>One of the visuals shipped with the app, named by <see cref="BackgroundSettings.BundledName"/>.</summary>
    Bundled,

    /// <summary>A user-chosen image or video at <see cref="BackgroundSettings.FilePath"/>.</summary>
    File,
}

public sealed class BackgroundSettings
{
    public BackgroundKind Kind { get; set; } = BackgroundKind.Bundled;
    public string BundledName { get; set; } = "dawn";
    public string? FilePath { get; set; }
}

/// <summary>Look of the frosted verse panel. Colours are <c>#RRGGBB</c>; a null tint means "sample from the background".</summary>
public sealed class PanelSettings
{
    public double Blur { get; set; } = 24;
    public double Grain { get; set; } = 0.35;
    public string? TintColor { get; set; }
    public double TintOpacity { get; set; } = 0.42;
    public string ArabicColor { get; set; } = "#FBF5E6";
    public string TranslationColor { get; set; } = "#C9C9C9";
    public bool WordHighlight { get; set; } = true;
}

/// <summary>Everything the user can change. Persisted as JSON; bump <see cref="SchemaVersion"/> when the shape changes.</summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Quran.com recitation ids, in rotation order.</summary>
    public List<int> ReciterIds { get; set; } = [7];

    /// <summary>With several reciters: take turns surah by surah. Otherwise the first is used.</summary>
    public bool RotateReciters { get; set; } = true;

    /// <summary>Surah numbers to play; null means all 114.</summary>
    public List<int>? SelectedSurahs { get; set; }

    public PlayOrder Order { get; set; } = PlayOrder.Sequential;

    /// <summary>Sequential play continues from the last surah played rather than restarting at the first.</summary>
    public bool ResumeWhereLeftOff { get; set; } = true;

    /// <summary>0..1.</summary>
    public double Volume { get; set; } = 0.8;

    /// <summary>
    /// Manual trim applied to the timing files: positive delays the on-screen text, negative advances it.
    /// The audio clock is sample-accurate, so this only compensates a bias in a reciter's timestamps.
    /// </summary>
    public int SyncOffsetMs { get; set; }

    /// <summary>
    /// Which speakers to play through, as a Windows endpoint id. Null follows the system default — so a
    /// headset can stay the default while the screensaver plays through the room's speakers.
    /// </summary>
    public string? OutputDeviceId { get; set; }

    /// <summary>Remembered only so Settings can name a device that is currently unplugged.</summary>
    public string? OutputDeviceName { get; set; }

    // The isti'ādhah and the basmalah are part of how the Qur'an is recited, not preferences:
    // they always play, so there is nothing about them here.

    public bool ShowTranslation { get; set; } = true;

    /// <summary>Quran.com translation resource id.</summary>
    public int TranslationId { get; set; } = 20;

    public BackgroundSettings Background { get; set; } = new();

    public PanelSettings Panel { get; set; } = new();

    /// <summary>Show the verse panel on every monitor rather than only the primary.</summary>
    public bool MirrorOnAllMonitors { get; set; }

    /// <summary>
    /// Where downloaded recitations live. Null means the default under <c>%LOCALAPPDATA%</c>; set it to
    /// keep a large library on another drive.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>The cache root to actually use, falling back to the default for a blank or unusable value.</summary>
    public string ResolvedCacheDirectory =>
        string.IsNullOrWhiteSpace(CacheDirectory) ? AppPaths.CacheDirectory : CacheDirectory;

    /// <summary>Clamps out-of-range values so a hand-edited file cannot produce a silent or invisible screensaver.</summary>
    public void Normalize()
    {
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 1) : 0.8;
        SyncOffsetMs = Math.Clamp(SyncOffsetMs, -2000, 2000);
        Panel.Blur = double.IsFinite(Panel.Blur) ? Math.Clamp(Panel.Blur, 0, 60) : 24;
        Panel.Grain = double.IsFinite(Panel.Grain) ? Math.Clamp(Panel.Grain, 0, 1) : 0.35;
        Panel.TintOpacity = double.IsFinite(Panel.TintOpacity) ? Math.Clamp(Panel.TintOpacity, 0, 1) : 0.42;
        ReciterIds = ReciterIds.Where(id => id > 0).Distinct().ToList();
        if (string.IsNullOrWhiteSpace(CacheDirectory))
            CacheDirectory = null;
        if (string.IsNullOrWhiteSpace(OutputDeviceId))
        {
            OutputDeviceId = null;
            OutputDeviceName = null;
        }
        SelectedSurahs = SelectedSurahs?.Where(s => s is >= 1 and <= 114).Distinct().Order().ToList();
        if (SelectedSurahs is { Count: 0 })
            SelectedSurahs = null;
    }
}
