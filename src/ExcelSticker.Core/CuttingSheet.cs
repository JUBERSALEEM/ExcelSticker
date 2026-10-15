using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ExcelSticker.Core;

public abstract class CuttingObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name=null) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    protected bool Set(ref string field,string value,[CallerMemberName] string? name=null)
    { if(field==value)return false;field=value;Notify(name);return true; }
}

/// <summary>Editable source values stay strings: incomplete work is retained, never silently coerced to valid labels.</summary>
public sealed class CuttingRow : CuttingObservable
{
    private string _glassRef="",_w1="",_h1="",_w2="0",_h2="0",_qty="1",_price="";
    public string GlassRef { get=>_glassRef;set=>Set(ref _glassRef,value); }
    public string W1 { get=>_w1;set=>Set(ref _w1,value); }
    public string H1 { get=>_h1;set=>Set(ref _h1,value); }
    public string W2 { get=>_w2;set=>Set(ref _w2,value); }
    public string H2 { get=>_h2;set=>Set(ref _h2,value); }
    public string Qty { get=>_qty;set=>Set(ref _qty,value); }
    public string Price { get=>_price;set=>Set(ref _price,value); }
    public bool HasOwnMetadata { get; set; }
    public string OrderNo { get; set; }="";
    public string CustomerName { get; set; }="";
    public string JobNo { get; set; }="";
    public string InnerQty { get; set; }="";
    public string OuterGlass { get; set; }="";
    public string InnerGlass { get; set; }="";
    public string Source { get; set; }="Cutting sheet";
    public int Serial { get; internal set; }
    public CuttingGroup? Group { get; internal set; }
    private static string N(decimal value)=>value.ToString(CultureInfo.InvariantCulture);
    public static decimal Number(string value,string name,decimal maximum=100000,bool allowBlank=false)
    {
        if(allowBlank && string.IsNullOrWhiteSpace(value))return 0;
        if(!decimal.TryParse(value.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n) || n<0 || n>maximum)
            throw new FormatException($"{name}: use a number from 0 to {maximum} with a decimal point, no thousands separator.");
        return n;
    }
    public decimal Rate => Number(string.IsNullOrWhiteSpace(Price) ? Group?.BasePrice ?? "0" : Price,"Price",1000000000);
    public string Error
    {
        get
        {
            try
            {
                if(Number(W1,"W1")==0 || Number(H1,"H1")==0)throw new FormatException("Enter W1 and H1 greater than zero.");
                if(!int.TryParse(Qty,NumberStyles.None,CultureInfo.InvariantCulture,out var q)||q<1||q>10000)throw new FormatException("Qty must be a whole number from 1 to 10000.");
                var w=Number(W2,"W2",allowBlank:true);var h=Number(H2,"H2",allowBlank:true);
                if((w==0)!=(h==0))throw new FormatException("Enter both W2 and H2, or use 0/blank for both.");
                if(Group?.GlassType=="SGU" && (w!=0||h!=0||InnerQty.Length>0||InnerGlass.Length>0))throw new FormatException("SGU cannot have inner dimensions or overrides. Choose DGU or clear the inner values in entry details.");
                _=Rate;
                if(GlassRef.Length>60||GlassRef.Any(char.IsControl))throw new FormatException("Glass ref: maximum 60 characters, no line breaks.");
                return "";
            }
            catch(FormatException ex){return ex.Message;}
        }
    }
    public decimal Sqm => Error.Length==0 ? decimal.Round(Number(W1,"W1")*Number(H1,"H1")/1000000,3,MidpointRounding.AwayFromZero) : 0;
    public decimal TotalSqm => Error.Length==0 ? Sqm*int.Parse(Qty,CultureInfo.InvariantCulture) : 0;
    public decimal TotalPrice => Error.Length==0 ? decimal.Round(TotalSqm*Rate,2,MidpointRounding.AwayFromZero) : 0;
    public string SqmDisplay => Error.Length==0 ? Sqm.ToString("0.000",CultureInfo.InvariantCulture) : "—";
    public string TotalSqmDisplay => Error.Length==0 ? TotalSqm.ToString("0.000",CultureInfo.InvariantCulture) : "—";
    public string TotalPriceDisplay => Error.Length==0 ? TotalPrice.ToString("N2",CultureInfo.InvariantCulture) : "—";
    public string Details => $"{Source} · Order: {OrderNo} · Customer: {CustomerName}" + (InnerQty.Length>0?$" · Inner Qty override: {InnerQty} (edit in entry details)":"") + (Error.Length>0?" · "+Error:"");
    public string PriceDisplay => string.IsNullOrWhiteSpace(Price)?Group?.BasePrice??"0":Price;
    public string RateHint => $"Blank price uses group base: {Group?.BasePrice ?? "0"} AED/m²";
    internal void Refresh() { foreach(var p in new[]{nameof(Serial),nameof(Error),nameof(SqmDisplay),nameof(TotalSqmDisplay),nameof(TotalPriceDisplay),nameof(RateHint),nameof(PriceDisplay),nameof(Details)})Notify(p); }
    public GlassEntry ToEntry(string order,string customer,string job)
    {
        if(Error.Length>0)throw new FormatException(Error);
        if(Group==null)throw new FormatException("Row has no specification group.");
        if(Group.GlassType is not ("SGU" or "DGU"))throw new FormatException("Choose SGU or DGU.");
        var zero=Number(W2,"W2",allowBlank:true)==0 && Number(H2,"H2",allowBlank:true)==0;
        return GlassEntry.Create(HasOwnMetadata||OrderNo.Length>0?OrderNo:order,Group.Specification,GlassRef,HasOwnMetadata||CustomerName.Length>0?CustomerName:customer,
            HasOwnMetadata||JobNo.Length>0?JobNo:job,W1,H1,Qty,Group.GlassType=="DGU",zero?"":W2,zero?"":H2,InnerQty,OuterGlass,InnerGlass);
    }
    public CuttingRow Copy() => new() { GlassRef=GlassRef,W1=W1,H1=H1,W2=W2,H2=H2,Qty=Qty,Price=Price,HasOwnMetadata=HasOwnMetadata,OrderNo=OrderNo,
        CustomerName=CustomerName,JobNo=JobNo,InnerQty=InnerQty,OuterGlass=OuterGlass,InnerGlass=InnerGlass,Source=Source,Group=Group };
    public void CopyValues(CuttingRow other)
    {
        _glassRef=other.GlassRef;_w1=other.W1;_h1=other.H1;_w2=other.W2;_h2=other.H2;_qty=other.Qty;_price=other.Price;
        HasOwnMetadata=other.HasOwnMetadata;OrderNo=other.OrderNo;CustomerName=other.CustomerName;JobNo=other.JobNo;InnerQty=other.InnerQty;OuterGlass=other.OuterGlass;InnerGlass=other.InnerGlass;Source=other.Source;
        Notify("");
    }
    public static CuttingRow FromEntry(GlassEntry entry,string source)
    {
        var o=entry.Outer;var i=entry.Inner;var parts=entry.Specification.Split('+',StringSplitOptions.TrimEntries);
        return new CuttingRow { HasOwnMetadata=true,GlassRef=o.ReferenceNo,W1=N(o.Width),H1=N(o.Height),W2=i==null?"0":N(i.Width),H2=i==null?"0":N(i.Height),Qty=N(o.Qty),
            InnerQty=i!=null?N(i.Qty):"",OrderNo=o.OrderNo,CustomerName=o.CustomerName,JobNo=o.JobNo,Source=source,
            OuterGlass=o.GlassType==(entry.IsDouble && parts.Length==3?parts[0]:entry.Specification)?"":o.GlassType,
            InnerGlass=i==null || (parts.Length==3 && i.GlassType==parts[2])?"":i.GlassType };
    }
}

public sealed class CuttingGroup : CuttingObservable
{
    private string _glassType="SGU",_specification="",_basePrice="0";
    public string GlassType { get=>_glassType;set { if(Set(ref _glassType,value))Changed?.Invoke(this,EventArgs.Empty); } }
    public string Specification { get=>_specification;set { if(Set(ref _specification,value))Changed?.Invoke(this,EventArgs.Empty); } }
    public string BasePrice { get=>_basePrice;set { if(Set(ref _basePrice,value))Changed?.Invoke(this,EventArgs.Empty); } }
    public ObservableCollection<CuttingRow> Rows { get; }=new();
    public event EventHandler? Changed;
    private readonly HashSet<CuttingRow> _attached=new();
    public CuttingGroup() { Rows.CollectionChanged+=RowsChanged; }
    private void RowsChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        if(e.Action==NotifyCollectionChangedAction.Reset)
        {
            foreach(var old in _attached){old.PropertyChanged-=RowChanged;old.Group=null;}_attached.Clear();
        }
        if(e.OldItems!=null)foreach(CuttingRow old in e.OldItems){old.PropertyChanged-=RowChanged;old.Group=null;_attached.Remove(old);}
        var added=e.Action==NotifyCollectionChangedAction.Reset?Rows.AsEnumerable():e.NewItems?.Cast<CuttingRow>()??Enumerable.Empty<CuttingRow>();
        foreach(var row in added){row.Group=this;row.PropertyChanged+=RowChanged;_attached.Add(row);}
        Changed?.Invoke(this,EventArgs.Empty);
    }
    private void RowChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is "GlassRef" or "W1" or "H1" or "W2" or "H2" or "Qty" or "Price" or "")Changed?.Invoke(this,EventArgs.Empty);
    }
    public int Quantity => Rows.Where(r=>r.Error.Length==0).Sum(r=>int.Parse(r.Qty,CultureInfo.InvariantCulture));
    public decimal TotalSqm => Rows.Sum(r=>r.TotalSqm);
    public decimal TotalPrice => Rows.Sum(r=>r.TotalPrice);
    public string Summary => $"{Rows.Count} rows · {Quantity:N0} pcs · {TotalSqm:0.000} m² · AED {TotalPrice:N2}" + (Rows.Any(r=>r.Error.Length>0)?" · INCOMPLETE — invalid rows excluded from totals":"");
    public string ValidationSummary => string.Join("\n",Rows.Where(r=>r.Error.Length>0).Take(5).Select(r=>$"Row {r.Serial}: {r.Error}")) + (Rows.Count(r=>r.Error.Length>0)>5?"\nMore incomplete rows — hover a row for details.":"");
    internal void Refresh(){ foreach(var row in Rows)row.Refresh();Notify(nameof(Summary));Notify(nameof(ValidationSummary)); }
}

public sealed class CuttingSheet : CuttingObservable
{
    public ObservableCollection<CuttingGroup> Groups { get; }=new();
    public event EventHandler? Changed;
    private readonly HashSet<CuttingGroup> _attached=new();
    private int _updating;
    public IEnumerable<CuttingRow> Rows => Groups.SelectMany(g=>g.Rows);
    public CuttingSheet() { Groups.CollectionChanged+=(_,_)=>{
        foreach(var old in _attached.Where(g=>!Groups.Contains(g)).ToArray()){old.Changed-=GroupChanged;_attached.Remove(old);}
        foreach(var group in Groups.Where(g=>!_attached.Contains(g))){group.Changed+=GroupChanged;_attached.Add(group);}
        Refresh(); }; }
    private void GroupChanged(object? sender,EventArgs e)=>Refresh();
    public void Refresh()
    {
        if(_updating>0)return;
        var serial=0;foreach(var row in Rows)row.Serial=++serial;
        foreach(var group in Groups)group.Refresh();Notify(nameof(Summary));Changed?.Invoke(this,EventArgs.Empty);
    }
    public void Change(Action change) { _updating++;try { change(); } finally { _updating--;Refresh(); } }
    public string Summary => $"GRAND TOTAL · {Groups.Count} specifications · {Rows.Count()} rows · {Groups.Sum(g=>g.Quantity):N0} pcs · {Groups.Sum(g=>g.TotalSqm):0.000} m² · AED {Groups.Sum(g=>g.TotalPrice):N2}" + (Rows.Any(r=>r.Error.Length>0)?" · INCOMPLETE":"");
    public void CheckCapacity(int extra) { if(Rows.Count()+extra>LabelCsv.MaxLabels)throw new FormatException("The cutting sheet is limited to 10,000 rows. Nothing was added."); }
    public IReadOnlyList<CuttingRow> AddEntries(IReadOnlyList<GlassEntry> entries,string source)
    {
        CheckCapacity(entries.Count);
        var staged=entries.Select(e=>(Entry:e,Row:CuttingRow.FromEntry(e,source))).ToArray();
        foreach(var item in staged){item.Row.Group=new CuttingGroup { GlassType=item.Entry.IsDouble?"DGU":"SGU",Specification=item.Entry.Specification };_=item.Row.ToEntry("","","");}
        Change(()=>{foreach(var item in staged){var type=item.Entry.IsDouble?"DGU":"SGU";
            var group=Groups.FirstOrDefault(g=>g.GlassType==type && g.Specification==item.Entry.Specification);
            if(group==null)
            {
                if(Groups.Count==1 && Groups[0].Rows.Count==0 && string.IsNullOrWhiteSpace(Groups[0].Specification)){group=Groups[0];group.GlassType=type;group.Specification=item.Entry.Specification;}
                else{group=new CuttingGroup {GlassType=type,Specification=item.Entry.Specification};Groups.Add(group);}
            }group.Rows.Add(item.Row);}});
        return staged.Select(x=>x.Row).ToArray();
    }
}
