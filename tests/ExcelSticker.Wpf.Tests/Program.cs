using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;
using ExcelSticker.Wpf;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow();
            window.Measure(new Size(1460, 1000)); window.Arrange(new Rect(0, 0, 1460, 1000)); window.UpdateLayout();
            T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
            LabelView Preview() => (LabelView)Find<ContentControl>("PreviewHost").Content;
            var grid = Find<DataGrid>("BatchGrid");
            Require(grid.Items.Count == 0, "Batch starts empty");
            Require(!Find<Button>("PrintButton").IsEnabled, "Empty batch cannot print");
            Require(Preview().Label.Width == 2550 && Preview().Label.Qty == 4, "Example outer preview");
            Find<ComboBox>("PreviewPane").SelectedIndex = 1;
            Require(Preview().Label.Pane == "INNER" && Preview().Label.GlassType == "6MM CLEAR GLASS", "Inner material derived from spec");
            Find<TextBox>("W2").Text = "2540";
            Require(Find<ContentControl>("PreviewHost").Content == null, "Incomplete dimensions clear stale preview");
            Find<TextBox>("H2").Text = "2640";
            Find<TextBox>("InnerQty").Text = "2";
            Require(Preview().Label.Width == 2540 && Preview().Label.Height == 2640 && Preview().Label.Qty == 2, "Typing updates inner preview live");
            Find<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Find<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(grid.Items.Count == 2 && !ReferenceEquals(grid.Items[0], grid.Items[1]), "Duplicate entries have distinct identities");
            Require(window.GetPrintLabels().Count == 4, "Two double-glass rows yield four pane labels");
            Find<ComboBox>("PrintScope").SelectedIndex = 1;
            Find<ComboBox>("PrintLayers").SelectedIndex = 2;
            Require(window.GetPrintLabels().Single().Pane == "INNER", "Selected entry / inner-only printing");
            Find<CheckBox>("RepeatQuantity").IsChecked = true;
            Require(Find<TextBlock>("PrintSummary").Text.StartsWith("2 pages"), "Inner quantity controls copy count");

            var imported = EntryTable.ImportCsv("OrderNo,Spec,W1,H1,Qty,W2,H2,InnerQty,CustomerName\nEX-1,GREY + AIR + CLEAR,500,600,3,490,590,2,Customer A\nEX-2,GREY + AIR + CLEAR,700,800,5,690,790,4,Customer B");
            window.AppendEntries(imported, "test.xlsx · Sheet1");
            Require(Preview().Label.OrderNo == "EX-1" && Preview().Label.Width == 490, "First imported row previews immediately");
            grid.SelectedIndex = 3;
            Require(Preview().Label.OrderNo == "EX-2" && Preview().Label.Width == 690 && Preview().Label.Qty == 4, "Clicking another imported row switches live preview");
            Find<ComboBox>("PreviewPane").SelectedIndex = 0;
            Require(Preview().Label.Pane == "OUTER" && Preview().Label.Width == 700 && Preview().Label.Qty == 5, "Outer/inner switch follows selected entry");
            window.AppendEntries(EntryTable.ImportCsv("OrderNo,GlassType,Width,Height,Qty,CustomerName\nS1,CLEAR,100,70,1,C"), "single.csv");
            Require(!Find<ComboBoxItem>("InnerPreviewOption").IsEnabled, "Single-glass rows disable inner preview");
            Require(!Find<Button>("PrintButton").IsEnabled, "Inner-only scope disables printing for single glass");
            Find<ComboBox>("PrintLayers").SelectedIndex = 0;
            Require(Find<Button>("PrintButton").IsEnabled, "Both scope includes single-glass rows");

            var label = GlassLabel.Create("7494/71", "3", "6MM CLEAR LOW E", "W6* F5", "316", "800", "FUJSENG", "Job 1") with { Pane = "OUTER" };
            var paginator = new LabelPaginator(new[] { label, label }, true, new CompanyProfile());
            Require(paginator.PageCount == 6, "Quantity pagination");
            Require(new LabelPaginator(new[] { label }, false, new CompanyProfile()).PageCount == 1, "Default one page per pane");
            Require(paginator.GetPage(6) == DocumentPage.Missing, "Out of range page missing");
            var page = paginator.GetPage(0);
            Require(Math.Abs(page.Size.Width * 25.4 / 96 - 100) < 0.001, "Physical width 100 mm");
            Require(Math.Abs(page.Size.Height * 25.4 / 96 - 70) < 0.001, "Physical height 70 mm");
            var bitmap = new RenderTargetBitmap(378, 265, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page.Visual);
            Require(((FrameworkElement)page.Visual).ActualHeight > 264, "Label renders at native size");
            var longLabel = label with { GlassType = new string('W', 80), CustomerName = new string('W', 80) };
            bitmap.Render(new LabelPaginator(new[] { longLabel }, false, new CompanyProfile()).GetPage(0).Visual);
            Console.WriteLine("All Windows live-preview, batch, and layout smoke checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine($"PASS {name}");
    }
}
