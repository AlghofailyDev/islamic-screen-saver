using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Rendering;

/// <summary>Full-bleed background: a bundled procedural visual, or the user's image or video, with a slow drift and a vignette.</summary>
public partial class BackgroundLayer : UserControl
{
    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".wmv", ".avi", ".mkv", ".webm", ".m4v"];
    private static readonly TimeSpan DriftPeriod = TimeSpan.FromSeconds(60);
    private const double DriftMaxScale = 1.04;

    public BackgroundLayer()
    {
        InitializeComponent();
        Video.MediaEnded += (_, _) => { Video.Position = TimeSpan.Zero; Video.Play(); };
        Video.MediaFailed += (_, e) => Log.Error("Background video failed; falling back to the bundled visual.", e.ErrorException);
    }

    /// <summary>Colour the verse panel should be tinted with when the user has not chosen one.</summary>
    public Color SuggestedTint { get; private set; } = Color.FromRgb(0x0F, 0x2A, 0x2E);

    public void Load(BackgroundSettings settings)
    {
        Procedural.Children.Clear();
        Photo.Visibility = Visibility.Collapsed;
        Video.Visibility = Visibility.Collapsed;

        if (settings.Kind == BackgroundKind.File && !string.IsNullOrWhiteSpace(settings.FilePath) && File.Exists(settings.FilePath))
        {
            if (VideoExtensions.Contains(System.IO.Path.GetExtension(settings.FilePath).ToLowerInvariant()))
                LoadVideo(settings.FilePath);
            else if (LoadPhoto(settings.FilePath))
            {
                StartDrift();
                return;
            }
            else
                LoadBundled(BundledBackgrounds.Dawn);
        }
        else
        {
            if (settings.Kind == BackgroundKind.File)
                Log.Info($"Background file not found ({settings.FilePath}); using bundled visual.");
            LoadBundled(settings.BundledName);
        }

        StartDrift();
    }

    private void LoadBundled(string? name)
    {
        var visual = BundledBackgrounds.Get(name);
        foreach (var layer in visual.Layers)
            Procedural.Children.Add(new Rectangle { Fill = layer, IsHitTestVisible = false });
        SuggestedTint = visual.PanelTint;
    }

    private bool LoadPhoto(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();

            Photo.Source = image;
            Photo.Visibility = Visibility.Visible;
            SuggestedTint = TintFrom(image);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Cannot load background image {path}.", ex);
            return false;
        }
    }

    private void LoadVideo(string path)
    {
        Video.Source = new Uri(path);
        Video.Visibility = Visibility.Visible;
        Video.Play();
        // No cheap way to sample a frame before it plays; a neutral dark glass suits most footage.
        SuggestedTint = Color.FromRgb(0x14, 0x18, 0x1C);
    }

    private void StartDrift()
    {
        var animation = new DoubleAnimation(1.0, DriftMaxScale, DriftPeriod)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        DriftScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        DriftScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private ScaleTransform DriftScaleTransform => (ScaleTransform)Drift.RenderTransform;

    /// <summary>Average colour of the image, darkened and slightly enriched so the glass reads as part of the scene.</summary>
    private static Color TintFrom(BitmapSource source)
    {
        try
        {
            var small = new TransformedBitmap(source, new ScaleTransform(16.0 / source.PixelWidth, 16.0 / source.PixelHeight));
            var converted = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
            var w = converted.PixelWidth;
            var h = converted.PixelHeight;
            var pixels = new byte[w * h * 4];
            converted.CopyPixels(pixels, w * 4, 0);

            double r = 0, g = 0, b = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                b += pixels[i];
                g += pixels[i + 1];
                r += pixels[i + 2];
            }
            var n = pixels.Length / 4;
            return Darken(Color.FromRgb((byte)(r / n), (byte)(g / n), (byte)(b / n)));
        }
        catch (Exception ex)
        {
            Log.Error("Could not sample background colour.", ex);
            return Color.FromRgb(0x14, 0x18, 0x1C);
        }
    }

    /// <summary>Keeps the hue, pushes saturation up a little and value down a lot — a deep tinted glass, not a muddy grey.</summary>
    private static Color Darken(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var v = max;
        var s = max == 0 ? 0 : (max - min) / max;
        double hue = 0;
        if (max != min)
        {
            var d = max - min;
            hue = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            hue /= 6;
        }

        s = Math.Min(1, s * 1.25 + 0.08);
        v = Math.Clamp(v * 0.35, 0.08, 0.22);

        var i = (int)Math.Floor(hue * 6);
        var f = hue * 6 - i;
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        var (rr, gg, bb) = (i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return Color.FromRgb((byte)(rr * 255), (byte)(gg * 255), (byte)(bb * 255));
    }
}
