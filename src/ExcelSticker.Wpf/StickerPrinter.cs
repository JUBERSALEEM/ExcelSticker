using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

public sealed record PrinterReview(string Description,bool StockVerified,Rect? ImageableArea,string? BlockReason,IReadOnlyList<string> Warnings)
{
    public ZebraZt411? Zebra {get;init;}
    public string Summary {get;init;}="";
    public static PrinterReview Inspect(string name,PrintTicket ticket,PrintCapabilities? capabilities,Size? requestedSize=null)
    {
        static string Mm(double? value)=>value.HasValue?(value.Value*25.4/96).ToString("0.##",CultureInfo.InvariantCulture):"unknown";
        var requested=requestedSize??new Size(LabelView.LabelWidth,LabelView.LabelHeight);
        var dimensions=$"{Mm(requested.Width)} × {Mm(requested.Height)} mm";
        var stock=ticket.PageMediaSize;
        var verified=PrintStock.MatchesLabel(stock?.Width,stock?.Height,ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape,requested.Width/LabelView.UnitsPerMm,requested.Height/LabelView.UnitsPerMm);
        var scaling=InspectScaling(ticket);
        string? blocked=null;
        if(ticket.CopyCount.HasValue && ticket.CopyCount!=1)blocked="Set printer-driver Copies to 1; the app already creates each numbered sticker.";
        else if(ticket.PagesPerSheet.HasValue && ticket.PagesPerSheet!=1)blocked="Set the printer to one page per sheet (no N-up).";
        else if(scaling.Problem!=null)blocked=scaling.Problem;
        else if(ticket.Duplexing is Duplexing.TwoSidedLongEdge or Duplexing.TwoSidedShortEdge)blocked="Use one-sided printing for label stock.";
        if(PrintStock.TooSmall(stock?.Width,stock?.Height,ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape,requested.Width/LabelView.UnitsPerMm,requested.Height/LabelView.UnitsPerMm))
            blocked??=$"Printer paper is {Mm(stock?.Width)} × {Mm(stock?.Height)} mm; the sticker is {dimensions}. Open Windows printer preferences and set matching label paper. Nothing was sent.";
        var warnings=new List<string>();
        if(!verified)warnings.Add($"Driver has not verified {dimensions} stock. Wrong media/orientation can clip, rotate or offset labels.");
        if(!scaling.Verified && scaling.Problem==null)warnings.Add("Driver scaling is unverified. Verify Actual size / 100%, not Fit to page, in Printing Preferences.");
        if(ticket.CopyCount==null || ticket.PagesPerSheet==null)warnings.Add("Some driver copy/imposition settings are unreported. Verify Copies 1 and one page per sheet.");
        Rect? area=null;var reported=capabilities?.PageImageableArea;
        if(verified && reported!=null && new[]{reported.OriginWidth,reported.OriginHeight,reported.ExtentWidth,reported.ExtentHeight}.All(double.IsFinite)
            && reported.OriginWidth>=0 && reported.OriginHeight>=0 && reported.ExtentWidth>0 && reported.ExtentHeight>0)
            area=new Rect(reported.OriginWidth,reported.OriginHeight,reported.ExtentWidth,reported.ExtentHeight);
        else warnings.Add("Printable boundary unavailable or stock unverified; no boundary is simulated.");
        return new PrinterReview($"{name}\nDriver media: {Mm(stock?.Width)} × {Mm(stock?.Height)} mm · {ticket.PageOrientation?.ToString()??"orientation unknown"}\nApp page: {dimensions} · driver scale: {(scaling.Verified?"100% / actual size":ticket.PageScalingFactor is int percent?$"{percent}% (see warnings)":"unverified (see warnings)")} · copies: {ticket.CopyCount?.ToString(CultureInfo.InvariantCulture)??"unknown"}",verified,area,blocked,warnings);
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

/// <summary>Windows queue adapter. The printer service supplies the explicit destination; no selection dialog or confirmation is opened.</summary>
public sealed class StickerPrinter : IDisposable
{
    private sealed record Preference(string PrinterName);
    private readonly string _preferencePath=Path.Combine(AppDiagnostics.DataDirectory,"printer.json");
    private string? _savedPrinter;
    private PrintServer? _server;
    private PrintQueue? _queue;
    private PrintTicket? _ticket;
    public string? PreferredQueueName=>_savedPrinter;
    public PrinterReview? Review {get;private set;}
    private Size? _size;
    public StickerPrinter()
    {
        try
        {
            if(File.Exists(_preferencePath))
            {
                var preference=JsonSerializer.Deserialize<Preference>(File.ReadAllText(_preferencePath));
                _savedPrinter=preference?.PrinterName; // Old RAW preference is intentionally ignored: installed queues always use their driver.
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){AppDiagnostics.Log("Read printer preference",ex);}
    }
    private static PrintTicket Request(Size size,PrintTicket? previous=null)
    {
        var ticket=previous?.Clone()??new PrintTicket();
        ticket.PageMediaSize=new PageMediaSize(size.Width,size.Height);ticket.PageOrientation=PageOrientation.Portrait;
        ticket.CopyCount=1;ticket.PagesPerSheet=1;ticket.PageScalingFactor=100;ticket.Duplexing=Duplexing.OneSided;ticket.OutputColor=OutputColor.Monochrome;
        return ticket;
    }
    public void SelectQueue(string name,Size size)
    {
        if(_queue!=null && string.Equals(_queue.FullName,name,StringComparison.OrdinalIgnoreCase))
        {_queue.Refresh();Apply(_queue,_queue.UserPrintTicket??_queue.DefaultPrintTicket,size);return;}
        PrintServer? server=null;PrintQueue? queue=null;
        try
        {
            var split=name.LastIndexOf('\\');var remote=name.StartsWith(@"\\",StringComparison.Ordinal)&&split>1;
            server=remote?new PrintServer(name[..split]):new LocalPrintServer();
            queue=new PrintQueue(server,remote?name[(split+1)..]:name);queue.Refresh();
            Apply(queue,queue.UserPrintTicket??queue.DefaultPrintTicket,size);_server=server;SavePreference();
        }
        catch{queue?.Dispose();server?.Dispose();throw;}
    }
    private void Apply(PrintQueue queue,PrintTicket? selected,Size size)
    {
        var requested=Request(size,selected);var driver=queue.QueueDriver.Name;
        var isZebra=ZebraZt411.IsDriver(driver);
        var dimensions=$"{size.Width/LabelView.UnitsPerMm:0.##} × {size.Height/LabelView.UnitsPerMm:0.##} mm";
        // A queue's model name does not verify the printer's active language. Let its installed driver
        // generate EPL/ZPL/other output. Only the separately verified TCP service sends raw ZPL.
        AppDiagnostics.Log($"Configure selected Windows queue: {queue.FullName}; driver={driver}; requested={dimensions}");
        var ticket=queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket,requested).ValidatedPrintTicket;
        var review=PrinterReview.Inspect(queue.FullName,ticket,null,size);
        var media=ticket.PageMediaSize;
        if(isZebra && size.Width/LabelView.UnitsPerMm>104.00001)review=review with {BlockReason="The ZT411 prints up to 104 mm wide. Reduce the label-paper width in Logo / sticker layout. Glass dimensions can still be 300 × 300 mm."};
        review=review with {Summary=$"{queue.FullName} · {dimensions} · Windows driver"};
        AppDiagnostics.Log($"Printer configured: queue={queue.FullName}; driver={driver}; requested={dimensions}; validated media={media?.Width}x{media?.Height} DIP; orientation={ticket.PageOrientation}; scale={ticket.PageScalingFactor}; copies={ticket.CopyCount}; pages/sheet={ticket.PagesPerSheet}; duplex={ticket.Duplexing}; block={review.BlockReason??"none"}");
        var retainedTicket=ticket.Clone();var retainedName=queue.FullName;
        if(_queue!=null && !ReferenceEquals(_queue,queue)){_queue.Dispose();_server?.Dispose();_server=null;}
        _queue=queue;_ticket=retainedTicket;Review=review;_size=size;_savedPrinter=retainedName;
    }
    private void SavePreference()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_preferencePath)!);
            File.WriteAllText(_preferencePath+".tmp",JsonSerializer.Serialize(new Preference(_savedPrinter??"")));
            File.Move(_preferencePath+".tmp",_preferencePath,true);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppDiagnostics.Log("Remember printer",ex);}
    }
    public Task<string> SendAsync(DocumentPaginator pages,CancellationToken cancellation,Action validateSnapshot,Action submitting)
    {
        if(_queue==null || _ticket==null || Review==null)throw new InvalidOperationException("Select a printer above the live sticker.");
        if(_size!=pages.PageSize)throw new InvalidOperationException("Sticker size changed. Click Print sticker again.");
        if(Review.BlockReason!=null)throw new InvalidOperationException(Review.BlockReason);
        if(pages.PageCount<1 || pages.PageCount>10000)throw new FormatException("Choose 1–10,000 stickers.");
        var name=_queue.FullName;var review=Review;
        void CheckSnapshot(){cancellation.ThrowIfCancellationRequested();validateSnapshot();if(!ReferenceEquals(review,Review))throw new InvalidOperationException("Printer changed. Print again with the new settings.");}
        CheckSnapshot();
        submitting();
        var dialog=new PrintDialog {PrintQueue=_queue,PrintTicket=_ticket.Clone(),UserPageRangeEnabled=false};
        // WPF's writer needs the UI-owned visual/paginator. It is synchronous; do not run it on a
        // worker and violate dispatcher affinity. No ShowDialog / preview / retry is involved.
        dialog.PrintDocument(pages,"LABLE STUDIO stickers");
        return Task.FromResult(name);
    }
    public void Dispose(){_queue?.Dispose();_server?.Dispose();_server=null;_queue=null;_ticket=null;Review=null;_size=null;}
}
