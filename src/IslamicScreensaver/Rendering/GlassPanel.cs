using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace IslamicScreensaver.Rendering;

/// <summary>
/// A frosted-glass surface: whatever is behind it (the <see cref="Backdrop"/> visual) is sampled,
/// blurred, tinted and grained, with a hairline edge. XAML children are laid on top.
/// </summary>
/// <remarks>
/// Built on WPF's own GPU <see cref="BlurEffect"/> over a live <see cref="VisualBrush"/> rather than a
/// custom shader, so there is no shader-compiler step. The blurred rectangle overscans the panel by
/// twice the radius and is clipped back, which keeps the edges crisp instead of bleeding transparent.
/// </remarks>
public sealed class GlassPanel : Grid
{
    private const double GrainStrength = 0.16;
    private static readonly Brush EdgeBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)));

    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop), typeof(Visual), typeof(GlassPanel), new PropertyMetadata(null, (d, e) => ((GlassPanel)d)._brush.Visual = (Visual?)e.NewValue));

    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius), typeof(double), typeof(GlassPanel), new PropertyMetadata(24.0, (d, _) => ((GlassPanel)d).ApplyBlur()));

    public static readonly DependencyProperty GrainProperty = DependencyProperty.Register(
        nameof(Grain), typeof(double), typeof(GlassPanel), new PropertyMetadata(0.35, (d, e) => ((GlassPanel)d)._grain.Opacity = (double)e.NewValue * GrainStrength));

    public static readonly DependencyProperty TintColorProperty = DependencyProperty.Register(
        nameof(TintColor), typeof(Color), typeof(GlassPanel), new PropertyMetadata(Color.FromRgb(0x0F, 0x2A, 0x2E), (d, e) => ((GlassPanel)d)._tint.Fill = Freeze(new SolidColorBrush((Color)e.NewValue))));

    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity), typeof(double), typeof(GlassPanel), new PropertyMetadata(0.42, (d, e) => ((GlassPanel)d)._tint.Opacity = (double)e.NewValue));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(GlassPanel), new PropertyMetadata(20.0, (d, _) => ((GlassPanel)d).ApplyCorner()));

    private readonly VisualBrush _brush = new()
    {
        ViewboxUnits = BrushMappingMode.Absolute,
        Stretch = Stretch.Fill,
        AlignmentX = AlignmentX.Left,
        AlignmentY = AlignmentY.Top,
    };

    private readonly BlurEffect _blur = new() { KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
    private readonly Rectangle _backdrop;
    private readonly Rectangle _tint;
    private readonly Rectangle _grain;
    private readonly Border _edge;
    private readonly RectangleGeometry _clip = new();
    private Rect _lastViewbox;

    public GlassPanel()
    {
        _backdrop = new Rectangle { Fill = _brush, Effect = _blur, IsHitTestVisible = false };
        _tint = new Rectangle { IsHitTestVisible = false, Fill = Freeze(new SolidColorBrush(TintColor)), Opacity = TintOpacity };
        _grain = new Rectangle { IsHitTestVisible = false, Fill = NoiseTexture.Brush, Opacity = Grain * GrainStrength };
        _edge = new Border { IsHitTestVisible = false, BorderBrush = EdgeBrush, BorderThickness = new Thickness(1) };

        Children.Add(_backdrop);
        Children.Add(_tint);
        Children.Add(_grain);
        Children.Add(_edge);

        Clip = _clip;
        ApplyBlur();
        ApplyCorner();
        SizeChanged += (_, _) => ApplyCorner();
        LayoutUpdated += (_, _) => UpdateViewbox();
    }

    public Visual? Backdrop { get => (Visual?)GetValue(BackdropProperty); set => SetValue(BackdropProperty, value); }
    public double BlurRadius { get => (double)GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public double Grain { get => (double)GetValue(GrainProperty); set => SetValue(GrainProperty, value); }
    public Color TintColor { get => (Color)GetValue(TintColorProperty); set => SetValue(TintColorProperty, value); }
    public double TintOpacity { get => (double)GetValue(TintOpacityProperty); set => SetValue(TintOpacityProperty, value); }
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    private void ApplyBlur()
    {
        _blur.Radius = BlurRadius;
        var overscan = -Math.Max(2 * BlurRadius, 4);
        _backdrop.Margin = new Thickness(overscan);
    }

    private void ApplyCorner()
    {
        var radius = Math.Min(CornerRadius, Math.Min(ActualWidth, ActualHeight) / 2);
        _clip.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
        _clip.RadiusX = radius;
        _clip.RadiusY = radius;
        _edge.CornerRadius = new CornerRadius(radius);
    }

    /// <summary>Points the brush at the region of the backdrop that lies behind the (overscanned) blur rectangle.</summary>
    private void UpdateViewbox()
    {
        if (Backdrop is not { } backdrop || !_backdrop.IsVisible)
            return;

        Rect region;
        try
        {
            region = _backdrop.TransformToVisual(backdrop).TransformBounds(new Rect(_backdrop.RenderSize));
        }
        catch (InvalidOperationException)
        {
            return; // Not yet in the same visual tree.
        }

        if (region != _lastViewbox)
        {
            _lastViewbox = region;
            _brush.Viewbox = region;
        }
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
