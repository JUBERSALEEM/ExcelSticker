using System.Windows;
using System.Windows.Media;

namespace ExcelSticker.Theming;

/// <summary>Shared circular-corner geometry. Adjacent radii shrink together instead of flattening one side.</summary>
public static class ClayGeometry
{
    public static CornerRadius Inset(CornerRadius radius, double depth) => new(
        Math.Max(0, radius.TopLeft - depth), Math.Max(0, radius.TopRight - depth),
        Math.Max(0, radius.BottomRight - depth), Math.Max(0, radius.BottomLeft - depth));

    public static CornerRadius Normalize(Rect bounds, CornerRadius radius)
    {
        // Keep a straight segment on every side: large legacy radii must not make pills/circles.
        var limit = Math.Max(0, Math.Min(32, Math.Min(bounds.Width, bounds.Height) * .25));
        radius = new CornerRadius(Math.Min(radius.TopLeft,limit),Math.Min(radius.TopRight,limit),
            Math.Min(radius.BottomRight,limit),Math.Min(radius.BottomLeft,limit));
        var scale = 1d;
        void Limit(double available, double sum) { if (sum > 0) scale = Math.Min(scale, available / sum); }
        Limit(bounds.Width, radius.TopLeft + radius.TopRight); Limit(bounds.Width, radius.BottomLeft + radius.BottomRight);
        Limit(bounds.Height, radius.TopLeft + radius.BottomLeft); Limit(bounds.Height, radius.TopRight + radius.BottomRight);
        return new CornerRadius(radius.TopLeft * scale, radius.TopRight * scale, radius.BottomRight * scale, radius.BottomLeft * scale);
    }
    public static StreamGeometry Rounded(Rect bounds, CornerRadius radius)
    {
        var r = Normalize(bounds, radius);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            void Arc(Point end, double size)
            {
                if (size <= 0) path.LineTo(end, true, false);
                else path.ArcTo(end, new Size(size, size), 0, false, SweepDirection.Clockwise, true, false);
            }
            path.BeginFigure(new Point(bounds.Left + r.TopLeft, bounds.Top), true, true);
            path.LineTo(new Point(bounds.Right - r.TopRight, bounds.Top), true, false);
            Arc(new Point(bounds.Right, bounds.Top + r.TopRight), r.TopRight);
            path.LineTo(new Point(bounds.Right, bounds.Bottom - r.BottomRight), true, false);
            Arc(new Point(bounds.Right - r.BottomRight, bounds.Bottom), r.BottomRight);
            path.LineTo(new Point(bounds.Left + r.BottomLeft, bounds.Bottom), true, false);
            Arc(new Point(bounds.Left, bounds.Bottom - r.BottomLeft), r.BottomLeft);
            path.LineTo(new Point(bounds.Left, bounds.Top + r.TopLeft), true, false);
            Arc(new Point(bounds.Left + r.TopLeft, bounds.Top), r.TopLeft);
        }
        geometry.Freeze();
        return geometry;
    }
}
