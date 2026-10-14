using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExcelSticker.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StickerFormat { NormalJobOrder, SGU, DGU }

public sealed class StickerElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Field { get; set; } = "CustomText";
    public string Prefix { get; set; } = "";
    public string Text { get; set; } = "Your text";
    public double X { get; set; } = 4;
    public double Y { get; set; } = 4;
    public double Width { get; set; } = 45;
    public double Height { get; set; } = 6;
    public double FontSize { get; set; } = 10; // Points, independent of the UI theme.
    public string FontFamily { get; set; } = "Arial";
    public string Alignment { get; set; } = "Left";
    public bool Bold { get; set; } = true;
    public bool Underline { get; set; }
    public bool Wrap { get; set; }
    public bool AutoFit { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public double Stroke { get; set; } = .25;
    [JsonIgnore] public string DisplayName => $"{(Visible?"":"[hidden] ")}{Field}{(Locked?" · locked":"")}";
    public StickerElement Clone() => JsonSerializer.Deserialize<StickerElement>(JsonSerializer.Serialize(this))!;
}

public sealed class StickerLayout
{
    public int Version { get; set; } = 1;
    public StickerFormat Format { get; set; }
    public List<StickerElement> Elements { get; set; } = new();
    public static readonly string[] Fields = { "CompanyName","Address","Phone","Website","Email","OrderNo","Quantity","TotalQty","CopyNumber","GlassType","Specification","Size","Width","Height","ReferenceNo","CustomerName","JobNo","Pane","Format","CustomText","Line","Box" };
    public StickerLayout Clone() => JsonSerializer.Deserialize<StickerLayout>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if(Version!=1 || !Enum.IsDefined(Format)) throw new FormatException("Unsupported sticker format/version.");
        if(Elements==null || Elements.Count==0 || Elements.Count>60) throw new FormatException("A layout needs 1–60 elements.");
        if(Elements.Any(e=>e==null) || Elements.Select(e=>e.Id).Distinct().Count()!=Elements.Count) throw new FormatException("Element IDs must be unique.");
        foreach(var e in Elements)
        {
            if(string.IsNullOrWhiteSpace(e.Id) || e.Id.Length>80 || !Fields.Contains(e.Field)) throw new FormatException("Invalid element ID or data field.");
            if(e.Prefix==null || e.Text==null || e.Prefix.Length>80 || e.Text.Length>240) throw new FormatException("Prefix/text is too long.");
            if(!new[]{"Arial","Segoe UI","Calibri","Verdana","Tahoma"}.Contains(e.FontFamily)) throw new FormatException("Unsupported sticker font.");
            if(!new[]{"Left","Center","Right"}.Contains(e.Alignment)) throw new FormatException("Invalid text alignment.");
            if(new[]{e.X,e.Y,e.Width,e.Height,e.FontSize,e.Stroke}.Any(n=>!double.IsFinite(n))) throw new FormatException("Layout coordinates must be finite numbers.");
            if(e.X<0 || e.Y<0 || e.Width<1 || e.Height<.2 || e.X+e.Width>100.001 || e.Y+e.Height>70.001) throw new FormatException($"{e.Field} extends outside the 100 × 70 mm sticker.");
            if(e.FontSize<6 || e.FontSize>36 || e.Stroke<.1 || e.Stroke>2) throw new FormatException("Font size must be 6–36 pt; line thickness 0.1–2 mm.");
        }
    }
    public static StickerLayout Load(string path)
    {
        if (new FileInfo(path).Length > 128 * 1024) throw new FormatException("A single layout must be at most 128 KB.");
        var layout = JsonSerializer.Deserialize<StickerLayout>(File.ReadAllText(path)) ?? throw new FormatException("Empty sticker format.");
        layout.Validate(); return layout;
    }
    public void Save(string path) { Validate(); LayoutLibrary.WriteJson(this, path); }
    public static StickerLayout Default(StickerFormat format)
    {
        var layout=new StickerLayout {Format=format};
        void Text(string field,double x,double y,double w,double h,double pt,string prefix="",bool bold=true,string align="Left")
            =>layout.Elements.Add(new StickerElement {Field=field,X=x,Y=y,Width=w,Height=h,FontSize=pt,Prefix=prefix,Bold=bold,Alignment=align});
        Text("CompanyName",4,3,92,7,16);
        Text("Address",4,11,92,3.5,7,bold:false);
        Text("Phone",4,15,36,3.5,7,bold:false); Text("Website",42,15,54,3.5,7,bold:false);
        Text("Email",4,19,92,3.5,7,bold:false);
        Text("Line",4,24,92,.3,8);
        Text("Format",4,26,58,5,9);
        Text("Quantity",65,26,31,6,12,"QTY: ",align:"Right");
        Text("OrderNo",4,33,92,6,12,"Order: ");
        Text("GlassType",4,40,92,6,11);
        Text("Size",4,47,92,6,11,"SIZE: ");
        Text("ReferenceNo",4,54,58,5,10,"REF: ");
        Text("Pane",64,54,32,5,10,align:"Right");
        Text("CustomerName",4,61,92,6,12,align:"Center");
        layout.Elements.Last().Underline=true;
        if(format==StickerFormat.NormalJobOrder)
        {
            layout.Elements.First(e=>e.Field=="Format").Field="JobNo";layout.Elements.First(e=>e.Field=="JobNo").Prefix="JOB: ";
        }
        if(format==StickerFormat.SGU)
        {
            layout.Elements.First(e=>e.Field=="Pane").Field="JobNo";layout.Elements.First(e=>e.Field=="JobNo").Prefix="JOB: ";
        }
        layout.Validate();return layout;
    }
}
