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
    var label=Make(qty:"4") with {Numbering=NumberingStyle.QuantityOnly};LabelPrintPlan.Build(new[]{label},true,NumberingStyle.EntryCopy);Equal(NumberingStyle.QuantityOnly,label.Numbering);Equal(1,label.CopyIndex);
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
Check("title-row CSV header detection", () =>
{
    var rows=ImportMapping.ReadCsv("Ocean Glass export\nOrderNo,Spec,W1,H1,Qty,CustomerName\nA,CLEAR,2550,2650,4,C",out var delimiter);
    Equal(',',delimiter);var header=ImportMapping.SuggestHeader(rows);Equal(1,header);
    Equal(4,ImportMapping.Import(rows,header,rows.Count,ImportMapping.SuggestColumns(rows[header]))[0].Outer.Qty);
});
Check("semicolon CSV with explicit comma decimals", () =>
{
    var rows=ImportMapping.ReadCsv("OrderNo;Spec;W1;H1;Qty;CustomerName\nA;CLEAR;2550,5;2650,5;4;C",out var delimiter);
    Equal(';',delimiter);Equal(2550.5m,ImportMapping.Import(rows,0,rows.Count,ImportMapping.SuggestColumns(rows[0]),commaDecimals:true)[0].Outer.Width);
});
Check("tab-separated import", () =>
{
    var rows=ImportMapping.ReadCsv("OrderNo\tSpec\tW1\tH1\tQty\tCustomerName\nA\tCLEAR\t100\t70\t1\tC",out var delimiter);
    Equal('\t',delimiter);Equal(1,ImportMapping.Import(rows,0,rows.Count,ImportMapping.SuggestColumns(rows[0])).Count);
});
Check("vendor headings require explicit mapping and metadata", () =>
{
    var rows=new[]{new[]{"Material","Outside W","Outside H","Pieces"},new[]{"CLEAR","100","70","4"}};
    var map=ImportMapping.SuggestColumns(rows[0]);map["Spec"]=0;map["W1"]=1;map["H1"]=2;map["Qty"]=3;
    Invalid(()=>ImportMapping.Import(rows,0,2,map),"OrderNo");
    var entries=ImportMapping.Import(rows,0,2,map,new Dictionary<string,string>{{"OrderNo","NEW-1"},{"CustomerName","Confirmed customer"}});
    Equal("NEW-1",entries[0].Outer.OrderNo);Equal("Confirmed customer",entries[0].Outer.CustomerName);
});
Check("duplicate aliases require a manual choice", () => Equal(-1,ImportMapping.SuggestColumns(new[]{"Spec","W1","Width","H1","Qty"})["W1"]));
Check("import range excludes totals footer", () =>
{
    var rows=ImportMapping.ReadCsv("OrderNo,Spec,W1,H1,Qty,CustomerName\nA,CLEAR,100,70,4,C\nTotal,,,,4,",out _);
    Equal(1,ImportMapping.Import(rows,0,2,ImportMapping.SuggestColumns(rows[0])).Count);
    Invalid(()=>ImportMapping.Import(rows,0,3,ImportMapping.SuggestColumns(rows[0])),"Nothing was imported");
});
Check("mixed decimal and thousands separators rejected", () =>
{
    var rows=new[]{new[]{"OrderNo","Spec","W1","H1","Qty","CustomerName"},new[]{"A","CLEAR","1.234,5","70","1","C"}};
    Invalid(()=>ImportMapping.Import(rows,0,2,ImportMapping.SuggestColumns(rows[0]),commaDecimals:true),"mixed");
});
Check("XLSX title row does not limit table width", () =>
{
    using var memory=Workbook(Row(1,"Ocean Glass export")+Row(2,"OrderNo","Spec","W1","H1","Qty","CustomerName")+Row(3,"A","CLEAR","100","70","4","C"));
    using var workbook=new XlsxWorkbook(memory);var rows=workbook.ReadTable("Glass");var header=ImportMapping.SuggestHeader(rows);
    Equal(1,header);Equal(6,rows[0].Length);Equal(4,ImportMapping.Import(rows,header,rows.Count,ImportMapping.SuggestColumns(rows[header]))[0].Outer.Qty);
});
Check("XLSX missing cell references use sequential columns", () =>
{
    using var memory=Workbook("<row r=\"1\"><c t=\"inlineStr\"><is><t>First</t></is></c><c><v>42</v></c></row>");
    using var workbook=new XlsxWorkbook(memory);Equal("42",workbook.ReadTable("Glass")[0][1]);
});
Check("Strict Open XML imports", () =>
{
    using var memory=Workbook(headers+Row(2,"A","CLEAR","100","70","4","C"));
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Update,true))
    {
        foreach(var entry in zip.Entries.Where(e=>e.FullName.EndsWith(".xml")).ToArray())
        {
            string xml;using(var reader=new StreamReader(entry.Open()))xml=reader.ReadToEnd();
            var path=entry.FullName;entry.Delete();
            xml=xml.Replace("http://schemas.openxmlformats.org/spreadsheetml/2006/main","http://purl.oclc.org/ooxml/spreadsheetml/main")
                .Replace("http://schemas.openxmlformats.org/officeDocument/2006/relationships","http://purl.oclc.org/ooxml/officeDocument/relationships");
            using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(xml);
        }
    }
    memory.Position=0;using var workbook=new XlsxWorkbook(memory);Equal(2,workbook.ReadTable("Glass").Count);
});
Check("Legacy Excel mapping recognizes Glass Ref", () =>
{
    Equal("ref",EntryTable.CanonicalHeader("Glass Ref"));
    Equal("G-1",EntryTable.ImportCsv("OrderNo,Spec,W1,H1,Qty,Glass Ref,CustomerName\nA,CLEAR,100,200,1,G-1,C")[0].Outer.ReferenceNo);
});
Check("Cutting math matches reference, rounding SQM before quantity", () =>
{
    var sheet=new CuttingSheet();var group=new CuttingGroup {GlassType="DGU",Specification="GREY + AIR + CLEAR",BasePrice="170"};sheet.Groups.Add(group);
    Equal(3,CuttingTable.Paste(sheet,group,"11\t1050\t2127\t0\t0\t2\t170\n4\t854\t1981\t0\t0\t2\t170\n7\t1003\t1857\t0\t0\t2\t170"));
    Equal(2.233m,group.Rows[0].Sqm);Equal(759.22m,group.Rows[0].TotalPrice);Equal(1967.92m,group.TotalPrice);Equal(11.576m,group.TotalSqm);
    var entry=group.Rows[0].ToEntry("A","Customer","");Equal(1050m,entry.Inner!.Width);Equal(2127m,entry.Inner.Height);
});
Check("Cutting paste is atomic and partial cell paste preserves identity", () =>
{
    var sheet=new CuttingSheet();var group=new CuttingGroup {Specification="CLEAR"};sheet.Groups.Add(group);
    CuttingTable.Paste(sheet,group,"A\t1000\t2000\t0\t0\t2\t10");var row=group.Rows[0];
    Invalid(()=>CuttingTable.Paste(sheet,group,"B\t1200\t1000\t0\t0\t1\t10\nC\tBAD\t1000\t0\t0\t1\t10"),"Nothing pasted");
    Equal(1,group.Rows.Count);Equal("A",row.GlassRef);
    CuttingTable.Paste(sheet,group,"1500\t2500",0,2);Equal(true,ReferenceEquals(row,group.Rows[0]));Equal("1500",row.W1);Equal("2500",row.H1);
    Invalid(()=>CuttingTable.Paste(sheet,group,"1\t2",0,10),"Paste rows");Equal("1500",row.W1);
});
Check("Cutting headers ignore supplied calculated cells and support copied action column", () =>
{
    var sheet=new CuttingSheet();var group=new CuttingGroup {Specification="CLEAR"};sheet.Groups.Add(group);
    var text=string.Join("\t",CuttingTable.Columns)+"\t\n99\tA\t1000\t1000\t0\t0\t2\t999\t999\t10\t999\t";
    CuttingTable.Paste(sheet,group,text);Equal(1,group.Rows[0].Serial);Equal(20m,group.Rows[0].TotalPrice);
});
Check("Cutting base rates, overrides and invalid draft rows", () =>
{
    var sheet=new CuttingSheet();var group=new CuttingGroup {Specification="CLEAR",BasePrice="20"};sheet.Groups.Add(group);
    var row=new CuttingRow {W1="1000",H1="1000",Qty="2"};group.Rows.Add(row);Equal(40m,row.TotalPrice);
    group.BasePrice="30";Equal(60m,row.TotalPrice);row.Price="10";Equal(20m,row.TotalPrice);
    row.W2="200";Equal(true,row.Error.Length>0);Equal(0m,row.TotalPrice);
    row.W2="0";row.W1="0";Equal(true,row.Error.Length>0);Equal(0,group.Quantity);
    Invalid(()=>row.ToEntry("A","C",""),"greater than zero");
});
Check("Cutting file round trip preserves groups, prices and separate inner quantity", () =>
{
    var sheet=new CuttingSheet();var entries=new[]{GlassEntry.Create("A","GREY + AIR + CLEAR","R","Customer","J","1000","2000","4",true,"900","1900","2")};
    sheet.AddEntries(entries,"test.xlsx");sheet.Groups[0].BasePrice="12.5";
    var csv=CuttingTable.Export(sheet,"","","");var table=LabelCsv.Parse(csv);var imported=CuttingTable.Read(table,CuttingTable.FindHeader(table));
    Equal(1,imported.Count);Equal(100m,imported[0].TotalPrice);var row=imported[0].Rows[0];
    Equal("2",row.InnerQty);Equal("A",row.OrderNo);Equal("J",row.JobNo);Equal(900m,row.ToEntry("","","").Inner!.Width);
});
Check("Cutting entries retain complex pane overrides and duplicate identity", () =>
{
    var sheet=new CuttingSheet();sheet.Groups.Add(new CuttingGroup());
    var entry=GlassEntry.Create("A","LAMINATED + COMPLEX + EXTRA + CLEAR","R","C","","100","200","2",true,outerGlass:"LAMINATED",innerGlass:"CLEAR");
    var rows=sheet.AddEntries(new[]{entry,entry},"file");Equal(1,sheet.Groups.Count);Equal(false,ReferenceEquals(rows[0],rows[1]));
    Equal("LAMINATED",rows[0].ToEntry("","","").Outer.GlassType);
    Equal("",rows[0].ToEntry("OTHER","OTHER","NEW JOB").Outer.JobNo);
    rows[0].Qty="3";Equal(2,rows[0].ToEntry("","","").Inner!.Qty);
    sheet.Groups[0].Rows.Remove(rows[0]);Equal(1,rows[1].Serial);
});
Check("Cutting rejects fractional quantities and unsupported imports", () =>
{
    var sheet=new CuttingSheet();var group=new CuttingGroup {Specification="CLEAR"};sheet.Groups.Add(group);
    Invalid(()=>CuttingTable.Paste(sheet,group,"A\t100\t200\t0\t0\t1.5\t10"),"integer");Equal(0,group.Rows.Count);
    var table=LabelCsv.Parse("GlassType,Specification,W1,H1,Qty,Price\nTGU,CLEAR,100,200,1,10");
    Invalid(()=>CuttingTable.Read(table,0),"SGU/DGU");
});
Check("Native partial-column headers and W1-style references are unambiguous at the target", () =>
{
    var sheet=new CuttingSheet();var group=new CuttingGroup {Specification="CLEAR"};sheet.Groups.Add(group);
    CuttingTable.Paste(sheet,group,"A\t1000\t2000\t0\t0\t2\t10");
    CuttingTable.Paste(sheet,group,"W1\n1400",0,2);Equal("1400",group.Rows[0].W1);Equal(1,group.Rows.Count);
    CuttingTable.Paste(sheet,group,"Glass Ref\nW1",0,1);Equal("W1",group.Rows[0].GlassRef);
    CuttingTable.Paste(sheet,group,"W1\nW2",0,1);Equal("W1",group.Rows[0].GlassRef);Equal("W2",group.Rows[1].GlassRef);
    Invalid(()=>CuttingTable.Paste(sheet,group,"SQM\n99",0,7),"editable");
});
Check("Cutting export preserves row order across recurring specifications", () =>
{
    var sheet=new CuttingSheet();
    foreach(var spec in new[]{"CLEAR","GREY","CLEAR"})
    {
        var group=new CuttingGroup {Specification=spec};sheet.Groups.Add(group);
        group.Rows.Add(new CuttingRow {GlassRef=sheet.Groups.Count.ToString(),W1="100",H1="200"});
    }
    var table=LabelCsv.Parse(CuttingTable.Export(sheet,"A","C",""));
    var groups=CuttingTable.Read(table,CuttingTable.FindHeader(table));
    Equal(3,groups.Count);Equal("1,2,3",string.Join(",",groups.SelectMany(g=>g.Rows).Select(r=>r.GlassRef)));
});
Check("Cutting CSV keeps formula-looking text literal and round trips apostrophes", () =>
{
    var sheet=new CuttingSheet();sheet.AddEntries(new[]{GlassEntry.Create("-ORDER","=1+1","+REF","'CUSTOM","@JOB","100","200","1",false)},"manual");
    var table=LabelCsv.Parse(CuttingTable.Export(sheet,"","",""));Equal("'=1+1",table[1][1]);
    var row=CuttingTable.Read(table,0)[0].Rows[0];var entry=row.ToEntry("","","");
    Equal("=1+1",entry.Specification);Equal("+REF",entry.Outer.ReferenceNo);Equal("'CUSTOM",entry.Outer.CustomerName);Equal("@JOB",entry.Outer.JobNo);
});
Check("Sticker quantity defaults to glass serial / copy", () =>
{
    Equal("1/1",Make(qty:"4").QuantityText);
    Equal("7/2",(Make(qty:"4") with {EntryNumber=7,CopyIndex=2}).QuantityText);
    var pages=LabelPrintPlan.Build(new[]{Make(qty:"4"),Make(qty:"2") with {EntryNumber=7}},true,NumberingStyle.EntryCopy);
    Equal("1/1,1/2,1/3,1/4,7/1,7/2",string.Join(",",pages.Select(p=>p.QuantityText)));
});
Check("Every stock sticker includes a separate glass serial and one numbered QTY", () =>
{
    foreach(var format in Enum.GetValues<StickerFormat>())
    {
        var layout=StickerLayout.Default(format);layout.Validate();Equal(3,layout.Version);
        Equal(1,layout.Elements.Count(e=>e.Visible && e.Field=="GlassSrNo"));
        Equal(1,layout.Elements.Count(e=>e.Visible && e.Field=="Quantity"));
        Equal(false,layout.Elements.Any(e=>e.Field=="TotalQty"));
        var serial=layout.Elements.Single(e=>e.Field=="GlassSrNo");var order=layout.Elements.Single(e=>e.Field=="OrderNo");
        Equal(true,order.X+order.Width<serial.X);Equal("SR NO: ",serial.Prefix);
    }
});
Check("Legacy default layouts upgrade once without resetting custom company text", () =>
{
    var legacy=StickerLayout.Default(StickerFormat.DGU);legacy.Version=1;
    legacy.Elements.RemoveAll(e=>e.Field=="GlassSrNo");legacy.Elements.Single(e=>e.Field=="OrderNo").Width=92;
    legacy.Elements.Single(e=>e.Field=="Quantity").Field="TotalQty";
    legacy.Elements[0].Prefix="MY COMPANY: ";var oldId=legacy.Elements[0].Id;
    legacy.UpgradeLegacyNumbering();legacy.UpgradeLegacyNumbering();
    Equal(1,legacy.Elements.Count(e=>e.Field=="GlassSrNo"));Equal(58d,legacy.Elements.Single(e=>e.Field=="OrderNo").Width);
    Equal(oldId,legacy.Elements[0].Id);Equal("MY COMPANY: ",legacy.Elements[0].Prefix);
    Equal(true,legacy.Elements.Any(e=>e.Field=="Quantity"));
});
Check("Custom legacy layouts use vacant space without moving existing fields", () =>
{
    var layout=new StickerLayout {Version=1,Elements=new(){new StickerElement {Field="CompanyName",X=4,Y=3,Width=92,Height=7}}};
    var original=layout.Elements[0];layout.UpgradeLegacyNumbering();
    var serial=layout.Elements.Single(e=>e.Field=="GlassSrNo");
    Equal(4d,original.X);Equal(3d,original.Y);Equal(92d,original.Width);Equal(true,serial.Y>=original.Y+original.Height);
});
Check("Dense legacy layouts are never overwritten by an automatic serial field", () =>
{
    var layout=new StickerLayout {Version=1,Elements=new(){new StickerElement {X=0,Y=0,Width=100,Height=70}}};
    layout.UpgradeLegacyNumbering();Equal(1,layout.Elements.Count);Equal(3,layout.Version);
});
Check("Legacy layout loading upgrades in memory without overwriting the saved file", () =>
{
    var dir=Path.Combine(Path.GetTempPath(),"sticker-serial-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"layouts.json");
    try
    {
        var library=new LayoutLibrary();
        foreach(var layout in library.Layouts){layout.Version=1;layout.Elements.RemoveAll(e=>e.Field=="GlassSrNo");layout.Elements.Single(e=>e.Field=="OrderNo").Width=92;}
        library.Save(path);var original=File.ReadAllText(path);var loaded=LayoutLibrary.Load(path);
        Equal(original,File.ReadAllText(path));Equal(true,loaded.Layouts.All(l=>l.Elements.Any(e=>e.Field=="GlassSrNo")));
    }
    finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
});
Check("Custom sticker stock resizes atomically and snapshots keep independent geometry", () =>
{
    var library=new LayoutLibrary();var snapshot=library.Clone();library.ResizeStock(120,80,true);
    Equal(true,library.Layouts.All(l=>l.WidthMm==120 && l.HeightMm==80));Equal(100d,snapshot.Layouts[0].WidthMm);
    Equal(4.8,library.Layouts[0].Elements[0].X);Equal(16d,library.Layouts[0].Elements[0].FontSize);
    Invalid(()=>library.ResizeStock(20,20,false),"outside");Equal(120d,library.Layouts[0].WidthMm);
    Invalid(()=>library.ResizeStock(double.NaN,70,true),"20–300");Equal(120d,library.Layouts[0].WidthMm);
    library.ResizeStock(20,20,true);library.Validate();
});
Check("Mixed stock formats cannot enter one printer job", () =>
{
    var library=new LayoutLibrary();library.Layouts[0].WidthMm=110;
    Invalid(library.Validate,"same stock");
    Equal(true,PrintStock.MatchesLabel(120*96/25.4,80*96/25.4,false,120,80));
    Equal(false,PrintStock.MatchesLabel(120*96/25.4,80*96/25.4,false));
});
Check("Version 2 layouts retain default stock and upgrade without moving fields", () =>
{
    var old=StickerLayout.Default(StickerFormat.SGU);old.Version=2;var x=old.Elements[0].X;
    old.UpgradeLegacyNumbering();Equal(3,old.Version);Equal(100d,old.WidthMm);Equal(70d,old.HeightMm);Equal(x,old.Elements[0].X);
});
Check("Logo payload and orthogonal rotation validation", () =>
{
    var layout=StickerLayout.Default(StickerFormat.SGU);var logo=new StickerElement {Field="Logo",Width=10,Height=8};layout.Elements.Add(logo);
    layout.Validate();logo.ImagePng="not a PNG";Invalid(layout.Validate,"PNG");logo.ImagePng="";
    logo.Rotation=45;Invalid(layout.Validate,"rotation");logo.Rotation=90;layout.Validate();
    var clone=layout.Clone();clone.Elements.Last().X=10;Equal(4d,logo.X);
});
CuttingWorkbookChecks.Run(Check);
Console.WriteLine($"All {passed} core checks passed.");
