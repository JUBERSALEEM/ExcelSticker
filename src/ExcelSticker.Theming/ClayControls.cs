using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExcelSticker.Theming;

public sealed class ClayCard : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius),typeof(CornerRadius),typeof(ClayCard),new FrameworkPropertyMetadata(new CornerRadius(20)));
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty,value); }
}

public enum ClayLighting { Balanced, Directional }
public enum ClayRelief { Raised, Inset }

/// <summary>Only decorative geometry. Never put text below an Effect: presenters are siblings of this chrome.</summary>
public sealed class ClayChrome : FrameworkElement
{
    private static DependencyProperty Property<T>(string name,T value) => DependencyProperty.Register(name,typeof(T),typeof(ClayChrome),new FrameworkPropertyMetadata(value,FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ClayChrome)d)._drawing = null));
    private DrawingGroup? _drawing;
    private Size _drawingSize;
    public static readonly DependencyProperty LightingProperty=Property(nameof(Lighting),ClayLighting.Balanced);
    public static readonly DependencyProperty ReliefProperty=Property(nameof(Relief),ClayRelief.Raised);
    public static readonly DependencyProperty RimOpacityProperty=Property(nameof(RimOpacity),.35d);
    public static readonly DependencyProperty ShadeColorProperty=Property(nameof(ShadeColor),Color.FromRgb(82,98,126));
    public ClayLighting Lighting { get=>(ClayLighting)GetValue(LightingProperty);set=>SetValue(LightingProperty,value); }
    public ClayRelief Relief { get=>(ClayRelief)GetValue(ReliefProperty);set=>SetValue(ReliefProperty,value); }
    public double RimOpacity { get=>(double)GetValue(RimOpacityProperty);set=>SetValue(RimOpacityProperty,value); }
    public Color ShadeColor { get=>(Color)GetValue(ShadeColorProperty);set=>SetValue(ShadeColorProperty,value); }
    public static readonly DependencyProperty FillProperty=Property<Brush>(nameof(Fill),Brushes.White);
    public static readonly DependencyProperty EdgeProperty=Property<Brush>(nameof(Edge),Brushes.Gray);
    public static readonly DependencyProperty RadiusProperty=Property(nameof(Radius),new CornerRadius(14));
    public static readonly DependencyProperty LineWidthProperty=Property(nameof(LineWidth),1d);
    public static readonly DependencyProperty InsetProperty=Property(nameof(Inset),5d);
    public static readonly DependencyProperty HighlightProperty=Property(nameof(Highlight),.4d);
    public static readonly DependencyProperty ShadeProperty=Property(nameof(Shade),.12d);
    public static readonly DependencyProperty LightAngleProperty=Property(nameof(LightAngle),135d);
    public Brush Fill { get=>(Brush)GetValue(FillProperty);set=>SetValue(FillProperty,value); }
    public Brush Edge { get=>(Brush)GetValue(EdgeProperty);set=>SetValue(EdgeProperty,value); }
    public CornerRadius Radius { get=>(CornerRadius)GetValue(RadiusProperty);set=>SetValue(RadiusProperty,value); }
    public double LineWidth { get=>(double)GetValue(LineWidthProperty);set=>SetValue(LineWidthProperty,value); }
    public double Inset { get=>(double)GetValue(InsetProperty);set=>SetValue(InsetProperty,value); }
    public double Highlight { get=>(double)GetValue(HighlightProperty);set=>SetValue(HighlightProperty,value); }
    public double Shade { get=>(double)GetValue(ShadeProperty);set=>SetValue(ShadeProperty,value); }
    public double LightAngle { get=>(double)GetValue(LightAngleProperty);set=>SetValue(LightAngleProperty,value); }
    public ClayChrome() { IsHitTestVisible=false; }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        if (_drawing == null || _drawingSize != RenderSize)
        {
            _drawingSize = RenderSize;
            _drawing = BuildMaterial();
            if (_drawing.CanFreeze) _drawing.Freeze();
        }
        dc.DrawDrawing(_drawing);
    }
    private DrawingGroup BuildMaterial()
    {
        var drawing = new DrawingGroup();
        using var dc = drawing.Open();
        var bounds = new Rect(RenderSize);
        var radius = ClayGeometry.Normalize(bounds, Radius);
        var geometry = ClayGeometry.Rounded(bounds, radius);
        // Clone borrowed brushes before freezing the cached drawing (never freeze a caller's brush).
        dc.DrawGeometry(Fill?.CloneCurrentValue(), null, geometry);
        dc.PushClip(geometry);
        var baseColor = (Fill as SolidColorBrush)?.Color ?? Colors.White;
        var light = ThemeEngine.Mix(baseColor, Colors.White, .88);
        var shade = ThemeEngine.Mix(baseColor, ShadeColor, .65);
        var depth = Math.Min(Math.Max(0, Inset), Math.Min(ActualWidth, ActualHeight) / 2 - .5);
        // Every band traverses the WHOLE rounded perimeter at the same depth. Balanced mode
        // deliberately has no card-wide gradient: opposite edges/corners receive identical light.
        const double step = .5;
        for (var offset = step / 2; offset < depth; offset += step)
        {
            var t = offset / depth;
            var highlightWeight = Math.Exp(-Math.Pow((t - .20) / .25, 2));
            var shadeWeight = Math.Exp(-Math.Pow((t - .65) / .23, 2));
            if (Relief == ClayRelief.Inset) (highlightWeight, shadeWeight) = (shadeWeight, highlightWeight);
            var rect = bounds; rect.Inflate(-offset, -offset);
            var ring = ClayGeometry.Rounded(rect, ClayGeometry.Inset(radius, offset));
            var highlightBrush = Band(light, Math.Clamp(Highlight, 0, 1) * highlightWeight, false);
            var shadeBrush = Band(shade, Math.Clamp(Shade, 0, 1) * shadeWeight, true);
            // Slight overlap avoids cracks at fractional device-pixel scales; content is a sibling.
            dc.DrawGeometry(null, new Pen(shadeBrush, step + .15), ring);
            dc.DrawGeometry(null, new Pen(highlightBrush, step + .15), ring);
        }
        if (LineWidth > 0 && RimOpacity > 0)
        {
            dc.PushOpacity(Math.Clamp(RimOpacity, 0, 1));
            dc.DrawGeometry(null, new Pen(Edge?.CloneCurrentValue(), LineWidth * 2), geometry);
            dc.Pop();
        }
        dc.Pop();
        return drawing;
    }
    private Brush Band(Color color, double opacity, bool reverse)
    {
        Color WithAlpha(double amount) => Color.FromArgb((byte)Math.Round(255 * Math.Clamp(amount, 0, 1)), color.R, color.G, color.B);
        if (Lighting == ClayLighting.Balanced) return new SolidColorBrush(WithAlpha(opacity));
        var angle = LightAngle * Math.PI / 180;
        var start = new Point(.5 + .5 * Math.Cos(angle), .5 - .5 * Math.Sin(angle));
        var end = new Point(1 - start.X, 1 - start.Y);
        // Even directional mode retains fill-tinted relief on the unlit edges, not a white wash.
        return new LinearGradientBrush(WithAlpha(opacity * (reverse ? .45 : 1)), WithAlpha(opacity * (reverse ? 1 : .45)), start, end);
    }
}
