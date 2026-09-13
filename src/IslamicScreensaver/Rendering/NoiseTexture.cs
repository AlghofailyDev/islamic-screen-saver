using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IslamicScreensaver.Rendering;

/// <summary>A tiled monochrome noise brush for the panel's film-grain layer. Generated once, frozen, shared.</summary>
internal static class NoiseTexture
{
    private const int Size = 256;

    public static ImageBrush Brush { get; } = Build();

    private static ImageBrush Build()
    {
        var random = new Random(20260913);
        var pixels = new byte[Size * Size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Mostly mid-grey with a wide spread; alpha carries the visible strength so the brush stays cheap to blend.
            var v = (byte)random.Next(0, 256);
            pixels[i] = v;
            pixels[i + 1] = v;
            pixels[i + 2] = v;
            pixels[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, pixels, Size * 4);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, Size, Size),
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }
}
