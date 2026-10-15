namespace ExcelSticker.Theming;

/// <summary>Complete, editable theme recipes derived from the six supplied visual references.</summary>
public sealed record ReferenceStyle(string Id, string Name, string Description, Func<ThemeTokens> Create);

public static class ReferenceStyles
{
    public static IReadOnlyList<ReferenceStyle> All { get; } = new ReferenceStyle[]
    {
        new("clayhub", "Clayhub", "Lavender workspace, mint plates, peach rows and blue preview; crisp rolled rims.", () => Create(
            "Clayhub", "#DED1FA", "#C9EFDE", "#FBE1CD", "#BFE2F8", "#FFF0B5", "#F6D1E1", 24, 11, .30, .35, 1.1, 3)),
        new("puffy-studio", "Puffy studio", "Plump lilac surfaces, candy accents, broad white glints and soft lift.", () => Create(
            "Puffy studio", "#D9BDF6", "#B8EFD9", "#FFD9C2", "#B8DDFB", "#FFF0AA", "#F8BCD8", 30, 18, .65, .44, 1.35, 5)),
        new("daily-progress", "Daily progress", "Saturated lavender with a mint inset plate and a luminous curved shoulder.", () => Create(
            "Daily progress", "#D1B6F4", "#B4E8CE", "#FFD4B3", "#C7DBF4", "#FFF1B5", "#F7CEE0", 32, 17, .48, .4, 1.2, 5)),
        new("clayboard", "Clayboard", "Pale nested task-like panels, blue accents and restrained shallow relief.", () => Create(
            "Clayboard", "#E7DDF7", "#D1F0E6", "#FCE5D0", "#B8DFFA", "#FFF1CB", "#FADAD8", 18, 7, .12, .22, 1, 2)),
        new("clayshop", "Clayshop", "Lilac, mint, peach, sky, butter and rose tiles with coral actions.", () => Create(
            "Clayshop", "#CCAFF0", "#A8DCC7", "#F5CBAF", "#AED8E9", "#F5E39B", "#F1C0CC", 24, 10, .2, .36, 1.15, 3)),
        new("clayplay", "Clayplay", "Mint and violet surfaces, blue preview and raised coral transport-style actions.", () => Create(
            "Clayplay", "#C3AAF6", "#AEEDD7", "#FFD0B6", "#ACE0F4", "#F9E69B", "#F4C8DE", 22, 12, .3, .37, 1.2, 3))
    };

    private static ThemeTokens Create(string name, string lavender, string mint, string peach, string blue,
        string yellow, string rose, double radius, double bevel, double gloss, double shade, double curvature, double depth) => new()
    {
        Name=name, Canvas="#F6F6FA", Surface="#F9F7FD", Accent="#F38177", Text="#171B37", Muted="#41465E",
        Edge="#817398", Shadow="#675080", PaletteLavender=lavender, PaletteMint=mint, PalettePeach=peach,
        PaletteBlue=blue, PaletteYellow=yellow, PaletteRose=rose, SurfaceTint=1, ColorfulCards=true,
        SurfaceShadowTint=.75, TopLeft=radius, TopRight=radius, BottomLeft=radius, BottomRight=radius,
        PlateRelief=name is "Clayboard" or "Clayshop" or "Clayplay" ? "Raised" : "Inset",
        PlateRadius=name=="Puffy studio" ? 14 : 12,
        ButtonRadius=10, InputRadius=7, InsetDepth=bevel, ControlBevelWidth=name=="Clayboard" ? 3 : 5,
        Highlight=.76, Shade=shade, Gloss=gloss, GlossFocus=name=="Puffy studio" ? 20 : 30,
        Curvature=curvature, LightElevation=45, ShadowBlur=name=="Clayboard" ? 10 : 16,
        ShadowDepth=depth, ShadowOpacity=.25, AmbientLift=.55, RimOpacity=.14,
        FontFamily=name=="Puffy studio" || name=="Daily progress" ? "Calibri" : "Segoe UI",
        PageTreatment="Solid", Density="Compact"
    };
}
