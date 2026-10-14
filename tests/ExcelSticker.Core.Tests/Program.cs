using System.Globalization;
using System.IO.Compression;
using System.Text;
using ExcelSticker.Core;

var passed = 0;
void Check(string name, Action test)
{
    test(); passed++; Console.WriteLine($"PASS {name}");
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'.");
}
void Invalid(Action action, string messagePart)
{
    try { action(); }
    catch (FormatException ex) when (ex.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase)) { return; }
    throw new Exception($"Expected a validation error containing '{messagePart}'.");
}
GlassLabel Make(string qty = "1", string width = "316.0", string height = "800.0", string order = "7494/71", string customer = "FUJSENG")
    => GlassLabel.Create(order, qty, "6MM CLEAR LOW E", "W6* F5", width, height, customer, "");
string Csv(string row) => LabelCsv.Header + "\r\n" + row;
const string sample = "7494/71,1,6MM CLEAR LOW E,W6* F5,316.0,800.0,FUJSENG,";

Check("manual entry matches reference", () => { var label = Make(); Equal("316.0 × 800.0", label.Size); Equal(1, label.Qty); });
Check("text is trimmed", () => Equal("7494/71", Make(order: " 7494/71 ").OrderNo));
Check("quantity lower bound", () => Invalid(() => Make(qty: "0"), "Qty"));
Check("quantity upper bound", () => Invalid(() => Make(qty: "10001"), "Qty"));
Check("fractional quantity rejected", () => Invalid(() => Make(qty: "1.5"), "Qty"));
Check("negative dimension rejected", () => Invalid(() => Make(width: "-1"), "Width"));
Check("zero dimension rejected", () => Invalid(() => Make(height: "0"), "Height"));
Check("huge dimension rejected", () => Invalid(() => Make(width: "100001"), "Width"));
Check("nonfinite dimension rejected", () => Invalid(() => Make(width: "NaN"), "Width"));
Check("required order", () => Invalid(() => Make(order: "  "), "Order No"));
Check("required customer", () => Invalid(() => Make(customer: ""), "Customer Name"));
Check("long text rejected", () => Invalid(() => Make(order: new string('x', 41)), "40"));
Check("control characters rejected", () => Invalid(() => Make(order: "one\ntwo"), "control"));
Check("comma decimals rejected", () => Invalid(() => Make(width: "316,0"), "dot"));
Check("CSV imports optional blank fields", () => { var rows = LabelCsv.Import(Csv(sample)); Equal(1, rows.Count); Equal("", rows[0].JobNo); });
Check("UTF-8 BOM and blank records", () => Equal(1, LabelCsv.Import("\uFEFF" + Csv(sample) + "\r\n\r\n,,,,,,,\r\n").Count));
Check("quoted comma and escaped quotes", () =>
{
    var label = LabelCsv.Import(Csv("A,2,\"CLEAR, LOW E\",\"W6\"\" F5\",100,70,Customer,Job"))[0];
    Equal("CLEAR, LOW E", label.GlassType); Equal("W6\" F5", label.ReferenceNo); Equal("Job", label.JobNo);
});
Check("headers can be reordered and spaced", () =>
{
    var label = LabelCsv.Import("job no,customer name,height,width,reference no,glass type,qty,order no\nJ,C,70,100,R,G,2,A")[0];
    Equal("A", label.OrderNo); Equal(100m, label.Width); Equal(2, label.Qty);
});
Check("Unicode text preserved", () => Equal("زجاج", LabelCsv.Import(Csv("A,1,زجاج,,100,70,عميل,"))[0].GlassType));
Check("empty file rejected", () => Invalid(() => LabelCsv.Import(""), "empty"));
Check("template without data rejected", () => Invalid(() => LabelCsv.Import(LabelCsv.Header + "\n"), "no labels"));
Check("missing heading rejected", () => Invalid(() => LabelCsv.Import("OrderNo,Qty\nA,1"), "Missing"));
Check("duplicate heading rejected", () => Invalid(() => LabelCsv.Import(LabelCsv.Header + ",Qty\n"), "unique"));
Check("wrong field count rejected", () => Invalid(() => LabelCsv.Import(Csv("A,1")), "record 2"));
Check("unclosed quote rejected", () => Invalid(() => LabelCsv.Import(Csv("\"A,1")), "unclosed"));
Check("embedded unescaped quote rejected", () => Invalid(() => LabelCsv.Import(Csv("A\"B,1")), "beginning"));
Check("text after closing quote rejected", () => Invalid(() => LabelCsv.Import(Csv("\"A\"B,1")), "closing"));
Check("multiline quoted field parsed then rejected by label validation", () => Invalid(() => LabelCsv.Import(Csv("\"A\nB\",1,G,,100,70,C,")), "control"));
Check("invalid second data row rejects whole import", () => Invalid(() => LabelCsv.Import(Csv(sample + "\nA,0,G,,100,70,C,")), "record 3"));
Check("dot decimals independent of current culture", () =>
{
    var original = CultureInfo.CurrentCulture;
    try { CultureInfo.CurrentCulture = new CultureInfo("de-DE"); Equal("316.0 × 800.0", LabelCsv.Import(Csv(sample))[0].Size); }
    finally { CultureInfo.CurrentCulture = original; }
});
Check("batch size limit", () => Invalid(() => LabelCsv.Import(Csv(string.Join("\n", Enumerable.Repeat(sample, LabelCsv.MaxLabels + 1)))), "at most"));
// Outer/inner, worksheet schema, and printer compatibility regressions.
const string spec = "6MM HD GREY FT GLASS + 12MM ASP + 6MM CLEAR GLASS";
GlassEntry Pair(string w2 = "", string h2 = "", string innerQty = "") => GlassEntry.Create("A", spec, "R", "C", "J", "2550", "2650", "4", true, w2, h2, innerQty);
Check("example generates two correct panes", () =>
{
    var entry = Pair(); Equal(2, entry.Labels.Count());
    Equal("6MM HD GREY FT GLASS", entry.Outer.GlassType); Equal("6MM CLEAR GLASS", entry.Inner!.GlassType);
    Equal("OUTER", entry.Outer.Pane); Equal("INNER", entry.Inner.Pane);
    Equal(2550m, entry.Inner.Width); Equal(2650m, entry.Inner.Height); Equal(4, entry.Inner.Qty);
});
Check("inner dimensions and quantity independent", () =>
{
    var entry = Pair("2540", "2640", "2"); Equal(2550m, entry.Outer.Width); Equal(2540m, entry.Inner!.Width);
    Equal(2640m, entry.Inner.Height); Equal(2, entry.Inner.Qty); Equal(4, entry.Outer.Qty);
});
Check("incomplete inner dimensions rejected", () => Invalid(() => Pair("100"), "both W2 and H2"));
Check("inner validation identifies pane", () => Invalid(() => Pair(innerQty: "0"), "Inner:"));
Check("complex spec requires overrides", () => Invalid(() => GlassEntry.Create("A", "G1+S1+G2+S2+G3", "", "C", "", "100", "70", "1", true), "overrides"));
Check("explicit material overrides supported", () =>
{
    var entry = GlassEntry.Create("A", "LAMINATED UNIT", "", "C", "", "100", "70", "1", true, outerGlass: "Laminated outer", innerGlass: "Laminated inner");
    Equal("Laminated inner", entry.Inner!.GlassType);
});
Check("legacy CSV remains supported by new importer", () => Equal(false, EntryTable.ImportCsv(Csv(sample))[0].IsDouble));
Check("new minimal schema and shared quantity", () =>
{
    var entry = EntryTable.ImportCsv($"OrderNo,Spec,W1,H1,Qty,CustomerName\nA,{spec},2550,2650,4,C")[0];
    Equal(4, entry.Inner!.Qty); Equal(2, entry.Labels.Count());
});
Check("quantity aliases and outer override", () =>
{
    var entry = EntryTable.ImportCsv($"Order,Specification,W1,H1,Qty,Qty1,Qty2,Customer\nA,{spec},2550,2650,4,3,2,C")[0];
    Equal(3, entry.Outer.Qty); Equal(2, entry.Inner!.Qty);
});
Check("duplicate aliases rejected", () => Invalid(() => EntryTable.ImportCsv("Order,OrderNo,Spec,W1,H1,Qty,Customer\nA,A,G,1,1,1,C"), "unique"));
Check("bad inner row rejects entire table", () => Invalid(() => EntryTable.ImportCsv($"OrderNo,Spec,W1,H1,Qty,W2,H2,CustomerName\nA,{spec},2550,2650,4,,,C\nB,{spec},2550,2650,4,200,,C"), "Record 3"));
Check("physical stock match", () => Equal(true, PrintStock.MatchesLabel(100 * 96 / 25.4, 70 * 96 / 25.4, false)));
Check("rotated stock match", () => Equal(true, PrintStock.MatchesLabel(70 * 96 / 25.4, 100 * 96 / 25.4, true)));
Check("unknown stock requires warning", () => Equal(false, PrintStock.MatchesLabel(null, null, false)));
Check("A4 stock requires warning", () => Equal(false, PrintStock.MatchesLabel(210 * 96 / 25.4, 297 * 96 / 25.4, false)));
Check("nonfinite stock requires warning", () => Equal(false, PrintStock.MatchesLabel(double.NaN, 200, false)));

MemoryStream Workbook(string sheetXml, string? sharedStrings = null)
{
    var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
    {
        void Part(string path, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(xml);
        }
        Part("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Glass\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Part("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        Part("xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + sheetXml + "</sheetData></worksheet>");
        if (sharedStrings != null) Part("xl/sharedStrings.xml", sharedStrings);
    }
    memory.Position = 0; return memory;
}
string Inline(string address, string value) => $"<c r=\"{address}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>";
string Row(int n, params string[] values) => $"<row r=\"{n}\">" + string.Concat(values.Select((value, i) => Inline($"{(char)('A' + i)}{n}", value))) + "</row>";
var headers = Row(1, "OrderNo", "Spec", "W1", "H1", "Qty", "CustomerName");
Check("XLSX inline strings import example", () =>
{
    using var memory = Workbook(headers + Row(2, "A", spec, "2550", "2650", "4", "عميل"));
    using var workbook = new XlsxWorkbook(memory); Equal("Glass", workbook.SheetNames.Single());
    var entry = EntryTable.Import(workbook.ReadSheet("Glass"))[0]; Equal(4, entry.Inner!.Qty); Equal("عميل", entry.Outer.CustomerName);
});
Check("XLSX shared strings, sparse cells and numeric scientific notation", () =>
{
    using var memory = Workbook("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"C1\"><v>2.55E3</v></c></row>", "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><r><t>HEL</t></r><r><t>LO</t></r></si></sst>");
    using var workbook = new XlsxWorkbook(memory); var row = workbook.ReadSheet("Glass")[0];
    Equal("HELLO", row[0]); Equal("", row[1]); Equal("2550", row[2]);
});
Check("XLSX cached formula accepted", () =>
{
    using var memory = Workbook("<row r=\"1\"><c r=\"A1\"><f>2+2</f><v>4</v></c></row>");
    using var workbook = new XlsxWorkbook(memory); Equal("4", workbook.ReadSheet("Glass")[0][0]);
});
Check("XLSX formula without cached result rejected", () =>
{
    using var memory = Workbook("<row r=\"1\"><c r=\"A1\"><f>2+2</f></c></row>");
    using var workbook = new XlsxWorkbook(memory); Invalid(() => workbook.ReadSheet("Glass"), "saved result");
});
Check("XLSX error cell rejected", () =>
{
    using var memory = Workbook("<row r=\"1\"><c r=\"A1\" t=\"e\"><v>#REF!</v></c></row>");
    using var workbook = new XlsxWorkbook(memory); Invalid(() => workbook.ReadSheet("Glass"), "Excel error");
});
Check("XLSX blank leading rows ignored", () =>
{
    using var memory = Workbook("<row r=\"1\"/>" + Row(2, "Header"));
    using var workbook = new XlsxWorkbook(memory); Equal("Header", workbook.ReadSheet("Glass")[0][0]);
});
Check("XLSX missing worksheet rejected", () =>
{
    using var memory = Workbook(headers); using var workbook = new XlsxWorkbook(memory);
    Invalid(() => workbook.ReadSheet("Not a worksheet"), "Select a worksheet");
});
Check("XLSX extra unheaded columns rejected", () =>
{
    using var memory = Workbook(Row(1, "Header") + Row(2, "value", "unexpected"));
    using var workbook = new XlsxWorkbook(memory); Invalid(() => workbook.ReadSheet("Glass"), "beyond the header");
});
Check("quantity four produces the requested 1/1 through 1/4", () =>
{
    var pages=LabelPrintPlan.Build(new[]{Make(qty:"4")},true,NumberingStyle.EntryCopy);
    Equal("1/1,1/2,1/3,1/4",string.Join(",",pages.Select(p=>p.QuantityText)));
    Equal(4,pages.Length); Equal(4,pages[3].Qty);
});
Check("next batch entry has its own numbering prefix", () =>
{
    var pages=LabelPrintPlan.Build(new[]{Make(qty:"2") with {EntryNumber=3}},true,NumberingStyle.EntryCopy);
    Equal("3/1",pages[0].QuantityText); Equal("3/2",pages[1].QuantityText);
});
Check("DGU numbering restarts independently for each pane", () =>
{
    var pages=LabelPrintPlan.Build(Pair(innerQty:"2").Labels,true,NumberingStyle.EntryCopy);
    Equal(6,pages.Length);Equal("1/4",pages[3].QuantityText);Equal("1/1",pages[4].QuantityText);Equal("INNER",pages[4].Pane);
});
Check("copy-of-total option", () => Equal("2/4",LabelPrintPlan.Build(new[]{Make(qty:"4")},true,NumberingStyle.CopyOfTotal)[1].QuantityText));
Check("quantity-only option does not alter totals", () => Equal("4",LabelPrintPlan.Build(new[]{Make(qty:"4")},true,NumberingStyle.QuantityOnly)[3].QuantityText));
Check("copies off produces one page with stable numbering", () => Equal(1,LabelPrintPlan.Build(new[]{Make(qty:"4")},false,NumberingStyle.EntryCopy).Length));
Check("numbered print plan limits expansion", () => Invalid(()=>LabelPrintPlan.Build(new[]{Make(qty:"10000"),Make(qty:"1")},true,NumberingStyle.EntryCopy),"10,000"));
Check("print plan does not mutate source label", () =>
{
    var label=Make(qty:"4");LabelPrintPlan.Build(new[]{label},true,NumberingStyle.EntryCopy);Equal(NumberingStyle.QuantityOnly,label.Numbering);Equal(1,label.CopyIndex);
});
Check("default library has three independently valid formats", () =>
{
    var library=new LayoutLibrary(); library.Validate();Equal(3,library.Layouts.Count);
    foreach(var format in Enum.GetValues<StickerFormat>())Equal(format,library.For(format).Format);
});
Check("automatic format routing", () => {Equal(StickerFormat.SGU,LayoutLibrary.FormatFor(Make()));Equal(StickerFormat.DGU,LayoutLibrary.FormatFor(Pair().Outer));});
Check("layout clones are independent", () =>
{
    var original=new LayoutLibrary();var clone=original.Clone();clone.For(StickerFormat.SGU).Elements[0].X=8;Equal(4d,original.For(StickerFormat.SGU).Elements[0].X);
});
Check("off-label fields rejected", () => {var layout=StickerLayout.Default(StickerFormat.SGU);layout.Elements[0].X=99;Invalid(layout.Validate,"outside");});
Check("nonfinite fields rejected", () => {var layout=StickerLayout.Default(StickerFormat.SGU);layout.Elements[0].X=double.NaN;Invalid(layout.Validate,"finite");});
Check("unreadably small fonts rejected", () => {var layout=StickerLayout.Default(StickerFormat.SGU);layout.Elements[0].FontSize=3;Invalid(layout.Validate,"6–36");});
Check("duplicate element IDs rejected", () => {var layout=StickerLayout.Default(StickerFormat.SGU);layout.Elements.Add(layout.Elements[0].Clone());Invalid(layout.Validate,"unique");});
Check("unknown fields rejected", () => {var layout=StickerLayout.Default(StickerFormat.SGU);layout.Elements[0].Field="ArbitraryXaml";Invalid(layout.Validate,"data field");});
Check("missing format rejected", () => {var library=new LayoutLibrary();library.Layouts.RemoveAt(0);Invalid(library.Validate,"once each");});
Check("layout save, reload, and last-file backup", () =>
{
    var dir=Path.Combine(Path.GetTempPath(),"excelsticker-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"layouts.json");
    try
    {
        var library=new LayoutLibrary();library.Save(path);library.For(StickerFormat.SGU).Elements[0].X=5;library.Save(path);
        Equal(5d,LayoutLibrary.Load(path).For(StickerFormat.SGU).Elements[0].X);
        Equal(4d,LayoutLibrary.Load(path+".bak").For(StickerFormat.SGU).Elements[0].X);
        var single=Path.Combine(dir,"sgu.json"); library.For(StickerFormat.SGU).Save(single);
        Equal(StickerFormat.SGU,StickerLayout.Load(single).Format);
        Equal(5d,StickerLayout.Load(single).Elements[0].X);
    }
    finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
});
Console.WriteLine($"All {passed} core checks passed.");
