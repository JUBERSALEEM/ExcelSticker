namespace ExcelSticker.Core;

/// <summary>One source row. A double-glazed entry produces independently sized/quantified outer and inner labels.</summary>
public sealed record GlassEntry(string Specification, GlassLabel Outer, GlassLabel? Inner)
{
    public bool IsDouble => Inner is not null;
    public IEnumerable<GlassLabel> Labels => Inner is null ? new[] { Outer } : new[] { Outer, Inner! };

    public static GlassEntry Create(string orderNo, string specification, string referenceNo, string customerName,
        string jobNo, string w1, string h1, string outerQty, bool doubleGlazed,
        string w2 = "", string h2 = "", string innerQty = "", string outerGlass = "", string innerGlass = "")
    {
        specification = specification.Trim();
        if (specification.Length > 240 || specification.Any(char.IsControl))
            throw new FormatException("Spec must be at most 240 characters with no line breaks.");
        if (specification.Length == 0) throw new FormatException("Spec is required.");
        var parts = specification.Split('+', StringSplitOptions.TrimEntries);
        if (doubleGlazed)
        {
            if (string.IsNullOrWhiteSpace(outerGlass) || string.IsNullOrWhiteSpace(innerGlass))
            {
                if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
                    throw new FormatException("For outer/inner glass, use Spec: outer glass + spacer + inner glass, or enter both glass overrides explicitly. Complex/laminated specs need overrides.");
                if (string.IsNullOrWhiteSpace(outerGlass)) outerGlass = parts[0];
                if (string.IsNullOrWhiteSpace(innerGlass)) innerGlass = parts[2];
            }
        }
        else if (string.IsNullOrWhiteSpace(outerGlass)) outerGlass = specification;

        var outer = GlassLabel.Create(orderNo, outerQty, outerGlass, referenceNo, w1, h1, customerName, jobNo)
            with { Pane = doubleGlazed ? "OUTER" : "", Specification = specification };
        if (!doubleGlazed) return new GlassEntry(specification, outer, null);
        if (string.IsNullOrWhiteSpace(w2) != string.IsNullOrWhiteSpace(h2))
            throw new FormatException("Enter both W2 and H2, or leave both blank to use W1 and H1.");
        if (string.IsNullOrWhiteSpace(w2)) { w2 = w1; h2 = h1; }
        if (string.IsNullOrWhiteSpace(innerQty)) innerQty = outerQty;
        GlassLabel inner;
        try { inner = GlassLabel.Create(orderNo, innerQty, innerGlass, referenceNo, w2, h2, customerName, jobNo) with { Pane = "INNER", Specification = specification }; }
        catch (FormatException ex) { throw new FormatException($"Inner: {ex.Message}", ex); }
        return new GlassEntry(specification, outer, inner);
    }
}
