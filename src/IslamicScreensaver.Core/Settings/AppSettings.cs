namespace IslamicScreensaver.Core.Settings;

public enum PlayOrder
{
    Sequential,
    Shuffled,
}

public enum AppTheme
{
    /// <summary>Follow the Windows app theme.</summary>
    System,
    Light,
    Dark,
}

public enum AppLanguage
{
    /// <summary>Arabic if Windows is set to Arabic, otherwise English.</summary>
    System,
    English,
    Arabic,
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
/// <summary>Where the verse panel sits on screen, or that it is not shown at all.</summary>
public enum PanelPosition
{
    Top,
    Middle,
    Bottom,

    /// <summary>No panel and no reciter name — the background alone, with the recitation.</summary>
    Hidden,
}

public sealed class PanelSettings
{
    public PanelPosition Position { get; set; } = PanelPosition.Bottom;

    /// <summary>
    /// Multiplies the verse and translation type together, keeping their proportion. 1.0 is the
    /// design size (66 px Arabic at 1080p). Long verses are still shrunk to fit from here.
    /// </summary>
    public double TextScale { get; set; } = 1.0;

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
    /// <summary>2 introduced source-qualified recitation ids in place of bare Quran.com reciter numbers.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Recitations to play, in rotation order, as source-qualified ids (<c>qurancom:7</c>).
    /// Schema 1 held bare Quran.com numbers; <see cref="SettingsStore"/> migrates those.
    /// </summary>
    public List<string> RecitationIds { get; set; } = [Content.RecitationId.QuranCom(7).ToString()];

    /// <summary>Schema 1 only: bare Quran.com reciter numbers, read once and then migrated away.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("ReciterIds")]
    public List<int>? LegacyReciterIds { get; set; }

    /// <summary>Set when loading stepped an older file forward, so the result is written back once.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool WasMigrated { get; set; }

    /// <summary>The recitations to play, parsed. Unparsable entries are dropped rather than crashing playback.</summary>
    public IReadOnlyList<Content.RecitationId> Recitations =>
        RecitationIds.Select(id => Content.RecitationId.TryParse(id, out var parsed) ? parsed : (Content.RecitationId?)null)
                     .Where(id => id is not null)
                     .Select(id => id!.Value)
                     .ToList();

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

    /// <summary>
    /// Turn the speakers up if they are muted or nearly silent when the screensaver starts, and put
    /// them back exactly as they were on the way out.
    ///
    /// Off by default: a muted PC is usually muted on purpose, and a screensaver that speaks at two
    /// in the morning is worse than one that stays quiet. On, for a PC left on a shelf whose volume
    /// someone turned down days ago.
    /// </summary>
    public bool OverrideSystemVolume { get; set; }

    /// <summary>The device level <see cref="OverrideSystemVolume"/> raises to, 0..1.</summary>
    public double SystemVolumeFloor { get; set; } = 0.5;

    // The isti'ādhah and the basmalah are part of how the Qur'an is recited, not preferences:
    // they always play, so there is nothing about them here.

    /// <summary>Light or dark chrome for the settings app. The screensaver itself is always dark.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Language of the settings app and the screensaver's own labels — not the translation.</summary>
    public AppLanguage Language { get; set; } = AppLanguage.System;

    public bool ShowTranslation { get; set; } = true;

    /// <summary>Quran.com translation resource id.</summary>
    public int TranslationId { get; set; } = 20;

    public BackgroundSettings Background { get; set; } = new();

    public PanelSettings Panel { get; set; } = new();

    /// <summary>A window of the day to stay out of. Off by default; see <see cref="QuietHours"/>.</summary>
    public QuietHours Quiet { get; set; } = new();

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
        SystemVolumeFloor = double.IsFinite(SystemVolumeFloor) ? Math.Clamp(SystemVolumeFloor, 0.05, 1) : 0.5;
        SyncOffsetMs = Math.Clamp(SyncOffsetMs, -2000, 2000);
        if (!Enum.IsDefined(Panel.Position))
            Panel.Position = PanelPosition.Bottom;
        Panel.TextScale = double.IsFinite(Panel.TextScale) ? Math.Clamp(Panel.TextScale, 0.7, 1.5) : 1.0;
        if (!Enum.IsDefined(Quiet.Behaviour))
            Quiet.Behaviour = QuietHoursBehaviour.DarkAndSilent;
        Panel.Blur = double.IsFinite(Panel.Blur) ? Math.Clamp(Panel.Blur, 0, 60) : 24;
        Panel.Grain = double.IsFinite(Panel.Grain) ? Math.Clamp(Panel.Grain, 0, 1) : 0.35;
        Panel.TintOpacity = double.IsFinite(Panel.TintOpacity) ? Math.Clamp(Panel.TintOpacity, 0, 1) : 0.42;
        RecitationIds = RecitationIds
            .Where(id => Content.RecitationId.TryParse(id, out _))
            .Select(id => Content.RecitationId.Parse(id).ToString())   // canonical spelling
            .Distinct()
            .ToList();
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
