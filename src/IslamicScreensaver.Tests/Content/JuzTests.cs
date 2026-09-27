using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class JuzTests
{
    [Theory]
    [InlineData(1, 1, 1)]        // the very first verse
    [InlineData(2, 141, 1)]      // last verse before juz' 2
    [InlineData(2, 142, 2)]      // سيقول السفهاء — the boundary itself
    [InlineData(2, 252, 2)]
    [InlineData(2, 253, 3)]
    [InlineData(18, 74, 15)]
    [InlineData(18, 75, 16)]
    [InlineData(67, 1, 29)]      // تبارك
    [InlineData(77, 50, 29)]     // still juz' 29, right up to the end
    [InlineData(78, 1, 30)]      // عمَّ
    [InlineData(114, 6, 30)]     // the very last verse
    public void EveryBoundaryLandsInTheRightPart(int surah, int ayah, int expected)
    {
        Assert.Equal(expected, Juz.Of(surah, ayah));
    }

    [Fact]
    public void TheNamesAreTheOnesAMushafPrints()
    {
        Assert.Equal("عمَّ", Juz.Name(30, arabic: true));
        Assert.Equal("تبارك", Juz.Name(29, arabic: true));
        Assert.Equal("'Amma", Juz.Name(30, arabic: false));
    }

    [Fact]
    public void EveryPartHasAStartAndTwoNames()
    {
        for (var juz = 1; juz <= Juz.Count; juz++)
        {
            Assert.NotEmpty(Juz.Name(juz, arabic: true));
            Assert.NotEmpty(Juz.Name(juz, arabic: false));
            Assert.Equal(juz, Juz.Of(Juz.StartOf(juz)));
        }
    }

    [Fact]
    public void ThePartsRunForwardAndCoverTheWholeQuran()
    {
        // Each part starts after the one before it, and walking every verse never leaves 1..30.
        for (var juz = 2; juz <= Juz.Count; juz++)
            Assert.True(Juz.StartOf(juz).CompareTo(Juz.StartOf(juz - 1)) > 0);

        var seen = new HashSet<int>();
        for (var surah = 1; surah <= 114; surah++)
            for (var ayah = 1; ayah <= 286; ayah++)
                seen.Add(Juz.Of(surah, ayah));

        Assert.Equal(Juz.Count, seen.Count);
    }

    [Fact]
    public void NonsenseInputIsClampedRatherThanThrown()
    {
        // The panel asks for whatever is playing; a bad number must not take the screensaver down.
        Assert.Equal(1, Juz.Of(0, 0));
        Assert.Equal(30, Juz.Of(200, 1));
        Assert.NotEmpty(Juz.Name(0, arabic: true));
        Assert.NotEmpty(Juz.Name(99, arabic: true));
    }
}
