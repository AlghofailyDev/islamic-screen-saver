using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;
using VerseKey = IslamicScreensaver.Core.Content.VerseKey;

namespace IslamicScreensaver.Tests.Playlist;

public sealed class BasmalahSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));
    private readonly ContentCache _cache;

    public BasmalahSourceTests()
    {
        _cache = new ContentCache(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task GiveAlFatihah(int reciterId, IReadOnlyList<VerseTiming>? timings = null)
    {
        timings ??=
        [
            new VerseTiming(new VerseKey(1, 1), 0, 6090, [new WordSegment(1, 0, 580)]),
            new VerseTiming(new VerseKey(1, 2), 6090, 11680, [new WordSegment(1, 6025, 7025)]),
        ];
        await _cache.WriteTimingsAsync(RecitationId.QuranCom(reciterId), 1, timings);
        Directory.CreateDirectory(Path.GetDirectoryName(_cache.AudioPath(RecitationId.QuranCom(reciterId), 1))!);
        await File.WriteAllBytesAsync(_cache.AudioPath(RecitationId.QuranCom(reciterId), 1), new byte[16]);
        _cache.MarkSurahAudioComplete(RecitationId.QuranCom(reciterId), 1);
    }

    [Fact]
    public async Task WithAlFatihah_SlicesItsFirstVerse()
    {
        await GiveAlFatihah(7);

        var clip = BasmalahSource.For(_cache, RecitationId.QuranCom(7));

        Assert.NotNull(clip);
        Assert.Equal(_cache.AudioPath(RecitationId.QuranCom(7), 1), clip.Path);
        Assert.Equal(TimeSpan.Zero, clip.Start);
        // Verse 1 runs until verse 2 begins (its first word), which is what the listener hears as the basmalah.
        Assert.Equal(TimeSpan.FromMilliseconds(6025), clip.Duration);
        Assert.True(BasmalahSource.IsAvailable(_cache, RecitationId.QuranCom(7)));
    }

    [Fact]
    public void WithoutAlFatihah_ThereIsNoBasmalah()
    {
        Assert.Null(BasmalahSource.For(_cache, RecitationId.QuranCom(7)));
        Assert.False(BasmalahSource.IsAvailable(_cache, RecitationId.QuranCom(7)));
    }

    [Fact]
    public async Task WithAlFatihahButNoTimings_ThereIsNoBasmalah()
    {
        await GiveAlFatihah(7, timings: []);
        Assert.Null(BasmalahSource.For(_cache, RecitationId.QuranCom(7)));
    }

    [Fact]
    public async Task RecitersMissingIt_NamesOnlyTheOnesThatCannotSupplyIt()
    {
        await GiveAlFatihah(7);
        await GiveAlFatihah(2);

        // 1 and 4 have other surahs but no Al-Fatihah — exactly the case that made this silent before.
        Assert.Equal([RecitationId.QuranCom(1), RecitationId.QuranCom(4)], BasmalahSource.RecitationsMissingIt(_cache, new[] { 7, 2, 1, 4, 7 }.Select(RecitationId.QuranCom)));
        Assert.Empty(BasmalahSource.RecitationsMissingIt(_cache, new[] { 7, 2 }.Select(RecitationId.QuranCom)));
    }
}
