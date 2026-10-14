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
            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow();
            window.Measure(new Size(1220, 920));
            window.Arrange(new Rect(0, 0, 1220, 920));
            window.UpdateLayout();
            T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
            var grid = Find<DataGrid>("BatchGrid");
            Require(grid.Items.Count == 0, "Batch starts empty");
            Require(!Find<Button>("PrintButton").IsEnabled, "Empty batch cannot be printed");
            Find<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Find<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(grid.Items.Count == 2, "Identical rows can both be added");
            Require(!ReferenceEquals(grid.Items[0], grid.Items[1]), "Duplicate rows have distinct identities");
            Require(Find<Button>("PrintButton").IsEnabled, "Nonempty batch enables printing");

            var label = GlassLabel.Create("7494/71", "3", "6MM CLEAR LOW E", "W6* F5", "316", "800", "FUJSENG", "Job 1");
            var paginator = new LabelPaginator(new[] { label, label }, true, new CompanyProfile());
            Require(paginator.PageCount == 6, "Quantity copy count");
            Require(new LabelPaginator(new[] { label }, false, new CompanyProfile()).PageCount == 1, "Default one page per row");
            Require(paginator.GetPage(6) == DocumentPage.Missing, "Out of range pages are missing");
            var page = paginator.GetPage(0);
            Require(Math.Abs(page.Size.Width * 25.4 / 96 - 100) < 0.001, "Physical width is 100 mm");
            Require(Math.Abs(page.Size.Height * 25.4 / 96 - 70) < 0.001, "Physical height is 70 mm");
            var bitmap = new RenderTargetBitmap(378, 265, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page.Visual);
            Require(((FrameworkElement)page.Visual).ActualHeight > 264, "Label renders at native size");
            var longLabel = label with { GlassType = new string('W', 80), CustomerName = new string('W', 80) };
            var longPage = new LabelPaginator(new[] { longLabel }, false, new CompanyProfile()).GetPage(0);
            bitmap.Render(longPage.Visual);
            Console.WriteLine("All Windows UI/layout smoke checks passed.");
            // Do not close the populated window: close intentionally prompts about unsaved data.
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
