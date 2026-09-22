using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class Mp3QuranParserTests
{
    // The shape mp3quran.net returns, trimmed to the fields we read. Captured 2026-09-22.
    private const string SurahTwo = """
        [{"ayah":0,"start_time":0,"end_time":6444,"page":"…"},
         {"ayah":1,"start_time":6444,"end_time":15444},
         {"ayah":2,"start_time":15444,"end_time":25188},
         {"ayah":3,"start_time":25188,"end_time":66300}]
        """;

    [Fact]
    public void TheBasmalahMarkerIsNotAVerse()
    {
        // Ayah 0 is this source's way of labelling the basmalah before the surah. Kept as a verse it
        // would shift every ayah by one and put the wrong text on screen for the whole surah.
        var timings = Mp3QuranParser.ParseTimings(SurahTwo, 2);

        Assert.Equal(3, timings.Count);
        Assert.Equal(new VerseKey(2, 1), timings[0].Key);
        Assert.Equal(6444, timings[0].FromMs);
        Assert.Equal(15444, timings[0].ToMs);
    }

    [Fact]
    public void VerseEndsComeFromTheSource_NotFromTheNextVerse()
    {
        var timings = Mp3QuranParser.ParseTimings(SurahTwo, 2);
        Assert.Equal(66300, timings[^1].ToMs);
    }

    [Fact]
    public void ThereAreNoWordSegments_SoTheHighlightHasNothingToFollow()
    {
        Assert.All(Mp3QuranParser.ParseTimings(SurahTwo, 2), t => Assert.Empty(t.Words));
    }

    [Fact]
    public void ASurahThatOpensWithoutTheBasmalahParsesUnchanged()
    {
        var timings = Mp3QuranParser.ParseTimings("""[{"ayah":1,"start_time":100,"end_time":8360}]""", 9);

        Assert.Equal(new VerseKey(9, 1), Assert.Single(timings).Key);
        Assert.Equal(100, timings[0].FromMs);
    }

    [Fact]
    public void AnEmptyResponseIsAnError_NotASilentlyUnsyncedSurah()
    {
        // Some reads are listed as timed and return nothing; that must not be written to the cache
        // as "this surah has no verses".
        Assert.Throws<FormatException>(() => Mp3QuranParser.ParseTimings("[]", 2));
        Assert.Throws<FormatException>(() => Mp3QuranParser.ParseTimings("""[{"ayah":0,"start_time":0,"end_time":5}]""", 2));
    }
}
