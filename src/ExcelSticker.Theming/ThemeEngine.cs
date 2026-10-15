using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ExcelSticker.Theming;

/// <summary>Independent theme engine: validated tokens → live WPF resources. It has no label/printing dependencies.</summary>
public sealed class ThemeEngine
{
    private readonly ResourceDictionary _resources;
    public string StorePath { get; }
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private ThemeTokens _current = new();
    private bool _editing;
    public ThemeTokens Current => _current.Clone();
    public ResourceDictionary Resources => _resources;
    public long Revision { get; private set; }
    public event EventHandler<ThemeChangedEventArgs>? Changed;
    public ThemeEditSession BeginEdit()
    {
        _dispatcher.VerifyAccess();
        if (_editing) throw new InvalidOperationException("A theme editing session is already open. Reopen that studio instead.");
        _editing = true;
        return new ThemeEditSession(this);
    }
    internal void EndEdit() => _editing = false;
    public string? LoadWarning { get; private set; }
    public ThemeEngine(ResourceDictionary resources, string? storePath = null)
    {
        _resources = resources;
        StorePath = Path.GetFullPath(storePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OceanGlass", "theme.json"));
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
        var json = File.ReadAllText(path);
        var tokens = JsonSerializer.Deserialize<ThemeTokens>(json) ?? throw new FormatException("Empty theme.");
        using var document = JsonDocument.Parse(json);
        // Preserve legacy palette/corner choices. New density/lighting defaults still apply;
        // no preference file is rewritten until the user explicitly saves.
        if (!document.RootElement.TryGetProperty(nameof(ThemeTokens.ColorfulCards), out _)) tokens.ColorfulCards = false;
        if (!document.RootElement.TryGetProperty(nameof(ThemeTokens.UniformCorners), out _)) tokens.UniformCorners = false;
        tokens.MigrateGeometry();
        tokens.Validate();
        return tokens;
    }
    public void Save(ThemeTokens tokens, string? path = null)
    {
        _dispatcher.VerifyAccess();
        ValidateForApply(tokens);
        path = Path.GetFullPath(path ?? StorePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void ValidateForApply(ThemeTokens tokens)
    {
        tokens.Validate();
        if (tokens.ContrastGuard && !ContrastPasses(tokens))
        {
            var ratios = Ratios(tokens);
            throw new FormatException($"Not applied or saved: text contrast must be at least 4.5:1 (body {ratios.Body:0.00}, secondary {ratios.Secondary:0.00}). Adjust Text/Muted or deliberately disable Contrast guard.");
        }
    }
    public void Apply(ThemeTokens tokens) => ApplyCore(tokens, ThemeChangeKind.Applied);
    internal void ApplyCore(ThemeTokens tokens, ThemeChangeKind kind)
    {
        _dispatcher.VerifyAccess();
        ValidateForApply(tokens);
        tokens = tokens.Clone();
        // Prepare everything before publishing. Invalid drafts never leave half-updated resources.
        var prepared = new Dictionary<string, object?>();
        Color C(string value) => ThemeTokens.ParseColor(value);
        void Put(string key, object value) { if (value is Freezable f && f.CanFreeze) f.Freeze(); prepared[key] = value; }
        var surface = C(tokens.Surface); var accent = C(tokens.Accent);
        var dark = Luminance(surface) < .3;
        var selection = Mix(surface, accent, dark ? .18 : .12);
        Put("Ink", new SolidColorBrush(C(tokens.Text))); Put("Muted", new SolidColorBrush(C(tokens.Muted)));
        Put("ClaySurface", new SolidColorBrush(surface)); Put("ClayInput", new SolidColorBrush(surface));
        var samePage = Revision > 0 && tokens.Canvas == _current.Canvas && tokens.Surface == _current.Surface &&
            tokens.SurfaceTint == _current.SurfaceTint && tokens.ColorfulCards == _current.ColorfulCards &&
            tokens.PageTreatment == _current.PageTreatment && tokens.ReducedEffects == _current.ReducedEffects;
        Put("ClayCanvas", samePage ? _resources["ClayCanvas"] : PageBrush(tokens));
        var sameTexture = Revision > 0 && tokens.Text == _current.Text && tokens.Pattern == _current.Pattern &&
            tokens.PatternSize == _current.PatternSize && tokens.PatternOpacity == _current.PatternOpacity && tokens.ReducedEffects == _current.ReducedEffects;
        Put("ClayCanvasTexture", sameTexture ? _resources["ClayCanvasTexture"] : Texture(tokens));
        Put("ClayButton", new SolidColorBrush(surface));
        Put("ClayPrimary", new SolidColorBrush(accent)); Put("ClaySelection", new SolidColorBrush(selection));
        var panels = PanelColors(tokens);
        Put("ClayLavender", new SolidColorBrush(panels[0])); Put("ClayMint", new SolidColorBrush(panels[1]));
        Put("ClayPeach", new SolidColorBrush(panels[2])); Put("ClayBlue", new SolidColorBrush(panels[3]));
        Put("ClayLighting", tokens.EdgeLighting == "Balanced" ? ClayLighting.Balanced : ClayLighting.Directional);
        Put("ClayRimOpacity", tokens.RimOpacity); Put("ClayShadeColor", C(tokens.Shadow));
        Put("ClayEdge", new SolidColorBrush(C(tokens.Edge))); Put("ClayFocus", new SolidColorBrush(C(tokens.Text)));
        var primaryInk = Contrast(Colors.White, accent) >= Contrast(Colors.Black, accent) ? Colors.White : Colors.Black;
        var awayFromInk = primaryInk == Colors.White ? Colors.Black : Colors.White;
        Put("ClayPrimaryInk", new SolidColorBrush(primaryInk));
        Put("ClayHover", new SolidColorBrush(Mix(surface, accent, dark ? .12 : .08)));
        Put("ClayPressed", new SolidColorBrush(selection));
        Put("ClayPrimaryHover", new SolidColorBrush(Mix(accent, awayFromInk, .12)));
        Put("ClayPrimaryPressed", new SolidColorBrush(Mix(accent, awayFromInk, .22)));
        Put("ClayDanger", new SolidColorBrush(dark ? Color.FromRgb(255, 165, 168) : Color.FromRgb(158, 42, 54)));
        Put("ClayCardRadius", tokens.UniformCorners ? new CornerRadius(tokens.TopLeft) : new CornerRadius(tokens.TopLeft, tokens.TopRight, tokens.BottomRight, tokens.BottomLeft));
        Put("ClayButtonRadius", new CornerRadius(tokens.ButtonRadius)); Put("ClayInputRadius", new CornerRadius(tokens.InputRadius));
        Put("ClayBorder", tokens.ShowBorders ? tokens.BorderWidth : 0d);
        Put("ClayHoverBorder", tokens.ShowBorders ? Math.Min(4, Math.Max(2, tokens.BorderWidth + 1)) : 0d);
        Put("ClayBorderThickness", new Thickness(tokens.ShowBorders ? tokens.BorderWidth : 0));
        var density = DensityScale(tokens.Density);
        var inset = tokens.ReducedEffects ? 0 : tokens.InsetDepth;
        var controlInset = tokens.ReducedEffects ? 0 : tokens.ControlBevelWidth;
        // Reserve a flat text bed instead of silently truncating the requested material width.
        var cardPadding = Math.Max(Math.Round(tokens.CardPadding * density, 1), inset + 3);
        var controlPadding = Math.Max(Math.Max(4, Math.Round(tokens.ControlPadding * density, 1)), controlInset + 2);
        Put("ClayCardPadding", new Thickness(cardPadding));
        Put("ClayPlatePadding", new Thickness(Math.Max(8, cardPadding - 3)));
        Put("ClayControlPadding", new Thickness(controlPadding + 3, controlPadding, controlPadding + 3, controlPadding));
        Put("ClayCardMargin", new Thickness(8, 6, 8, 12 * density));
        Put("ClayFieldMargin", new Thickness(0, 0, 8 * density, 9 * density));
        Put("ClayButtonMargin", new Thickness(0, 0, 8 * density, 6 * density));
        Put("ClayComboMargin", new Thickness(0, 0, 0, 8 * density));
        Put("ClaySectionGap", new Thickness(0, 0, 0, 12 * density));
        Put("ClayWorkspaceMargin", new Thickness(14 * density));
        Put("ClayHeaderPadding", new Thickness(8, 7 * density, 8, 7 * density));
        Put("ClayPreviewColumnWidth", new GridLength(420 + 2 * cardPadding));
        Put("ClayFontSize", tokens.FontSize); Put("ClaySmallFontSize", tokens.SmallFontSize); Put("ClayFontFamily", new FontFamily(tokens.FontFamily));
        Put("ClaySingleRowHeight", Math.Ceiling(1.35 * tokens.FontSize + 2 * controlPadding + 4));
        Put("ClayRowHeight", Math.Ceiling(1.35 * (tokens.FontSize + tokens.SmallFontSize) + 2 * controlPadding + 4));
        Put("ClayInset", inset);
        Put("ClayControlInset", controlInset);
        Put("ClaySmallInset", Math.Min(controlInset, 2)); Put("ClayInputInset", controlInset * .65);
        Put("ClayPlateInset", Math.Min(inset * .5, 6));
        Put("ClayLightElevation", tokens.LightElevation); Put("ClayCurvature", tokens.Curvature);
        Put("ClayGloss", tokens.ReducedEffects ? 0 : tokens.Gloss); Put("ClayGlossFocus", tokens.GlossFocus);
        Put("ClayHighlight", tokens.Highlight); Put("ClayShade", tokens.Shade); Put("ClayLightAngle", tokens.LightAngle);
        Put("ClayInputHighlight", tokens.Highlight * .25); Put("ClayInputShade", Math.Min(.4, tokens.Shade * 1.5)); Put("ClayPressedShade", Math.Min(.4, tokens.Shade + .08));
        if (tokens.ReducedEffects) { prepared["ClayShadow"] = null; prepared["ClayControlShadow"] = null; prepared["ClayAmbientShadow"] = null; prepared["ClayControlAmbientShadow"] = null; }
        else
        {
            var direction = (tokens.LightAngle + 180) % 360;
            Put("ClayAmbientShadow", new DropShadowEffect { Color=C(tokens.Shadow), BlurRadius=Math.Min(72, tokens.ShadowBlur * 1.7),
                ShadowDepth=0, Opacity=tokens.ShadowOpacity * tokens.AmbientLift, RenderingBias=RenderingBias.Quality });
            Put("ClayControlAmbientShadow", new DropShadowEffect { Color=C(tokens.Shadow), BlurRadius=Math.Min(20, tokens.ShadowBlur * 1.3),
                ShadowDepth=0, Opacity=tokens.ShadowOpacity * tokens.AmbientLift * .8, RenderingBias=RenderingBias.Quality });
            Put("ClayShadow", new DropShadowEffect { Color=C(tokens.Shadow), BlurRadius=tokens.ShadowBlur,
                ShadowDepth=tokens.ShadowDepth, Opacity=tokens.ShadowOpacity, Direction=direction, RenderingBias=RenderingBias.Quality });
            Put("ClayControlShadow", new DropShadowEffect { Color=C(tokens.Shadow), BlurRadius=Math.Min(tokens.ShadowBlur, 10),
                ShadowDepth=Math.Min(tokens.ShadowDepth, 2), Opacity=tokens.ShadowOpacity * .8, Direction=direction, RenderingBias=RenderingBias.Quality });
        }
        // This runs on the owning UI dispatcher: WPF renders only after publication completes.
        foreach (var (key, value) in prepared)
            if (!_resources.Contains(key) || !Equivalent(_resources[key], value)) _resources[key] = value!;
        _current = tokens;
        Revision++;
        Changed?.Invoke(this, new ThemeChangedEventArgs(kind, tokens.Name, Revision));
    }
    private static bool Equivalent(object? oldValue, object? newValue)
    {
        if (Equals(oldValue, newValue)) return true;
        // Reuse unchanged frozen resources: a corner slider should not invalidate every text brush/font.
        if (oldValue is SolidColorBrush a && newValue is SolidColorBrush b)
            return a.IsFrozen && a.Color == b.Color && a.Opacity == b.Opacity && a.Transform.Value == b.Transform.Value && a.RelativeTransform.Value == b.RelativeTransform.Value;
        if (oldValue is DropShadowEffect x && newValue is DropShadowEffect y)
            return x.IsFrozen && x.Color == y.Color && x.BlurRadius == y.BlurRadius && x.ShadowDepth == y.ShadowDepth &&
                x.Opacity == y.Opacity && x.Direction == y.Direction && x.RenderingBias == y.RenderingBias;
        return false;
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
        var backgrounds = new[] {surface,canvas,selected}.Concat(PanelColors(t)).Concat(PageColors(t)).ToArray();
        var patterned = t.Pattern!="None" && !t.ReducedEffects
            ? backgrounds.Select(bg=>Mix(bg,ThemeTokens.ParseColor(t.Text),t.PatternOpacity)) : backgrounds;
        double Minimum(string hex) => backgrounds.Concat(patterned).Min(bg=>Contrast(ThemeTokens.ParseColor(hex),bg));
        return (Minimum(t.Text), Minimum(t.Muted));
    }
    public static double DensityScale(string density) => density switch { "Compact" => .8, "Comfortable" => 1, "Spacious" => 1.15, _ => throw new FormatException("Unknown workspace density.") };
    public static Color[] PanelColors(ThemeTokens tokens)
    {
        var surface = ThemeTokens.ParseColor(tokens.Surface);
        var amount = tokens.ColorfulCards && !tokens.ReducedEffects ? tokens.SurfaceTint * (Luminance(surface) < .3 ? .16 : 1) : 0;
        return new[] { "#DCCBFA", "#C6EFDF", "#FBE0CC", "#CCE7F8" }.Select(hex => Mix(surface, ThemeTokens.ParseColor(hex), amount)).ToArray();
    }
    public static double EdgeContrast(ThemeTokens t)
    {
        var opacity = t.ShowBorders && t.BorderWidth > 0 ? t.RimOpacity : 0;
        var edge = ThemeTokens.ParseColor(t.Edge);
        return new[] { ThemeTokens.ParseColor(t.Canvas), ThemeTokens.ParseColor(t.Surface) }.Concat(PanelColors(t))
            .Min(bg => Contrast(Mix(bg, edge, opacity), bg));
    }
    public static bool ContrastPasses(ThemeTokens t) { var (a,b)=Ratios(t); return a>=4.5 && b>=4.5; }
    public static Color[] PageColors(ThemeTokens t)
    {
        var canvas=ThemeTokens.ParseColor(t.Canvas);
        if (t.PageTreatment=="Solid" || t.ReducedEffects) return new[]{canvas};
        if (!t.ColorfulCards)
        {
            var surface = ThemeTokens.ParseColor(t.Surface);
            // A monochrome palette still needs a page wash, not four identical gradient stops.
            return new[] { Mix(canvas, ThemeTokens.ParseColor(t.Accent), .08), Mix(canvas, surface, .24), canvas, Mix(canvas, surface, .4) };
        }
        return PanelColors(t).Select(panel=>Mix(canvas,panel,.24)).ToArray();
    }
    private static Brush PageBrush(ThemeTokens t)
    {
        var colors=PageColors(t);
        if(colors.Length==1)return new SolidColorBrush(colors[0]);
        var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
        for(var i=0;i<colors.Length;i++)brush.GradientStops.Add(new GradientStop(colors[i],i/(double)(colors.Length-1)));
        return brush;
    }
    private static Brush Texture(ThemeTokens t)
    {
        if(t.Pattern=="None" || t.ReducedEffects)return Brushes.Transparent;
        var size=t.PatternSize;
        var group=new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,size,size))));
        var color=ThemeTokens.ParseColor(t.Text);color.A=(byte)(t.PatternOpacity*255);
        var ink=new SolidColorBrush(color);
        // Small rectangular flecks replace legacy circular dots; no round layout decorations.
        if(t.Pattern=="Speckles")group.Children.Add(new GeometryDrawing(ink,null,new RectangleGeometry(new Rect(size/2,size/2,1,1))));
        else group.Children.Add(new GeometryDrawing(null,new Pen(ink,1),Geometry.Parse($"M0,0 L{size.ToString(System.Globalization.CultureInfo.InvariantCulture)},0 M0,0 L0,{size.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));
        return new DrawingBrush(group){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,size,size),Stretch=Stretch.None};
    }
}
