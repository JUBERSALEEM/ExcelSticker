using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>One vector, monochrome label. No UI theme effects, ancestor scaling, or silent sub-6pt shrinkage.</summary>
public sealed class LabelView : Border
{
    public const double UnitsPerMm=96.0/25.4;
    public const double LabelWidth=100*UnitsPerMm;
    public const double LabelHeight=70*UnitsPerMm;
    public GlassLabel Label {get;}
    public StickerLayout Layout {get;}
    public List<string> Warnings {get;}=new();
    public LabelView(GlassLabel label,CompanyProfile company,StickerLayout? layout=null)
    {
        Label=label; Layout=(layout??StickerLayout.Default(LayoutLibrary.FormatFor(label))).Clone();Layout.UpgradeLegacyNumbering();
        if(!Layout.Elements.Any(e=>e.Visible && e.Field=="GlassSrNo"))Warnings.Add("Glass Sr No is missing or hidden. Add/show GlassSrNo in Sticker Studio, or reset this format.");
        Width=Layout.WidthMm*UnitsPerMm;Height=Layout.HeightMm*UnitsPerMm;Background=Brushes.White;SnapsToDevicePixels=true;
        // Physical mm positions must not be rounded to UI pixels when spooling a printer page.
        UseLayoutRounding=false;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Auto);
        var canvas=new Canvas {Width=this.Width,Height=this.Height,Background=Brushes.White,ClipToBounds=true};Child=canvas;
        foreach(var item in Layout.Elements.Where(e=>e.Visible))
        {
            FrameworkElement visual;
            if(item.Field=="Logo")
            {
                System.Windows.Media.Imaging.BitmapSource? source=null;
                try{source=LogoImages.Rotated(item.ImagePng,item.Rotation);}
                catch(Exception ex) when(ex is FormatException or System.IO.IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException){Warnings.Add("Logo cannot be decoded: "+ex.Message);}
                if(source==null)Warnings.Add("Logo is empty or invalid. Import a logo in Sticker Studio or hide/remove this field.");
                else if(source.PixelWidth/(item.Width/25.4)<150 || source.PixelHeight/(item.Height/25.4)<150)Warnings.Add("Logo resolution may be low at this print size (below 150 DPI).");
                var overlaps=Layout.Elements.Where(e=>e!=item && e.Visible && e.Field is not ("Line" or "Box") && item.X<e.X+e.Width && item.X+item.Width>e.X && item.Y<e.Y+e.Height && item.Y+item.Height>e.Y).Select(e=>e.Field).Distinct().Take(3).ToArray();
                if(overlaps.Length>0)Warnings.Add("Logo overlaps: "+string.Join(", ",overlaps)+". Move or resize it in Sticker Studio.");
                visual=new Image {Source=source,Stretch=item.KeepAspect?Stretch.Uniform:Stretch.Fill};
                RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
            }
            else if(item.Field is "Line" or "Box")
            {
                visual=new Border {BorderBrush=Brushes.Black,BorderThickness=item.Field=="Line"?new Thickness(0,item.Stroke*UnitsPerMm,0,0):new Thickness(item.Stroke*UnitsPerMm)};
            }
            else
            {
                var text=Resolve(item,label,company,Layout.Format);
                var pt=item.FontSize;var width=item.Width*UnitsPerMm;var height=item.Height*UnitsPerMm;
                var face=new Typeface(new FontFamily(item.FontFamily),FontStyles.Normal,item.Bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal);
                bool Fits(double points)
                {
                    var formatted=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,points*96/72,Brushes.Black,1);
                    if(item.Wrap)formatted.MaxTextWidth=width;
                    return formatted.WidthIncludingTrailingWhitespace<=width+.1 && formatted.Height<=height+.1;
                }
                if(item.AutoFit)while(pt>6&&!Fits(pt))pt=Math.Max(6,pt-.25);
                if(!Fits(pt))Warnings.Add($"{item.Field}: text does not fit at {pt:0.##} pt. Enlarge the box, shorten text, or use wrapping.");
                visual=new TextBlock {Text=text,FontFamily=new FontFamily(item.FontFamily),FontSize=pt*96/72,FontWeight=item.Bold?FontWeights.Bold:FontWeights.Normal,
                    Foreground=Brushes.Black,Background=Brushes.White,TextAlignment=Enum.Parse<TextAlignment>(item.Alignment),
                    TextWrapping=item.Wrap?TextWrapping.Wrap:TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,
                    TextDecorations=item.Underline?TextDecorations.Underline:null,ClipToBounds=true};
            }
            visual.Width=item.Width*UnitsPerMm;visual.Height=item.Height*UnitsPerMm;
            Canvas.SetLeft(visual,item.X*UnitsPerMm);Canvas.SetTop(visual,item.Y*UnitsPerMm);canvas.Children.Add(visual);
        }
    }
    public static string Resolve(StickerElement item,GlassLabel label,CompanyProfile company,StickerFormat format)
    {
        string Number(decimal n)=>n.ToString("0.0",CultureInfo.InvariantCulture);
        var value=item.Field switch
        {
            "CompanyName"=>company.Name,"Address"=>company.Address,"Phone"=>company.Phone,"Website"=>company.Website,"Email"=>company.Email,
            "OrderNo"=>label.OrderNo,"GlassSrNo"=>label.EntryNumber.ToString(CultureInfo.InvariantCulture),
            "Quantity" or "TotalQty"=>label.QuantityText, // TotalQty is a legacy alias: stickers use Sr No / copy.
            "CopyNumber"=>$"{label.EntryNumber.ToString(CultureInfo.InvariantCulture)}/{label.CopyIndex.ToString(CultureInfo.InvariantCulture)}","GlassType"=>label.GlassType,"Specification"=>string.IsNullOrEmpty(label.Specification)?label.GlassType:label.Specification,
            "Size"=>label.Size,"Width"=>Number(label.Width),"Height"=>Number(label.Height),"ReferenceNo"=>label.ReferenceNo,"CustomerName"=>label.CustomerName,
            "JobNo"=>label.JobNo,"Pane"=>label.Pane,"Format"=>format==StickerFormat.NormalJobOrder?"JOB ORDER":format+" GLASS",
            "CustomText"=>item.Text,_=>""
        };
        return string.IsNullOrEmpty(value)?"":item.Prefix+value;
    }
}
