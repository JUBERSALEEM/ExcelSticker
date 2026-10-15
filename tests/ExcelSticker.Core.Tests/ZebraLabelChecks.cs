using System.Globalization;
using System.Text.RegularExpressions;
using ExcelSticker.Core;

internal static class ZebraLabelChecks
{
    private static void Assert(bool condition,string why){if(!condition)throw new Exception(why);}
    private static void Reject<T>(Action action) where T:Exception
    {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    private static string Encode(MonochromeLabel raster){using var writer=new StringWriter();raster.WriteZpl(writer);return writer.ToString();}
    public static void Run(Action<string,Action> check)
    {
        check("ZT411 detection uses driver model and verified resolution, including EPL queues",()=>
        {
            foreach(var (dpi,grid) in new[]{(203,8),(300,12),(600,24)})
            {
                var printer=ZebraZt411.Detect($"ZDesigner ZT411-{dpi}dpi ZPL",dpi)!;
                Assert(printer.DotsPerMm==grid && printer.Pixels(100,70)==(100*grid,70*grid),"Physical dot dimensions");
            }
            Assert(ZebraZt411.Detect("ZDesigner ZT411-203dpi EPL",null)?.DotsPerMm==8,"User's legacy EPL driver");
            Assert(ZebraZt411.Detect("ZDesigner ZT411",305)?.DotsPerMm==12,"Exact 12 dots/mm driver resolution");
            Assert(ZebraZt411.Detect("ZDesigner ZT411-203dpi ZPL",300)==null,"Conflicting driver resolutions must not be guessed");
            Assert(ZebraZt411.Detect("ZT411-203dpi",203,300)==null && ZebraZt411.Detect("ZT411-203dpi",1200)==null,"Conflicting or unsupported axes block direct output");
            Assert(ZebraZt411.Detect("ZT411",null)==null && ZebraZt411.Detect("ZT411",1200)==null,"Unknown resolution");
            Assert(ZebraZt411.Detect("Microsoft Print to PDF",203)==null && ZebraZt411.Detect("ZDesigner ZT410-203dpi",203)==null,"No RAW on PDF or another Zebra model");
        });
        check("ZT411 width limit and finite dimensions are enforced without silently scaling",()=>
        {
            var printer=new ZebraZt411(24);Assert(printer.Pixels(104,300)==(2496,7200),"Maximum app stock");
            foreach(var width in new[]{105d,114,300,double.NaN,double.PositiveInfinity,0})Reject<FormatException>(()=>printer.Pixels(width,70));
            foreach(var height in new[]{301d,double.NaN,double.NegativeInfinity,0})Reject<FormatException>(()=>printer.Pixels(100,height));
            Reject<FormatException>(()=>new ZebraZt411(10).Pixels(100,70));
            Assert(new ZebraZt411(8).Pixels(100.0625,70)==(801,560),"Half-dot quantization rounds consistently");
        });
        check("One-bit raster polarity, MSB order and right-edge padding are correct",()=>
        {
            var label=new MonochromeLabel(9,1,new byte[]{0,255,255,255,255,255,255,0,0});
            Assert(label.Stride==2 && label.BlackBits.SequenceEqual(new byte[]{0x81,0x80}),"Black bits/padding");
            Assert(new MonochromeLabel(8,1,Enumerable.Repeat((byte)255,8).ToArray()).BlackBits[0]==0,"White remains white");
            Assert(new MonochromeLabel(8,1,new byte[8]).BlackBits[0]==255,"Solid black remains black");
            Assert(Encode(label).Contains("^GFA,2,2,2,8180^FS"),"Hex payload matches bytes");
            Reject<FormatException>(()=>new MonochromeLabel(9,1,new byte[8]));
        });
        check("ZPL bands round-trip every pixel and stay within the 99,999 byte field limit",()=>
        {
            foreach(var (width,height) in new[]{(800,560),(1200,840),(2400,1680),(2496,7200)})
            {
                var gray=Enumerable.Range(0,width*height).Select(i=>(byte)(i%256)).ToArray();
                var label=new MonochromeLabel(width,height,gray);var zpl=Encode(label);var decoded=new List<byte>();var row=0;
                var fields=Regex.Matches(zpl,@"\^FO0,(\d+)\^GFA,(\d+),(\d+),(\d+),([0-9A-F]+)\^FS");
                foreach(Match field in fields)
                {
                    int N(int group)=>int.Parse(field.Groups[group].Value,CultureInfo.InvariantCulture);
                    var bytes=Convert.FromHexString(field.Groups[5].Value);
                    Assert(N(1)==row && N(2)==N(3) && N(2)<=99999 && N(4)==label.Stride,"Contiguous valid fields");
                    Assert(bytes.Length==N(2) && bytes.Length%label.Stride==0,"Declared byte count and whole rows");
                    decoded.AddRange(bytes);row+=bytes.Length/label.Stride;
                }
                Assert(row==height && decoded.SequenceEqual(label.BlackBits),"No missing/duplicated stripe rows");
                Assert(zpl.Contains($"^PW{width}\n^LL{height},Y") && zpl.IndexOf("^LL",StringComparison.Ordinal)<zpl.IndexOf("^FS",StringComparison.Ordinal),"Width and gap/mark/continuous length before first field separator");
                Assert(Regex.Matches(zpl,@"\^PQ1,0,0,N").Count==1 && zpl.EndsWith("^XZ\n",StringComparison.Ordinal),"One expanded label only");
                Assert(zpl.Contains("^JMA^MUD,200,200") && !zpl.Contains("^MN") && !zpl.Contains("^JUS"),"Native dot grid, no conversion, no sensing-mode override or permanent save");
            }
        });
        check("ZPL encoding is culture-independent and contains no interpolated customer commands",()=>
        {
            var old=CultureInfo.CurrentCulture;
            try{CultureInfo.CurrentCulture=new CultureInfo("ar-AE");Assert(Encode(new MonochromeLabel(9,1,new byte[9])).Contains("^PW9\n^LL1,Y"),"ASCII numeric formatting");}
            finally{CultureInfo.CurrentCulture=old;}
        });
        check("Spool transfers handle partial writes without duplicating bytes",()=>
        {
            var expected=Enumerable.Range(0,1027).Select(i=>(byte)i).ToArray();using var input=new MemoryStream(expected);using var output=new MemoryStream();var buffer=new byte[64];
            LabelSpoolTransfer.Copy(input,buffer,(offset,count)=>{var accepted=Math.Min(7,count);output.Write(buffer,offset,accepted);return accepted;},CancellationToken.None);
            Assert(output.ToArray().SequenceEqual(expected),"All bytes written exactly once");
        });
        check("Spool page boundaries neither consume the next label nor silently accept truncation",()=>
        {
            using var source=new MemoryStream(new byte[]{1,2,3,4,5});var buffer=new byte[4];var actual=new List<byte>();
            int Write(int offset,int count){actual.AddRange(buffer.Skip(offset).Take(count));return count;}
            LabelSpoolTransfer.Copy(source,buffer,Write,CancellationToken.None,3);
            Assert(source.Position==3 && actual.SequenceEqual(new byte[]{1,2,3}),"First label only");
            LabelSpoolTransfer.Copy(source,buffer,Write,CancellationToken.None,2);
            Assert(actual.SequenceEqual(new byte[]{1,2,3,4,5}),"Next label starts at the correct byte");
            Reject<EndOfStreamException>(()=>LabelSpoolTransfer.Copy(source,buffer,Write,CancellationToken.None,1));
            Reject<ArgumentOutOfRangeException>(()=>LabelSpoolTransfer.Copy(source,buffer,Write,CancellationToken.None,-1));
        });
        check("Spool transfers stop on cancellation or stalled/invalid writes, without retry",()=>
        {
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();var writes=0;
            Reject<OperationCanceledException>(()=>LabelSpoolTransfer.Copy(new MemoryStream(new byte[10]),new byte[4],(_,count)=>{writes++;return count;},cancelled.Token));
            Assert(writes==0,"No writes after initial cancellation");
            using var during=new CancellationTokenSource();
            Reject<OperationCanceledException>(()=>LabelSpoolTransfer.Copy(new MemoryStream(new byte[10]),new byte[4],(_,count)=>{writes++;during.Cancel();return 1;},during.Token));
            Assert(writes==1,"Cancellation between partial writes");
            foreach(var invalid in new[]{0,-1,5})Reject<IOException>(()=>LabelSpoolTransfer.Copy(new MemoryStream(new byte[4]),new byte[4],(_,_)=>invalid,CancellationToken.None));
            var attempts=0;
            Reject<IOException>(()=>LabelSpoolTransfer.Copy(new MemoryStream(new byte[4]),new byte[4],(_,_)=>{attempts++;throw new IOException("offline");},CancellationToken.None));
            Assert(attempts==1,"Do not retry uncertain errors");
        });
    }
}
