using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class SurahRangeTests
{
    [Theory]
    [InlineData("", new int[0])]
    [InlineData("1", new[] { 1 })]
    [InlineData("1,2", new[] { 1, 2 })]              // two in a row stay a list, not a range
    [InlineData("1-3", new[] { 1, 2, 3 })]
    [InlineData("1,4-6,9", new[] { 9, 6, 1, 5, 4 })] // sorted and grouped on the way out
    public void Format_CollapsesRuns(string expected, int[] surahs)
    {
        Assert.Equal(expected, SurahRange.Format(surahs));
    }

    [Fact]
    public void Format_KeepsAGapBetweenRuns()
    {
        // The shape a "juz' 'amma only" recording takes.
        Assert.Equal("1,78-114", SurahRange.Format([1, .. Enumerable.Range(78, 37)]));
    }

    [Fact]
    public void Format_DropsDuplicatesAndSurahsOutsideTheQuran()
    {
        Assert.Equal("1,114", SurahRange.Format([1, 1, 0, 115, 114, -3]));
    }

    [Theory]
    [InlineData("1,4-6,9", new[] { 1, 4, 5, 6, 9 })]
    [InlineData(" 1 , 3 ", new[] { 1, 3 })]
    [InlineData("", new int[0])]
    [InlineData("6-4", new int[0])]                            // a backwards range yields nothing
    [InlineData("1,oops,3", new[] { 1, 3 })]                   // junk is skipped, the rest survives
    [InlineData("110-120", new[] { 110, 111, 112, 113, 114 })] // clamped to the 114 surahs
    public void Parse_ReadsWhatItCan(string text, int[] expected)
    {
        Assert.Equal(expected, SurahRange.Parse(text));
    }

    [Fact]
    public void RoundTrips_ThroughEveryShape()
    {
        int[] surahs = [1, 2, 3, 7, 9, 10, 11, 114];
        Assert.Equal(surahs, SurahRange.Parse(SurahRange.Format(surahs)));
    }

    [Fact]
    public void Catalog_WritesSurahsAsARangeAndReadsThemBack()
    {
        var recitation = new Recitation(
            RecitationId.Mp3Quran(1), "mq-1", "حفص عن عاصم", "مرتل", "Murattal",
            TimingLevel.Verse, [1, 2, 3, 112, 113, 114]);
        var catalog = new Catalog(Catalog.CurrentVersion, DateTimeOffset.UnixEpoch,
            [new ReciterEntry("mq-1", "إبراهيم الأخضر", "Ibrahim Al-Akdar", [recitation])]);

        var json = CatalogStore.Serialize(catalog);

        Assert.Contains("\"1-3,112-114\"", json);
        Assert.Contains("إبراهيم الأخضر", json);   // Arabic is not escaped away

        var back = CatalogStore.Parse(json)?.Find(RecitationId.Mp3Quran(1));
        Assert.NotNull(back);
        Assert.Equal(recitation.AvailableSurahs, back.AvailableSurahs);
        Assert.Equal(recitation.RiwayahArabic, back.RiwayahArabic);
        Assert.Equal(recitation.Timing, back.Timing);
        Assert.Equal("mq-1", back.ReciterId);
    }

    [Fact]
    public void ARecitationKnowsWhichOfItsSurahsThePanelCanFollow()
    {
        // mp3quran.net times a recording surah by surah, so some sets are only part-way done.
        var partly = new Recitation(
            RecitationId.Mp3Quran(1), "mq-1", "حفص عن عاصم", null, null, TimingLevel.Verse, [1, 2, 3])
        {
            TimedSurahs = [1, 3],
        };

        Assert.True(partly.HasSurah(2));
        Assert.False(partly.IsTimed(2));
        Assert.True(partly.IsTimed(3));

        var untimed = partly with { Timing = TimingLevel.None, TimedSurahs = [] };
        Assert.True(untimed.HasSurah(1));
        Assert.False(untimed.IsTimed(1));
    }

    [Fact]
    public void AnUnknownBasmalahStaysUnknownAcrossTheFile()
    {
        // Null is not false: "we could not tell" must not read back as "there is no basmalah".
        var recitation = new Recitation(
            RecitationId.Mp3Quran(1), "mq-1", "حفص عن عاصم", null, null, TimingLevel.None, [2]);
        var json = CatalogStore.Serialize(new Catalog(Catalog.CurrentVersion, DateTimeOffset.UnixEpoch,
            [new ReciterEntry("mq-1", "ا", "A", [recitation])]));

        Assert.DoesNotContain("AudioIncludesBasmalah", json);
        Assert.Null(CatalogStore.Parse(json)!.Find(RecitationId.Mp3Quran(1))!.AudioIncludesBasmalah);
    }

    [Fact]
    public void Catalog_FromANewerVersionIsRefusedRatherThanHalfRead()
    {
        var json = CatalogStore.Serialize(
            new Catalog(Catalog.CurrentVersion + 1, DateTimeOffset.UnixEpoch, []));

        Assert.Null(CatalogStore.Parse(json));
    }

    [Fact]
    public void Recitation_ResolvesBothUrlShapes()
    {
        var recitation = new Recitation(
            RecitationId.Mp3Quran(1), "mq-1", "حفص عن عاصم", null, null, TimingLevel.Verse, [7])
        {
            AudioUrlTemplate = "https://server6.mp3quran.net/akdr/{surah:000}.mp3",
            TimingsUrlTemplate = "https://mp3quran.net/api/v3/ayat_timing?surah={surah}&read=1",
        };

        Assert.Equal("https://server6.mp3quran.net/akdr/007.mp3", recitation.ResolveUrl(7));
        Assert.Equal("https://mp3quran.net/api/v3/ayat_timing?surah=7&read=1", recitation.ResolveTimingsUrl(7));
        Assert.Equal("حفص عن عاصم", recitation.Describe(arabic: true));
    }
}
