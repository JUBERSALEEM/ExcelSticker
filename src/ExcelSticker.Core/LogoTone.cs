namespace ExcelSticker.Core;

/// <summary>Deterministic grayscale adjustments. The embedded source is never rewritten.</summary>
public static class LogoTone
{
    public static void Validate(double darkness,double contrast,int threshold)
    {
        if(!double.IsFinite(darkness)||darkness<1||darkness>4 || !double.IsFinite(contrast)||contrast<.5||contrast>3 || threshold<1||threshold>254)
            throw new FormatException("Logo darkness must be 1–4, contrast 0.5–3, and black/white threshold 1–254.");
    }
    public static byte[] Lookup(double darkness=1,double contrast=1,bool blackWhite=false,int threshold=200)
    {
        Validate(darkness,contrast,threshold);
        var result=new byte[256];
        for(var i=0;i<256;i++)
        {
            var value=Math.Clamp((i-127.5)*contrast+127.5,0,255)/255;
            var gray=(byte)Math.Round(255*Math.Pow(value,darkness),MidpointRounding.AwayFromZero);
            result[i]=blackWhite?(byte)(gray<threshold?0:255):gray;
        }
        // Preserve the white backing/transparency and solid black, even at low contrast.
        result[0]=0;result[255]=255;return result;
    }
}
