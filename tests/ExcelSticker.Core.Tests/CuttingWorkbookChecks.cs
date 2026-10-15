using System.IO.Compression;
using System.Xml.Linq;
using ExcelSticker.Core;

internal static class CuttingWorkbookChecks
{
    private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
    private static CuttingSheet Sample()
    {
        var sheet=new CuttingSheet();var group=CuttingGroup.Blank();group.GlassType="DGU";group.Specification="GREY + AIR + CLEAR";group.BasePrice="170";sheet.Groups.Add(group);
        group.Rows[0].CopyValues(CuttingRow.FromEntry(GlassEntry.Create("OWN","GREY + AIR + CLEAR","=1+1","Owner","","1050","2127","2",true,"1000","2100","1"),"source.xlsx"));
        group.Rows[1].W1="draft";
        var same=CuttingGroup.Blank();same.GlassType="DGU";same.Specification=group.Specification;same.BasePrice="185";sheet.Groups.Add(same);
        return sheet;
    }
    private static MemoryStream Save(CuttingSheet sheet)
    {var stream=new MemoryStream();CuttingWorkbook.Write(stream,sheet,"DEFAULT","Factory","JOB");stream.Position=0;return stream;}
    private static CuttingWorkbookData Read(MemoryStream stream)
    {stream.Position=0;using var book=new XlsxWorkbook(stream);return CuttingWorkbook.Read(book,"Cutting list");}
    private static void Edit(MemoryStream stream,Action<XDocument> edit)
    {
        stream.Position=0;using var zip=new ZipArchive(stream,ZipArchiveMode.Update,true);
        var part=zip.GetEntry("xl/worksheets/sheet1.xml")!;XDocument xml;using(var input=part.Open())xml=XDocument.Load(input);
        edit(xml);part.Delete();using var output=zip.CreateEntry("xl/worksheets/sheet1.xml").Open();xml.Save(output);
    }
    private static XElement Cell(XDocument xml,string address)=>xml.Descendants(S+"c").Single(c=>(string?)c.Attribute("r")==address);
    public static void Run(Action<string,Action> check)
    {
        check("Five blank input rows are not priced or treated as incomplete entries",()=>
        {
            var group=CuttingGroup.Blank();Assert(group.Rows.Count==5 && group.Rows.All(r=>r.IsEmpty && r.Error=="" && r.SqmDisplay==""),"Blank starter rows");
            Assert(group.Quantity==0 && group.TotalPrice==0 && group.ValidationSummary=="","No phantom quantities/errors");
            group.Rows[1].Qty="1";Assert(!group.Rows[1].IsEmpty && group.Rows[1].Error.Length>0,"A quantity-only partial row is not mistaken for an unused slot");
            group.Rows[0].W1="100";Assert(!group.Rows[0].IsEmpty && group.Rows[0].Error.Length>0,"Partial row is still a draft");
        });
        check("Excel paste and legacy import reuse starter rows",()=>
        {
            var sheet=new CuttingSheet();var group=CuttingGroup.Blank();sheet.Groups.Add(group);group.Specification="CLEAR";
            CuttingTable.Paste(sheet,group,"A\t100\t200\t0\t0\t2\t170");Assert(group.Rows.Count==5 && group.Rows[0].GlassRef=="A","Paste fills first blank slot");
            var added=sheet.AddEntries(new[]{GlassEntry.Create("A","CLEAR","B","C","","100","200","1",false)},"file");
            Assert(group.Rows.Count==5 && added[0].Serial==2 && sheet.ActiveRows.Count()==2,"Imports reuse blank slots with correct serial");
        });
        check("Formatted Excel round trip preserves groups, blanks, prices, drafts and hidden pane metadata",()=>
        {
            using var stream=Save(Sample());using(var book=new XlsxWorkbook(stream))Assert(book.SheetNames.SequenceEqual(new[]{"Cutting list"}),"Technical metadata must not appear in sheet chooser");
            var read=Read(stream);var row=read.Sheet.Groups[0].Rows[0];
            Assert(read.OrderNo=="DEFAULT" && read.CustomerName=="Factory" && read.JobNo=="JOB","Sheet defaults");
            Assert(read.Sheet.Groups.Count==2 && read.Sheet.Rows.Count()==10 && read.Sheet.Groups[1].Rows.All(r=>r.IsEmpty),"Duplicate spec cards and blank rows retained");
            Assert(row.GlassRef=="=1+1" && row.Price=="" && row.Rate==170 && row.TotalPrice==759.22m,"Visible inputs and inherited rate");
            Assert(row.InnerQty=="1" && row.Source=="source.xlsx" && row.ToEntry("DEFAULT","Factory","JOB").Inner!.Width==1000 && row.JobNo=="" && row.HasOwnMetadata,"Independent metadata not replaced by defaults");
            Assert(read.Sheet.Groups[0].Rows[1].W1=="draft" && read.Sheet.Groups[0].Rows[1].Error.Length>0,"Raw draft retained");
        });
        check("Formatted workbook has a clean eleven-column page, formula caches, hidden metadata and print setup",()=>
        {
            using var stream=Save(Sample());using var zip=new ZipArchive(stream,ZipArchiveMode.Read);
            using var input=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var xml=XDocument.Load(input);
            Assert(xml.Descendants(S+"col").Single(c=>(string?)c.Attribute("min")=="12").Attribute("hidden")?.Value=="1","Only internal ID column hidden");
            Assert(Cell(xml,"B9").Attribute("t")?.Value=="inlineStr" && Cell(xml,"B9").Element(S+"f")==null,"Formula-looking reference is literal text");
            Assert(Cell(xml,"K9").Element(S+"v")?.Value=="759.22" && Cell(xml,"H9").Element(S+"f")!=null,"Calculated cells have formulas and cached results");
            Assert(xml.Descendants(S+"pageSetup").Single().Attribute("orientation")?.Value=="landscape","A4 landscape print page");
            Assert(xml.Descendants(S+"mergeCell").Any(c=>c.Attribute("ref")?.Value=="F6:K6"),"Specification is a page heading, not repeated data columns");
        });
        check("Edited Excel input values override stored data, and formulas need no cached totals",()=>
        {
            using var stream=Save(Sample());Edit(stream,xml=>
            {
                Cell(xml,"C9").Element(S+"v")!.Value="1200";
                foreach(var c in xml.Descendants(S+"c").Where(c=>c.Element(S+"f")!=null))c.Element(S+"v")?.Remove();
                var price=Cell(xml,"J9");price.Element(S+"f")!.Remove();price.Attribute("t")?.Remove();price.Add(new XElement(S+"v","200"));
            });
            var row=Read(stream).Sheet.Groups[0].Rows[0];Assert(row.W1=="1200" && row.Price=="200" && row.TotalPrice==1020.80m,"Use current visible cells, not stale caches/hidden copies");
        });
        check("Changing Excel base rate keeps formula-priced rows inheriting it",()=>
        {
            using var stream=Save(Sample());Edit(stream,xml=>Cell(xml,"C7").Element(S+"v")!.Value="200");
            var row=Read(stream).Sheet.Groups[0].Rows[0];Assert(row.Price=="" && row.Rate==200,"Price formula remains inherited, not stale cached 170");
        });
        check("New Excel rows can be imported without hidden IDs",()=>
        {
            using var stream=Save(Sample());Edit(stream,xml=>
            {
                Cell(xml,"L10").Remove();var width=Cell(xml,"C10");width.Attribute("t")?.Remove();width.Element(S+"is")?.Remove();width.Add(new XElement(S+"v","500"));
                foreach(var address in new[]{"D10","G10"}){var cell=Cell(xml,address);cell.Attribute("t")?.Remove();cell.Element(S+"is")?.Remove();cell.Add(new XElement(S+"v",address.StartsWith("D")?"700":"1"));}
            });
            var row=Read(stream).Sheet.Groups[0].Rows[1];Assert(row.W1=="500" && row.Qty=="1" && !row.HasOwnMetadata,"New row uses sheet metadata defaults");
        });
        check("Blank formatted workbook round trips as five editable rows",()=>
        {
            var sheet=new CuttingSheet();sheet.Groups.Add(CuttingGroup.Blank());using var stream=Save(sheet);
            var read=Read(stream);Assert(read.Sheet.Rows.Count()==5 && !read.Sheet.ActiveRows.Any(),"Blank rows survive Excel save/open");
        });
        check("Failed Excel save leaves existing file intact",()=>
        {
            var dir=Path.Combine(Path.GetTempPath(),"cutting-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var path=Path.Combine(dir,"list.xlsx");
            try
            {
                File.WriteAllText(path,"original");var sheet=Sample();sheet.Groups[0].Rows[0].GlassRef=new string('X',32768);
                try {CuttingWorkbook.Save(path,sheet,"","","");throw new Exception("Expected oversized-cell rejection");}catch(FormatException){}
                Assert(File.ReadAllText(path)=="original" && Directory.GetFiles(dir).Length==1,"Atomic save protects original and removes temporary file");
            }
            finally{Directory.Delete(dir,true);}
        });
    }
}
