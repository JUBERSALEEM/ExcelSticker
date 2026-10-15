namespace ExcelSticker.Core;

public static class PrintStock
{
    /// <summary>Unknown media is not proof of a size conflict. Larger stock/PDF pages can carry
    /// an actual-size sticker; reject only a known smaller (or invalid) media rectangle.</summary>
    public static bool TooSmall(double? mediaWidth,double? mediaHeight,bool landscape,double widthMm,double heightMm)
    {
        if(!double.IsFinite(widthMm)||!double.IsFinite(heightMm)||widthMm<=0||heightMm<=0)throw new ArgumentOutOfRangeException(nameof(widthMm));
        if(!mediaWidth.HasValue || !mediaHeight.HasValue)return false;
        var width=landscape?mediaHeight.Value:mediaWidth.Value;var height=landscape?mediaWidth.Value:mediaHeight.Value;
        return !double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0 || width+2<widthMm*96/25.4 || height+2<heightMm*96/25.4;
    }
    /// <summary>WPF printer dimensions are 1/96 inch. Landscape rotates the reported media rectangle.</summary>
    public static bool MatchesLabel(double? mediaWidth, double? mediaHeight, bool landscape,double widthMm=100,double heightMm=70)
    {
        if(!double.IsFinite(widthMm) || !double.IsFinite(heightMm) || widthMm<=0 || heightMm<=0)return false;
        if (!mediaWidth.HasValue || !mediaHeight.HasValue) return false;
        var width = landscape ? mediaHeight.Value : mediaWidth.Value;
        var height = landscape ? mediaWidth.Value : mediaHeight.Value;
        return double.IsFinite(width) && double.IsFinite(height)
            && Math.Abs(width - widthMm * 96.0 / 25.4) <= 2
            && Math.Abs(height - heightMm * 96.0 / 25.4) <= 2;
    }
}
