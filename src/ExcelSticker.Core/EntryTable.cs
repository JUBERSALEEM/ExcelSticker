namespace ExcelSticker.Core;

/// <summary>Shared schema/validation for CSV and XLSX, including the original single-glass template.</summary>
public static class EntryTable
{
    public const string Header = "OrderNo,Spec,W1,H1,Qty,W2,H2,OuterQty,InnerQty,ReferenceNo,CustomerName,JobNo,OuterGlass,InnerGlass";
    public static IReadOnlyList<GlassEntry> ImportCsv(string csv) => Import(LabelCsv.Parse(csv.TrimStart('\uFEFF')));

    private static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static string Canonical(string value) => Normalize(value) switch
    {
        "order" or "orderno" or "ordernumber" => "order",
        "spec" or "specification" or "glasstype" or "glass" => "spec",
        "w1" or "width1" or "width" or "widthmm" => "w1",
        "h1" or "height1" or "height" or "heightmm" => "h1",
        "w2" or "width2" => "w2", "h2" or "height2" => "h2",
        "qty" or "quantity" => "qty",
        "outerqty" or "qtyouter" or "qty1" => "outerqty",
        "innerqty" or "qtyinner" or "qty2" => "innerqty",
        "reference" or "referenceno" or "ref" => "ref",
        "customer" or "customername" => "customer",
        "job" or "jobno" or "jobnumber" => "job",
        "outerglass" => "outerglass", "innerglass" => "innerglass",
        var other => other
    };

    public static IReadOnlyList<GlassEntry> Import(IReadOnlyList<string[]> rows)
    {
        if (rows.Count == 0) throw new FormatException("The file is empty.");
        var headers = rows[0].Select(Canonical).ToArray();
        var named = headers.Where(h => h.Length > 0).ToArray();
        if (named.Distinct().Count() != named.Length) throw new FormatException("Column headings (including aliases such as Qty1 and OuterQty) must be unique.");
        foreach (var required in new[] { "order", "spec", "w1", "h1", "customer" })
            if (!headers.Contains(required)) throw new FormatException($"Missing column: {required}. Use the downloaded template.");
        if (!headers.Contains("qty") && !headers.Contains("outerqty")) throw new FormatException("Missing Qty or OuterQty column.");
        var result = new List<GlassEntry>();
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Length != headers.Length) throw new FormatException($"Record {index + 1}: expected {headers.Length} fields, found {row.Length}.");
            string Get(string key) { var column = Array.IndexOf(headers, key); return column < 0 ? "" : row[column].Trim(); }
            try
            {
                var outerQty = Get("outerqty");
                if (outerQty.Length == 0) outerQty = Get("qty");
                var paired = Get("spec").Contains('+') || new[] { "w2", "h2", "innerqty", "innerglass" }.Any(key => Get(key).Length > 0);
                result.Add(GlassEntry.Create(Get("order"), Get("spec"), Get("ref"), Get("customer"), Get("job"),
                    Get("w1"), Get("h1"), outerQty, paired, Get("w2"), Get("h2"), Get("innerqty"), Get("outerglass"), Get("innerglass")));
            }
            catch (FormatException ex) { throw new FormatException($"Record {index + 1}: {ex.Message}", ex); }
            if (result.Count > LabelCsv.MaxLabels) throw new FormatException($"A batch can contain at most {LabelCsv.MaxLabels} entries.");
        }
        if (result.Count == 0) throw new FormatException("The file contains headings but no entries.");
        return result;
    }
}
