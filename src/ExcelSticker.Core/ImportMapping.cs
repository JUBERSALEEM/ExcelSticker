namespace ExcelSticker.Core;

/// <summary>Explicit vendor-column mapping. No labels are committed before complete validation.</summary>
public static class ImportMapping
{
    public static readonly string[] Fields = EntryTable.Header.Split(',');
    public static IReadOnlyList<string[]> ReadCsv(string text, out char delimiter)
    {
        text = text.TrimStart('\uFEFF'); List<string[]>? best = null; var bestScore = -1; delimiter = ',';
        foreach (var candidate in new[] { ',', ';', '\t' })
        {
            try {
                var rows = LabelCsv.Parse(text, candidate);
                if (rows.Count > LabelCsv.MaxLabels + 100) throw new FormatException("Too many records.");
                var score = rows.Take(50).Select(row => Score(row) * 1000 + Math.Min(256, row.Length)).DefaultIfEmpty(0).Max();
                if (score > bestScore) { best = rows; bestScore = score; delimiter = candidate; }
            } catch (FormatException) { }
        }
        if (best == null) throw new FormatException("Cannot read this CSV. Check quoting, comma/semicolon/tab delimiters, and the 10,100-record limit.");
        if (best.Count == 0) throw new FormatException("The CSV is empty.");
        if (best.Any(row => row.Length > 256)) throw new FormatException("Import supports at most 256 columns.");
        return best;
    }
    private static int Score(string[] row)
    {
        var names = row.Select(EntryTable.CanonicalHeader).Distinct().ToArray();
        return new[] { "order", "spec", "w1", "h1", "qty", "outerqty", "customer" }.Count(names.Contains);
    }
    public static int SuggestHeader(IReadOnlyList<string[]> rows) => rows.Take(50).Select((row, index) => (Score: Score(row), Index: index)).OrderByDescending(x => x.Score).FirstOrDefault().Index;
    public static Dictionary<string, int> SuggestColumns(string[] header)
    {
        var names = header.Select(EntryTable.CanonicalHeader).ToArray();
        return Fields.ToDictionary(field => field, field => {
            var key = EntryTable.CanonicalHeader(field);
            return names.Count(h => h == key) == 1 ? Array.IndexOf(names, key) : -1;
        });
    }
    public static IReadOnlyList<GlassEntry> Import(IReadOnlyList<string[]> rows, int header, int lastRowExclusive,
        IReadOnlyDictionary<string, int> columns, IReadOnlyDictionary<string, string>? defaults = null, bool commaDecimals = false)
    {
        if (header < 0 || header >= rows.Count || lastRowExclusive <= header + 1 || lastRowExclusive > rows.Count)
            throw new FormatException("Select a header and a last row that includes data.");
        var width = rows.Skip(header).Take(lastRowExclusive - header).Max(r => r.Length);
        foreach (var (field, index) in columns)
            if (!Fields.Contains(field) || index < -1 || index >= width) throw new FormatException($"Invalid column for {field}.");
        bool Mapped(string field) => columns.TryGetValue(field, out var col) && col >= 0;
        bool HasDefault(string field) => defaults != null && defaults.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value);
        foreach (var field in new[] { "Spec", "W1", "H1" }) if (!Mapped(field)) throw new FormatException($"Map the {field} column.");
        foreach (var field in new[] { "OrderNo", "CustomerName" })
            if (!Mapped(field) && !HasDefault(field)) throw new FormatException($"Map {field} or enter a file-wide fallback below the mapping controls.");
        if (!Mapped("Qty") && !Mapped("OuterQty")) throw new FormatException("Map Qty or OuterQty.");
        var translated = new List<string[]> { Fields };
        for (var i = header + 1; i < lastRowExclusive; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) { translated.Add(Fields.Select(_ => "").ToArray()); continue; }
            translated.Add(Fields.Select(field => {
                var value = Mapped(field) && columns[field] < row.Length ? row[columns[field]].Trim() : "";
                if (value.Length == 0 && (field is "OrderNo" or "CustomerName" or "ReferenceNo" or "JobNo") && HasDefault(field)) value = defaults![field].Trim();
                if (commaDecimals && (field is "W1" or "H1" or "W2" or "H2")) {
                    if (value.Contains('.') && value.Contains(',')) throw new FormatException($"Parsed row {i + 1}: mixed decimal/thousands separators in {field}.");
                    value = value.Replace(',', '.');
                }
                return value;
            }).ToArray());
        }
        try { return EntryTable.Import(translated); }
        catch (FormatException ex) { throw new FormatException(ex.Message + "\nRecord numbers refer to the selected range (header is record 1). Nothing was imported.", ex); }
    }
}
