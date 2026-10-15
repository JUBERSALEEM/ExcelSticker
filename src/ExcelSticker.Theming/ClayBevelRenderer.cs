using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExcelSticker.Theming;

/// <summary>
/// Bounded nine-slice material: four DPI-aware corner patches and four one-pixel-wide edge strips.
/// The middle stays vector fill; text is never part of these images. At most 128×128 pixels per corner.
/// </summary>
internal static class ClayBevelRenderer
{
    public static void Draw(DrawingContext dc, Size size, CornerRadius radius, ClayMaterialSettings material, DpiScale dpi)
    {
        if (material.BevelWidth <= 0 || material.Fill.A == 0) return;
        var depth = Math.Min(Math.Clamp(material.BevelWidth, 0, 24), Math.Min(size.Width, size.Height) * .24);
        var sx = Math.Clamp(dpi.DpiScaleX, 1, 4); var sy = Math.Clamp(dpi.DpiScaleY, 1, 4);
        var model = new ClayLightingModel(size, radius, material);
        var tl = Math.Max(radius.TopLeft, depth); var tr = Math.Max(radius.TopRight, depth);
        var br = Math.Max(radius.BottomRight, depth); var bl = Math.Max(radius.BottomLeft, depth);
        Patch(new Rect(0, 0, tl, tl)); Patch(new Rect(size.Width - tr, 0, tr, tr));
        Patch(new Rect(size.Width - br, size.Height - br, br, br)); Patch(new Rect(0, size.Height - bl, bl, bl));
        Patch(new Rect(tl, 0, Math.Max(0, size.Width - tl - tr), depth), horizontal: true);
        Patch(new Rect(bl, size.Height - depth, Math.Max(0, size.Width - bl - br), depth), horizontal: true);
        Patch(new Rect(0, tl, depth, Math.Max(0, size.Height - tl - bl)), vertical: true);
        Patch(new Rect(size.Width - depth, tr, depth, Math.Max(0, size.Height - tr - br)), vertical: true);
        void Patch(Rect rect, bool horizontal = false, bool vertical = false)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            var width = horizontal ? 1 : Math.Max(1, (int)Math.Ceiling(rect.Width * sx));
            var height = vertical ? 1 : Math.Max(1, (int)Math.Ceiling(rect.Height * sy));
            var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = model.Sample(rect.X + (x + .5) * rect.Width / width, rect.Y + (y + .5) * rect.Height / height);
                var index = (y * width + x) * 4;
                pixels[index] = (byte)(color.B * color.A / 255); pixels[index + 1] = (byte)(color.G * color.A / 255);
                pixels[index + 2] = (byte)(color.R * color.A / 255); pixels[index + 3] = color.A;
            }
            var image = BitmapSource.Create(width, height, 96 * sx, 96 * sy, PixelFormats.Pbgra32, null, pixels, width * 4);
            image.Freeze(); dc.DrawImage(image, rect);
        }
    }
}
