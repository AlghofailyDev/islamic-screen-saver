namespace IslamicScreensaver.Core.Screensaver;

/// <summary>
/// Decides when user input should end the screensaver. Mirrors stock Windows behaviour:
/// keys and clicks exit immediately; the mouse has to actually travel (not just jitter) and nothing
/// the pointer does during a short start-up grace period counts — Windows and WPF both synthesise
/// mouse-move events as windows appear.
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

    public TimeSpan GracePeriod => _gracePeriod;

    /// <summary>A key press, click or wheel tick. Always exits.</summary>
    public bool ShouldExitOnDiscreteInput() => true;

    /// <summary>
    /// A pointer position in screen pixels. Positions inside the grace period are discarded. The first
    /// position after it becomes the origin; movement is measured from there so slow drift still adds up.
    /// The host should feed the real cursor position once the grace period ends so the origin is
    /// the resting position rather than whatever event happens to arrive first.
    /// </summary>
    public bool ShouldExitOnMouseMove(TimeSpan elapsed, double x, double y)
    {
        if (elapsed < _gracePeriod)
            return false;

        if (_origin is null)
        {
            _origin = (x, y);
            return false;
        }

        var dx = x - _origin.Value.X;
        var dy = y - _origin.Value.Y;
        return Math.Sqrt(dx * dx + dy * dy) >= _moveThreshold;
    }
}
