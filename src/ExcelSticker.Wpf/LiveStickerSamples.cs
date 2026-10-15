using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>Demonstration data only. Never inserted into the cutting sheet or print plan.</summary>
public static class LiveStickerSamples
{
    public static GlassEntry Sgu()=>GlassEntry.Create("DEMO-SGU","6MM CLEAR TEMPERED","SAMPLE-01","SAMPLE CUSTOMER","DEMO-001","1200","1800","4",false);
    public static GlassEntry Dgu()=>GlassEntry.Create("DEMO-DGU","6MM GREY + 12MM AIR + 6MM CLEAR","SAMPLE-02","SAMPLE CUSTOMER","DEMO-002","1200","1800","4",true,"1190","1790","2");
}
