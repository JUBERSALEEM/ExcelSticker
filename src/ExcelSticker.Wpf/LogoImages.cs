using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>Local-only import, bounded decode, white-backed grayscale PNG, no external asset dependencies.</summary>
public static class LogoImages
{
    private static readonly Dictionary<string,BitmapSource> Cache=new(ReferenceEqualityComparer.Instance);
    public static string Import(string path)
    {
        using var stream=File.OpenRead(path);
        if(stream.Length>8*1024*1024)throw new FormatException("Choose a PNG, JPEG or BMP logo up to 8 MB.");
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
        if(decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder))throw new FormatException("Use PNG, JPEG or BMP. SVG and animated images are not supported.");
        var frame=decoder.Frames[0];
        if(frame.PixelWidth<1 || frame.PixelHeight<1 || (long)frame.PixelWidth*frame.PixelHeight>16_000_000)throw new FormatException("Logo must be at most 16 megapixels.");
        stream.Position=0;
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;
        if(frame.PixelWidth>=frame.PixelHeight)image.DecodePixelWidth=Math.Min(1000,frame.PixelWidth);
        else image.DecodePixelHeight=Math.Min(1000,frame.PixelHeight);
        image.EndInit();image.Freeze();
        var gray=ToGrayscale(image);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(gray));using var output=new MemoryStream();encoder.Save(output);
        var data=Convert.ToBase64String(output.ToArray());LogoData.Validate(data);return data;
    }
    public static BitmapSource? Decode(string data)
    {
        if(data.Length==0)return null;
        if(Cache.TryGetValue(data,out var known))return known;
        using var stream=new MemoryStream(LogoData.Validate(data));
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();
        var gray=ToGrayscale(image);
        if(Cache.Count>=12)Cache.Clear();Cache[data]=gray;return gray;
    }
    private static BitmapSource ToGrayscale(BitmapSource image)
    {
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen()){var rect=new Rect(0,0,image.PixelWidth,image.PixelHeight);dc.DrawRectangle(Brushes.White,null,rect);dc.DrawImage(image,rect);}
        var raster=new RenderTargetBitmap(image.PixelWidth,image.PixelHeight,96,96,PixelFormats.Pbgra32);raster.Render(visual);
        var gray=new FormatConvertedBitmap(raster,PixelFormats.Gray8,null,0);gray.Freeze();return gray;
    }
    public static BitmapSource? Rotated(string data,int degrees)
    {
        var source=Decode(data);if(source==null || degrees==0)return source;
        var rotated=new TransformedBitmap(source,new RotateTransform(degrees));rotated.Freeze();return rotated;
    }
    public static void ValidateLibrary(LayoutLibrary library)
    {
        library.Validate();
        foreach(var e in library.Layouts.SelectMany(l=>l.Elements).Where(e=>e.Field=="Logo" && e.ImagePng.Length>0))_=Decode(e.ImagePng);
    }
}
