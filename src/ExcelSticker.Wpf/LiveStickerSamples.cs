using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>Standalone sample jobs. Never inserted into the cutting sheet or production label selection.</summary>
public static class LiveStickerSamples
{
    public const string SguType="6mm HD Grey Glass";
    public const string DguType="6mm HD Grey Glass + 12mm ASP + 6mm Clear Glass";
    public static GlassEntry Sgu()=>GlassEntry.Create("DEMO-SGU",SguType,"SAMPLE-01","SAMPLE CUSTOMER","DEMO-001","300","300","4",false);
    public static GlassEntry Dgu()
    {
        var entry=GlassEntry.Create("DEMO-DGU",DguType,"SAMPLE-02","SAMPLE CUSTOMER","DEMO-002","300","300","4",true,"300","300","2");
        // Show the requested complete assembly on the sample's TYPE line. Production pane materials are unchanged.
        return entry with {Outer=entry.Outer with {GlassType=DguType},Inner=entry.Inner! with {GlassType=DguType}};
    }
}
