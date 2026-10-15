namespace ExcelSticker.Core;

public static class PrintStock
{
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
