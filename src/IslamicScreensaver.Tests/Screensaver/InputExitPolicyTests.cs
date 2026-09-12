using IslamicScreensaver.Core.Screensaver;

namespace IslamicScreensaver.Tests.Screensaver;

public class InputExitPolicyTests
{
    private static readonly TimeSpan AfterGrace = TimeSpan.FromSeconds(1);

    private static InputExitPolicy Policy() => new(TimeSpan.FromMilliseconds(500), moveThreshold: 8);

    [Fact]
    public void KeyOrClick_AlwaysExits()
    {
        Assert.True(Policy().ShouldExitOnDiscreteInput());
    }

    [Fact]
    public void FirstMoveAfterGrace_SetsOrigin_DoesNotExit()
    {
        Assert.False(Policy().ShouldExitOnMouseMove(AfterGrace, 100, 100));
    }

    [Fact]
    public void MovementDuringGrace_IsDiscardedEntirely()
    {
        var p = Policy();
        Assert.False(p.ShouldExitOnMouseMove(TimeSpan.Zero, 100, 100));
        Assert.False(p.ShouldExitOnMouseMove(TimeSpan.FromMilliseconds(100), 900, 900));
        // Origin is only taken now; the big jump above never counted.
        Assert.False(p.ShouldExitOnMouseMove(AfterGrace, 500, 500));
        Assert.False(p.ShouldExitOnMouseMove(AfterGrace, 503, 502));
    }

    [Fact]
    public void JitterBelowThreshold_IsIgnored()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(AfterGrace, 100, 100);
        Assert.False(p.ShouldExitOnMouseMove(AfterGrace, 104, 103));
    }

    [Fact]
    public void RealMovement_Exits()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(AfterGrace, 100, 100);
        Assert.True(p.ShouldExitOnMouseMove(AfterGrace, 110, 100));
    }

    [Fact]
    public void SlowDrift_AccumulatesFromOrigin()
    {
        var p = Policy();
        p.ShouldExitOnMouseMove(AfterGrace, 0, 0);
        Assert.False(p.ShouldExitOnMouseMove(AfterGrace, 3, 0));
        Assert.False(p.ShouldExitOnMouseMove(AfterGrace, 6, 0));
        Assert.True(p.ShouldExitOnMouseMove(AfterGrace, 9, 0));
    }
}
