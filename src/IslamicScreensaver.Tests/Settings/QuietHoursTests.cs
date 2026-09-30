using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Settings;

public sealed class QuietHoursTests
{
    private static QuietHours Night(int fromHour = 23, int toHour = 7) =>
        new() { Enabled = true, From = new TimeOnly(fromHour, 0), To = new TimeOnly(toHour, 0) };

    private static TimeOnly At(int hour, int minute = 0) => new(hour, minute);

    [Fact]
    public void OffByDefault_SoNothingChangesUntilItIsAskedFor()
    {
        var quiet = new QuietHours();

        Assert.False(quiet.Enabled);
        Assert.False(quiet.Covers(At(3)));
        Assert.Null(quiet.TimeUntilStart(At(20)));
    }

    [Theory]
    // 23:00 → 07:00, the shape of a night: the window runs through midnight.
    [InlineData(23, 0, true)]     // the moment it opens
    [InlineData(23, 59, true)]
    [InlineData(0, 0, true)]      // past midnight, still inside
    [InlineData(3, 0, true)]      // the hour that started all this
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]     // the end is exclusive: 07:00 is morning
    [InlineData(12, 0, false)]
    [InlineData(22, 59, false)]
    public void AnOvernightWindowWrapsPastMidnight(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, Night().Covers(At(hour, minute)));
    }

    [Theory]
    // 13:00 → 15:00, an afternoon window that does not wrap.
    [InlineData(12, false)]
    [InlineData(13, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    [InlineData(23, false)]
    [InlineData(3, false)]
    public void AWindowInsideOneDayDoesNotWrap(int hour, bool expected)
    {
        Assert.Equal(expected, Night(13, 15).Covers(At(hour)));
    }

    [Fact]
    public void TwoIdenticalTimesAreNoWindowAtAll_NotASilentForever()
    {
        // The dangerous reading of From == To would silence the screensaver permanently.
        var quiet = new QuietHours { Enabled = true, From = At(22), To = At(22) };

        Assert.False(quiet.Covers(At(22)));
        Assert.False(quiet.Covers(At(3)));
        Assert.Null(quiet.TimeUntilStart(At(21)));
    }

    [Fact]
    public void SwitchingItOffStopsItCovering_WhateverTheTimes()
    {
        var quiet = Night();
        quiet.Enabled = false;
        Assert.False(quiet.Covers(At(3)));
    }

    [Theory]
    [InlineData(22, 0, 60)]        // an hour before it opens
    [InlineData(20, 30, 150)]
    [InlineData(22, 59, 1)]
    [InlineData(7, 0, 960)]        // just left it: the next one is tonight, 16 hours off
    public void TimeUntilStart_CountsForwardToTheNextOpening(int hour, int minute, int expectedMinutes)
    {
        var until = Night().TimeUntilStart(At(hour, minute));

        Assert.NotNull(until);
        Assert.Equal(expectedMinutes, (int)until.Value.TotalMinutes);
    }

    [Fact]
    public void NoCountdownWhileAlreadyInside()
    {
        // The session goes quiet at launch in that case, not on a timer.
        Assert.Null(Night().TimeUntilStart(At(3)));
        Assert.Null(Night().TimeUntilStart(At(23)));
    }

    [Fact]
    public void TheWindowIsDescribedForTheLogAndThePage()
    {
        Assert.Equal("23:00 – 07:00", Night().Describe());
    }

    [Fact]
    public void ANonsenseBehaviourFallsBackRatherThanThrowing()
    {
        // A hand-edited settings file should not be able to produce an undefined behaviour.
        var settings = new AppSettings { Quiet = new QuietHours { Behaviour = (QuietHoursBehaviour)99 } };

        settings.Normalize();

        Assert.Equal(QuietHoursBehaviour.DarkAndSilent, settings.Quiet.Behaviour);
    }

    [Theory]
    [InlineData(0.4, 0.7)]     // below the range
    [InlineData(9.0, 1.5)]     // above it
    [InlineData(double.NaN, 1.0)]
    [InlineData(1.2, 1.2)]     // a real choice is left alone
    public void TextScaleIsClampedToSomethingReadable(double stored, double expected)
    {
        var settings = new AppSettings();
        settings.Panel.TextScale = stored;

        settings.Normalize();

        Assert.Equal(expected, settings.Panel.TextScale);
    }
}
