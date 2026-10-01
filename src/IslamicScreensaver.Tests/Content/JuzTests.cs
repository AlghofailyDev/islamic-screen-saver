using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Content;

public sealed class JuzTests : IDisposable
{
    public void Dispose() => Strings.Use(AppLanguage.English);

    /// <summary>
    /// The first verse of every juzʾ, exactly as Quran.com publishes them at api/v4/juzs (fetched
    /// 2026-10-01) — the Madani muṣḥaf, whose text and ۞ marks are what the panel shows. Every one of
    /// these verses carries a ۞ in that text. If this list and the table in Juz ever disagree, the
    /// table is wrong.
    /// </summary>
    public static readonly TheoryData<int, int, int> QuranComStarts = new()
    {
        { 1, 1, 1 },    { 2, 2, 142 },  { 3, 2, 253 },  { 4, 3, 93 },   { 5, 4, 24 },
        { 6, 4, 148 },  { 7, 5, 82 },   { 8, 6, 111 },  { 9, 7, 88 },   { 10, 8, 41 },
        { 11, 9, 93 },  { 12, 11, 6 },  { 13, 12, 53 }, { 14, 15, 1 },  { 15, 17, 1 },
        { 16, 18, 75 }, { 17, 21, 1 },  { 18, 23, 1 },  { 19, 25, 21 }, { 20, 27, 56 },
        { 21, 29, 46 }, { 22, 33, 31 }, { 23, 36, 28 }, { 24, 39, 32 }, { 25, 41, 47 },
        { 26, 46, 1 },  { 27, 51, 31 }, { 28, 58, 1 },  { 29, 67, 1 },  { 30, 78, 1 },
    };

    [Theory]
    [MemberData(nameof(QuranComStarts))]
    public void EveryBoundaryIsTheMadaniOne(int juz, int surah, int ayah)
    {
        Assert.Equal(new VerseKey(surah, ayah), Juz.StartOf(juz));
        Assert.Equal(juz, Juz.Of(surah, ayah));                      // the opening verse is in it
        if (ayah > 1)
            Assert.Equal(juz - 1, Juz.Of(surah, ayah - 1));          // and the verse before is not
    }

    [Fact]
    public void LanTanaluIsTheLastVerseOfJuzThree()
    {
        // The boundary that was wrong. South Asian muṣḥafs start juzʾ 4 at 3:92, «لن تنالوا البر»;
        // the Madani muṣḥaf this app displays starts it one verse later, at «كل الطعام».
        Assert.Equal(3, Juz.Of(3, 92));
        Assert.Equal(4, Juz.Of(3, 93));
    }

    [Theory]
    [InlineData(2, 141, 1)]
    [InlineData(77, 50, 29)]     // still juzʾ 29, right up to the end of al-Mursalat
    [InlineData(114, 6, 30)]     // the very last verse
    public void VersesInsideAPartBelongToIt(int surah, int ayah, int expected)
    {
        Assert.Equal(expected, Juz.Of(surah, ayah));
    }

    [Fact]
    public void ThePartsRunForwardAndCoverTheWholeQuran()
    {
        for (var juz = 2; juz <= Juz.Count; juz++)
            Assert.True(Juz.StartOf(juz).CompareTo(Juz.StartOf(juz - 1)) > 0);

        var seen = new HashSet<int>();
        for (var surah = 1; surah <= 114; surah++)
            for (var ayah = 1; ayah <= 286; ayah++)
                seen.Add(Juz.Of(surah, ayah));

        Assert.Equal(Juz.Count, seen.Count);
    }

    [Theory]
    [InlineData(1, "Juz 1", "الجزء الأول")]
    [InlineData(4, "Juz 4", "الجزء الرابع")]
    [InlineData(11, "Juz 11", "الجزء الحادي عشر")]
    [InlineData(20, "Juz 20", "الجزء العشرون")]
    [InlineData(21, "Juz 21", "الجزء الحادي والعشرون")]
    [InlineData(30, "Juz 30", "الجزء الثلاثون")]
    public void ThePartIsNamedByItsNumber(int juz, string english, string arabic)
    {
        Strings.Use(AppLanguage.English);
        Assert.Equal(english, Strings.Get(Juz.LabelKey(juz)));
        Strings.Use(AppLanguage.Arabic);
        Assert.Equal(arabic, Strings.Get(Juz.LabelKey(juz)));
    }

    [Fact]
    public void EveryPartHasALabelInBothLanguages()
    {
        // A missing key comes back as the key itself, which would put "juz.17" on screen.
        for (var juz = 1; juz <= Juz.Count; juz++)
            Assert.Contains(Juz.LabelKey(juz), Strings.AllKeys);
    }

    [Fact]
    public void NonsenseInputIsClampedRatherThanThrown()
    {
        // The panel asks for whatever is playing; a bad number must not take the screensaver down.
        Assert.Equal(1, Juz.Of(0, 0));
        Assert.Equal(30, Juz.Of(200, 1));
        Assert.Equal("juz.1", Juz.LabelKey(0));
        Assert.Equal("juz.30", Juz.LabelKey(99));
    }
}
