using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ExcelSticker.Wpf.Studio;

public sealed class PreviewPrintRequestEventArgs(int firstPage,int count):EventArgs
{
    public int FirstPage {get;}=firstPage;
    public int Count {get;}=count;
}

public partial class PrintJobPreview:UserControl
{
    public sealed record PageItem(int Index,string Caption);
    public LabelPaginator? Job {get;private set;}
    public int CurrentIndex {get;private set;}
    public bool IsStale {get;private set;}=true;
    public bool CanPrint=>!_busy && Job!=null && !IsStale && _printer!=null && _printer.BlockReason==null;
    public LabelView? CurrentView=>PageHost.Content as LabelView;
    public event EventHandler? RefreshRequested;
    public event EventHandler? PrinterRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler<PreviewPrintRequestEventArgs>? PrintRequested;
    private PrinterReview? _printer;
    private bool _loading;
    private bool _busy;
    public void SetBusy(bool value){_busy=value;UpdateButtons();}
    private bool _ready;
    public PrintJobPreview(){InitializeComponent();_ready=true;UpdateButtons();ApplyZoom();}
    public void LoadJob(LabelPaginator job,string summary)
    {
        if(job.PageCount==0)throw new FormatException("No sticker pages to preview.");
        if(Job!=null && Job.PageSize!=job.PageSize)ClearPrinter();
        PaperSize.Width=job.PageSize.Width;PaperSize.Height=job.PageSize.Height;
        Job=job;IsStale=false;CurrentIndex=0;_loading=true;
        try
        {
            Pages.ItemsSource=Enumerable.Range(0,job.PageCount).Select(i=>
            {
                var label=job.LabelAt(i);
                return new PageItem(i,$"Page {i+1:N0} · SR {label.EntryNumber} · {label.QuantityText}\n{(label.Pane.Length==0?"SINGLE":label.Pane)} · {label.ReferenceNo}\n{label.Size} mm · Order {label.OrderNo}");
            }).ToArray();
            Pages.SelectedIndex=0;
        }
        finally{_loading=false;}
        JobSummary.Text=$"{job.PageCount:N0} physical sticker pages · {summary}";
        Freshness.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
        Freshness.Text="Prepared snapshot · only these pages will be sent. Changes require Refresh.";
        RangeFrom.Text="1";RangeTo.Text=job.PageCount.ToString(CultureInfo.InvariantCulture);Feedback.Text="";ShowPage(0);
    }
    public void Invalidate(string reason)
    {
        if(Job==null)return;
        Freshness.SetResourceReference(TextBlock.ForegroundProperty,"ClayDanger");
        IsStale=true;Freshness.Text="OUT OF DATE · "+reason+" Refresh from cutting list before printing.";UpdateButtons();
    }
    public void ClearPrinter(){_printer=null;PrinterDetails.Text="Sticker size changed. Choose printer settings for this stock before printing.";DrawBoundary();UpdateButtons();}
    public void SetPrinter(PrinterReview review)
    {
        _printer=review;
        PrinterDetails.Text=review.Description+"\n"+string.Join(" ",review.Warnings)+(review.BlockReason==null?"":"\nBLOCKED: "+review.BlockReason);
        DrawBoundary();UpdateButtons();
    }
    public void ShowPage(int index)
    {
        if(Job==null || index<0 || index>=Job.PageCount)return;
        CurrentIndex=index;
        PageHost.Content=Job.GetPage(index).Visual; // The print paginator, not a separate mock-up/layout path.
        _loading=true;try{Pages.SelectedIndex=index;Pages.ScrollIntoView(Pages.SelectedItem);}finally{_loading=false;}
        var label=Job.LabelAt(index);Position.Text=$"{index+1:N0} / {Job.PageCount:N0}";GoToPage.Text=(index+1).ToString(CultureInfo.InvariantCulture);
        PageDetails.Text=$"{Job.PageSize.Width/LabelView.UnitsPerMm:0.##} × {Job.PageSize.Height/LabelView.UnitsPerMm:0.##} mm stock · {Job.FormatAt(index)} · SR NO {label.EntryNumber} · QTY {label.QuantityText} · {(label.Pane.Length==0?"SINGLE GLASS":label.Pane)} · {label.Size} mm";
        DrawBoundary();UpdateButtons();
    }
    public IReadOnlyList<string> WarningsFor(LabelView view)
    {
        var warnings=view.Warnings.ToList();
        if(_printer?.ImageableArea is Rect area)
        {
            var outside=view.Layout.Elements.Where(e=>e.Visible && !area.Contains(new Rect(e.X*LabelView.UnitsPerMm,e.Y*LabelView.UnitsPerMm,e.Width*LabelView.UnitsPerMm,e.Height*LabelView.UnitsPerMm))).Select(e=>e.Field).Distinct().ToArray();
            if(outside.Length>0)warnings.Add("Driver boundary may clip: "+string.Join(", ",outside.Take(6)));
        }
        return warnings;
    }
    private void DrawBoundary()
    {
        if(!_ready)return;
        BoundaryOverlay.Children.Clear();var warnings=CurrentView is {} current?WarningsFor(current):Array.Empty<string>();
        if(_printer?.ImageableArea is Rect area && CurrentView!=null)
        {
            if(ShowBoundary.IsChecked==true)
            {
                var paper=new Rect(new Point(0,0),Job!.PageSize);var inside=Rect.Intersect(paper,area);
                Geometry region=inside.IsEmpty?new RectangleGeometry(paper):new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(paper),new RectangleGeometry(inside));
                BoundaryOverlay.Children.Add(new System.Windows.Shapes.Path {Data=region,Fill=new SolidColorBrush(Color.FromArgb(40,200,35,35))});
                if(!inside.IsEmpty)
                {
                    var boundary=new Rectangle {Width=inside.Width,Height=inside.Height,Stroke=Brushes.Firebrick,StrokeThickness=.8,StrokeDashArray=new DoubleCollection {4,3}};
                    Canvas.SetLeft(boundary,inside.Left);Canvas.SetTop(boundary,inside.Top);BoundaryOverlay.Children.Add(boundary);
                }
            }
        }
        PageWarnings.Text=string.Join("\n",warnings.Take(5));
    }
    private void UpdateButtons()
    {
        if(!_ready)return;
        CancelButton.Visibility=_busy?Visibility.Visible:Visibility.Collapsed;
        RefreshButton.IsEnabled=PrinterButton.IsEnabled=!_busy;
        CurrentButton.IsEnabled=AllButton.IsEnabled=RangeButton.IsEnabled=CanPrint;
        Pages.IsEnabled=GoButton.IsEnabled=GoToPage.IsEnabled=RangeFrom.IsEnabled=RangeTo.IsEnabled=!_busy && Job!=null;
        FirstButton.IsEnabled=PreviousButton.IsEnabled=!_busy && Job!=null && CurrentIndex>0;
        NextButton.IsEnabled=LastButton.IsEnabled=!_busy && Job!=null && CurrentIndex<Job.PageCount-1;
        AllButton.Content=Job==null?"Print all preview pages":$"Print all preview pages ({Job.PageCount:N0})";
    }
    private void Request(int first,int count){if(CanPrint)PrintRequested?.Invoke(this,new PreviewPrintRequestEventArgs(first,count));}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>CancelRequested?.Invoke(this,EventArgs.Empty);
    private void Refresh_Click(object sender,RoutedEventArgs e)=>RefreshRequested?.Invoke(this,EventArgs.Empty);
    private void Printer_Click(object sender,RoutedEventArgs e)=>PrinterRequested?.Invoke(this,EventArgs.Empty);
    private void Current_Click(object sender,RoutedEventArgs e)=>Request(CurrentIndex,1);
    private void All_Click(object sender,RoutedEventArgs e){if(Job!=null)Request(0,Job.PageCount);}
    private void Range_Click(object sender,RoutedEventArgs e)
    {
        if(Job==null)return;
        if(!int.TryParse(RangeFrom.Text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out var first) || !int.TryParse(RangeTo.Text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out var last) || first<1 || last<first || last>Job.PageCount)
        {Feedback.Text=$"Enter a page range from 1 to {Job.PageCount}. Nothing was sent.";return;}
        Feedback.Text="";Request(first-1,last-first+1);
    }
    private void First_Click(object sender,RoutedEventArgs e)=>ShowPage(0);
    private void Previous_Click(object sender,RoutedEventArgs e)=>ShowPage(CurrentIndex-1);
    private void Next_Click(object sender,RoutedEventArgs e)=>ShowPage(CurrentIndex+1);
    private void Last_Click(object sender,RoutedEventArgs e){if(Job!=null)ShowPage(Job.PageCount-1);}
    private void Pages_Changed(object sender,SelectionChangedEventArgs e){if(!_loading && Pages.SelectedItem is PageItem page)ShowPage(page.Index);}
    private void Go_Click(object sender,RoutedEventArgs e)
    {
        if(Job!=null && int.TryParse(GoToPage.Text.Trim(),out var page) && page>=1 && page<=Job.PageCount){Feedback.Text="";ShowPage(page-1);}
        else Feedback.Text="Enter a valid preview page number.";
    }
    private void ApplyZoom()
    {
        if(!_ready)return;
        var factor=new[]{1d,1.5,2,3}[Math.Clamp(Zoom.SelectedIndex,0,3)];
        PaperZoom.LayoutTransform=new ScaleTransform(factor,factor);
    }
    private void Zoom_Changed(object sender,SelectionChangedEventArgs e)=>ApplyZoom();
    private void Boundary_Changed(object sender,RoutedEventArgs e)=>DrawBoundary();
}
