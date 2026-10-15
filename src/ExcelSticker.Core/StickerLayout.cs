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
    public string ImagePng { get; set; } = ""; // Embedded normalized PNG; no machine-local file paths.
    public double LogoDarkness {get;set;}=1;
    public double LogoContrast {get;set;}=1;
    public bool LogoBlackWhite {get;set;}
    public int LogoThreshold {get;set;}=200;
    public int Rotation { get; set; } // Clockwise degrees for logo: 0, 90, 180, 270.
    public bool KeepAspect { get; set; } = true;
    public double Stroke { get; set; } = .25;
    [JsonIgnore] public string DisplayName => $"{(Visible?"":"[hidden] ")}{Field}{(Locked?" · locked":"")}";
    public StickerElement Clone() => (StickerElement)MemberwiseClone();
}

public sealed class StickerLayout
{
    public int Version { get; set; } = 3;
    public double WidthMm { get; set; } = 100;
    public double HeightMm { get; set; } = 70;
    public StickerFormat Format { get; set; }
    public List<StickerElement> Elements { get; set; } = new();
    public static readonly string[] Fields = { "CompanyName","Address","Phone","Website","Email","OrderNo","GlassSrNo","Quantity","TotalQty","CopyNumber","GlassType","Specification","Size","Width","Height","ReferenceNo","CustomerName","JobNo","Pane","Format","CustomText","Line","Box","Logo" };
    public StickerLayout Clone() => new() {Version=Version,Format=Format,WidthMm=WidthMm,HeightMm=HeightMm,Elements=Elements.Select(e=>e.Clone()).ToList()};
    public void Validate()
    {
        if(Version is not (1 or 2 or 3) || !Enum.IsDefined(Format)) throw new FormatException("Unsupported sticker format/version.");
        if(!double.IsFinite(WidthMm) || !double.IsFinite(HeightMm) || WidthMm<20 || WidthMm>300 || HeightMm<20 || HeightMm>300)throw new FormatException("Sticker width and height must be 20–300 mm.");
        if(Elements==null || Elements.Count==0 || Elements.Count>60) throw new FormatException("A layout needs 1–60 elements.");
        if(Elements.Any(e=>e==null) || Elements.Select(e=>e.Id).Distinct().Count()!=Elements.Count) throw new FormatException("Element IDs must be unique.");
        if(Elements.Count(e=>e.Field=="Logo")>3 || Elements.Count(e=>!string.IsNullOrEmpty(e.ImagePng))>3)throw new FormatException("Use at most three logos per format.");
        foreach(var e in Elements)
        {
            LogoData.Check(e.ImagePng);
            LogoTone.Validate(e.LogoDarkness,e.LogoContrast,e.LogoThreshold);
            if(e.Rotation is not (0 or 90 or 180 or 270))throw new FormatException("Logo rotation must be 0, 90, 180 or 270 degrees.");
            if(string.IsNullOrWhiteSpace(e.Id) || e.Id.Length>80 || !Fields.Contains(e.Field)) throw new FormatException("Invalid element ID or data field.");
            if(e.Prefix==null || e.Text==null || e.Prefix.Length>80 || e.Text.Length>240) throw new FormatException("Prefix/text is too long.");
            if(!new[]{"Arial","Segoe UI","Calibri","Verdana","Tahoma"}.Contains(e.FontFamily)) throw new FormatException("Unsupported sticker font.");
            if(!new[]{"Left","Center","Right"}.Contains(e.Alignment)) throw new FormatException("Invalid text alignment.");
            if(new[]{e.X,e.Y,e.Width,e.Height,e.FontSize,e.Stroke}.Any(n=>!double.IsFinite(n))) throw new FormatException("Layout coordinates must be finite numbers.");
            if(e.X<0 || e.Y<0 || e.Width<1 || e.Height<.2 || e.X+e.Width>WidthMm+.001 || e.Y+e.Height>HeightMm+.001) throw new FormatException($"{e.Field} extends outside the {WidthMm:0.##} × {HeightMm:0.##} mm sticker.");
            if(e.FontSize<6 || e.FontSize>36 || e.Stroke<.1 || e.Stroke>2) throw new FormatException("Font size must be 6–36 pt; line thickness 0.1–2 mm.");
        }
    }
    /// <summary>Upgrade old layouts in memory, without overwriting the user's saved layout file.</summary>
    public void UpgradeLegacyNumbering()
    {
        Validate();
        // Older custom formats sometimes used TotalQty instead of the numbered Quantity field.
        foreach(var item in Elements.Where(e=>e.Field=="TotalQty"))item.Field="Quantity";
        if(Version!=1){Version=3;return;}
        if(!Elements.Any(e=>e.Field=="GlassSrNo") && Elements.Count<60)
        {
            var serial=new StickerElement {Field="GlassSrNo",X=65,Y=33,Width=31,Height=6,FontSize=10,Prefix="SR NO: ",Alignment="Right"};
            bool Free(StickerElement? except=null)=>serial.X+serial.Width<=WidthMm && serial.Y+serial.Height<=HeightMm && !Elements.Any(e=>e!=except && e.Visible &&
                serial.X<e.X+e.Width+.5 && serial.X+serial.Width+.5>e.X && serial.Y<e.Y+e.Height+.5 && serial.Y+serial.Height+.5>e.Y);
            var order=Elements.FirstOrDefault(e=>e.Field=="OrderNo" && e.Visible && !e.Locked && e.X==4 && e.Y==33 && e.Width==92 && e.Height==6);
            var placed=false;
            if(order!=null && Free(order)) { order.Width=58;placed=true; }
            else
            {
                // A custom layout is not reset or covered by a new field. Use genuinely vacant space.
                for(var y=3; y<=61 && !placed; y++)
                for(var x=65; x>=4 && !placed; x--)
                { serial.X=x;serial.Y=y;placed=Free(); }
            }
            if(placed)Elements.Add(serial);
        }
        // Dense/custom layouts without room get a renderer warning and can be adjusted in Sticker Studio.
        Version=3;Validate();
    }
    public static StickerLayout Load(string path)
    {
        if (new FileInfo(path).Length > 3 * 1024 * 1024) throw new FormatException("A single layout must be at most 3 MB.");
        var layout = JsonSerializer.Deserialize<StickerLayout>(File.ReadAllText(path)) ?? throw new FormatException("Empty sticker format.");
        layout.UpgradeLegacyNumbering(); return layout;
    }
    public void Save(string path) { Validate(); LayoutLibrary.WriteJson(this, path); }
    /// <summary>Explicit, non-mutating preset. Keep stock and every logo's data/geometry; never
    /// apply automatically to a saved design. Custom-positioned logos may still need adjustment.</summary>
    public StickerLayout SimplePreset()
    {
        Validate();
        var library=new LayoutLibrary();library.ResizeStock(WidthMm,HeightMm,true);
        var simple=library.For(Format);
        simple.Elements.AddRange(Elements.Where(e=>e.Field=="Logo").Select(e=>e.Clone()));
        var header=simple.Elements.Single(e=>e.Field=="CompanyName");
        var logos=simple.Elements.Where(e=>e.Field=="Logo" && e.Visible && e.Y<header.Y+header.Height && e.Y+e.Height>header.Y).ToArray();
        // Keep the largest free horizontal interval in the header; do not move or resize a logo.
        var intervals=new List<(double Left,double Right)> {(header.X,header.X+header.Width)};
        foreach(var logo in logos)
        {
            var next=new List<(double Left,double Right)>();
            foreach(var interval in intervals)
            {
                if(logo.X>=interval.Right || logo.X+logo.Width<=interval.Left){next.Add(interval);continue;}
                if(logo.X-1>interval.Left)next.Add((interval.Left,logo.X-1));
                if(logo.X+logo.Width+1<interval.Right)next.Add((logo.X+logo.Width+1,interval.Right));
            }
            intervals=next;
        }
        var space=intervals.OrderByDescending(i=>i.Right-i.Left).FirstOrDefault();
        if(space.Right-space.Left>=WidthMm*.25){header.X=space.Left;header.Width=space.Right-space.Left;}
        // Avoid an imported logo ID colliding with a newly generated text ID.
        foreach(var text in simple.Elements.Where(e=>e.Field!="Logo"))
            while(simple.Elements.Any(e=>!ReferenceEquals(e,text) && e.Id==text.Id))text.Id=Guid.NewGuid().ToString("N");
        simple.Validate();return simple;
    }
    /// <summary>Explicit contact-rich alternative, preserving stock and all logo settings.</summary>
    public StickerLayout CompanyPreset()
    {
        var result=SimplePreset();var sx=WidthMm/100;var sy=HeightMm/70;
        void Place(string field,double y,double height,double font)
        {
            var element=result.Elements.Single(e=>e.Field==field);
            element.Y=y*sy;element.Height=Math.Max(.2,height*sy);element.FontSize=font;
        }
        Place("Line",24,.3,8);
        Place(Format==StickerFormat.NormalJobOrder?"JobNo":"Format",26,5,9);
        Place("Quantity",26,6,12);Place("GlassType",33,10,11);Place("Size",44,7,16);
        Place("OrderNo",52,4,9);Place("GlassSrNo",52,4,9);Place("ReferenceNo",57,4,9);
        // SGU replaces the pane caption with its job number.
        Place(Format==StickerFormat.SGU?"JobNo":"Pane",57,4,9);Place("CustomerName",63,4,10);
        foreach(var (field,x,y,width) in new[]{("Address",4d,11d,92d),("Phone",4d,15d,36d),("Website",42d,15d,54d),("Email",4d,19d,92d)})
            result.Elements.Add(new StickerElement {Field=field,X=x*sx,Y=y*sy,Width=width*sx,Height=3.5*sy,FontSize=7.5,Bold=false});
        result.Validate();return result;
    }
    public static StickerLayout Default(StickerFormat format)
    {
        var layout=new StickerLayout {Format=format};
        void Text(string field,double x,double y,double w,double h,double pt,string prefix="",bool bold=true,string align="Left")
            =>layout.Elements.Add(new StickerElement {Field=field,X=x,Y=y,Width=w,Height=h,FontSize=pt,Prefix=prefix,Bold=bold,Alignment=align});
        Text("CompanyName",4,3,92,7,16);
        Text("Line",4,12,92,.3,8);
        Text("Format",4,15,40,5,10);
        Text("Quantity",48,14,48,7,14,"QTY: ",align:"Right");
        Text("GlassType",4,24,92,13,13);
        layout.Elements.Last().Wrap=true;
        Text("Size",4,39,92,8,18,"SIZE: ");
        Text("OrderNo",4,50,58,5,10,"Order: ");
        Text("GlassSrNo",65,50,31,5,10,"SR NO: ",align:"Right");
        Text("ReferenceNo",4,56,58,5,10,"REF: ");
        Text("Pane",64,56,32,5,10,align:"Right");
        Text("CustomerName",4,62,92,5,11);
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
