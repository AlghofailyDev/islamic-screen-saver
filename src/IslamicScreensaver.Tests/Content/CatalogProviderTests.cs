using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class CatalogProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));
    private readonly string _appDir;

    public CatalogProviderTests()
    {
        _appDir = Path.Combine(_root, "app");
        Directory.CreateDirectory(_appDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private CatalogProvider Provider() => new(Path.Combine(_root, "cache"), _appDir);

    private static Catalog Built(DateTimeOffset at, string reciterName) =>
        new(Catalog.CurrentVersion, at,
            [new ReciterEntry("mq-1", reciterName, reciterName,
                [new Recitation(RecitationId.Mp3Quran(1), "mq-1", "حفص عن عاصم", null, null, TimingLevel.Verse, [1])])]);

    [Fact]
    public void NoCatalogAnywhere_IsEmptyRatherThanAThrow()
    {
        // A screensaver that cannot start is worse than one with no reciter names.
        Assert.Empty(Provider().Load().Reciters);
    }

    [Fact]
    public void TheShippedCopyIsUsedWhenNothingHasBeenDownloaded()
    {
        var provider = Provider();
        CatalogStore.Write(provider.ShippedPath, Built(DateTimeOffset.UnixEpoch, "shipped"));

        Assert.Equal("shipped", Provider().Load().Reciters[0].NameArabic);
    }

    [Fact]
    public void TheNewerOfTheTwoWins()
    {
        var provider = Provider();
        CatalogStore.Write(provider.ShippedPath, Built(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "shipped"));
        provider.SaveDownloaded(Built(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), "downloaded"));

        Assert.Equal("downloaded", Provider().Load().Reciters[0].NameArabic);
    }

    [Fact]
    public void AReinstallCanShipSomethingNewerThanAnOldDownload()
    {
        var provider = Provider();
        CatalogStore.Write(provider.ShippedPath, Built(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), "shipped"));
        provider.SaveDownloaded(Built(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "downloaded"));

        Assert.Equal("shipped", Provider().Load().Reciters[0].NameArabic);
    }

    [Fact]
    public void ACorruptDownloadFallsBackToWhatShipped()
    {
        var provider = Provider();
        CatalogStore.Write(provider.ShippedPath, Built(DateTimeOffset.UnixEpoch, "shipped"));
        Directory.CreateDirectory(Path.GetDirectoryName(provider.DownloadedPath)!);
        File.WriteAllText(provider.DownloadedPath, "{ not json");

        Assert.Equal("shipped", Provider().Load().Reciters[0].NameArabic);
    }

    [Fact]
    public void ThePublishedCatalogIsReadable()
    {
        // The real file, as the build wrote it — a smoke test that what we publish is what we parse.
        var published = Path.Combine(RepoRoot(), "assets", "catalog", CatalogStore.FileName);
        Assert.True(File.Exists(published), $"No catalog at {published}; run tools/CatalogBuilder.");

        var catalog = CatalogStore.Read(published);
        Assert.NotNull(catalog);
        Assert.True(catalog.Reciters.Count > 100, $"Only {catalog.Reciters.Count} reciters.");

        // Husary: the reason the model groups recordings under a person rather than listing them flat.
        var husary = catalog.ReciterOf(RecitationId.QuranCom(6));
        Assert.NotNull(husary);
        Assert.True(husary.Recitations.Count > 1);
        Assert.Contains(husary.Recitations, r => r.Id.Source == RecitationSource.Mp3Quran);
        Assert.Contains(husary.Recitations, r => r.RiwayahArabic.Contains("ورش"));

        // The recording that lists 114 surahs and hosts none must not be offered.
        Assert.Null(catalog.Find(RecitationId.QuranCom(8)));

        // Every recording has somewhere to fetch from, and a timed one says where its timings are.
        Assert.All(catalog.AllRecitations, r =>
        {
            Assert.NotEmpty(r.AvailableSurahs);
            Assert.False(string.IsNullOrEmpty(r.ResolveUrl(r.AvailableSurahs[0])));
            Assert.Equal(r.Timing == TimingLevel.None, r.TimedSurahs.Count == 0);
            if (r.Timing == TimingLevel.Verse)
                Assert.False(string.IsNullOrEmpty(r.ResolveTimingsUrl(r.TimedSurahs[0])));
        });
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IslamicScreensaver.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
