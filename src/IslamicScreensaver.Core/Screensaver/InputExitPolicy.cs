namespace IslamicScreensaver.Core.Screensaver;

/// <summary>
/// Decides when user input should end the screensaver. Mirrors stock Windows behaviour:
/// keys and clicks exit immediately; the mouse has to actually travel (not just jitter, and not the
/// spurious move Windows sends when a window appears) and only counts after a short grace period.
/// </summary>
public sealed class InputExitPolicy
{
    private readonly TimeSpan _gracePeriod;
    private readonly double _moveThreshold;
    private (double X, double Y)? _origin;

    public InputExitPolicy(TimeSpan gracePeriod, double moveThreshold)
    {
        _gracePeriod = gracePeriod;
        _moveThreshold = moveThreshold;
    }

    public static InputExitPolicy Default { get; } = new(TimeSpan.FromMilliseconds(500), moveThreshold: 8);

    /// <summary>A key press, click or wheel tick. Always exits.</summary>
    public bool ShouldExitOnDiscreteInput() => true;

    /// <summary>
    /// A pointer position in screen pixels. The first position seen becomes the origin; movement is
    /// measured from there so slow drift still adds up to an exit.
    /// </summary>
    public bool ShouldExitOnMouseMove(TimeSpan elapsed, double x, double y)
    {
        if (_origin is null)
        {
            _origin = (x, y);
            return false;
        }

        if (elapsed < _gracePeriod)
            return false;

        var dx = x - _origin.Value.X;
        var dy = y - _origin.Value.Y;
        return Math.Sqrt(dx * dx + dy * dy) >= _moveThreshold;
    }
}
