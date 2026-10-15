using System.Text.Json;
using System.Windows.Media;

namespace ExcelSticker.Theming;

/// <summary>Portable, versioned data. Themes never execute XAML or application code.</summary>
public sealed class ThemeTokens
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Porcelain";
    public string Canvas { get; set; } = "#E9EDF5";
    public string Surface { get; set; } = "#F5F7FC";
    public string Accent { get; set; } = "#5666B5";
    public string Text { get; set; } = "#24344F";
    public string Muted { get; set; } = "#4D5B73";
    public string Edge { get; set; } = "#7A89A2";
    public string Shadow { get; set; } = "#52627E";
    public string Density { get; set; } = "Compact";
    public string EdgeLighting { get; set; } = "Directional";
    public bool UniformCorners { get; set; } = true;
    public bool ColorfulCards { get; set; } = true;
    public double SurfaceTint { get; set; } = .75;
    public double RimOpacity { get; set; } = .35;
    public double TopLeft { get; set; } = 20;
    public double TopRight { get; set; } = 20;
    public double BottomRight { get; set; } = 20;
    public double BottomLeft { get; set; } = 20;
    public double ButtonRadius { get; set; } = 8;
    public double InputRadius { get; set; } = 6;
    public double BorderWidth { get; set; } = 1;
    public bool ShowBorders { get; set; } = true;
    public double ShadowBlur { get; set; } = 14;
    public double ShadowDepth { get; set; } = 3;
    public double ShadowOpacity { get; set; } = .20;
    public double LightAngle { get; set; } = 135;
    public double InsetDepth { get; set; } = 12;
    public double ControlBevelWidth { get; set; } = 4;
    public double LightElevation { get; set; } = 45;
    public double Curvature { get; set; } = 1.2;
    public double Gloss { get; set; } = .22;
    public double GlossFocus { get; set; } = 28;
    public double AmbientLift { get; set; } = .4;
    public double Highlight { get; set; } = .58;
    public double Shade { get; set; } = .22;
    public double CardPadding { get; set; } = 16;
    public double ControlPadding { get; set; } = 7;
    public double FontSize { get; set; } = 13;
    public double SmallFontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "Segoe UI";
    public string PageTreatment { get; set; } = "Pastel wash";
    public string Pattern { get; set; } = "None";
    public double PatternSize { get; set; } = 24;
    public double PatternOpacity { get; set; } = .04;
    public bool ReducedEffects { get; set; }
    public bool ContrastGuard { get; set; } = true;
    public ThemeTokens Clone() => JsonSerializer.Deserialize<ThemeTokens>(JsonSerializer.Serialize(this))!;

    public static Color ParseColor(string? text)
    {
        if (text == null || text.Length != 7 || text[0] != '#' || !text.Skip(1).All(Uri.IsHexDigit))
            throw new FormatException("Colors must use opaque #RRGGBB notation.");
        return (Color)ColorConverter.ConvertFromString(text);
    }
    public void Validate()
    {
        if (!new[] { "Compact", "Comfortable", "Spacious" }.Contains(Density)) throw new FormatException("Unknown workspace density.");
        if (!new[] { "Balanced", "Directional" }.Contains(EdgeLighting)) throw new FormatException("Unknown edge lighting mode.");
        Range(SurfaceTint, 0, 1, "Pastel tint"); Range(RimOpacity, 0, 1, "Rim opacity");
        if (Version != 1) throw new FormatException("Unsupported theme version.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80) throw new FormatException("Theme name must contain 1–80 characters.");
        foreach (var value in new[] { Canvas, Surface, Accent, Text, Muted, Edge, Shadow }) ParseColor(value);
        foreach (var value in new[] { TopLeft, TopRight, BottomRight, BottomLeft }) Range(value, 0, 32, "Card corner radius");
        Range(ButtonRadius, 0, 10, "Button corner radius"); Range(InputRadius, 0, 8, "Input corner radius");
        Range(BorderWidth, 0, 4, "Border width"); Range(ShadowBlur, 0, 48, "Shadow blur"); Range(ShadowDepth, 0, 20, "Shadow depth");
        Range(ShadowOpacity, 0, .6, "Shadow opacity"); Range(LightAngle, 0, 360, "Light angle"); Range(InsetDepth, 0, 24, "Card bevel width");
        Range(ControlBevelWidth, 0, 10, "Control bevel width"); Range(LightElevation, 15, 80, "Light elevation");
        Range(Curvature, .5, 2, "Bevel curvature"); Range(Gloss, 0, 1, "Gloss"); Range(GlossFocus, 4, 96, "Gloss focus"); Range(AmbientLift, 0, 1, "Ambient lift");
        Range(Highlight, 0, .8, "Highlight"); Range(Shade, 0, .7, "Shade"); Range(CardPadding, 8, 36, "Card padding");
        Range(ControlPadding, 4, 18, "Control padding"); Range(FontSize, 12, 20, "Font size"); Range(SmallFontSize, 11, 18, "Small font size");
        Range(PatternSize, 8, 64, "Pattern spacing"); Range(PatternOpacity, 0, .15, "Pattern opacity");
        if (!new[] { "Solid", "Pastel wash" }.Contains(PageTreatment)) throw new FormatException("Unknown page color treatment.");
        if (!new[] { "None", "Speckles", "Grid" }.Contains(Pattern)) throw new FormatException("Unknown background pattern.");
        if (!new[] { "Segoe UI", "Arial", "Calibri", "Verdana", "Tahoma" }.Contains(FontFamily)) throw new FormatException("Choose a supported UI font family.");
    }
    /// <summary>Read-time migration only. Keep the user's palette; do not rewrite the file implicitly.</summary>
    public void MigrateGeometry()
    {
        static double Cap(double value, double limit) => double.IsFinite(value) ? Math.Min(value, limit) : value;
        TopLeft=Cap(TopLeft,32); TopRight=Cap(TopRight,32); BottomRight=Cap(BottomRight,32); BottomLeft=Cap(BottomLeft,32);
        ButtonRadius=Cap(ButtonRadius,10); InputRadius=Cap(InputRadius,8);
        if (Pattern == "Dots") Pattern = "Speckles";
    }
    private static void Range(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max) throw new FormatException($"{name} must be {min}–{max}.");
    }
    public static IReadOnlyList<ThemeTokens> Presets => new[]
    {
        new ThemeTokens(),
        new ThemeTokens { Name="Pastel clay", Canvas="#F6F5FA", Surface="#F6F2FC", Accent="#F1847B", Text="#25263F", Muted="#4B4D67", Edge="#73738C", Shadow="#82739F", SurfaceTint=.85 },
        new ThemeTokens { Name="Coral sculpted", Canvas="#F4D0CF", Surface="#F0B5B4", Accent="#B52E3B", Text="#291E2C", Muted="#50353D", Edge="#914651", Shadow="#8E303D",
            ColorfulCards=false, TopLeft=28, TopRight=28, BottomLeft=28, BottomRight=28, InsetDepth=18, ControlBevelWidth=6,
            Highlight=.78, Shade=.58, LightElevation=40, Curvature=1.25, Gloss=.5, GlossFocus=24, AmbientLift=.65,
            ShadowBlur=18, ShadowDepth=4, ShadowOpacity=.28, RimOpacity=.12 },
        new ThemeTokens { Name="Ocean", Canvas="#E1EDF2", Surface="#F1F8FB", Accent="#25617A", Text="#183847", Muted="#405F6B", Edge="#6F8B97", Shadow="#365868" },
        new ThemeTokens { Name="Mint", Canvas="#E4EFE9", Surface="#F1F8F4", Accent="#28684F", Text="#203F32", Muted="#4C6658", Edge="#718F7D", Shadow="#436851" },
        new ThemeTokens { Name="Lavender", Canvas="#EAE6F3", Surface="#F8F5FE", Accent="#705096", Text="#3B2B50", Muted="#685574", Edge="#8E7E9E", Shadow="#605075", TopLeft=32, TopRight=32, BottomLeft=32, BottomRight=32 },
        new ThemeTokens { Name="Graphite", Canvas="#202630", Surface="#303846", Accent="#A4BAF3", Text="#F7F8FC", Muted="#CED6E3", Edge="#758199", Shadow="#000000", Highlight=.10, Shade=.2 },
        new ThemeTokens { Name="High contrast", ColorfulCards=false, RimOpacity=1, Canvas="#FFFFFF", Surface="#FFFFFF", Accent="#163F76", Text="#111111", Muted="#333333", Edge="#303030", Shadow="#000000", ReducedEffects=true, BorderWidth=2, TopLeft=8, TopRight=8, BottomRight=8, BottomLeft=8 }
    };
}
