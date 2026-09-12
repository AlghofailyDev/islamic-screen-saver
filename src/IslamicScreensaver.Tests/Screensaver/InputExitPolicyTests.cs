using IslamicScreensaver.Core.Screensaver;

namespace IslamicScreensaver.Tests.Screensaver;

public class InputExitPolicyTests
{
    private static InputExitPolicy Policy() => new(TimeSpan.FromMilliseconds(500), moveThreshold: 8);

    [Fact]
    public void KeyOrClick_AlwaysExits()
    {
        Assert.True(Policy().ShouldExitOnDiscreteInput());
    }

    [Fact]
    public void FirstMouseMove_NeverExits()
    {
        Assert.False(Policy().ShouldExitOnMouseMove(TimeSpan.FromSeconds(10), 100, 100));
    }

    [Fact]
    public void MovementDuringGracePeriod_IsIgnored()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(TimeSpan.Zero, 100, 100);
        Assert.False(p.ShouldExitOnMouseMove(TimeSpan.FromMilliseconds(100), 500, 500));
    }

    [Fact]
    public void JitterBelowThreshold_IsIgnored()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(TimeSpan.Zero, 100, 100);
        Assert.False(p.ShouldExitOnMouseMove(TimeSpan.FromSeconds(1), 104, 103));
    }

    [Fact]
    public void RealMovementAfterGrace_Exits()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(TimeSpan.Zero, 100, 100);
        Assert.True(p.ShouldExitOnMouseMove(TimeSpan.FromSeconds(1), 110, 100));
    }

    [Fact]
    public void SlowDrift_AccumulatesFromOrigin()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(TimeSpan.Zero, 0, 0);
        Assert.False(p.ShouldExitOnMouseMove(TimeSpan.FromSeconds(1), 3, 0));
        Assert.False(p.ShouldExitOnMouseMove(TimeSpan.FromSeconds(2), 6, 0));
        Assert.True(p.ShouldExitOnMouseMove(TimeSpan.FromSeconds(3), 9, 0));
    }
}
