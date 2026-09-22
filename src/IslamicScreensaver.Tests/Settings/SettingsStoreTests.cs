using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));
    private string File => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void SchemaOne_MigratesBareReciterNumbersToRecitationIds()
    {
        Directory.CreateDirectory(_dir);
        System.IO.File.WriteAllText(File, """
            { "SchemaVersion": 1, "ReciterIds": [7, 2], "Volume": 0.5 }
            """);

        var s = SettingsStore.Load(File);

        Assert.Equal(["qurancom:7", "qurancom:2"], s.RecitationIds);
        Assert.Equal(2, s.SchemaVersion);
        Assert.Equal(0.5, s.Volume);          // everything else survives the migration
        Assert.Null(s.LegacyReciterIds);

        // And the migrated form is what gets written back.
        SettingsStore.Save(s, File);
        var text = System.IO.File.ReadAllText(File);
        Assert.Contains("qurancom:7", text);
        Assert.DoesNotContain("\"ReciterIds\"", text);
    }

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var s = SettingsStore.Load(File);
        Assert.Equal(["qurancom:7"], s.RecitationIds);
        Assert.Null(s.SelectedSurahs);
        Assert.Equal(PlayOrder.Sequential, s.Order);
        Assert.Equal(20, s.TranslationId);
    }

    [Fact]
    public void RoundTrips_WithEnumsAsStrings()
    {
        var s = new AppSettings { Order = PlayOrder.Shuffled, SelectedSurahs = [3, 1, 1], Volume = 0.5 };
        s.Panel.TintColor = "#112233";

        SettingsStore.Save(s, File);
        var text = System.IO.File.ReadAllText(File);
        var back = SettingsStore.Load(File);

        Assert.Contains("\"Shuffled\"", text);
        Assert.Equal(PlayOrder.Shuffled, back.Order);
        Assert.Equal([1, 3], back.SelectedSurahs);   // normalised: deduplicated and sorted
        Assert.Equal(0.5, back.Volume);
        Assert.Equal("#112233", back.Panel.TintColor);
    }

    [Fact]
    public void CorruptFile_GivesDefaults()
    {
        Directory.CreateDirectory(_dir);
        System.IO.File.WriteAllText(File, "{{{ nope");

        Assert.Equal(["qurancom:7"], SettingsStore.Load(File).RecitationIds);
    }

    [Fact]
    public void Normalize_ClampsAndCleans()
    {
        var s = new AppSettings
        {
            Volume = 7,
            RecitationIds = ["qurancom:7", "qurancom:7", "nonsense", "", "mp3quran:3"],
            SelectedSurahs = [0, 200],
        };
        s.Panel.Blur = double.NaN;

        s.Normalize();

        Assert.Equal(1, s.Volume);
        Assert.Equal(["qurancom:7", "mp3quran:3"], s.RecitationIds);
        Assert.Null(s.SelectedSurahs);
        Assert.Equal(24, s.Panel.Blur);
    }

    [Fact]
    public void Recitations_SkipsUnparsableEntriesRatherThanFailing()
    {
        var s = new AppSettings { RecitationIds = ["qurancom:7", "rubbish", "mp3quran:1"] };

        Assert.Equal([RecitationId.QuranCom(7), RecitationId.Mp3Quran(1)], s.Recitations);
    }
}
