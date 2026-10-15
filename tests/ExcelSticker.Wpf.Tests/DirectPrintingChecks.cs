using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Wpf.Studio;

internal static class DirectPrintingChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run(MainWindow main)
    {
        T Find<T>(string name) where T:FrameworkElement=>(T)main.FindName(name);
        var preview=Find<PrintJobPreview>("JobPreview");
        Check(preview.Job==null,"Direct print tests start without a prepared page");
        Check(Find<Button>("PrintLiveButton").Content.ToString()=="Print sticker" && Find<ComboBox>("PrinterPicker")!=null,"A direct live print button and explicit printer selector are present");
        Find<Button>("NextCopyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var live=(LabelView)Find<ContentControl>("PreviewHost").Content;
        var pages=main.CaptureLiveSticker();using var page=pages.GetPage(0);var printed=(LabelView)page.Visual;
        Check(pages.PageCount==1 && printed.Label==live.Label && printed.Label.CopyIndex==2,"Direct printing captures exactly one live sticker without restarting its copy index");
        Check(pages.GetPage(1)==DocumentPage.Missing && pages.GetPage(-1)==DocumentPage.Missing,"Single-sticker output cannot expand into quantity copies");
        Check(preview.Job==null && !Find<Expander>("PrintPreviewSection").IsExpanded,"Capturing the direct print does not create/open a batch preview");
        var batch=main.CapturePrintBatch();Check(batch.PageCount==4 && preview.Job==null,"Print all captures quantity-expanded data without a preview prerequisite");
        Find<Button>("PreviousCopyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((LabelView)pages.GetPage(0).Visual).Label.CopyIndex==2,"Later live navigation cannot mutate the already-captured sticker");
        var company=new CompanyProfile {Name="ORIGINAL"};var model=LiveStickerSamples.Dgu().Inner! with {EntryNumber=7,CopyIndex=2};
        var layout=StickerLayout.Default(StickerFormat.DGU);var view=new LabelView(model,company,layout);var snapshot=new LiveStickerPaginator(view,company);
        company.Name="CHANGED";view.Layout.Elements.Clear();
        using var saved=snapshot.GetPage(0);var content=(LabelView)saved.Visual;
        Check(content.Label.QuantityText=="7/2" && content.Layout.Elements.Count>0 && content.Width==LabelView.LabelWidth,"Direct output snapshots layout/company and preserves independent inner numbering");
        Check(main.GetPrintLabels().Count==0 && !Find<CuttingWorkspace>("CuttingView").Sheet.ActiveRows.Any(),"Direct sample jobs never enter production cutting data");
        // Deliberately no print-button invocation, network probe or spool submission in these checks.
    }
}
