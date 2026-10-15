using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Wpf.Studio;

internal static class ZebraPrintingChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run()
    {
        var library=new LayoutLibrary();var label=LiveStickerSamples.Sgu().Outer with {EntryNumber=7};
        // Embedded real PNG data, not a font-based or printer-resident replacement for the company logo.
        var png=new PngBitmapEncoder();var gray=Enumerable.Range(0,400).Select(i=>(byte)(i%20<10?0:255)).ToArray();
        png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(20,20,96,96,PixelFormats.Gray8,null,gray,20)));
        using var imageData=new MemoryStream();png.Save(imageData);
        library.For(StickerFormat.SGU).Elements.Add(new StickerElement {Field="Logo",X=5,Y=50,Width=10,Height=10,ImagePng=Convert.ToBase64String(imageData.ToArray())});
        var job=new LabelPaginator(new[]{label},true,new CompanyProfile(),library);
        var preview=new PrintJobPreview();preview.LoadJob(job,"ZT411 regression");preview.ShowPage(3);
        foreach(var grid in new[]{8,12,24})
        {
            var profile=new ZebraZt411(grid);
            preview.SetPrinter(new PrinterReview("ZT411",false,null,null,Array.Empty<string>()) {Zebra=profile,Summary="Test ZT411"});
            var image=(Image)((ContentControl)preview.FindName("PageHost")).Content;var bitmap=(BitmapSource)image.Source;
            using var printPage=job.SelectPages(3,1).GetPage(0);var printView=(LabelView)printPage.Visual;var raster=ZebraRasterizer.Render(printView,profile);
            var displayed=new byte[raster.BlackBits.Length];bitmap.CopyPixels(displayed,raster.Stride,0);
            Check(bitmap.PixelWidth==100*grid && bitmap.PixelHeight==70*grid && displayed.Select(b=>(byte)~b).SequenceEqual(raster.BlackBits),$"ZT411 {grid} dots/mm preview pixels match the selected output page bit-for-bit");
            Check(preview.CurrentView!.Label.QuantityText=="7/4" && printView.Label.QuantityText=="7/4" && printView.LayoutTransform.Value.IsIdentity,"Raster output retains original copy number and no preview zoom");
            bool Black(int x,int y)=>(raster.BlackBits[y*raster.Stride+x/8]&(128>>(x%8)))!=0;
            Check(Black(7*grid,55*grid) && !Black(13*grid,55*grid),"Embedded black/white logo reaches the final printer-resolution raster with correct polarity and millimetre placement");
            ((ComboBox)preview.FindName("Zoom")).SelectedIndex=3;
            Check(ZebraRasterizer.Render((LabelView)job.GetPage(3).Visual,profile).BlackBits.SequenceEqual(raster.BlackBits),"Screen zoom does not alter dot data");
        }
        preview.SetPrinter(new PrinterReview("Windows test",true,null,null,Array.Empty<string>()));
        Check(((ContentControl)preview.FindName("PageHost")).Content is LabelView,"Windows driver review uses the same vector paginator view");
        preview.Invalidate("changed during preparation");Check(!preview.CanPrint,"Direct ZT411 mode cannot bypass stale-job protection");
    }
}
