using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Timing;

namespace IslamicScreensaver.Tests.Timing;

public class VerseClockTests
{
    private static VerseClock Clock() => new(
    [
        new VerseTiming(new VerseKey(1, 2), 6090, 11680, [new(1, 6025, 7025), new(2, 7025, 7885), new(3, 7885, 8515), new(4, 8515, 11550)]),
        new VerseTiming(new VerseKey(1, 1), 1000, 6090, [new(1, 1000, 2000), new(2, 2000, 3000)]),
        new VerseTiming(new VerseKey(1, 3), 11680, 15000, []),
    ]);

    [Fact]
    public void SortsTimingsByStart()
    {
        var clock = Clock();
        Assert.Equal(3, clock.VerseCount);
        Assert.Equal(new VerseKey(1, 1), clock[0].Key);
        Assert.Equal(new VerseKey(1, 3), clock[2].Key);
    }

    [Fact]
    public void BeforeFirstVerse_IsBeforeFirst()
    {
        Assert.Equal(VerseClock.Position.BeforeFirst, Clock().Lookup(0));
        Assert.Equal(VerseClock.Position.BeforeFirst, Clock().Lookup(999));
    }

    [Theory]
    [InlineData(1000, 0, 1)]
    [InlineData(2500, 0, 2)]
    [InlineData(6024, 0, 2)]   // just before verse 2's first word: still verse 1
    [InlineData(6025, 1, 1)]   // verse 2 begins with its first word, 65 ms before the verse-level timestamp
    [InlineData(6090, 1, 1)]
    [InlineData(7900, 1, 3)]
    [InlineData(11600, 1, 4)]  // past the last word's end but still inside the verse: last word stays lit
    [InlineData(11680, 2, 0)]  // verse with no word timings
    [InlineData(99999, 2, 0)]  // after everything: last verse holds
    public void Lookup_FindsVerseAndWord(int ms, int verse, int word)
    {
        Assert.Equal(new VerseClock.Position(verse, word), Clock().Lookup(ms));
    }

    [Fact]
    public void StartOf_ReturnsVerseStart_OrZeroWhenOutOfRange()
    {
        var clock = Clock();
        Assert.Equal(6025, clock.StartOf(1));
        Assert.Equal(0, clock.StartOf(-1));
        Assert.Equal(0, clock.StartOf(3));
    }

    [Fact]
    public void EmptyTimings_AlwaysBeforeFirst()
    {
        var clock = new VerseClock([]);
        Assert.Equal(0, clock.VerseCount);
        Assert.Equal(VerseClock.Position.BeforeFirst, clock.Lookup(5000));
    }
}
