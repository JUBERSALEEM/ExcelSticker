using System.Windows;
using System.Windows.Media;

namespace ExcelSticker.Theming;

/// <summary>Material parameters are separate from shape, spacing and drop-shadow parameters.</summary>
public readonly record struct ClayMaterialSettings(
    Color Fill, Color Shadow, double BevelWidth, double LightAngle, double LightElevation,
    double Highlight, double Shade, double Gloss, double GlossFocus, double Curvature,
    bool Balanced, bool Recessed);

/// <summary>
/// Analytic rounded-surface lighting. A quarter-round bevel supplies a 3D normal;
/// diffuse light and a view-facing specular lobe are evaluated relative to the flat center.
/// No XAML, shaders or external assets are loaded by this model.
/// </summary>
public sealed class ClayLightingModel
{
    private readonly Size _size;
    private readonly CornerRadius _radius;
    private readonly ClayMaterialSettings _material;
    private readonly double _lx, _ly, _lz, _hx, _hy, _hz, _flatSpecular;
    private readonly Color _light, _shade;
    public ClayLightingModel(Size size, CornerRadius radius, ClayMaterialSettings material)
    {
        _size = size; _radius = ClayGeometry.Normalize(new Rect(size), radius);
        _material = material;
        var angle = material.LightAngle * Math.PI / 180;
        var elevation = Math.Clamp(material.LightElevation, 15, 80) * Math.PI / 180;
        _lx = Math.Cos(angle) * Math.Cos(elevation); _ly = -Math.Sin(angle) * Math.Cos(elevation); _lz = Math.Sin(elevation);
        var length = Math.Sqrt(_lx * _lx + _ly * _ly + (1 + _lz) * (1 + _lz));
        _hx = _lx / length; _hy = _ly / length; _hz = (1 + _lz) / length;
        _flatSpecular = Math.Pow(_hz, Math.Clamp(material.GlossFocus, 4, 96));
        _light = ThemeEngine.Mix(material.Fill, Colors.White, .94);
        _shade = ThemeEngine.Mix(material.Fill, material.Shadow, .8);
    }
    public Color Sample(double x, double y)
    {
        var (distance, ox, oy) = Boundary(x, y);
        var width = Math.Min(Math.Clamp(_material.BevelWidth, 0, 24), Math.Min(_size.Width, _size.Height) * .24);
        if (width <= 0 || distance >= width) return _material.Fill;
        var t = Math.Clamp(distance / width, 0, 1);
        // 0 at the outer edge: horizontal normal. 1 at the inner edge: flat face normal.
        var slope = Math.Pow(t, Math.Clamp(_material.Curvature, .5, 2)) * Math.PI / 2;
        var horizontal = Math.Cos(slope); var nz = Math.Sin(slope);
        var sign = _material.Recessed ? -1 : 1;
        var nx = ox * horizontal * sign; var ny = oy * horizontal * sign;
        double dot, specular;
        if (_material.Balanced)
        {
            // An omnidirectional studio rim, preserving the same profile on all four sides.
            dot = horizontal * Math.Sqrt(_lx * _lx + _ly * _ly) + nz * _lz;
            specular = horizontal * Math.Sqrt(_hx * _hx + _hy * _hy) + nz * _hz;
            if (_material.Recessed)
            {
                dot = -horizontal * Math.Sqrt(_lx * _lx + _ly * _ly) + nz * _lz;
                specular = -horizontal * Math.Sqrt(_hx * _hx + _hy * _hy) + nz * _hz;
            }
        }
        else
        {
            dot = nx * _lx + ny * _ly + nz * _lz;
            specular = nx * _hx + ny * _hy + nz * _hz;
        }
        var diffuse = dot - _lz;
        var bright = Math.Max(0, diffuse) / Math.Max(.1, 1 - _lz) * _material.Highlight;
        var dark = Math.Max(0, -diffuse) / Math.Max(.1, _lz) * _material.Shade;
        var shine = Math.Max(0, Math.Pow(Math.Max(0, specular), Math.Clamp(_material.GlossFocus, 4, 96)) - _flatSpecular) * _material.Gloss;
        var result = ThemeEngine.Mix(_material.Fill, _shade, Math.Clamp(dark, 0, .85));
        result = ThemeEngine.Mix(result, _light, Math.Clamp(bright + shine, 0, .92));
        return Color.FromArgb(_material.Fill.A, result.R, result.G, result.B);
    }
    private (double Distance, double X, double Y) Boundary(double x, double y)
    {
        (double, double, double) Corner(double cx, double cy, double radius)
        {
            var dx = x - cx; var dy = y - cy; var length = Math.Sqrt(dx * dx + dy * dy);
            return length > .00001 ? (radius - length, dx / length, dy / length) : (radius, 0, -1);
        }
        if (x < _radius.TopLeft && y < _radius.TopLeft) return Corner(_radius.TopLeft, _radius.TopLeft, _radius.TopLeft);
        if (x > _size.Width - _radius.TopRight && y < _radius.TopRight) return Corner(_size.Width - _radius.TopRight, _radius.TopRight, _radius.TopRight);
        if (x > _size.Width - _radius.BottomRight && y > _size.Height - _radius.BottomRight) return Corner(_size.Width - _radius.BottomRight, _size.Height - _radius.BottomRight, _radius.BottomRight);
        if (x < _radius.BottomLeft && y > _size.Height - _radius.BottomLeft) return Corner(_radius.BottomLeft, _size.Height - _radius.BottomLeft, _radius.BottomLeft);
        var distance = x; var nx = -1d; var ny = 0d;
        void Edge(double candidate, double ex, double ey) { if (candidate < distance) { distance = candidate; nx = ex; ny = ey; } }
        Edge(_size.Width - x, 1, 0); Edge(y, 0, -1); Edge(_size.Height - y, 0, 1);
        return (distance, nx, ny);
    }
}
