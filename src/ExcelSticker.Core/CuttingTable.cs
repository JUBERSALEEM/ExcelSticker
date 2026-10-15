using System.Globalization;
using System.Text;

namespace ExcelSticker.Core;

/// <summary>Atomic rectangular clipboard paste and price-preserving cutting-sheet CSV interchange.</summary>
public static class CuttingTable
{
    public static readonly string[] Columns={"Sr No","Glass Ref","W1","H1","W2","H2","Qty","SQM","Total SQM","Price","Total Price"};
    private static string Key(string text)=>string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant() switch
    {
        "sr" or "srno" or "serial" or "serialno"=>"sr",
        "glassref" or "glassreference" or "referenceno" or "ref" or "reference"=>"ref",
        "width" or "width1" or "w1mm"=>"w1", "height" or "height1" or "h1mm"=>"h1",
        "width2" or "w2mm"=>"w2", "height2" or "h2mm"=>"h2",
        "quantity" or "outerqty"=>"qty", "rate" or "unitprice"=>"price",
        "spec"=>"specification", "order"=>"orderno", "customer"=>"customername", "job"=>"jobno",
        "value" or "amount"=>"totalprice", var key=>key
    };
    private static readonly string[] Keys=Columns.Select(Key).ToArray();
    private static void Assign(CuttingRow row,string key,string value)
    {
        value=value.Trim();
        if(value.Any(char.IsControl))throw new FormatException("Pasted cells cannot contain line breaks or control characters.");
        switch(key)
        {
            case "ref": if(value.Length>60)throw new FormatException("Glass ref exceeds 60 characters.");row.GlassRef=value;break;
            case "w1": case "h1": case "w2": case "h2":
                _=CuttingRow.Number(value,key,allowBlank:true);
                if(key=="w1")row.W1=value;else if(key=="h1")row.H1=value;else if(key=="w2")row.W2=value;else row.H2=value;break;
            case "qty": case "innerqty":
                if(value.Length>0 && (!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var qty)||qty<0||qty>10000))throw new FormatException("Qty must be an integer from 0 to 10000; zero remains an incomplete row.");
                if(key=="qty")row.Qty=value;else row.InnerQty=value;break;
            case "price": _=CuttingRow.Number(value,"Price",1000000000,true);row.Price=value;break;
            case "orderno": row.OrderNo=value;break;case "customername":row.CustomerName=value;break;case "jobno":row.JobNo=value;break;
            case "outerglass":row.OuterGlass=value;break;case "innerglass":row.InnerGlass=value;break;
            case "sr": case "sqm": case "totalsqm": case "totalprice": break; // Recalculated, never trusted.
            default: throw new FormatException($"Unsupported column: {key}.");
        }
    }
    public static int Paste(CuttingSheet sheet,CuttingGroup group,string text,int? startRow=null,int startColumn=1)
    {
        if(text.Length>2*1024*1024 || Encoding.UTF8.GetByteCount(text)>2*1024*1024)throw new FormatException("Paste is limited to 2 MB.");
        if(!sheet.Groups.Contains(group))throw new FormatException("Choose a specification group first.");
        var rows=LabelCsv.Parse(text.TrimStart('\uFEFF'),'\t').Where(r=>!r.All(string.IsNullOrWhiteSpace)).ToArray();
        if(rows.Length==0)return 0;
        if(rows.All(r=>r.Length==12 && string.IsNullOrWhiteSpace(r[11])))rows=rows.Select(r=>r.Take(11).ToArray()).ToArray();
        var header=rows[0].Select(Key).ToArray();
        var hasHeader=(header.Contains("w1") && header.Contains("h1")) ||
            (rows.Length>1 && startRow.HasValue && startColumn>=0 && header.SequenceEqual(Keys.Skip(startColumn).Take(header.Length)));
        string[] mapping;
        if(hasHeader){mapping=header;rows=rows.Skip(1).ToArray();if(mapping.Distinct().Count()!=mapping.Length)throw new FormatException("Duplicate paste headings.");}
        else if(startRow==null && rows[0].Length==7)mapping=new[]{"ref","w1","h1","w2","h2","qty","price"};
        else if(startRow==null && rows[0].Length==11)mapping=Keys;
        else if(startRow.HasValue && startColumn>=0 && startColumn+rows[0].Length<=Keys.Length)mapping=Keys.Skip(startColumn).Take(rows[0].Length).ToArray();
        else throw new FormatException("Paste rows with headings, seven input columns (Glass Ref, W1, H1, W2, H2, Qty, Price), or all eleven displayed columns. For a partial rectangle select its first cell and press Ctrl+V.");
        if(mapping.All(k=>k is "sr" or "sqm" or "totalsqm" or "totalprice"))throw new FormatException("Select editable input columns, not only calculated values.");
        var first=startRow??group.AppendIndex;
        if(first<0||first>group.Rows.Count)throw new FormatException("Paste start row is outside this group.");
        sheet.CheckCapacity(Math.Max(0,first+rows.Length-group.Rows.Count));
        var staged=new List<CuttingRow>();
        for(var r=0;r<rows.Length;r++)
        {
            if(rows[r].Length!=mapping.Length)throw new FormatException($"Paste row {r+1} has {rows[r].Length} cells; expected {mapping.Length}. Nothing pasted.");
            var copy=first+r<group.Rows.Count?group.Rows[first+r].Copy():new CuttingRow {Group=group,W2="",H2="",Qty=""};
            try { for(var c=0;c<mapping.Length;c++)Assign(copy,mapping[c],rows[r][c]); }
            catch(FormatException ex){throw new FormatException($"Paste row {r+1}: {ex.Message} Nothing pasted.",ex);}
            staged.Add(copy);
        }
        sheet.Change(()=>{for(var r=0;r<staged.Count;r++){if(first+r<group.Rows.Count)group.Rows[first+r].CopyValues(staged[r]);else group.Rows.Add(staged[r]);}});
        return staged.Count;
    }
    public const string Header="GlassType,Specification,Glass Ref,W1,H1,W2,H2,Qty,SQM,Total SQM,Price,Total Price,OrderNo,CustomerName,JobNo,InnerQty,OuterGlass,InnerGlass";
    public static int FindHeader(IReadOnlyList<string[]> rows)
    {
        for(var i=0;i<Math.Min(50,rows.Count);i++) {var keys=rows[i].Select(Key).ToArray();
            if(keys.Contains("w1")&&keys.Contains("h1")&&keys.Contains("specification")&&(keys.Contains("price")||keys.Contains("glasstype")))return i;}
        return -1;
    }
    private static bool NeedsExcelEscape(string value)=>value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' or '\'';
    private static string ReadText(string value)
    {
        value=value.Trim();
        // Recognize Excel's literal-text prefix; exports double an original leading apostrophe.
        return value.StartsWith("'",StringComparison.Ordinal) && NeedsExcelEscape(value[1..]) ? value[1..] : value;
    }
    public static IReadOnlyList<CuttingGroup> Read(IReadOnlyList<string[]> rows,int header)
    {
        if(header<0||header>=rows.Count)throw new FormatException("Cutting file header not found.");
        var keys=rows[header].Select(Key).ToArray();
        if(keys.Distinct().Count()!=keys.Length)throw new FormatException("Cutting file headings must be unique.");
        foreach(var key in new[]{"specification","w1","h1","qty"})if(!keys.Contains(key))throw new FormatException($"Missing cutting column: {key}.");
        var groups=new List<CuttingGroup>();var count=0;
        for(var i=header+1;i<rows.Count;i++)
        {
            var cells=rows[i].Select(ReadText).ToArray();if(cells.All(string.IsNullOrWhiteSpace))continue;
            if(cells.Length>keys.Length)throw new FormatException($"Record {i+1}: too many cells.");
            string Get(string key){var at=Array.IndexOf(keys,key);return at>=0&&at<cells.Length?cells[at].Trim():"";}
            var spec=Get("specification");var type=Get("glasstype");if(type.Length==0)type=spec.Contains('+')?"DGU":"SGU";type=type.ToUpperInvariant();
            if(type is not ("SGU" or "DGU") || spec.Length==0 || spec.Length>240 || spec.Any(char.IsControl))throw new FormatException($"Record {i+1}: choose SGU/DGU and a specification up to 240 characters.");
            var group=groups.LastOrDefault();
            if(group==null || group.GlassType!=type || group.Specification!=spec){group=new CuttingGroup {GlassType=type,Specification=spec};groups.Add(group);}
            var row=new CuttingRow {Group=group,Source="Cutting file"};
            try{for(var c=0;c<keys.Length;c++){if(keys[c] is "glasstype" or "specification")continue;Assign(row,keys[c],c<cells.Length?cells[c]:"");}}
            catch(FormatException ex){throw new FormatException($"Record {i+1}: {ex.Message}",ex);}
            row.HasOwnMetadata=row.OrderNo.Length>0 && row.CustomerName.Length>0;
            group.Rows.Add(row);if(++count>LabelCsv.MaxLabels)throw new FormatException("Cutting file exceeds 10,000 rows.");
        }
        if(count==0)throw new FormatException("No cutting rows in this file.");
        return groups;
    }
    public static string Export(CuttingSheet sheet,string order,string customer,string job)
    {
        static string Q(string s) { if(NeedsExcelEscape(s))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\""; }
        var result=new StringBuilder(Header+"\r\n");
        foreach(var g in sheet.Groups)foreach(var r in g.Rows.Where(r=>!r.IsEmpty))
        {
            // Preserve drafts and rate overrides; never export stale calculated cells.
            var price=string.IsNullOrWhiteSpace(r.Price)?g.BasePrice:r.Price;
            var values=new[]{g.GlassType,g.Specification,r.GlassRef,r.W1,r.H1,r.W2,r.H2,r.Qty,r.SqmDisplay,r.TotalSqmDisplay,price,r.TotalPriceDisplay,
                r.HasOwnMetadata||r.OrderNo.Length>0?r.OrderNo:order,r.HasOwnMetadata||r.CustomerName.Length>0?r.CustomerName:customer,r.HasOwnMetadata||r.JobNo.Length>0?r.JobNo:job,r.InnerQty,r.OuterGlass,r.InnerGlass};
            result.AppendLine(string.Join(",",values.Select(Q)));
        }
        return result.ToString();
    }
}
