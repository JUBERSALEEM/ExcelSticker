using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Theming;
using ExcelSticker.Wpf.Studio;
using System.IO;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var oldSettings = Environment.GetEnvironmentVariable("OCEANGLASS_DATA_DIR");
        var testSettings = Path.Combine(Path.GetTempPath(), "oceanglass-ui-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("OCEANGLASS_DATA_DIR", testSettings);
        try
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow(new CompanyProfile());
            ShowForTest(window, 1460, 1000);
            T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
            LabelView Preview() => (LabelView)Find<ContentControl>("PreviewHost").Content;
            var grid = Find<DataGrid>("BatchGrid");
            Require(Preview().Label.QuantityText == "1/1", "Numbered preview is enabled by default");
            Require(Find<CheckBox>("RepeatQuantity").IsChecked == true, "Quantity copies are on by default");
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
            Require(((LabelView)paginator.GetPage(2).Visual).Label.QuantityText == "1/3", "Paginator creates a distinct numbered copy");
            var changed = new LayoutLibrary();
            changed.For(StickerFormat.NormalJobOrder).Elements[0].X = 5;
            var customPaginator = new LabelPaginator(new[] { label }, false, new CompanyProfile(), changed, StickerFormat.NormalJobOrder);
            changed.For(StickerFormat.NormalJobOrder).Elements[0].X = 6;
            Require(((LabelView)customPaginator.GetPage(0).Visual).Layout.Elements[0].X == 5, "Print jobs snapshot saved formats");
            Require(Math.Abs(page.Size.Width * 25.4 / 96 - 100) < 0.001, "Physical width 100 mm");
            Require(Math.Abs(page.Size.Height * 25.4 / 96 - 70) < 0.001, "Physical height 70 mm");
            var bitmap = new RenderTargetBitmap(378, 265, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page.Visual);
            Require(((FrameworkElement)page.Visual).ActualHeight > 264, "Label renders at native size");
            var longLabel = label with { GlassType = new string('W', 80), CustomerName = new string('W', 80) };
            bitmap.Render(new LabelPaginator(new[] { longLabel }, false, new CompanyProfile()).GetPage(0).Visual);
            foreach (var preset in ThemeTokens.Presets)
            {
                preset.Validate(); Require(ThemeEngine.ContrastPasses(preset), preset.Name + " text contrast passes");
                Require(ThemeEngine.EdgeContrast(preset) >= (preset.Name == "High contrast" ? 3 : 1), preset.Name + " rendered rim contrast is reported without ignoring opacity");
            }
            var store = Path.Combine(Path.GetTempPath(), "clay-tests-" + Guid.NewGuid().ToString("N"), "theme.json");
            try
            {
                var resources = new ResourceDictionary(); var engine = new ThemeEngine(resources, store);
                var theme = ThemeTokens.Presets.First(t => t.Name == "Ocean");
                theme.TopLeft = 31; theme.BorderWidth = 2; engine.Apply(theme);
                Require(((CornerRadius)resources["ClayCardRadius"]).TopLeft == 31, "Theme corner tokens update resources");
                Require((double)resources["ClayBorder"] == 2, "Border width is dynamic");
                engine.Save(theme); Require(ThemeEngine.Read(store).TopLeft == 31, "Theme JSON round trip");
                var studio = new ThemeStudioWindow(engine);
                ShowForTest(studio, 1120, 840);
                Require(Descendants(studio).OfType<Slider>().Any(), "Theme Studio creates live token controls");
                studio.Close();
                var editor = new StickerEditorWindow(new LayoutLibrary(), new CompanyProfile(), Path.Combine(Path.GetDirectoryName(store)!, "layouts.json"));
                ShowForTest(editor, 1380, 900);
                Require(Descendants(editor).OfType<LabelView>().Any(), "Sticker Studio creates its real label renderer");
                editor.Close();
                engine.Apply(ThemeTokens.Presets.First(t => t.Name == "High contrast"));
                Require(resources["ClayShadow"] == null && (double)resources["ClayInset"] == 0, "Reduced effects remove shadow and inset rendering");
                File.WriteAllText(store, "invalid json");
                Require(new ThemeEngine(new ResourceDictionary(), store).LoadWarning != null, "Corrupt theme safely falls back");
            }
            finally { if (Directory.Exists(Path.GetDirectoryName(store)!)) Directory.Delete(Path.GetDirectoryName(store)!, true); }
            ThemeRegressionChecks.Run(window, testSettings);
            MaterialRegressionChecks.Run(window, testSettings);
            // Effects must only be attached to leaf decorative chrome, never to text or content ancestors.
            var button = Find<Button>("AddButton"); button.ApplyTemplate();
            Require(button.InputHitTest(new Point(3, button.ActualHeight / 2)) != null, "Button padding is hit-testable");
            var importPreview = new ImportPreviewWindow(ImportMapping.ReadCsv("OrderNo,Spec,W1,H1,Qty,CustomerName\nA,CLEAR,100,70,4,C", out _), "test.csv");
            ShowForTest(importPreview, 1260, 840);
            Require(importPreview.Entries?.Count == 1, "Import preview validates a standard file before confirmation");
            var importButton = Descendants(importPreview).OfType<Button>().First(b => b.Content is string content && content.StartsWith("Import 1"));
            Require(importButton.IsEnabled && importButton.InputHitTest(new Point(3, importButton.ActualHeight / 2)) != null, "Import button responds outside the text glyphs");
            var scrollThumb = Descendants(importPreview).OfType<ScrollBar>().SelectMany(Descendants).OfType<Thumb>().FirstOrDefault(t => t.ActualWidth > 0 && t.ActualHeight > 0);
            Require(scrollThumb != null && scrollThumb.InputHitTest(new Point(scrollThumb.ActualWidth / 2, scrollThumb.ActualHeight / 2)) != null, "Scrollbar thumb center is hit-testable");
            importPreview.Close();
            var card = Ancestors(button).OfType<ClayCard>().First(); card.ApplyTemplate();
            Require(card.Effect == null && button.Effect == null, "Card/button effects are not on content containers");
            Require(Descendants(button).OfType<ClayChrome>().Any(c => c.Effect != null), "Clay shadow remains on decorative chrome");
            foreach (var text in Descendants(button).OfType<TextBlock>())
                Require(Ancestors(text).OfType<UIElement>().All(parent => parent.Effect == null), "Text has no effect-bearing ancestor");
            Require(!Ancestors(Find<ContentControl>("PreviewHost")).OfType<Viewbox>().Any(), "Sticker preview no longer uses fractional Viewbox scaling");
            Console.WriteLine("All Windows preview, numbering, theme, and layout smoke checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable("OCEANGLASS_DATA_DIR", oldSettings);
            try { if (Directory.Exists(testSettings)) Directory.Delete(testSettings, true); } catch (IOException) { }
        }
    }
    private static void ShowForTest(Window window, double width, double height)
    {
        // A collapsed, never-shown Window does not perform a real control-template layout.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowInTaskbar = false; window.Left = -10000; window.Top = -10000;
        window.Width = width; window.Height = height; window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject item)
    {
        while ((item = VisualTreeHelper.GetParent(item)) != null) yield return item;
    }
    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine($"PASS {name}");
    }
}
