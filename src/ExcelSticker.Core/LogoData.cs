using System.Buffers.Binary;

namespace ExcelSticker.Core;

public static class LogoData
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string,object> Checked=new();
    public static void Check(string data)
    {
        if(data==null)throw new FormatException("Logo data cannot be null.");
        if(data.Length==0)return;
        _=Checked.GetValue(data,key=>{Validate(key);return new object();});
    }
    public const int MaxBytes=512*1024;
    public static byte[] Validate(string data)
    {
        if(data==null || data.Length>MaxBytes*4/3+4)throw new FormatException("Embedded logo is too large (512 KB maximum).");
        if(data.Length==0)return Array.Empty<byte>();
        byte[] bytes;
        try{bytes=Convert.FromBase64String(data);}catch(FormatException){throw new FormatException("Logo is not valid base64 PNG data.");}
        byte[] signature={137,80,78,71,13,10,26,10};
        if(bytes.Length<33 || bytes.Length>MaxBytes || !bytes.AsSpan(0,8).SequenceEqual(signature) || !bytes.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new FormatException("Logo must be an embedded PNG image.");
        var width=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4));var height=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if(width<1 || height<1 || width>1200 || height>1200)throw new FormatException("Embedded logo dimensions must be 1–1200 pixels. Import it through Sticker Studio to normalize it.");
        return bytes;
    }
}
