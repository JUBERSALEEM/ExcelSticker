using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Wpf.Studio;

internal static class PrintPreviewChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static void Reject(Action action,string message)
    {
        try{action();}catch(Exception ex) when(ex is FormatException or ArgumentOutOfRangeException){Console.WriteLine("PASS "+message);return;}
        throw new Exception(message);
    }
    private static string[] Text(LabelView view)=>((Canvas)view.Child).Children.OfType<TextBlock>().Select(t=>t.Text).ToArray();
    public static void Run(MainWindow main)
    {
        T Find<T>(string name) where T:FrameworkElement=>(T)main.FindName(name);
        LabelView Live()=>(LabelView)Find<ContentControl>("PreviewHost").Content;
        var cutting=Find<CuttingWorkspace>("CuttingView");
        Find<Button>("OpenPrintPreviewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var samples=Find<PrintJobPreview>("JobPreview");
        Check(Find<Expander>("StickersSection").IsExpanded && main.GetPrintLabels().Count==0 && samples.Job?.PageCount==4 && samples.CanPrint,"An empty sheet prepares an explicitly marked sample job without manual or Excel entry");
        Check(Find<Button>("OpenDesignerButton")!=null && Find<Button>("ImportLogoButton")!=null,"Designer and company-logo entry points are explicitly available");
        Check(((TextBlock)samples.FindName("JobSummary")).Text.Contains("SAMPLE") && Find<StackPanel>("CuttingPrintOptions").Visibility==Visibility.Collapsed,"Sample jobs are identified and do not expose unrelated cutting entry filters");
        for(DependencyObject? parent=Find<Expander>("ManualEntrySection");parent!=null;parent=LogicalTreeHelper.GetParent(parent))
            Check(!ReferenceEquals(parent,Find<Expander>("StickersSection")),"Cutting-entry form is outside the sticker section");
        Check(Live().Label.OrderNo=="DEMO-SGU" && Live().Label.QuantityText=="1/1" && Find<TextBlock>("PreviewSource").Text.Contains("SAMPLE"),"An empty cutting sheet shows a clearly marked default SGU sticker");
        Check(Live().Label.GlassType=="6mm HD Grey Glass" && Live().Label.Width==300 && Live().Label.Height==300,"SGU sample uses the requested concise glass type and 300 × 300 glass dimensions");
        Check(samples.CurrentView!.Label==Live().Label && Text(samples.CurrentView).SequenceEqual(Text(Live())),"Sample live card and prepared page render the same record and text");
        Find<ComboBox>("LayoutFormat").SelectedIndex=3;
        Check(Live().Layout.Format==StickerFormat.DGU && Live().Label.Pane=="OUTER" && samples.CurrentView!.Layout.Format==StickerFormat.DGU && !samples.IsStale,"Selecting DGU format automatically refreshes both live and open job previews");
        Check(Live().Label.GlassType=="6mm HD Grey Glass + 12mm ASP + 6mm Clear Glass" && Text(samples.CurrentView!).Contains(LiveStickerSamples.DguType),"Full DGU assembly appears on the sample TYPE line without explanatory Thickness / Color / Airspacer words");
        Find<ComboBox>("PreviewPane").SelectedIndex=1;
        Find<Button>("NextCopyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Live().Label.Pane=="INNER" && Live().Label.Width==300 && Live().Label.Height==300 && Live().Label.Qty==2 && samples.CurrentIndex==5,"Live pane/copy navigation selects the exact matching sample job page");
        var selectedSample=main.PreparePrintPreview();
        Check(samples.CurrentIndex==5 && samples.CurrentView!.Label==Live().Label && selectedSample.LabelAt(5).QuantityText=="1/2","Preparing again does not reset the live inner copy to outer copy one");
        Check(selectedSample.PageSize==new Size(LabelView.LabelWidth,LabelView.LabelHeight),"300 × 300 sample glass does not turn physical paper into an unsupported 300 mm label");
        Find<CheckBox>("RepeatQuantity").IsChecked=false;
        Check(samples.Job!.PageCount==2 && samples.CurrentIndex==1 && samples.CurrentView!.Label==Live().Label,"Turning Qty expansion off keeps both previews on inner copy one");
        Check(selectedSample.PageCount==6 && selectedSample.LabelAt(5).QuantityText=="1/2","Refreshing sample options replaces, rather than mutates, the original snapshot");
        Find<CheckBox>("RepeatQuantity").IsChecked=true;
        Find<ComboBox>("LayoutFormat").SelectedIndex=1;
        Check(samples.CurrentView!.Layout.Format==StickerFormat.NormalJobOrder && Live().Layout.Format==StickerFormat.NormalJobOrder,"Normal Job Order format also stays synchronized for samples");
        Find<ComboBox>("LayoutFormat").SelectedIndex=2;
        Check(samples.Job!.PageCount==4 && samples.CurrentView!.Label.GlassType==LiveStickerSamples.SguType && Live().Label.Pane=="","Switching back to SGU changes the sample data as well as the layout");
        Find<Expander>("PrintPreviewSection").IsExpanded=false;
        Find<ComboBox>("LayoutFormat").SelectedIndex=3;
        Check(samples.IsStale && !Find<Expander>("PrintPreviewSection").IsExpanded,"Changing sample format does not force a collapsed preview open");
        Find<Expander>("PrintPreviewSection").IsExpanded=true;
        Check(!samples.IsStale && samples.CurrentView!.Label==Live().Label,"Reopening a stale sample preview accepts the current live format automatically");
        Find<ComboBox>("PreviewExample").SelectedIndex=2;
        Check(Find<ComboBox>("LayoutFormat").SelectedIndex==3 && samples.CurrentView!.Label==Live().Label,"Explicit DGU sample selection and the format selector cannot disagree");
        Check(!cutting.Sheet.ActiveRows.Any() && main.BatchRows.Count==0 && main.GetPrintLabels().Count==0 && Find<TextBox>("OrderNo").Text=="" && Find<TextBox>("CustomerName").Text=="" && Find<Button>("PrintButton").IsEnabled,"Standalone sample jobs never populate cutting data or production label selection");
        Find<ComboBox>("PreviewExample").SelectedIndex=0;
        var draft=cutting.Sheet.Rows.First();draft.W1="incomplete";cutting.SelectRow(draft);
        Check(samples.IsStale,"Switching from sample to incomplete cutting work invalidates the sample job");
        Find<ComboBox>("PreviewExample").SelectedIndex=2;
        Check(main.PreparePrintPreview().PageCount==6 && main.GetPrintLabels().Count==0 && draft.W1=="incomplete","Explicit samples do not require valid cutting rows or modify drafts");
        Find<ComboBox>("PreviewExample").SelectedIndex=0;
        Reject(()=>main.PreparePrintPreview(),"Incomplete real rows still cannot print as samples");
        draft.W1="";Find<ComboBox>("PreviewPane").SelectedIndex=0;

        var source=LiveStickerSamples.Dgu();
        var company=new CompanyProfile {Name="SNAPSHOT COMPANY"};var layouts=new LayoutLibrary();
        var labels=source.Labels.Select(l=>l with {EntryNumber=7}).ToArray();
        var job=new LabelPaginator(labels,true,company,layouts);
        Check(job.PageCount==6 && job.LabelAt(3).QuantityText=="7/4" && job.LabelAt(4).QuantityText=="7/1" && job.LabelAt(4).Pane=="INNER","Expanded preview order preserves outer and independent inner copy sequences");
        var initial=(LabelView)job.GetPage(0).Visual;
        company.Name="CHANGED";layouts.For(StickerFormat.DGU).Elements.Clear();labels[0]=labels[0] with {OrderNo="CHANGED"};
        Check(Text(initial).SequenceEqual(Text((LabelView)job.GetPage(0).Visual)) && Text(initial).Any(t=>t.Contains("SNAPSHOT COMPANY")),"Company, layouts and labels are snapshotted rather than read from mutable live settings");
        var current=job.SelectPages(3,1);var range=job.SelectPages(3,3);
        Check(current.PageCount==1 && ((LabelView)current.GetPage(0).Visual).Label.QuantityText=="7/4","Printing current page does not regenerate it as copy 1");
        Check(Enumerable.Range(0,3).Select(i=>((LabelView)range.GetPage(i).Visual).Label.QuantityText).SequenceEqual(new[]{"7/4","7/1","7/2"}),"A page range preserves copy numbers across the outer / inner boundary");
        Check(range.GetPage(-1)==DocumentPage.Missing && range.GetPage(3)==DocumentPage.Missing && job.GetPage(6)==DocumentPage.Missing,"Missing pages are not silently clamped or duplicated");
        foreach(var bad in new[]{(-1,1),(0,0),(5,2),(int.MaxValue,1),(0,int.MaxValue)})Reject(()=>job.SelectPages(bad.Item1,bad.Item2),"Invalid range is rejected: "+bad);
        current.PageSize=new Size(1,1);
        Check(current.PageSize==new Size(LabelView.LabelWidth,LabelView.LabelHeight),"Selected pages retain exact 100 × 70 mm geometry");
        Check(new LabelPaginator(source.Labels,false,new CompanyProfile()).PageCount==2,"Disabling quantity expansion prepares one sticker per pane");
        Check(new LabelPaginator(source.Labels,false,new CompanyProfile(),format:StickerFormat.NormalJobOrder).FormatAt(0)==StickerFormat.NormalJobOrder,"Explicit job-order format also uses the preview paginator");
        Reject(()=>new LabelPaginator(new[]{source.Outer with {Qty=10000},source.Inner!},true,new CompanyProfile()),"Preview expansion enforces the 10,000 page job limit");

        var preview=new PrintJobPreview();preview.LoadJob(job,"test scope");
        Check(preview.CanPrint && preview.CurrentView!.Label==job.LabelAt(0),"A prepared real job can print immediately; printer selection is deferred to first print");
        Check(!((Expander)preview.FindName("AdvancedPrint")).IsExpanded && ((CheckBox)preview.FindName("ShowBoundary")).IsChecked==false,"Technical diagnostics and driver boundary are off the normal print path");
        var review=new PrinterReview("Test printer",true,new Rect(15,15,300,200),null,Array.Empty<string>());
        preview.SetPrinter(review);preview.ShowPage(3);
        Check(preview.CanPrint && preview.CurrentView!.Label.QuantityText=="7/4","Navigation displays the exact expanded page");
        ((CheckBox)preview.FindName("ShowBoundary")).IsChecked=true;
        Check(((Canvas)preview.FindName("BoundaryOverlay")).Children.Count>0 && preview.WarningsFor(preview.CurrentView!).Any(w=>w.Contains("boundary")),"Driver printable-area guide and clipping warnings are screen-only siblings of the print page");
        var printed=(LabelView)job.GetPage(3).Visual;
        Check(Text(preview.CurrentView!).SequenceEqual(Text(printed)) && ((Canvas)printed.Child).Children.Count==((Canvas)preview.CurrentView!.Child).Children.Count,"Preview and print contain identical label fields without guide objects");
        var requests=new List<PreviewPrintRequestEventArgs>();preview.PrintRequested+=(_,e)=>requests.Add(e);
        void Click(string name)=>((Button)preview.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Click("CurrentButton");Click("AllButton");
        ((TextBox)preview.FindName("RangeFrom")).Text="4";((TextBox)preview.FindName("RangeTo")).Text="6";Click("RangeButton");
        Check(requests.Select(r=>(r.FirstPage,r.Count)).SequenceEqual(new[]{(3,1),(0,6),(3,3)}),"Current / all / inclusive range commands request the intended physical pages");
        Check(requests.Select(r=>r.Kind).SequenceEqual(new[]{PreviewPrintKind.Current,PreviewPrintKind.All,PreviewPrintKind.Range}),"Status can distinguish Print all from Print current even for a one-page batch");
        ((TextBox)preview.FindName("RangeFrom")).Text="0";Click("RangeButton");
        Check(requests.Count==3,"An invalid page range cannot issue a print request");
        preview.SetBusy(true);Click("CurrentButton");Check(!preview.CanPrint && requests.Count==3,"Printing in progress cannot issue duplicate jobs");preview.SetBusy(false);
        preview.Invalidate("test mutation");Click("AllButton");
        Check(!preview.CanPrint && requests.Count==3 && preview.CurrentView!.Label.QuantityText=="7/4","Stale snapshots remain visible but cannot be sent");
        preview.LoadJob(job,"test scope");Check(preview.CanPrint && !preview.IsStale,"Explicit refresh re-enables a prepared job");
        preview.SetPrinter(review with {BlockReason="Invalid scaling"});Check(!preview.CanPrint,"Unsafe driver settings disable print commands");
        ((ComboBox)preview.FindName("Zoom")).SelectedIndex=3;
        Check(job.PageSize.Width==LabelView.LabelWidth && ((LabelView)job.GetPage(0).Visual).LayoutTransform.Value.IsIdentity,"Screen zoom never changes print size or page transforms");

        var ticket=new PrintTicket {PageMediaSize=new PageMediaSize(LabelView.LabelWidth,LabelView.LabelHeight),PageOrientation=PageOrientation.Portrait,CopyCount=1,PagesPerSheet=1,PageScalingFactor=100,Duplexing=Duplexing.OneSided};
        var inspected=PrinterReview.Inspect("Test",ticket,null);
        Check(inspected.StockVerified && inspected.BlockReason==null && inspected.ImageableArea==null && inspected.Description.Contains("100% / actual size"),"Correct stock is recognized without inventing missing imageable margins");
        ticket.CopyCount=2;Check(PrinterReview.Inspect("Test",ticket,null).BlockReason!=null,"Driver copy multiplication is blocked");ticket.CopyCount=1;
        ticket.PagesPerSheet=2;Check(PrinterReview.Inspect("Test",ticket,null).BlockReason!=null,"Driver N-up is blocked");ticket.PagesPerSheet=1;
        ticket.PageScalingFactor=90;Check(PrinterReview.Inspect("Test",ticket,null).BlockReason!=null,"Non-100-percent driver scaling is blocked");ticket.PageScalingFactor=100;
        ticket.Duplexing=Duplexing.TwoSidedLongEdge;Check(PrinterReview.Inspect("Test",ticket,null).BlockReason!=null,"Duplex label output is blocked");ticket.Duplexing=Duplexing.OneSided;
        ticket.CopyCount=1;ticket.PagesPerSheet=1;ticket.PageScalingFactor=100;ticket.Duplexing=Duplexing.OneSided;
        ticket.PageMediaSize=new PageMediaSize(210*LabelView.UnitsPerMm,297*LabelView.UnitsPerMm);
        Check(PrinterReview.Inspect("A4 carrier",ticket,null).BlockReason==null,"Larger Windows paper is allowed at actual size");
        ticket.PageMediaSize=new PageMediaSize(104*LabelView.UnitsPerMm,50.8*LabelView.UnitsPerMm);
        var tooShort=PrinterReview.Inspect("Old preset",ticket,null);
        Check(tooShort.BlockReason!=null && tooShort.BlockReason.Contains("50.8") && tooShort.BlockReason.Contains("100 × 70"),"Too-small driver paper reports both dimensions and a concrete correction");
        ticket.PageMediaSize=new PageMediaSize(816,1056);ticket.PageScalingFactor=null;
        inspected=PrinterReview.Inspect("Test",ticket,null);Check(!inspected.StockVerified && inspected.Warnings.Count>=2,"Wrong media and unknown scaling need explicit warnings, not a false match");

        PrintTicket ScalingTicket(string option,string properties="",string parameters="")
        {
            var xml=$"<psf:PrintTicket xmlns:psf=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\" xmlns:psk=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" version=\"1\"><psf:Feature name=\"psk:PageScaling\"><psf:Option name=\"psk:{option}\">{properties}</psf:Option></psf:Feature>{parameters}</psf:PrintTicket>";
            using var stream=new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));return new PrintTicket(stream);
        }
        Check(PrinterReview.Inspect("Test",ScalingTicket("FitApplicationMediaSizeToPageMediaSize"),null).BlockReason!=null,"Standard Fit-to-page scaling cannot bypass the percentage safeguard");
        Check(!PrinterReview.Inspect("Test",ScalingTicket("None"),null).Warnings.Any(w=>w.Contains("scaling")),"Standard None scaling is recognized as actual size rather than falsely unknown");
        var custom=ScalingTicket("Custom","<psf:ScoredProperty name=\"psk:ScaleWidth\"><psf:ParameterRef name=\"psk:PageScalingScaleWidth\"/></psf:ScoredProperty><psf:ScoredProperty name=\"psk:ScaleHeight\"><psf:ParameterRef name=\"psk:PageScalingScaleHeight\"/></psf:ScoredProperty>","<psf:ParameterInit name=\"psk:PageScalingScaleWidth\"><psf:Value xsi:type=\"xsd:integer\">100</psf:Value></psf:ParameterInit><psf:ParameterInit name=\"psk:PageScalingScaleHeight\"><psf:Value xsi:type=\"xsd:integer\">90</psf:Value></psf:ParameterInit>");
        Check(custom.PageScalingFactor==100 && PrinterReview.Inspect("Test",custom,null).BlockReason!=null,"X=100 / Y=90 scaling is blocked despite the public factor reporting 100");

        var real=GlassEntry.Create("REAL-1","CLEAR","A1","Factory","","500","700","4",false);
        main.AppendEntries(new[]{real},"Print preview regression");
        for(var copy=1;copy<4;copy++)Find<Button>("NextCopyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var prepared=main.PreparePrintPreview();var inline=Find<PrintJobPreview>("JobPreview");
        Check(inline.CurrentIndex==3 && inline.CurrentView!.Label==Live().Label && Live().Label.CopyIndex==4,"Preparing a real cutting job also preserves the currently displayed live copy");
        Check(prepared.PageCount==4 && inline.Job==prepared && !Live().Label.OrderNo.StartsWith("DEMO") && Find<Expander>("PrintPreviewSection").IsExpanded,"Real selected rows replace samples and prepare an inline print snapshot");
        cutting.SelectRow(cutting.Sheet.Rows.First());Check(!inline.IsStale,"Selection alone does not stale an all-rows snapshot");
        cutting.Sheet.Rows.First().W1="510";
        Check(inline.IsStale && prepared.LabelAt(0).Width==500,"Inline cutting edits block the old job without mutating its reviewed contents");
        main.PreparePrintPreview();Find<ComboBox>("PrintScope").SelectedIndex=1;Check(inline.IsStale,"Scope changes invalidate the snapshot");
        main.PreparePrintPreview();cutting.SelectRow(cutting.Sheet.Rows.Skip(1).First());Check(inline.IsStale,"Changing the selected source row invalidates a selected-row job");
        cutting.SelectRow(cutting.Sheet.Rows.First());main.PreparePrintPreview();Find<CheckBox>("RepeatQuantity").IsChecked=false;Check(inline.IsStale,"Quantity mode changes invalidate the snapshot");
        main.PreparePrintPreview();Find<ComboBox>("LayoutFormat").SelectedIndex=1;Check(inline.IsStale,"Layout format changes invalidate the snapshot");
        main.PreparePrintPreview();Find<ComboBox>("PrintLayers").SelectedIndex=1;Check(inline.IsStale,"Pane filters invalidate the snapshot");
        main.PreparePrintPreview();Find<TextBox>("JobNo").Text="NEW-DEFAULT";Check(inline.IsStale,"Job metadata changes invalidate the snapshot");
        main.PreparePrintPreview();Find<Button>("EditEntryButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(inline.IsStale && !Find<Button>("PrintButton").IsEnabled,"Unsaved entry-details edits block prepared printing");
        Find<Button>("CancelEditButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        cutting.Sheet.Rows.First().W1="";cutting.SelectRow(cutting.Sheet.Rows.First());
        Check(Find<ContentControl>("PreviewHost").Content==null && main.GetPrintLabels().Count==0,"An incomplete real row is not disguised as a valid demonstration sticker");
        Find<TextBox>("JobNo").Text="";Find<ComboBox>("PrintLayers").SelectedIndex=0;
        Find<ComboBox>("PrintScope").SelectedIndex=0;Find<ComboBox>("LayoutFormat").SelectedIndex=0;Find<CheckBox>("RepeatQuantity").IsChecked=true;
        cutting.Sheet.Change(()=>{cutting.Sheet.Groups.Clear();cutting.Sheet.Groups.Add(CuttingGroup.Blank());});
        Find<Expander>("PrintPreviewSection").IsExpanded=false;Find<Expander>("ManualEntrySection").IsExpanded=false;Find<Expander>("StickersSection").IsExpanded=false;
        Check(Live().Label.OrderNo=="DEMO-SGU" && !cutting.Sheet.ActiveRows.Any(),"Clearing real data restores the default sample without creating work");
    }
}
