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
        Assert.False(cache.HasSurahAudio(7, 1));
        Assert.False(cache.HasSurahTranslation(20, 1));
        Assert.Empty(cache.CachedReciters());
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
        await cache.WriteTimingsAsync(7, 1, [new VerseTiming(new VerseKey(1, 1), 0, 100, [])]);

        Assert.False(cache.HasSurahAudio(7, 1));
        Assert.Throws<InvalidOperationException>(() => cache.MarkSurahAudioComplete(7, 1));

        Directory.CreateDirectory(Path.GetDirectoryName(cache.AudioPath(7, 1))!);
        await File.WriteAllBytesAsync(cache.AudioPath(7, 1), [1, 2, 3]);
        cache.MarkSurahAudioComplete(7, 1);

        Assert.True(cache.HasSurahAudio(7, 1));
        Assert.Equal([1], cache.CachedSurahsFor(7));
        Assert.Equal([7], cache.CachedReciters());
        Assert.Single(cache.ReadTimings(7, 1)!);
    }

    [Fact]
    public async Task DeleteSurahAudio_RemovesFilesAndManifestEntry()
    {
        var cache = new ContentCache(_root);
        await cache.WriteTimingsAsync(7, 1, []);
        Directory.CreateDirectory(Path.GetDirectoryName(cache.AudioPath(7, 1))!);
        await File.WriteAllBytesAsync(cache.AudioPath(7, 1), [1]);
        cache.MarkSurahAudioComplete(7, 1);

        cache.DeleteSurahAudio(7, 1);

        Assert.False(cache.HasSurahAudio(7, 1));
        Assert.False(File.Exists(cache.AudioPath(7, 1)));
        Assert.False(File.Exists(cache.TimingsPath(7, 1)));
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
