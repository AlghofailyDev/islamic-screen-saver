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
    public void MissingFile_GivesDefaults()
    {
        var s = SettingsStore.Load(File);
        Assert.Equal([7], s.ReciterIds);
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

        Assert.Equal([7], SettingsStore.Load(File).ReciterIds);
    }

    [Fact]
    public void Normalize_ClampsAndCleans()
    {
        var s = new AppSettings { Volume = 7, ReciterIds = [0, 7, 7, -1], SelectedSurahs = [0, 200] };
        s.Panel.Blur = double.NaN;

        s.Normalize();

        Assert.Equal(1, s.Volume);
        Assert.Equal([7], s.ReciterIds);
        Assert.Null(s.SelectedSurahs);
        Assert.Equal(24, s.Panel.Blur);
    }
}
