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
        Label=label; Layout=(layout??StickerLayout.Default(LayoutLibrary.FormatFor(label))).Clone();Layout.Validate();
        Width=LabelWidth;Height=LabelHeight;Background=Brushes.White;SnapsToDevicePixels=true;
        // Physical mm positions must not be rounded to UI pixels when spooling a printer page.
        UseLayoutRounding=false;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.Auto);
        var canvas=new Canvas {Width=LabelWidth,Height=LabelHeight,Background=Brushes.White,ClipToBounds=true};Child=canvas;
        foreach(var item in Layout.Elements.Where(e=>e.Visible))
        {
            FrameworkElement visual;
            if(item.Field is "Line" or "Box")
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
            "OrderNo"=>label.OrderNo,"Quantity"=>label.QuantityText,"TotalQty"=>label.Qty.ToString(CultureInfo.InvariantCulture),
            "CopyNumber"=>$"{label.EntryNumber}/{label.CopyIndex}","GlassType"=>label.GlassType,"Specification"=>string.IsNullOrEmpty(label.Specification)?label.GlassType:label.Specification,
            "Size"=>label.Size,"Width"=>Number(label.Width),"Height"=>Number(label.Height),"ReferenceNo"=>label.ReferenceNo,"CustomerName"=>label.CustomerName,
            "JobNo"=>label.JobNo,"Pane"=>label.Pane,"Format"=>format==StickerFormat.NormalJobOrder?"JOB ORDER":format+" GLASS",
            "CustomText"=>item.Text,_=>""
        };
        return string.IsNullOrEmpty(value)?"":item.Prefix+value;
    }
}
