using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

public sealed record PrinterReview(string Description,bool StockVerified,Rect? ImageableArea,string? BlockReason,IReadOnlyList<string> Warnings)
{
    public static PrinterReview Inspect(string name,PrintTicket ticket,PrintCapabilities? capabilities)
    {
        static string Mm(double? value)=>value.HasValue?(value.Value*25.4/96).ToString("0.##",CultureInfo.InvariantCulture):"unknown";
        var stock=ticket.PageMediaSize;
        var verified=PrintStock.MatchesLabel(stock?.Width,stock?.Height,ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape);
        var scaling=InspectScaling(ticket);
        string? blocked=null;
        if(ticket.CopyCount.HasValue && ticket.CopyCount!=1)blocked="Set printer-driver Copies to 1; copies are already expanded in the preview.";
        else if(ticket.PagesPerSheet.HasValue && ticket.PagesPerSheet!=1)blocked="Set the printer to one page per sheet (no N-up).";
        else if(scaling.Problem!=null)blocked=scaling.Problem;
        else if(ticket.Duplexing is Duplexing.TwoSidedLongEdge or Duplexing.TwoSidedShortEdge)blocked="Use one-sided printing for label stock.";
        var warnings=new List<string>();
        if(!verified)warnings.Add("Driver has not verified 100 × 70 mm stock. Wrong media/orientation can clip, rotate or offset labels.");
        if(!scaling.Verified && scaling.Problem==null)warnings.Add("Driver scaling is unverified. Verify Actual size / 100%, not Fit to page, in Printing Preferences.");
        if(ticket.CopyCount==null || ticket.PagesPerSheet==null)warnings.Add("Some driver copy/imposition settings are unreported. Verify Copies 1 and one page per sheet.");
        Rect? area=null;var reported=capabilities?.PageImageableArea;
        if(verified && reported!=null && new[]{reported.OriginWidth,reported.OriginHeight,reported.ExtentWidth,reported.ExtentHeight}.All(double.IsFinite)
            && reported.OriginWidth>=0 && reported.OriginHeight>=0 && reported.ExtentWidth>0 && reported.ExtentHeight>0)
            area=new Rect(reported.OriginWidth,reported.OriginHeight,reported.ExtentWidth,reported.ExtentHeight);
        else warnings.Add("Printable boundary unavailable or stock unverified; no boundary is simulated.");
        return new PrinterReview($"{name}\nDriver media: {Mm(stock?.Width)} × {Mm(stock?.Height)} mm · {ticket.PageOrientation?.ToString()??"orientation unknown"}\nApp page: 100 × 70 mm · driver scale: {(scaling.Verified?"100% / actual size":ticket.PageScalingFactor is int percent?$"{percent}% (see warnings)":"unverified (see warnings)")} · copies: {ticket.CopyCount?.ToString(CultureInfo.InvariantCulture)??"unknown"}",verified,area,blocked,warnings);
    }
    // The public PageScalingFactor getter reports only X for Custom (non-square) scaling.
    // Inspect standard Print Schema options as well: Y=90 or Fit must not pass as actual size.
    private static (bool Verified,string? Problem) InspectScaling(PrintTicket ticket)
    {
        const string problem="Set printer scaling to Actual size / 100% in both directions, not Fit to page.";
        if(ticket.PageScalingFactor is int scale && scale!=100)return(false,problem);
        XNamespace framework="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
        XNamespace keywords="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords";
        static XName? Name(XElement element)
        {
            var value=(string?)element.Attribute("name");if(string.IsNullOrWhiteSpace(value))return null;
            var parts=value.Split(':');
            if(parts.Length==1)return element.GetDefaultNamespace()+parts[0];
            return parts.Length==2 && element.GetNamespaceOfPrefix(parts[0]) is {} ns ? ns+parts[1]:null;
        }
        using var stream=ticket.GetXmlStream();var xml=XDocument.Load(stream);
        var feature=xml.Descendants(framework+"Feature").FirstOrDefault(e=>Name(e)==keywords+"PageScaling");
        var option=feature?.Element(framework+"Option");var name=option==null?null:Name(option);
        if(name==null || name.Namespace!=keywords)return(false,null); // Vendor-private/omitted settings are not proof of actual size.
        if(name.LocalName=="None")return(true,null);
        if(name.LocalName.StartsWith("Fit",StringComparison.Ordinal))return(false,problem);
        int? Value(string property)
        {
            var scored=option!.Elements(framework+"ScoredProperty").FirstOrDefault(e=>Name(e)==keywords+property);
            var value=scored?.Element(framework+"Value");
            if(value==null && scored?.Element(framework+"ParameterRef") is {} reference)
                value=xml.Descendants(framework+"ParameterInit").FirstOrDefault(e=>Name(e)==Name(reference))?.Element(framework+"Value");
            return int.TryParse(value?.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)?n:null;
        }
        if(name.LocalName=="CustomSquare")
        {
            var value=Value("Scale");return(value==100,value.HasValue && value!=100?problem:null);
        }
        if(name.LocalName=="Custom")
        {
            var x=Value("ScaleWidth");var y=Value("ScaleHeight");
            return(x==100 && y==100,(x.HasValue && x!=100)||(y.HasValue && y!=100)?problem:null);
        }
        return(false,null);
    }

}

/// <summary>Settings confirmation never spools. Sending uses only the reviewed ticket and preview paginator.</summary>
public sealed class StickerPrinter : IDisposable
{
    private PrintQueue? _queue;
    private PrintTicket? _ticket;
    public PrinterReview? Review {get;private set;}
    public bool Configure()
    {
        var dialog=new PrintDialog {UserPageRangeEnabled=false,SelectedPagesEnabled=false,CurrentPageEnabled=false};
        if(_queue!=null)dialog.PrintQueue=_queue;
        dialog.PrintTicket=_ticket?.Clone()??new PrintTicket {PageMediaSize=new PageMediaSize(LabelView.LabelWidth,LabelView.LabelHeight),PageOrientation=PageOrientation.Portrait,
            CopyCount=1,PagesPerSheet=1,PageScalingFactor=100,Duplexing=Duplexing.OneSided,OutputColor=OutputColor.Monochrome};
        if(dialog.ShowDialog()!=true)return false;
        var queue=dialog.PrintQueue??throw new InvalidOperationException("Choose a printer.");
        var ticket=queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket,dialog.PrintTicket).ValidatedPrintTicket;
        PrintCapabilities? capabilities=null;
        try {capabilities=queue.GetPrintCapabilities(ticket);}catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or NotSupportedException or ArgumentException or FormatException){ } // Some label drivers cannot report margins.
        var review=PrinterReview.Inspect(queue.FullName,ticket,capabilities);
        if(_queue!=null && !ReferenceEquals(_queue,queue))_queue.Dispose();
        _queue=queue;_ticket=ticket.Clone();Review=review;return true;
    }
    public string Send(DocumentPaginator pages)
    {
        if(_queue==null || _ticket==null || Review==null)throw new InvalidOperationException("Choose and review printer settings first.");
        if(Review.BlockReason!=null)throw new InvalidOperationException(Review.BlockReason);
        if(pages.PageCount<1 || pages.PageCount>10000)throw new FormatException("Choose 1–10,000 preview pages.");
        // No second dialog silently changes the reviewed settings between preview and submission.
        var dialog=new PrintDialog {PrintQueue=_queue,PrintTicket=_ticket.Clone(),UserPageRangeEnabled=false};
        dialog.PrintDocument(pages,"LABLE STUDIO reviewed sticker pages");return _queue.FullName;
    }
    public void Dispose(){_queue?.Dispose();_queue=null;_ticket=null;Review=null;}
}
