using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ExcelSticker.Theming;

/// <summary>Independent theme engine: validated tokens → live WPF resources. It has no label/printing dependencies.</summary>
public sealed class ThemeEngine
{
    private readonly ResourceDictionary _resources;
    public string StorePath { get; }
    public ThemeTokens Current { get; private set; } = new();
    public string? LoadWarning { get; private set; }
    public ThemeEngine(ResourceDictionary resources, string? storePath = null)
    {
        _resources = resources;
        StorePath = storePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OceanGlass", "theme.json");
        var tokens = new ThemeTokens();
        try
        {
            if (File.Exists(StorePath)) tokens = Read(StorePath);
            if (tokens.ContrastGuard && !ContrastPasses(tokens)) { tokens = new ThemeTokens(); throw new FormatException("Saved theme failed its contrast guard; default theme restored."); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException) { LoadWarning = ex.Message; }
        Apply(tokens);
    }
    public static ThemeTokens Read(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024) throw new FormatException("Theme files must be at most 64 KB.");
        var tokens = JsonSerializer.Deserialize<ThemeTokens>(File.ReadAllText(path)) ?? throw new FormatException("Empty theme.");
        tokens.Validate();
        return tokens;
    }
    public void Save(ThemeTokens tokens, string? path = null)
    {
        tokens.Validate();
        if (tokens.ContrastGuard && !ContrastPasses(tokens)) throw new FormatException("Text contrast is below 4.5:1. Adjust Text/Muted or disable Contrast guard deliberately.");
        path ??= StorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Apply(ThemeTokens tokens)
    {
        tokens.Validate();
        if (tokens.ContrastGuard && !ContrastPasses(tokens)) throw new FormatException("The palette fails the 4.5:1 text contrast guard.");
        Current = tokens.Clone();
        Color C(string value) => ThemeTokens.ParseColor(value);
        void Put(string key, object value) { if (value is Freezable f && f.CanFreeze) f.Freeze(); _resources[key] = value; }
        var surface = C(tokens.Surface); var canvas = C(tokens.Canvas); var accent = C(tokens.Accent);
        var dark = Luminance(surface) < .3;
        var selection = Mix(surface, accent, dark ? .18 : .12);
        Put("Ink", new SolidColorBrush(C(tokens.Text))); Put("Muted", new SolidColorBrush(C(tokens.Muted)));
        Put("ClaySurface", new SolidColorBrush(surface)); Put("ClayInput", new SolidColorBrush(surface));
        Put("ClayCanvas", Pattern(tokens, canvas)); Put("ClayButton", new SolidColorBrush(surface));
        Put("ClayPrimary", new SolidColorBrush(accent)); Put("ClaySelection", new SolidColorBrush(selection));
        Put("ClayEdge", new SolidColorBrush(C(tokens.Edge))); Put("ClayFocus", new SolidColorBrush(C(tokens.Text)));
        Put("ClayPrimaryInk", new SolidColorBrush(Contrast(Colors.White, accent) >= Contrast(Colors.Black, accent) ? Colors.White : Colors.Black));
        Put("ClayDanger", new SolidColorBrush(dark ? Color.FromRgb(255, 165, 168) : Color.FromRgb(158, 42, 54)));
        Put("ClayCardRadius", new CornerRadius(tokens.TopLeft, tokens.TopRight, tokens.BottomRight, tokens.BottomLeft));
        Put("ClayButtonRadius", new CornerRadius(tokens.ButtonRadius)); Put("ClayInputRadius", new CornerRadius(tokens.InputRadius));
        Put("ClayBorder", tokens.ShowBorders ? tokens.BorderWidth : 0d);
        Put("ClayBorderThickness", new Thickness(tokens.ShowBorders ? tokens.BorderWidth : 0));
        Put("ClayCardPadding", new Thickness(tokens.CardPadding)); Put("ClayControlPadding", new Thickness(tokens.ControlPadding + 2, tokens.ControlPadding, tokens.ControlPadding + 2, tokens.ControlPadding));
        Put("ClayFontSize", tokens.FontSize); Put("ClaySmallFontSize", tokens.SmallFontSize); Put("ClayFontFamily", new FontFamily(tokens.FontFamily));
        Put("ClayRowHeight", 2 * tokens.FontSize + 2 * tokens.ControlPadding + 12);
        var inset = tokens.ReducedEffects ? 0 : Math.Min(tokens.InsetDepth, Math.Max(0, Math.Min(tokens.CardPadding, tokens.ControlPadding) - 2));
        Put("ClayInset", inset); Put("ClaySmallInset", Math.Min(inset, 2)); Put("ClayInputInset", Math.Min(inset, 3));
        Put("ClayHighlight", tokens.Highlight); Put("ClayShade", tokens.Shade); Put("ClayLightAngle", tokens.LightAngle);
        Put("ClayInputHighlight", tokens.Highlight * .25); Put("ClayInputShade", Math.Min(.4, tokens.Shade * 1.5)); Put("ClayPressedShade", Math.Min(.4, tokens.Shade + .08));
        if (tokens.ReducedEffects) _resources["ClayShadow"] = null;
        else Put("ClayShadow", new DropShadowEffect { Color=C(tokens.Shadow), BlurRadius=tokens.ReducedEffects ? 0 : tokens.ShadowBlur,
            ShadowDepth=tokens.ReducedEffects ? 0 : tokens.ShadowDepth, Opacity=tokens.ReducedEffects ? 0 : tokens.ShadowOpacity, Direction=(tokens.LightAngle+180)%360, RenderingBias=RenderingBias.Quality });
    }
    public static Color Mix(Color a, Color b, double amount) => Color.FromRgb((byte)(a.R+(b.R-a.R)*amount), (byte)(a.G+(b.G-a.G)*amount), (byte)(a.B+(b.B-a.B)*amount));
    public static double Luminance(Color c)
    {
        static double Channel(byte value) { var n=value/255d; return n<=.04045 ? n/12.92 : Math.Pow((n+.055)/1.055,2.4); }
        return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);
    }
    public static double Contrast(Color a, Color b) { var x=Luminance(a); var y=Luminance(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
    public static (double Body, double Secondary) Ratios(ThemeTokens t)
    {
        var surface=ThemeTokens.ParseColor(t.Surface); var canvas=ThemeTokens.ParseColor(t.Canvas);
        var selected=Mix(surface,ThemeTokens.ParseColor(t.Accent),Luminance(surface)<.3 ? .18 : .12);
        var patterned = t.Pattern!="None" && !t.ReducedEffects ? Mix(canvas,ThemeTokens.ParseColor(t.Text),t.PatternOpacity) : canvas;
        double Minimum(string hex) => new[] {surface,canvas,selected,patterned}.Min(bg=>Contrast(ThemeTokens.ParseColor(hex),bg));
        return (Minimum(t.Text), Minimum(t.Muted));
    }
    public static double EdgeContrast(ThemeTokens t) => new[] { t.Canvas, t.Surface }.Min(bg => Contrast(ThemeTokens.ParseColor(t.Edge), ThemeTokens.ParseColor(bg)));
    public static bool ContrastPasses(ThemeTokens t) { var (a,b)=Ratios(t); return a>=4.5 && b>=4.5; }
    private static Brush Pattern(ThemeTokens t, Color canvas)
    {
        if (t.Pattern=="None" || t.ReducedEffects) return new SolidColorBrush(canvas);
        var size=t.PatternSize;
        var group=new DrawingGroup(); group.Children.Add(new GeometryDrawing(new SolidColorBrush(canvas),null,new RectangleGeometry(new Rect(0,0,size,size))));
        var color=ThemeTokens.ParseColor(t.Text); color.A=(byte)(t.PatternOpacity*255);
        var ink=new SolidColorBrush(color);
        if(t.Pattern=="Dots") group.Children.Add(new GeometryDrawing(ink,null,new EllipseGeometry(new Point(size/2,size/2),1,1)));
        else group.Children.Add(new GeometryDrawing(null,new Pen(ink,1),Geometry.Parse($"M0,0 L{size.ToString(System.Globalization.CultureInfo.InvariantCulture)},0 M0,0 L0,{size.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));
        return new DrawingBrush(group) { TileMode=TileMode.Tile, ViewportUnits=BrushMappingMode.Absolute, Viewport=new Rect(0,0,size,size), Stretch=Stretch.None };
    }
}
