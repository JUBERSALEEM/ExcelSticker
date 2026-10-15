using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExcelSticker.Core;

/// <summary>ZT411 physical dot grids, not rounded nominal dpi / 25.4 conversions.</summary>
public sealed record ZebraZt411(int DotsPerMm)
{
    public static bool IsDriver(string driverName)=>Regex.IsMatch(driverName,@"\bZT\s*411\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public static ZebraZt411? Detect(string driverName,int? resolution,int? verticalResolution=null)
    {
        if(!IsDriver(driverName))return null; // Never infer RAW support from a user-editable queue name.
        var match=Regex.Match(driverName,@"\b(203|300|600)\s*dpi\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        var named=match.Success?int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture):(int?)null;
        static int Grid(int? dpi)=>dpi switch {200 or 203=>8,300 or 305=>12,600 or 610=>24,_=>0};
        if(resolution is >0 && Grid(resolution)==0)return null;
        if(named.HasValue && Grid(resolution)>0 && Grid(named)!=Grid(resolution))return null;
        var grid=Grid(named??resolution);
        if(verticalResolution is >0 && Grid(verticalResolution)!=grid)return null;
        return grid==0?null:new ZebraZt411(grid);
    }
    public (int Width,int Height) Pixels(double widthMm,double heightMm)
    {
        if(DotsPerMm is not (8 or 12 or 24))throw new FormatException("Select a 203, 300 or 600 dpi ZT411 driver.");
        if(!double.IsFinite(widthMm) || widthMm<19.99999 || widthMm>104.00001)throw new FormatException("This sticker is too wide for the ZT411. Set its width between 20 and 104 mm in Logo / sticker layout.");
        if(!double.IsFinite(heightMm) || heightMm<19.99999 || heightMm>300.00001)throw new FormatException("Set the sticker height between 20 and 300 mm.");
        return ((int)Math.Round(widthMm*DotsPerMm,MidpointRounding.AwayFromZero),(int)Math.Round(heightMm*DotsPerMm,MidpointRounding.AwayFromZero));
    }
}

/// <summary>One-bit raster: most-significant bit first, 1 = black, zero padding at the right edge.</summary>
public sealed class MonochromeLabel
{
    public int Width {get;}
    public int Height {get;}
    public int Stride=>(Width+7)/8;
    public byte[] BlackBits {get;}
    public MonochromeLabel(int width,int height,byte[] gray)
    {
        if(width<1 || width>2496 || height<1 || height>7200 || gray.Length!=checked(width*height))throw new FormatException("Invalid label raster size.");
        Width=width;Height=height;BlackBits=new byte[checked(Stride*height)];
        // Ordered dithering keeps grayscale logos while retaining solid black text. Deterministic in preview/output.
        int[] bayer={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            if(gray[y*width+x]<bayer[(y%4)*4+x%4]*16+8)BlackBits[y*Stride+x/8]|=(byte)(128>>(x%8));
    }
    public void WriteZpl(TextWriter writer)
    {
        static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
        writer.Write($"^XA\n^JMA^MUD,200,200^PON^PMN^LRN^FWN^LH0,0^LT0^LS0\n^PW{N(Width)}\n^LL{N(Height)},Y\n");
        // ^GF counts are limited to 99,999 bytes. Band large/300/600 dpi labels instead of truncating them.
        var rowsPerField=99999/Stride;
        for(var y=0;y<Height;y+=rowsPerField)
        {
            var bytes=Math.Min(rowsPerField,Height-y)*Stride;
            writer.Write($"^FO0,{N(y)}^GFA,{N(bytes)},{N(bytes)},{N(Stride)},");
            writer.Write(Convert.ToHexString(BlackBits,y*Stride,bytes));writer.Write("^FS\n");
        }
        writer.Write("^PQ1,0,0,N\n^XZ\n"); // Exactly one physical label per already-expanded paginator page.
    }
}

/// <summary>Blocking spool writes may accept only part of a buffer. Never retry a failed job from its beginning.</summary>
public static class LabelSpoolTransfer
{
    public static void Copy(Stream input,byte[] buffer,Func<int,int,int> write,CancellationToken cancellation,long? byteCount=null)
    {
        if(buffer.Length==0)throw new ArgumentException("A spool buffer is required.",nameof(buffer));
        if(byteCount is <0)throw new ArgumentOutOfRangeException(nameof(byteCount));
        var remaining=byteCount;
        while(remaining!=0)
        {
            cancellation.ThrowIfCancellationRequested();
            var read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining??buffer.Length));
            if(read==0){if(remaining.HasValue)throw new EndOfStreamException("The prepared print file is incomplete.");return;}
            if(remaining.HasValue)remaining-=read;
            var offset=0;
            while(offset<read)
            {
                cancellation.ThrowIfCancellationRequested();var written=write(offset,read-offset);
                if(written<=0 || written>read-offset)throw new IOException("The printer stopped accepting sticker data.");
                offset+=written;
            }
        }
    }
}
