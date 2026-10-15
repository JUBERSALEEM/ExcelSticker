using System.Globalization;

namespace ExcelSticker.Core;

public sealed record GlassLabel(string OrderNo, int Qty, string GlassType, string ReferenceNo,
    decimal Width, decimal Height, string CustomerName, string JobNo)
{
    public string Pane { get; init; } = "";
    public string Specification { get; init; } = "";
    public int EntryNumber { get; init; } = 1;
    public int CopyIndex { get; init; } = 1;
    public NumberingStyle Numbering { get; init; } = NumberingStyle.EntryCopy;
    public string QuantityText => Numbering switch
    {
        NumberingStyle.EntryCopy => $"{EntryNumber.ToString(CultureInfo.InvariantCulture)}/{CopyIndex.ToString(CultureInfo.InvariantCulture)}",
        NumberingStyle.CopyOfTotal => $"{CopyIndex.ToString(CultureInfo.InvariantCulture)}/{Qty.ToString(CultureInfo.InvariantCulture)}",
        _ => Qty.ToString(CultureInfo.InvariantCulture)
    };

    public string Size => $"{Width.ToString("0.0", CultureInfo.InvariantCulture)} × {Height.ToString("0.0", CultureInfo.InvariantCulture)}";

    public static GlassLabel Create(string orderNo, string qty, string glassType, string referenceNo,
        string width, string height, string customerName, string jobNo)
    {
        static string Text(string value, string name, int limit, bool required = true)
        {
            value = value.Trim();
            if (required && value.Length == 0) throw new FormatException($"{name} is required.");
            if (value.Length > limit) throw new FormatException($"{name} must be at most {limit} characters.");
            if (value.Any(char.IsControl)) throw new FormatException($"{name} cannot contain line breaks or control characters.");
            return value;
        }
        static decimal Dimension(string value, string name)
        {
            if (!decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result)
                || result <= 0 || result > 100000)
                throw new FormatException($"{name} must be greater than 0 and at most 100000 mm. Use a dot for decimals.");
            return result;
        }
        if (!int.TryParse(qty.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity < 1 || quantity > 10000)
            throw new FormatException("Qty must be a whole number from 1 to 10000.");
        return new GlassLabel(Text(orderNo, "Order No", 40), quantity, Text(glassType, "Glass Type", 80),
            Text(referenceNo, "Reference No", 60, false), Dimension(width, "Width"), Dimension(height, "Height"),
            Text(customerName, "Customer Name", 80), Text(jobNo, "Job No", 40, false));
    }
}
