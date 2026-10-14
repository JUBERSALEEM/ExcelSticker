using System.Text;

namespace ExcelSticker.Core;

/// <summary>Comma-delimited UTF-8 CSV with RFC 4180 quoting; imports are validated before returning any labels.</summary>
public static class LabelCsv
{
    public const string Header = "OrderNo,Qty,GlassType,ReferenceNo,Width,Height,CustomerName,JobNo";
    public const int MaxLabels = 10000;

    public static IReadOnlyList<GlassLabel> Import(string csv)
    {
        var rows = Parse(csv.TrimStart('\uFEFF'));
        if (rows.Count == 0) throw new FormatException("The CSV file is empty.");
        static string Normalize(string text) => string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        var columns = rows[0].Select(Normalize).ToArray();
        if (columns.Distinct().Count() != columns.Length) throw new FormatException("CSV column headings must be unique.");
        var required = Header.Split(',');
        var indexes = required.Select(name => Array.IndexOf(columns, Normalize(name))).ToArray();
        for (var i = 0; i < indexes.Length; i++)
            if (indexes[i] < 0) throw new FormatException($"Missing CSV column: {required[i]}. Download the template for the expected headings.");
        var labels = new List<GlassLabel>();
        for (var row = 1; row < rows.Count; row++)
        {
            var cells = rows[row];
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            if (cells.Length != columns.Length) throw new FormatException($"CSV record {row + 1}: expected {columns.Length} fields, found {cells.Length}.");
            string Get(int i) => cells[indexes[i]];
            try { labels.Add(GlassLabel.Create(Get(0), Get(1), Get(2), Get(3), Get(4), Get(5), Get(6), Get(7))); }
            catch (FormatException ex) { throw new FormatException($"CSV record {row + 1}: {ex.Message}", ex); }
            if (labels.Count > MaxLabels) throw new FormatException($"A batch can contain at most {MaxLabels} rows.");
        }
        if (labels.Count == 0) throw new FormatException("The CSV contains headings but no labels.");
        return labels;
    }

    public static List<string[]> Parse(string csv)
    {
        var rows = new List<string[]>();
        var cells = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        for (var i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else field.Append(c);
                continue;
            }
            if (closedQuote && c != ',' && c != '\r' && c != '\n')
                throw new FormatException("Unexpected character after a closing CSV quote.");
            if (c == '"')
            {
                if (field.Length != 0) throw new FormatException("CSV quotes must start at the beginning of a field.");
                quoted = true;
            }
            else if (c == ',' || c == '\r' || c == '\n')
            {
                cells.Add(field.ToString()); field.Clear(); closedQuote = false;
                if (c != ',')
                {
                    rows.Add(cells.ToArray()); cells.Clear();
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                }
            }
            else field.Append(c);
        }
        if (quoted) throw new FormatException("The CSV contains an unclosed quoted field.");
        if (field.Length > 0 || cells.Count > 0 || closedQuote) { cells.Add(field.ToString()); rows.Add(cells.ToArray()); }
        return rows;
    }
}
