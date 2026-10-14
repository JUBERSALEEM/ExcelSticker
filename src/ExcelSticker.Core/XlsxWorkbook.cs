using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ExcelSticker.Core;

/// <summary>Read-only XLSX table import without Excel or external packages. Formula cells require cached results.</summary>
public sealed class XlsxWorkbook : IDisposable
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, string> _sheets = new(StringComparer.Ordinal);
    private readonly List<string> _strings;
    public IReadOnlyList<string> SheetNames => _sheets.Keys.ToArray();

    public XlsxWorkbook(Stream stream)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        try
        {
            if (_archive.Entries.Count > 10000 || _archive.Entries.Sum(e => e.Length) > 64L * 1024 * 1024)
                throw new FormatException("Workbook expands beyond the 64 MB import limit.");
            var workbook = ReadXml("xl/workbook.xml");
            if (workbook.Root?.Name.Namespace != S) throw new FormatException("Unsupported workbook XML format. Save as a standard Excel .xlsx workbook.");
            var relationships = ReadXml("xl/_rels/workbook.xml.rels").Root!.Elements(P + "Relationship")
                .Where(e => (string?)e.Attribute("TargetMode") != "External" && ((string?)e.Attribute("Type"))?.EndsWith("/worksheet", StringComparison.Ordinal) == true)
                .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
            foreach (var sheet in workbook.Descendants(S + "sheet"))
            {
                var id = (string?)sheet.Attribute(R + "id");
                var name = (string?)sheet.Attribute("name");
                if (id == null || name == null || !relationships.TryGetValue(id, out var target)) continue;
                var uri = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target);
                var path = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                if (uri.Host != "xlsx.local" || !path.StartsWith("xl/", StringComparison.Ordinal)) throw new FormatException("Invalid worksheet relationship.");
                _sheets.Add(name, path);
            }
            if (_sheets.Count == 0) throw new FormatException("No readable worksheets were found.");
            _strings = _archive.GetEntry("xl/sharedStrings.xml") == null ? new List<string>()
                : ReadXml("xl/sharedStrings.xml").Descendants(S + "si").Select(RichText).ToList();
        }
        catch { _archive.Dispose(); throw; }
    }

    private XDocument ReadXml(string path)
    {
        var entry = _archive.GetEntry(path) ?? throw new FormatException($"Workbook part is missing: {path}.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32L * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private static string RichText(XElement element) => string.Concat(element.Elements(S + "t").Concat(element.Elements(S + "r").Elements(S + "t")).Select(t => t.Value));

    public IReadOnlyList<string[]> ReadSheet(string name)
    {
        if (!_sheets.TryGetValue(name, out var path)) throw new FormatException("Select a worksheet from this workbook.");
        var data = ReadXml(path).Root?.Element(S + "sheetData") ?? throw new FormatException("Worksheet has no table data.");
        var rows = new List<string[]>();
        var width = 0;
        var started = false;
        foreach (var row in data.Elements(S + "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(S + "c"))
            {
                var address = (string?)cell.Attribute("r") ?? throw new FormatException("Worksheet cell has no address.");
                var value = CellValue(cell, address);
                if (string.IsNullOrWhiteSpace(value)) continue;
                var column = 0;
                foreach (var c in address.TakeWhile(char.IsLetter)) column = checked(column * 26 + char.ToUpperInvariant(c) - 'A' + 1);
                if (column < 1 || column > 256) throw new FormatException($"Cell {address}: import supports at most 256 columns.");
                cells.Add(column - 1, value);
            }
            if (!started && cells.Count == 0) continue;
            if (!started) { width = cells.Keys.Max() + 1; started = true; }
            if (cells.Keys.Any(column => column >= width)) throw new FormatException($"Worksheet row {(string?)row.Attribute("r")}: data extends beyond the header columns.");
            var fields = new string[width];
            Array.Fill(fields, "");
            foreach (var (column, value) in cells) fields[column] = value;
            rows.Add(fields);
            if (rows.Count > LabelCsv.MaxLabels + 1) throw new FormatException("Worksheet exceeds 10,000 data rows. Remove unused rows or split the workbook.");
        }
        if (rows.Count == 0) throw new FormatException("The selected worksheet is empty.");
        return rows;
    }

    private string CellValue(XElement cell, string address)
    {
        var type = (string?)cell.Attribute("t");
        var value = cell.Element(S + "v")?.Value;
        if (cell.Element(S + "f") != null && string.IsNullOrEmpty(value))
            throw new FormatException($"Cell {address} has a formula without a saved result. Recalculate and save in Excel, or paste values before importing.");
        if (type == "e") throw new FormatException($"Cell {address} contains an Excel error: {value}.");
        if (type == "s")
        {
            if (!int.TryParse(value, out var index) || index < 0 || index >= _strings.Count) throw new FormatException($"Invalid shared string in cell {address}.");
            return _strings[index];
        }
        if (type == "inlineStr") return cell.Element(S + "is") is { } text ? RichText(text) : "";
        if ((type == null || type == "n") && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            return numeric.ToString("0.############################", CultureInfo.InvariantCulture);
        return value ?? "";
    }
    public void Dispose() => _archive.Dispose();
}
