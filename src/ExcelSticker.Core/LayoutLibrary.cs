using System.Text.Json;

namespace ExcelSticker.Core;

public sealed class LayoutLibrary
{
    public int Version { get; set; } = 1;
    public List<StickerLayout> Layouts { get; set; } = Enum.GetValues<StickerFormat>().Select(StickerLayout.Default).ToList();
    public StickerLayout For(StickerFormat format) => Layouts.Single(l=>l.Format==format);
    public void Validate()
    {
        if(Version!=1 || Layouts==null || Layouts.Count!=3 || Layouts.Any(l=>l==null) || Layouts.Select(l=>l.Format).Distinct().Count()!=3) throw new FormatException("Layout library must contain Normal Job Order, SGU, and DGU once each.");
        foreach(var layout in Layouts)layout.Validate();
    }
    public LayoutLibrary Clone()=>JsonSerializer.Deserialize<LayoutLibrary>(JsonSerializer.Serialize(this))!;
    public static StickerFormat FormatFor(GlassLabel label)=>string.IsNullOrEmpty(label.Pane)?StickerFormat.SGU:StickerFormat.DGU;
    public static LayoutLibrary Load(string path)
    {
        if(!File.Exists(path))return new LayoutLibrary();
        if(new FileInfo(path).Length>512*1024)throw new FormatException("Layout library is too large.");
        var result=JsonSerializer.Deserialize<LayoutLibrary>(File.ReadAllText(path))??throw new FormatException("Empty layout library.");
        result.Validate();foreach(var layout in result.Layouts)layout.UpgradeLegacyNumbering();return result;
    }
    public void Save(string path)
    {
        Validate(); WriteJson(this, path);
    }
    internal static void WriteJson(object value, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(value,new JsonSerializerOptions {WriteIndented=true}));
            if(File.Exists(path))File.Copy(path,path+".bak",true);
            File.Move(temporary,path,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
