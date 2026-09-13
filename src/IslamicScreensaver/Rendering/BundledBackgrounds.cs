using System.Windows;
using System.Windows.Media;

namespace IslamicScreensaver.Rendering;

/// <summary>
/// The calm visuals shipped with the app, drawn procedurally so they are resolution-independent and
/// carry no asset licensing. Each returns its layers bottom-up plus the panel tint that suits it.
/// </summary>
internal static class BundledBackgrounds
{
    public const string Dawn = "dawn";
    public const string Night = "night";

    public static IReadOnlyList<string> Names { get; } = [Dawn, Night];

    public sealed record Visual(IReadOnlyList<Brush> Layers, Color PanelTint);

    public static Visual Get(string? name) => name?.ToLowerInvariant() switch
    {
        Night => BuildNight(),
        _ => BuildDawn(),
    };

    private static Visual BuildDawn()
    {
        var sky = Linear(
            (0.00, "#0D2A36"),
            (0.38, "#1A4A57"),
            (0.70, "#7A7A6A"),
            (1.00, "#D9A56A"));

        var horizonGlow = Radial(new Point(0.5, 1.0), 0.70, 0.55, Rgba(236, 160, 84, 0.55));
        var tealGlow = Radial(new Point(0.15, 0.20), 0.60, 0.50, Rgba(52, 120, 132, 0.60));

        return new Visual([sky, horizonGlow, tealGlow], Hex("#0F2A2E"));
    }

    private static Visual BuildNight()
    {
        var sky = Linear(
            (0.00, "#060A16"),
            (0.55, "#0D1730"),
            (1.00, "#142240"));

        var moonGlow = Radial(new Point(0.78, 0.18), 0.55, 0.45, Rgba(96, 120, 200, 0.28));
        var horizon = Radial(new Point(0.5, 1.0), 0.70, 0.40, Rgba(30, 60, 90, 0.70));

        return new Visual([sky, moonGlow, horizon, Stars(), Geometry()], Hex("#101A3A"));
    }

    // --- Brush helpers ---------------------------------------------------------------------

    private static LinearGradientBrush Linear(params (double Offset, string Hex)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        foreach (var (offset, hex) in stops)
            brush.GradientStops.Add(new GradientStop(Hex(hex), offset));
        return Frozen(brush);
    }

    /// <summary>A soft elliptical glow fading to transparent, positioned in relative coordinates.</summary>
    private static RadialGradientBrush Radial(Point center, double radiusX, double radiusY, Color color)
    {
        var brush = new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = radiusX,
            RadiusY = radiusY,
        };
        brush.GradientStops.Add(new GradientStop(color, 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0.7));
        return Frozen(brush);
    }

    /// <summary>A sparse tiled star field: a few soft dots per 420 px tile.</summary>
    private static DrawingBrush Stars()
    {
        var group = new DrawingGroup();
        (double X, double Y, double R, double A)[] dots =
        [
            (22, 38, 1.0, 0.7), (130, 84, 0.7, 0.5), (260, 30, 1.1, 0.8), (330, 150, 0.6, 0.45),
            (80, 210, 0.8, 0.55), (200, 260, 1.0, 0.6), (380, 300, 0.7, 0.5), (300, 390, 0.9, 0.65),
            (150, 360, 0.6, 0.4), (40, 330, 1.0, 0.6),
        ];
        foreach (var (x, y, r, a) in dots)
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Rgba(255, 255, 255, a)), null, new EllipseGeometry(new Point(x, y), r, r)));

        return Tile(group, 420);
    }

    /// <summary>The eight-point star motif: two squares, one rotated 45°, joined by connectors — very faint.</summary>
    private static DrawingBrush Geometry()
    {
        const double size = 140;
        var pen = new Pen(new SolidColorBrush(Rgba(255, 255, 255, 0.07)), 1);
        var group = new DrawingGroup();

        var square = new RectangleGeometry(new Rect(30, 30, 80, 80));
        var rotated = new RectangleGeometry(new Rect(30, 30, 80, 80)) { Transform = new RotateTransform(45, 70, 70) };
        group.Children.Add(new GeometryDrawing(null, pen, square));
        group.Children.Add(new GeometryDrawing(null, pen, rotated));

        var connectors = new PathGeometry();
        foreach (var (a, b) in new[] { (new Point(70, 0), new Point(70, 30)), (new Point(70, 110), new Point(70, 140)), (new Point(0, 70), new Point(30, 70)), (new Point(110, 70), new Point(140, 70)) })
            connectors.AddGeometry(new LineGeometry(a, b));
        group.Children.Add(new GeometryDrawing(null, pen, connectors));

        return Tile(group, size);
    }

    private static DrawingBrush Tile(Drawing drawing, double size)
    {
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, size, size),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, size, size),
            Stretch = Stretch.None,
        };
        return Frozen(brush);
    }

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Color Rgba(byte r, byte g, byte b, double a) => Color.FromArgb((byte)Math.Round(a * 255), r, g, b);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
