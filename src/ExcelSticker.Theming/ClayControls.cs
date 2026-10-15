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
    private DpiScale _drawingDpi;
    public static readonly DependencyProperty LightingProperty=Property(nameof(Lighting),ClayLighting.Directional);
    public static readonly DependencyProperty ReliefProperty=Property(nameof(Relief),ClayRelief.Raised);
    public static readonly DependencyProperty RimOpacityProperty=Property(nameof(RimOpacity),.35d);
    public static readonly DependencyProperty ShadeColorProperty=Property(nameof(ShadeColor),Color.FromRgb(82,98,126));
    public ClayLighting Lighting { get=>(ClayLighting)GetValue(LightingProperty);set=>SetValue(LightingProperty,value); }
    public ClayRelief Relief { get=>(ClayRelief)GetValue(ReliefProperty);set=>SetValue(ReliefProperty,value); }
    public double RimOpacity { get=>(double)GetValue(RimOpacityProperty);set=>SetValue(RimOpacityProperty,value); }
    public Color ShadeColor { get=>(Color)GetValue(ShadeColorProperty);set=>SetValue(ShadeColorProperty,value); }
    public static readonly DependencyProperty LightElevationProperty=Property(nameof(LightElevation),45d);
    public double LightElevation { get=>(double)GetValue(LightElevationProperty);set=>SetValue(LightElevationProperty,value); }
    public static readonly DependencyProperty GlossProperty=Property(nameof(Gloss),.22d);
    public double Gloss { get=>(double)GetValue(GlossProperty);set=>SetValue(GlossProperty,value); }
    public static readonly DependencyProperty GlossFocusProperty=Property(nameof(GlossFocus),28d);
    public double GlossFocus { get=>(double)GetValue(GlossFocusProperty);set=>SetValue(GlossFocusProperty,value); }
    public static readonly DependencyProperty CurvatureProperty=Property(nameof(Curvature),1.2d);
    public double Curvature { get=>(double)GetValue(CurvatureProperty);set=>SetValue(CurvatureProperty,value); }
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
        var dpi = VisualTreeHelper.GetDpi(this);
        if (_drawing == null || _drawingSize != RenderSize || _drawingDpi.DpiScaleX != dpi.DpiScaleX || _drawingDpi.DpiScaleY != dpi.DpiScaleY)
        {
            _drawingSize = RenderSize; _drawingDpi = dpi;
            _drawing = BuildMaterial();
            if (_drawing.CanFreeze) _drawing.Freeze();
        }
        dc.DrawDrawing(_drawing);
    }
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _drawing = null;
        InvalidateVisual();
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
        if (Fill is SolidColorBrush { Opacity: 1 } solid && solid.Color.A == 255)
        {
            var material = new ClayMaterialSettings(solid.Color, ShadeColor, Math.Max(0, Inset), LightAngle, LightElevation,
                Math.Clamp(Highlight,0,1), Math.Clamp(Shade,0,1), Math.Clamp(Gloss,0,1), GlossFocus, Curvature,
                Lighting == ClayLighting.Balanced, Relief == ClayRelief.Inset);
            ClayBevelRenderer.Draw(dc, RenderSize, radius, material, _drawingDpi);
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
}
