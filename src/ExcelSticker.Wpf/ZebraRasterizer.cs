using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

public static class ZebraRasterizer
{
    public static MonochromeLabel Render(LabelView view,ZebraZt411 printer)
    {
        var (width,height)=printer.Pixels(view.Layout.WidthMm,view.Layout.HeightMm);
        var size=new Size(view.Width,view.Height);view.Measure(size);view.Arrange(new Rect(size));view.UpdateLayout();
        var raster=new RenderTargetBitmap(width,height,printer.DotsPerMm*25.4,printer.DotsPerMm*25.4,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var context=background.RenderOpen())context.DrawRectangle(Brushes.White,null,new Rect(0,0,width*96/(printer.DotsPerMm*25.4),height*96/(printer.DotsPerMm*25.4)));
        raster.Render(background);raster.Render(view);
        var gray=new FormatConvertedBitmap(raster,PixelFormats.Gray8,null,0);var pixels=new byte[width*height];gray.CopyPixels(pixels,width,0);
        return new MonochromeLabel(width,height,pixels);
    }
    public static BitmapSource Preview(MonochromeLabel raster,ZebraZt411 printer)
    {
        var pixels=(byte[])raster.BlackBits.Clone();for(var i=0;i<pixels.Length;i++)pixels[i]=(byte)~pixels[i]; // WPF BlackWhite uses 0=black.
        var image=BitmapSource.Create(raster.Width,raster.Height,printer.DotsPerMm*25.4,printer.DotsPerMm*25.4,PixelFormats.BlackWhite,null,pixels,raster.Stride);
        image.Freeze();return image;
    }
}
