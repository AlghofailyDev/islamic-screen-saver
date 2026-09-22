using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class ContentCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FreshCache_HasNothing()
    {
        var cache = new ContentCache(_root);
        Assert.False(cache.HasSurahText(1));
        Assert.False(cache.HasSurahAudio(RecitationId.QuranCom(7), 1));
        Assert.False(cache.HasSurahTranslation(20, 1));
        Assert.Empty(cache.CachedRecitations());
        Assert.Null(cache.ReadSurahText(1));
    }

    [Fact]
    public async Task SurahText_RoundTrips_AndPersistsInManifest()
    {
        var cache = new ContentCache(_root);
        var verses = new List<VerseText> { new(new VerseKey(1, 1), "بِسْمِ ٱللَّهِ", ["بِسْمِ", "ٱللَّهِ"]) };

        await cache.WriteSurahTextAsync(1, verses);

        Assert.True(cache.HasSurahText(1));
        var back = cache.ReadSurahText(1);
        Assert.NotNull(back);
        Assert.Equal(verses[0].Key, back[0].Key);
        Assert.Equal(verses[0].Words, back[0].Words);

        // A new instance reads the manifest back from disk.
        Assert.True(new ContentCache(_root).HasSurahText(1));
        Assert.False(File.Exists(cache.TextPath(1) + ".tmp"));
    }

    [Fact]
    public async Task Audio_IsOnlyCompleteOnceBothFilesExistAndMarked()
    {
        var cache = new ContentCache(_root);
        await cache.WriteTimingsAsync(RecitationId.QuranCom(7), 1, [new VerseTiming(new VerseKey(1, 1), 0, 100, [])]);

        Assert.False(cache.HasSurahAudio(RecitationId.QuranCom(7), 1));
        Assert.Throws<InvalidOperationException>(() => cache.MarkSurahAudioComplete(RecitationId.QuranCom(7), 1));

        Directory.CreateDirectory(Path.GetDirectoryName(cache.AudioPath(RecitationId.QuranCom(7), 1))!);
        await File.WriteAllBytesAsync(cache.AudioPath(RecitationId.QuranCom(7), 1), [1, 2, 3]);
        cache.MarkSurahAudioComplete(RecitationId.QuranCom(7), 1);

        Assert.True(cache.HasSurahAudio(RecitationId.QuranCom(7), 1));
        Assert.Equal([1], cache.CachedSurahsFor(RecitationId.QuranCom(7)));
        Assert.Equal([RecitationId.QuranCom(7)], cache.CachedRecitations());
        Assert.Single(cache.ReadTimings(RecitationId.QuranCom(7), 1)!);
    }

    [Fact]
    public async Task DeleteSurahAudio_RemovesFilesAndManifestEntry()
    {
        var cache = new ContentCache(_root);
        await cache.WriteTimingsAsync(RecitationId.QuranCom(7), 1, []);
        Directory.CreateDirectory(Path.GetDirectoryName(cache.AudioPath(RecitationId.QuranCom(7), 1))!);
        await File.WriteAllBytesAsync(cache.AudioPath(RecitationId.QuranCom(7), 1), [1]);
        cache.MarkSurahAudioComplete(RecitationId.QuranCom(7), 1);

        cache.DeleteSurahAudio(RecitationId.QuranCom(7), 1);

        Assert.False(cache.HasSurahAudio(RecitationId.QuranCom(7), 1));
        Assert.False(File.Exists(cache.AudioPath(RecitationId.QuranCom(7), 1)));
        Assert.False(File.Exists(cache.TimingsPath(RecitationId.QuranCom(7), 1)));
    }

    [Fact]
    public void VersionOneManifest_IsUpgraded_SoTheLibrarySurvives()
    {
        // Before recitation ids, audio was keyed by a bare Quran.com number.
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "manifest.json"), """
            {"Version":1,"TextSurahs":[1,112],"AudioByReciter":{"7":[1,112],"2":[1]},
             "TranslationsByResource":{"20":[1]},"UnavailableByReciter":{"8":[1]}}
            """);

        var cache = new ContentCache(_root);

        Assert.True(cache.HasSurahAudio(RecitationId.QuranCom(7), 112));
        Assert.Equal([1], cache.CachedSurahsFor(RecitationId.QuranCom(2)));
        Assert.True(cache.IsUnavailable(RecitationId.QuranCom(8), 1));
        Assert.True(cache.HasSurahText(112));
        Assert.True(cache.HasSurahTranslation(20, 1));

        // Written back in the new shape, so the upgrade happens once.
        var text = File.ReadAllText(Path.Combine(_root, "manifest.json"));
        Assert.Contains("qurancom:7", text);
        Assert.Contains("\"Version\":2", text);
        Assert.True(new ContentCache(_root).HasSurahAudio(RecitationId.QuranCom(7), 112));
    }

    [Fact]
    public void LegacyAudioFolders_AreMovedUnderTheirSource()
    {
        Directory.CreateDirectory(Path.Combine(_root, "audio", "7"));
        File.WriteAllBytes(Path.Combine(_root, "audio", "7", "1.mp3"), [1, 2, 3]);

        var cache = new ContentCache(_root);

        Assert.True(File.Exists(cache.AudioPath(RecitationId.QuranCom(7), 1)));
        Assert.False(Directory.Exists(Path.Combine(_root, "audio", "7")));
    }

    [Fact]
    public async Task AnEmptyIndexIsRebuiltFromTheFilesOnDisk()
    {
        // The files are the truth; the manifest is only an index. Losing it must not cost a re-download.
        var seed = new ContentCache(_root);
        foreach (var surah in new[] { 1, 112 })
        {
            await seed.WriteTimingsAsync(RecitationId.QuranCom(7), surah, []);
            await File.WriteAllBytesAsync(seed.AudioPath(RecitationId.QuranCom(7), surah), [1]);
        }
        await seed.WriteSurahTextAsync(112, []);
        await seed.WriteSurahTranslationAsync(new SurahTranslation(20, 112, ["x"]));

        // A surah whose timings never arrived is not counted as present.
        await File.WriteAllBytesAsync(seed.AudioPath(RecitationId.QuranCom(7), 113), [1]);

        File.WriteAllText(Path.Combine(_root, "manifest.json"), """{"Version":2,"AudioByReciter":{}}""");

        var cache = new ContentCache(_root);

        Assert.Equal([1, 112], cache.CachedSurahsFor(RecitationId.QuranCom(7)));
        Assert.False(cache.HasSurahAudio(RecitationId.QuranCom(7), 113));
        Assert.True(cache.HasSurahText(112));
        Assert.True(cache.HasSurahTranslation(20, 112));
    }

    [Fact]
    public void CorruptManifest_StartsEmptyInsteadOfThrowing()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "manifest.json"), "{ not json");

        var cache = new ContentCache(_root);

        Assert.False(cache.HasSurahText(1));
    }

    [Fact]
    public async Task Translation_RoundTrips()
    {
        var cache = new ContentCache(_root);
        await cache.WriteSurahTranslationAsync(new SurahTranslation(20, 112, ["a", "b", "c", "d"]));

        Assert.True(cache.HasSurahTranslation(20, 112));
        Assert.Equal(["a", "b", "c", "d"], cache.ReadSurahTranslation(20, 112)!.Texts);
    }
}
