using System.Globalization;
using System.IO;
using System.Text;
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
    public bool WindowsDriverOnly {get;init;}
    public static PrinterReview Inspect(string name,PrintTicket ticket,PrintCapabilities? capabilities,Size? requestedSize=null)
    {
        static string Mm(double? value)=>value.HasValue?(value.Value*25.4/96).ToString("0.##",CultureInfo.InvariantCulture):"unknown";
        var requested=requestedSize??new Size(LabelView.LabelWidth,LabelView.LabelHeight);
        var dimensions=$"{Mm(requested.Width)} × {Mm(requested.Height)} mm";
        var stock=ticket.PageMediaSize;
        var verified=PrintStock.MatchesLabel(stock?.Width,stock?.Height,ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape,requested.Width/LabelView.UnitsPerMm,requested.Height/LabelView.UnitsPerMm);
        var scaling=InspectScaling(ticket);
        string? blocked=null;
        if(ticket.CopyCount.HasValue && ticket.CopyCount!=1)blocked="Set printer-driver Copies to 1; copies are already expanded in the preview.";
        else if(ticket.PagesPerSheet.HasValue && ticket.PagesPerSheet!=1)blocked="Set the printer to one page per sheet (no N-up).";
        else if(scaling.Problem!=null)blocked=scaling.Problem;
        else if(ticket.Duplexing is Duplexing.TwoSidedLongEdge or Duplexing.TwoSidedShortEdge)blocked="Use one-sided printing for label stock.";
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

/// <summary>One remembered printer. Routine current/all printing has no confirmation or driver-warning checklist.</summary>
public sealed class StickerPrinter : IDisposable
{
    private sealed record Preference(string PrinterName,bool WindowsDriverOnly);
    private readonly string _preferencePath=Path.Combine(AppDiagnostics.DataDirectory,"printer.json");
    private string? _savedPrinter;
    private PrintServer? _restoredServer;
    private PrintQueue? _queue;
    private PrintTicket? _ticket;
    public bool WindowsDriverOnly {get;private set;}
    public PrinterReview? Review {get;private set;}
    private Size? _size;
    public StickerPrinter()
    {
        try
        {
            if(File.Exists(_preferencePath))
            {
                var preference=JsonSerializer.Deserialize<Preference>(File.ReadAllText(_preferencePath));
                _savedPrinter=preference?.PrinterName;WindowsDriverOnly=preference?.WindowsDriverOnly??false;
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
    public bool Configure(Size size)
    {
        var dialog=new PrintDialog {UserPageRangeEnabled=false,SelectedPagesEnabled=false,CurrentPageEnabled=false};
        if(_queue!=null)dialog.PrintQueue=_queue;
        dialog.PrintTicket=Request(size,_ticket);
        if(dialog.ShowDialog()!=true)return false;
        var queue=dialog.PrintQueue??throw new InvalidOperationException("Choose a printer.");
        try{Apply(queue,dialog.PrintTicket,size);}
        catch{if(!ReferenceEquals(queue,_queue))queue.Dispose();throw;}
        SavePreference();return true;
    }
    // Restoring a missing/disconnected saved queue does NOT silently select another printer.
    public bool EnsureConfigured(Size size)=>TryPrepare(size)||Configure(size);
    public bool TryPrepare(Size size)
    {
        if(_queue!=null){if(_size!=size)Apply(_queue,_ticket,size);return true;}
        if(!string.IsNullOrWhiteSpace(_savedPrinter))
        {
            PrintServer? server=null;PrintQueue? queue=null;
            try
            {
                var split=_savedPrinter.LastIndexOf('\\');
                var remote=_savedPrinter.StartsWith(@"\\",StringComparison.Ordinal) && split>1;
                server=remote?new PrintServer(_savedPrinter[..split]):new LocalPrintServer();
                queue=new PrintQueue(server,remote?_savedPrinter[(split+1)..]:_savedPrinter);
                queue.Refresh();Apply(queue,queue.UserPrintTicket??queue.DefaultPrintTicket,size);_restoredServer=server;return true;
            }
            catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException)
            {queue?.Dispose();server?.Dispose();AppDiagnostics.Log("Restore saved printer; ask for a printer instead",ex);}
        }
        return false;
    }
    public void SetWindowsDriverOnly(bool value,Size size)
    {
        var old=WindowsDriverOnly;WindowsDriverOnly=value;
        try{if(_queue!=null)Apply(_queue,_ticket,size);SavePreference();}
        catch{WindowsDriverOnly=old;throw;}
    }
    private void Apply(PrintQueue queue,PrintTicket? selected,Size size)
    {
        var requested=Request(size,selected);var driver=queue.QueueDriver.Name;
        var isZebra=ZebraZt411.IsDriver(driver);
        var resolution=selected?.PageResolution??queue.DefaultPrintTicket?.PageResolution;
        var zebra=isZebra&&!WindowsDriverOnly?ZebraZt411.Detect(driver,resolution?.X,resolution?.Y):null;
        PrinterReview review;PrintTicket ticket;
        var dimensions=$"{size.Width/LabelView.UnitsPerMm:0.##} × {size.Height/LabelView.UnitsPerMm:0.##} mm";
        if(isZebra&&!WindowsDriverOnly)
        {
            string? blocked=null;
            if(zebra==null)blocked="The ZT411 resolution could not be identified. Choose the matching 203, 300 or 600 dpi ZT411 driver in Printer setup.";
            else try{zebra.Pixels(size.Width/LabelView.UnitsPerMm,size.Height/LabelView.UnitsPerMm);}catch(FormatException ex){blocked=ex.Message;}
            ticket=requested; // RAW ZPL owns size, scale and quantity; the legacy EPL DEVMODE is not used.
            review=new PrinterReview($"{queue.FullName}\nZT411 direct ZPL · {dimensions} · {zebra?.DotsPerMm.ToString()??"unknown"} dots/mm\nPaper size comes from the sticker layout, not the Windows driver's paper preset. Load matching stock and keep the printer calibrated. Printer language must accept ZPL. No permanent configuration or sensing-mode changes are sent.",false,null,blocked,Array.Empty<string>()) {Zebra=zebra};
        }
        else
        {
            ticket=queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket,requested).ValidatedPrintTicket;
            PrintCapabilities? capabilities=null;
            try{capabilities=queue.GetPrintCapabilities(ticket);}catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or NotSupportedException or ArgumentException or FormatException){AppDiagnostics.Log("Printer has no usable printable-area report",ex);}
            review=PrinterReview.Inspect(queue.FullName,ticket,capabilities,size);
            if(!review.StockVerified && ticket.PageMediaSize?.Width!=null && ticket.PageMediaSize?.Height!=null)
                review=review with {BlockReason=$"Paper size does not match. In Printer setup, choose {dimensions} paper."+(isZebra?" Or turn off ‘Use Windows driver’ in Advanced to use direct ZT411 printing.":"")};
            if(isZebra && size.Width/LabelView.UnitsPerMm>104.00001)review=review with {BlockReason="This sticker is too wide for the ZT411. Set its width to 104 mm or less in Logo / sticker layout."};
        }
        review=review with {Summary=$"{queue.FullName} · {dimensions}"+(zebra!=null?" · ZT411":""),WindowsDriverOnly=WindowsDriverOnly};
        var retainedTicket=ticket.Clone();var retainedName=queue.FullName;
        if(_queue!=null && !ReferenceEquals(_queue,queue)){_queue.Dispose();_restoredServer?.Dispose();_restoredServer=null;}
        _queue=queue;_ticket=retainedTicket;Review=review;_size=size;_savedPrinter=retainedName;
    }
    private void SavePreference()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_preferencePath)!);
            File.WriteAllText(_preferencePath+".tmp",JsonSerializer.Serialize(new Preference(_savedPrinter??"",WindowsDriverOnly)));
            File.Move(_preferencePath+".tmp",_preferencePath,true);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppDiagnostics.Log("Remember printer",ex);}
    }
    public async Task<string> SendAsync(DocumentPaginator pages,CancellationToken cancellation,Action<int,int> progress,Action validateSnapshot,Action submitting)
    {
        if(_queue==null || _ticket==null || Review==null)throw new InvalidOperationException("Choose a printer in Printer setup.");
        if(_size!=pages.PageSize)throw new InvalidOperationException("Sticker size changed. Refresh the preview.");
        if(Review.BlockReason!=null)throw new InvalidOperationException(Review.BlockReason);
        if(pages.PageCount<1 || pages.PageCount>10000)throw new FormatException("Choose 1–10,000 preview pages.");
        var name=_queue.FullName;var review=Review;
        void CheckSnapshot(){cancellation.ThrowIfCancellationRequested();validateSnapshot();if(!ReferenceEquals(review,Review))throw new InvalidOperationException("Printer changed. Print again with the new settings.");}
        CheckSnapshot();
        if(review.Zebra is not {} zebra)
        {
            submitting();
            var dialog=new PrintDialog {PrintQueue=_queue,PrintTicket=_ticket.Clone(),UserPageRangeEnabled=false};
            dialog.PrintDocument(pages,"LABLE STUDIO stickers");return name;
        }
        var file=Path.Combine(Path.GetTempPath(),"LableStudio-"+Guid.NewGuid().ToString("N")+".zpl");
        var pageLengths=new List<long>(pages.PageCount);var previousEnd=0L;
        try
        {
            // Prepare the entire immutable selection BEFORE opening a spool job. One raster at a time, bounded disk use.
            using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(output,Encoding.ASCII,64*1024))
            {
                for(var i=0;i<pages.PageCount;i++)
                {
                    CheckSnapshot();using var page=pages.GetPage(i);
                    if(page.Visual is not LabelView view)throw new InvalidOperationException("The sticker preview could not be rendered.");
                    ZebraRasterizer.Render(view,zebra).WriteZpl(writer);writer.Flush();
                    if(output.Length>512L*1024*1024)throw new InvalidOperationException("This print batch is too large. Use Print range to send a smaller batch.");
                    pageLengths.Add(output.Position-previousEnd);previousEnd=output.Position;
                    progress(i+1,pages.PageCount);
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            CheckSnapshot();submitting();
            await Task.Run(()=>RawLabelPrinter.Send(name,file,pageLengths,cancellation));return name;
        }
        finally
        {
            try{File.Delete(file);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppDiagnostics.Log("Remove temporary print file",ex);}
        }
    }
    public void Dispose(){_queue?.Dispose();_restoredServer?.Dispose();_restoredServer=null;_queue=null;_ticket=null;Review=null;_size=null;}
}
