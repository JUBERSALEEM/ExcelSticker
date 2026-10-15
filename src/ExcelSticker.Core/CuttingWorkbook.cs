using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ExcelSticker.Core;

public sealed record CuttingWorkbookData(CuttingSheet Sheet,string OrderNo,string CustomerName,string JobNo);

/// <summary>Formatted, editable XLSX cutting page. Visible cells are authoritative; hidden IDs retain pane metadata.</summary>
public static class CuttingWorkbook
{
    public const string FormatMarker="LABLE-STUDIO-CUTTING/1";
    public const string MetadataSheet="_StudioData";
    private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P="http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace C="http://schemas.openxmlformats.org/package/2006/content-types";
    private static string N(decimal value)=>value.ToString(CultureInfo.InvariantCulture);
    internal static string InheritFormula(int row,int dataRow)=>$"IF(AND(B{dataRow}=\"\",C{dataRow}=\"\",D{dataRow}=\"\",E{dataRow}=\"\",F{dataRow}=\"\",G{dataRow}=\"\"),\"\",IF($C${row}=\"\",\"\",$C${row}))";
    internal static bool IsInheritedFormula(string? formula,int row,int dataRow)=>row>0 && formula?.Replace("$","").Replace(" ","").ToUpperInvariant()==InheritFormula(row,dataRow).Replace("$","");
    private sealed record RowMetadata(string OrderNo,string CustomerName,string JobNo,string InnerQty,string OuterGlass,string InnerGlass,string Source,bool HasOwnMetadata);
    private static string Text(string value)
    {
        if(value.Length>32767)throw new FormatException("Excel cells are limited to 32,767 characters. Shorten the affected input before saving.");
        try {XmlConvert.VerifyXmlChars(value);}catch(XmlException ex){throw new FormatException("A cutting value contains a control character Excel cannot store.",ex);}
        return value;
    }
    private static XElement Cell(int column,int row,string value,int style=0,bool numeric=false,string? formula=null)
    {
        var cell=new XElement(S+"c",new XAttribute("r",$"{(char)('A'+column-1)}{row}"),new XAttribute("s",style));
        if(formula==null && value.Length==0)return cell;
        if(formula!=null)
        {
            cell.Add(new XElement(S+"f",formula));
            if(decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var cached))cell.Add(new XElement(S+"v",N(cached)));
            else {cell.SetAttributeValue("t","str");cell.Add(new XElement(S+"v",Text(value)));}
        }
        else if(numeric && decimal.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number))cell.Add(new XElement(S+"v",N(number)));
        else {cell.SetAttributeValue("t","inlineStr");cell.Add(new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),Text(value))));}
        return cell;
    }
    public static void Save(string path,CuttingSheet sheet,string order,string customer,string job)
    {
        if(!Path.GetExtension(path).Equals(".xlsx",StringComparison.OrdinalIgnoreCase))throw new FormatException("Save the cutting workbook with an .xlsx extension.");
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))Write(stream,sheet,order,customer,job);
            using(var input=File.OpenRead(temp))using(var check=new ZipArchive(input,ZipArchiveMode.Read))
                if(input.Length>10L*1024*1024 || check.Entries.Sum(e=>e.Length)>64L*1024*1024 || check.Entries.Any(e=>e.Length>32L*1024*1024))
                    throw new FormatException("Workbook exceeds the safe re-import size limit. Split the cutting list before saving.");
            File.Move(temp,path,true);
        }
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    public static void Write(Stream stream,CuttingSheet sheet,string order,string customer,string job)
    {
        if(sheet.Rows.Count()>LabelCsv.MaxLabels || sheet.Groups.Count>10000)throw new FormatException("The workbook is limited to 10,000 cutting rows/specifications.");
        sheet.Refresh();
        var data=new XElement(S+"sheetData");var hidden=new XElement(S+"sheetData");var merges=new List<string>();var validations=new List<XElement>();var line=0;
        XElement Row(double height=25)
        {var row=new XElement(S+"row",new XAttribute("r",++line),new XAttribute("ht",height),new XAttribute("customHeight",1));data.Add(row);return row;}
        void Merge(string range)=>merges.Add(range);
        var title=Row(34);title.Add(Cell(1,line,"LABLE STUDIO  |  CUTTING LIST",1),Cell(12,line,FormatMarker));Merge("A1:K1");
        var info=Row(28);info.Add(Cell(1,line,"ORDER NO",2),Cell(3,line,order,3),Cell(6,line,"CUSTOMER",2),Cell(8,line,customer,3));Merge("A2:B2");Merge("C2:E2");Merge("F2:G2");Merge("H2:K2");
        info=Row(28);info.Add(Cell(1,line,"JOB NO",2),Cell(3,line,job,3),Cell(6,line,"Dimensions: mm    •    Price: AED / m²",8));Merge("A3:B3");Merge("C3:E3");Merge("F3:K3");
        info=Row(24);info.Add(Cell(1,line,"Edit the white input cells. Shaded columns calculate automatically. Blank Price inherits the specification rate.",8));Merge("A4:K4");
        hidden.Add(new XElement(S+"row",new XAttribute("r",1),Cell(1,1,FormatMarker)));
        var metadataRow=1;
        foreach(var group in sheet.Groups)
        {
            Row(10);var id=Guid.NewGuid().ToString("N");
            var heading=Row(Math.Clamp(Math.Ceiling(group.Specification.Length/70d)*15+10,34,100));var headingLine=line;
            heading.Add(Cell(1,line,"GLASS TYPE",2),Cell(3,line,group.GlassType,3),Cell(4,line,"SPECIFICATION",2),Cell(6,line,group.Specification,3),Cell(12,line,"group:"+id));
            Merge($"A{line}:B{line}");Merge($"D{line}:E{line}");Merge($"F{line}:K{line}");
            validations.Add(new XElement(S+"dataValidation",new XAttribute("type","list"),new XAttribute("allowBlank",1),new XAttribute("showErrorMessage",1),new XAttribute("sqref",$"C{line}"),new XElement(S+"formula1","\"SGU,DGU\"")));
            var rate=Row(26);var rateLine=line;
            rate.Add(Cell(1,line,"BASE AED / m²",2),Cell(3,line,group.BasePrice,7,true),Cell(4,line,"SQM rounded to 3 decimals before quantity; totals in AED",8),Cell(12,line,"rate:"+id));Merge($"A{line}:B{line}");Merge($"D{line}:K{line}");
            var headers=Row(28);for(var c=0;c<11;c++)headers.Add(Cell(c+1,line,CuttingTable.Columns[c],4));headers.Add(Cell(12,line,"header"));
            var start=line+1;
            foreach(var item in group.Rows)
            {
                var row=Row();var r=line;var rowId=Guid.NewGuid().ToString("N");
                row.Add(Cell(1,r,item.Serial.ToString(CultureInfo.InvariantCulture),5,true),Cell(2,r,item.GlassRef,3),
                    Cell(3,r,item.W1,13,true),Cell(4,r,item.H1,13,true),Cell(5,r,item.W2,13,true),Cell(6,r,item.H2,13,true),Cell(7,r,item.Qty,13,true));
                // Excel formulas validate visible numeric inputs. Label metadata is validated again by the app on import.
                var effective=$"IF(J{r}=\"\",$C${rateLine},J{r})";
                var inner=$"AND(OR(E{r}=\"\",AND(ISNUMBER(E{r}),E{r}>=0,E{r}<=100000)),OR(F{r}=\"\",AND(ISNUMBER(F{r}),F{r}>=0,F{r}<=100000)),OR(AND(N(E{r})=0,N(F{r})=0),AND(E{r}>0,F{r}>0)),OR($C${headingLine}<>\"SGU\",AND(N(E{r})=0,N(F{r})=0,{(item.InnerQty.Length==0 && item.InnerGlass.Length==0?"TRUE":"FALSE")})))";
                var valid=$"AND(OR(J{r}<>\"\",$C${rateLine}<>\"\"),ISNUMBER(C{r}),C{r}>0,C{r}<=100000,ISNUMBER(D{r}),D{r}>0,D{r}<=100000,ISNUMBER(G{r}),G{r}>=1,G{r}<=10000,G{r}=INT(G{r}),ISNUMBER({effective}),{effective}>=0,{effective}<=1000000000,LEN(B{r})<=60,B{r}=CLEAN(B{r}),{inner})";
                row.Add(Cell(8,r,item.IsReady?N(item.Sqm):"",6,formula:$"IFERROR(IF({valid},ROUND(C{r}*D{r}/1000000,3),\"\"),\"\")"));
                row.Add(Cell(9,r,item.IsReady?N(item.TotalSqm):"",6,formula:$"IF(ISNUMBER(H{r}),H{r}*G{r},\"\")"));
                row.Add(string.IsNullOrWhiteSpace(item.Price)?Cell(10,r,item.IsEmpty?"":group.BasePrice,7,formula:InheritFormula(rateLine,r)):Cell(10,r,item.Price,7,true));
                row.Add(Cell(11,r,item.IsReady?N(item.TotalPrice):"",12,formula:$"IF(ISNUMBER(I{r}),ROUND(I{r}*{effective},2),\"\")"),Cell(12,r,"row:"+rowId));
                var meta=new RowMetadata(item.OrderNo,item.CustomerName,item.JobNo,item.InnerQty,item.OuterGlass,item.InnerGlass,item.Source,item.HasOwnMetadata);
                ++metadataRow;hidden.Add(new XElement(S+"row",new XAttribute("r",metadataRow),Cell(1,metadataRow,rowId),Cell(2,metadataRow,JsonSerializer.Serialize(meta))));
            }
            var end=line;var totals=Row(28);
            totals.Add(Cell(1,line,"SPECIFICATION TOTAL",9),Cell(7,line,group.Quantity.ToString(CultureInfo.InvariantCulture),14,formula:end>=start?$"SUMPRODUCT(--ISNUMBER(H{start}:INDEX(H:H,ROW()-1)),IFERROR(G{start}:INDEX(G:G,ROW()-1)*1,0))":"0"),
                Cell(9,line,N(group.TotalSqm),10,formula:end>=start?$"SUM(I{start}:INDEX(I:I,ROW()-1))":"0"),Cell(10,line,"AED",9),Cell(11,line,N(group.TotalPrice),11,formula:end>=start?$"SUM(K{start}:INDEX(K:K,ROW()-1))":"0"),Cell(12,line,"total"));Merge($"A{line}:F{line}");
        }
        Row(10);var grand=Row(32);
        string Sum(string column)=>$"SUMIF($L$5:$L${line-1},\"total\",{column}5:{column}{line-1})";
        grand.Add(Cell(1,line,"GRAND TOTAL",9),Cell(7,line,sheet.Groups.Sum(g=>g.Quantity).ToString(CultureInfo.InvariantCulture),14,formula:Sum("G")),Cell(9,line,N(sheet.Groups.Sum(g=>g.TotalSqm)),10,formula:Sum("I")),Cell(10,line,"AED",9),Cell(11,line,N(sheet.Groups.Sum(g=>g.TotalPrice)),11,formula:Sum("K")),Cell(12,line,"grand"));Merge($"A{line}:F{line}");
        Row(24).Add(Cell(1,line,"Blank rows are unused. Incomplete rows are not priced; review the list in LABLE STUDIO before printing stickers.",8),Cell(12,line,"note"));Merge($"A{line}:K{line}");
        var physicalRows=data.Elements(S+"row").ToDictionary(r=>(int)r.Attribute("r")!);
        foreach(var range in merges)
        {
            var ends=range.Split(':');var number=int.Parse(ends[0][1..],CultureInfo.InvariantCulture);var target=physicalRows[number];
            var first=target.Elements(S+"c").Single(c=>(string?)c.Attribute("r")==ends[0]);var style=(int)first.Attribute("s")!;
            for(var col=ends[0][0]+1;col<=ends[1][0];col++)target.Add(Cell(col-'A'+1,number,"",style));
            var sorted=target.Elements(S+"c").OrderBy(c=>((string)c.Attribute("r")!)[0]).ToArray();target.RemoveNodes();target.Add(sorted);
        }
        var columns=new XElement(S+"cols");var widths=new[]{8d,17,11,11,11,11,8,12,14,13,16};
        for(var c=1;c<=11;c++)columns.Add(new XElement(S+"col",new XAttribute("min",c),new XAttribute("max",c),new XAttribute("width",widths[c-1]),new XAttribute("customWidth",1)));
        columns.Add(new XElement(S+"col",new XAttribute("min",12),new XAttribute("max",12),new XAttribute("width",2),new XAttribute("hidden",1)));
        var visible=new XElement(S+"worksheet",new XElement(S+"sheetPr",new XElement(S+"pageSetUpPr",new XAttribute("fitToPage",1))),
            new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XAttribute("showGridLines",0),new XElement(S+"pane",new XAttribute("ySplit",4),new XAttribute("topLeftCell","A5"),new XAttribute("activePane","bottomLeft"),new XAttribute("state","frozen")))),
            new XElement(S+"sheetFormatPr",new XAttribute("defaultRowHeight",25)),columns,data,
            new XElement(S+"mergeCells",new XAttribute("count",merges.Count),merges.Select(m=>new XElement(S+"mergeCell",new XAttribute("ref",m)))));
        if(validations.Count>0)visible.Add(new XElement(S+"dataValidations",new XAttribute("count",validations.Count),validations));
        visible.Add(new XElement(S+"printOptions",new XAttribute("horizontalCentered",1)),new XElement(S+"pageMargins",new XAttribute("left",.25),new XAttribute("right",.25),new XAttribute("top",.35),new XAttribute("bottom",.35),new XAttribute("header",.15),new XAttribute("footer",.15)),
            new XElement(S+"pageSetup",new XAttribute("paperSize",9),new XAttribute("orientation","landscape"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)),new XElement(S+"headerFooter",new XElement(S+"oddFooter","&LLABLE STUDIO&CPage &P of &N")));
        using var zip=new ZipArchive(stream,ZipArchiveMode.Create,true);
        void Part(string path,XElement root){using var output=zip.CreateEntry(path,CompressionLevel.Optimal).Open();new XDocument(new XDeclaration("1.0","utf-8","yes"),root).Save(output);}
        Part("[Content_Types].xml",new XElement(C+"Types",new XElement(C+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),new XElement(C+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),
            new[]{("/xl/workbook.xml","sheet.main"),("/xl/worksheets/sheet1.xml","worksheet"),("/xl/worksheets/sheet2.xml","worksheet"),("/xl/styles.xml","styles")}.Select(x=>new XElement(C+"Override",new XAttribute("PartName",x.Item1),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml."+x.Item2+"+xml")))));
        Part("_rels/.rels",new XElement(P+"Relationships",new XElement(P+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",R.NamespaceName+"/officeDocument"),new XAttribute("Target","xl/workbook.xml"))));
        Part("xl/workbook.xml",new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",R),new XElement(S+"bookViews",new XElement(S+"workbookView",new XAttribute("activeTab",0))),new XElement(S+"sheets",new XElement(S+"sheet",new XAttribute("name","Cutting list"),new XAttribute("sheetId",1),new XAttribute(R+"id","rId1")),new XElement(S+"sheet",new XAttribute("name",MetadataSheet),new XAttribute("sheetId",2),new XAttribute(R+"id","rId2"),new XAttribute("state","veryHidden"))),
            new XElement(S+"definedNames",new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Area"),new XAttribute("localSheetId",0),$"'Cutting list'!$A$1:$K${line}"),new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Titles"),new XAttribute("localSheetId",0),"'Cutting list'!$1:$4")),new XElement(S+"calcPr",new XAttribute("calcId",191029),new XAttribute("fullCalcOnLoad",1))));
        Part("xl/_rels/workbook.xml.rels",new XElement(P+"Relationships",new[]{("rId1","worksheet","worksheets/sheet1.xml"),("rId2","worksheet","worksheets/sheet2.xml"),("rId3","styles","styles.xml")}.Select(x=>new XElement(P+"Relationship",new XAttribute("Id",x.Item1),new XAttribute("Type",R.NamespaceName+"/"+x.Item2),new XAttribute("Target",x.Item3)))));
        Part("xl/worksheets/sheet1.xml",visible);Part("xl/worksheets/sheet2.xml",new XElement(S+"worksheet",hidden));Part("xl/styles.xml",Styles());
    }
    private static XElement Styles()
    {
        XElement Font(int size,string color,bool bold=false)=>new(S+"font",bold?new XElement(S+"b"):null,new XElement(S+"sz",new XAttribute("val",size)),new XElement(S+"color",new XAttribute("rgb",color)),new XElement(S+"name",new XAttribute("val","Calibri")));
        XElement Fill(string color)=>new(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","solid"),new XElement(S+"fgColor",new XAttribute("rgb",color)),new XElement(S+"bgColor",new XAttribute("indexed",64))));
        XElement Xf(int font,int fill,int border,int num=0,string align="left")=>new(S+"xf",new XAttribute("numFmtId",num),new XAttribute("fontId",font),new XAttribute("fillId",fill),new XAttribute("borderId",border),new XAttribute("xfId",0),new XAttribute("applyNumberFormat",1),new XAttribute("applyAlignment",1),new XElement(S+"alignment",new XAttribute("horizontal",align),new XAttribute("vertical","center"),new XAttribute("wrapText",1)));
        XElement Border(bool lines)=>new(S+"border",new[]{"left","right","top","bottom","diagonal"}.Select(n=>new XElement(S+n,lines && n!="diagonal"?new XAttribute("style","thin"):null,lines && n!="diagonal"?new XElement(S+"color",new XAttribute("rgb","FFDCE3E8")):null)));
        return new XElement(S+"styleSheet",new XElement(S+"numFmts",new XAttribute("count",2),new XElement(S+"numFmt",new XAttribute("numFmtId",164),new XAttribute("formatCode","0.000")),new XElement(S+"numFmt",new XAttribute("numFmtId",165),new XAttribute("formatCode","0.00########"))),
            new XElement(S+"fonts",new XAttribute("count",4),Font(11,"FF172B36"),Font(18,"FFFFFFFF",true),Font(10,"FF46616B",true),Font(11,"FF164F43",true)),
            new XElement(S+"fills",new XAttribute("count",5),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","none"))),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","gray125"))),Fill("FF18333E"),Fill("FFF1F5F7"),Fill("FFE4F2ED")),
            new XElement(S+"borders",new XAttribute("count",2),Border(false),Border(true)),new XElement(S+"cellStyleXfs",new XAttribute("count",1),Xf(0,0,0)),
            new XElement(S+"cellXfs",new XAttribute("count",15),Xf(0,0,0),Xf(1,2,0),Xf(2,3,1),Xf(0,0,1),Xf(2,3,1,0,"center"),Xf(0,3,1,0,"center"),Xf(0,3,1,164,"right"),Xf(0,0,1,165,"right"),Xf(2,0,0),Xf(3,4,1),Xf(3,4,1,164,"right"),Xf(3,4,1,4,"right"),Xf(0,3,1,4,"right"),Xf(0,0,1,0,"right"),Xf(3,4,1,0,"right")),
            new XElement(S+"cellStyles",new XAttribute("count",1),new XElement(S+"cellStyle",new XAttribute("name","Normal"),new XAttribute("xfId",0),new XAttribute("builtinId",0))));
    }
    public static CuttingWorkbookData Read(XlsxWorkbook workbook,string name)
    {
        if(!workbook.IsCuttingPage(name))throw new FormatException("This is not a LABLE STUDIO cutting workbook.");
        var metadata=workbook.ReadPage(MetadataSheet,false);
        if(metadata.FirstOrDefault()?.Cells[0]!=FormatMarker)throw new FormatException("Cutting workbook metadata is missing or unsupported.");
        var lookup=new Dictionary<string,RowMetadata>(StringComparer.Ordinal);
        foreach(var record in metadata.Skip(1))
        {
            if(record.Cells.All(string.IsNullOrEmpty))continue;
            try
            {
                var meta=JsonSerializer.Deserialize<RowMetadata>(record.Cells[1])??throw new FormatException("Empty row metadata.");
                if(new[]{meta.OrderNo,meta.CustomerName,meta.JobNo,meta.InnerQty,meta.OuterGlass,meta.InnerGlass,meta.Source}.Any(v=>v==null) || !lookup.TryAdd(record.Cells[0],meta))throw new FormatException("Invalid or duplicate row metadata.");
            }
            catch(JsonException ex){throw new FormatException("Cutting workbook row metadata is damaged.",ex);}
        }
        var page=workbook.ReadPage(name,true);var sheet=new CuttingSheet();CuttingGroup? group=null;var rateLine=0;var count=0;
        string order="",customer="",job="";
        sheet.Change(()=>
        {
            foreach(var row in page)
            {
                var v=row.Cells;var tag=v[11];
                if(row.Number==2){order=v[2];customer=v[7];continue;}
                if(row.Number==3){job=v[2];continue;}
                if(row.Number<=4)continue;
                if(tag.StartsWith("group:",StringComparison.Ordinal))
                {group=new CuttingGroup {GlassType=v[2].Trim().ToUpperInvariant(),Specification=v[5]};sheet.Groups.Add(group);rateLine=0;continue;}
                if(tag.StartsWith("rate:",StringComparison.Ordinal))
                {if(group==null)throw new FormatException("Rate row is outside a specification.");group.BasePrice=v[2];rateLine=row.Number;continue;}
                if(tag is "header" or "total" or "grand" or "note")continue;
                var stored=tag.StartsWith("row:",StringComparison.Ordinal);
                if(!stored && v.Take(11).All(string.IsNullOrWhiteSpace))continue;
                if(!stored && tag.Length>0)throw new FormatException($"Unsupported cutting marker at Excel row {row.Number}.");
                if(group==null || rateLine==0)throw new FormatException($"Excel row {row.Number} has no specification/rate header.");
                var item=new CuttingRow {GlassRef=v[1],W1=v[2],H1=v[3],W2=v[4],H2=v[5],Qty=v[6],Price=IsInheritedFormula(row.Formulas.GetValueOrDefault(9),rateLine,row.Number)?"":v[9]};
                if(stored)
                {
                    if(!lookup.TryGetValue(tag[4..],out var meta))throw new FormatException($"Excel row {row.Number} has lost its stored metadata. Keep the hidden workbook data intact.");
                    item.OrderNo=meta.OrderNo;item.CustomerName=meta.CustomerName;item.JobNo=meta.JobNo;item.InnerQty=meta.InnerQty;item.OuterGlass=meta.OuterGlass;item.InnerGlass=meta.InnerGlass;item.Source=meta.Source;item.HasOwnMetadata=meta.HasOwnMetadata;
                }
                group.Rows.Add(item);if(++count>LabelCsv.MaxLabels)throw new FormatException("Cutting workbook exceeds 10,000 rows.");
            }
        });
        if(sheet.Groups.Count>10000)throw new FormatException("Too many specification groups.");
        return new CuttingWorkbookData(sheet,order,customer,job);
    }
}
