using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExcelSticker.Theming;

public sealed class ClayCard : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius),typeof(CornerRadius),typeof(ClayCard),new FrameworkPropertyMetadata(new CornerRadius(24)));
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty,value); }
}

/// <summary>Only decorative geometry. Never put text below an Effect: presenters are siblings of this chrome.</summary>
public sealed class ClayChrome : FrameworkElement
{
    private static DependencyProperty Property<T>(string name,T value) => DependencyProperty.Register(name,typeof(T),typeof(ClayChrome),new FrameworkPropertyMetadata(value,FrameworkPropertyMetadataOptions.AffectsRender));
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
    public ClayChrome()
    {
        IsHitTestVisible=false;
        SetResourceReference(InsetProperty,"ClayInset"); SetResourceReference(HighlightProperty,"ClayHighlight");
        SetResourceReference(ShadeProperty,"ClayShade"); SetResourceReference(LightAngleProperty,"ClayLightAngle");
        SetResourceReference(LineWidthProperty,"ClayBorder"); SetResourceReference(EdgeProperty,"ClayEdge");
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth<1 || ActualHeight<1) return;
        var bounds=new Rect(0,0,ActualWidth,ActualHeight);
        var geometry=Rounded(bounds,Radius);
        dc.DrawGeometry(Fill,null,geometry);
        dc.PushClip(geometry);
        // Feathered light/dark inset rings emulate a sculpted clay edge without rasterizing any text.
        var angle=LightAngle*Math.PI/180;
        for(var i=0; i<Math.Min(Inset,Math.Min(ActualWidth,ActualHeight)/2); i++)
        {
            var fraction=1-i/Math.Max(1,Inset);
            var light=Color.FromArgb((byte)(255*Highlight*fraction),255,255,255);
            var shade=Color.FromArgb((byte)(255*Shade*fraction),0,0,0);
            var brush=new LinearGradientBrush(light,shade,new Point(.5+.5*Math.Cos(angle),.5-.5*Math.Sin(angle)),new Point(.5-.5*Math.Cos(angle),.5+.5*Math.Sin(angle)));
            var rect=new Rect(i+.5,i+.5,Math.Max(0,ActualWidth-2*i-1),Math.Max(0,ActualHeight-2*i-1));
            dc.DrawGeometry(null,new Pen(brush,1.5),Rounded(rect,new CornerRadius(Math.Max(0,Radius.TopLeft-i),Math.Max(0,Radius.TopRight-i),Math.Max(0,Radius.BottomRight-i),Math.Max(0,Radius.BottomLeft-i))));
        }
        if(LineWidth>0) dc.DrawGeometry(null,new Pen(Edge,LineWidth*2),geometry);
        dc.Pop();
    }
    private static StreamGeometry Rounded(Rect r,CornerRadius corners)
    {
        var max=Math.Min(r.Width,r.Height)/2;
        var tl=Math.Min(max,corners.TopLeft); var tr=Math.Min(max,corners.TopRight); var br=Math.Min(max,corners.BottomRight); var bl=Math.Min(max,corners.BottomLeft);
        var g=new StreamGeometry();
        using(var c=g.Open())
        {
            c.BeginFigure(new Point(r.Left+tl,r.Top),true,true);
            c.LineTo(new Point(r.Right-tr,r.Top),true,false); c.QuadraticBezierTo(new Point(r.Right,r.Top),new Point(r.Right,r.Top+tr),true,false);
            c.LineTo(new Point(r.Right,r.Bottom-br),true,false); c.QuadraticBezierTo(new Point(r.Right,r.Bottom),new Point(r.Right-br,r.Bottom),true,false);
            c.LineTo(new Point(r.Left+bl,r.Bottom),true,false); c.QuadraticBezierTo(new Point(r.Left,r.Bottom),new Point(r.Left,r.Bottom-bl),true,false);
            c.LineTo(new Point(r.Left,r.Top+tl),true,false); c.QuadraticBezierTo(new Point(r.Left,r.Top),new Point(r.Left+tl,r.Top),true,false);
        }
        g.Freeze();return g;
    }
}
