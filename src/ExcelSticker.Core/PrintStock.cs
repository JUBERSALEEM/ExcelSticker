namespace ExcelSticker.Core;

public static class PrintStock
{
    /// <summary>WPF printer dimensions are 1/96 inch. Landscape rotates the reported media rectangle.</summary>
    public static bool MatchesLabel(double? mediaWidth, double? mediaHeight, bool landscape)
    {
        if (!mediaWidth.HasValue || !mediaHeight.HasValue) return false;
        var width = landscape ? mediaHeight.Value : mediaWidth.Value;
        var height = landscape ? mediaWidth.Value : mediaHeight.Value;
        return double.IsFinite(width) && double.IsFinite(height)
            && Math.Abs(width - 100 * 96.0 / 25.4) <= 2
            && Math.Abs(height - 70 * 96.0 / 25.4) <= 2;
    }
}
