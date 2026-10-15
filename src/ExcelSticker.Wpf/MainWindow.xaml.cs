using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExcelSticker.Core;
using Microsoft.Win32;
using ExcelSticker.Theming;
using ExcelSticker.Wpf.Studio;

namespace ExcelSticker.Wpf;

public partial class MainWindow : Window
{
    // Wrapper identity keeps duplicate Excel rows independently editable/removable.
    public sealed class BatchRow(GlassEntry entry, string source = "Manual entry")
    {
        public GlassEntry Entry { get; } = entry;
        public string Source { get; } = source;
        public string OrderNo => Entry.Outer.OrderNo;
        public string Specification => Entry.Specification;
        public string OuterSize => Entry.Outer.Size;
        public string InnerSize => Entry.Inner?.Size ?? "Single glass";
        public string CustomerName => Entry.Outer.CustomerName;
        public string Quantities => $"{Entry.Outer.Qty} / {Entry.Inner?.Qty.ToString(CultureInfo.InvariantCulture) ?? "—"}";
    }

    private readonly ObservableCollection<BatchRow> _batch = new();
    private readonly CompanyProfile _company;
    private BatchRow? _editing;
    private GlassEntry? _previewEntry;
    private LayoutLibrary _layouts = new();
    private int _previewCopy = 1;
    private BatchRow? _previewRow;
    private StickerFormat? FormatOverride => LayoutFormat.SelectedIndex switch { 1 => StickerFormat.NormalJobOrder, 2 => StickerFormat.SGU, 3 => StickerFormat.DGU, _ => null };
    private NumberingStyle NumberStyle => (NumberingStyle)Math.Max(0, Numbering.SelectedIndex);
    private bool _ready;
    private bool _loadingForm;

    public MainWindow() : this(null) { }

    public MainWindow(CompanyProfile? companyOverride)
    {
        _ = StudioServices.Theme;
        InitializeComponent();
        Title = "Ocean Glass Label Studio — " + AppBuildInfo.Short;
        ThemeState.Text = StudioServices.Theme.Current.Name + " · Active theme";
        StudioServices.Theme.Changed += Theme_Changed;
        Closed += (_,_) => StudioServices.Theme.Changed -= Theme_Changed;
        BuildInfoButton.Content = AppBuildInfo.Short + " · diagnostics";
        try { _company = companyOverride ?? CompanyProfile.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException)
        {
            _company = new CompanyProfile();
            StudioDialog.Error(this, $"Could not load company.json. Using sample details.\n\n{ex.Message}");
        }
        string? settingsWarning = StudioServices.Theme.LoadWarning;
        try { _layouts = LayoutLibrary.Load(StudioServices.LayoutPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException)
        { settingsWarning = "Saved layouts could not be loaded; using defaults. " + ex.Message; }
        WindowPlacement.FitOnFirstShow(this);
        SizeChanged += (_, _) =>
        {
            if (!_ready) return;
            var compact = ActualWidth < 1200;
            Grid.SetColumnSpan(OuterFields, compact ? 2 : 1);
            Grid.SetColumnSpan(InnerFields, compact ? 2 : 1);
            Grid.SetColumn(InnerFields, compact ? 0 : 1);
            Grid.SetRow(InnerFields, compact ? 1 : 0);
            OuterFields.Margin = compact ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 5, 8);
            InnerFields.Margin = compact ? new Thickness(0, 0, 0, 8) : new Thickness(5, 0, 0, 8);
        };
        BatchGrid.ItemsSource = _batch;
        _ready = true;
        PreviewForm();
        RefreshBatch();
        Status.Text = settingsWarning ?? "Ready. Theme Studio customizes the workspace; Sticker Editor customizes Normal / SGU / DGU layouts. Batch data stays in memory only.";
    }

    private GlassEntry ReadForm() => GlassEntry.Create(OrderNo.Text, Specification.Text, ReferenceNo.Text, CustomerName.Text,
        JobNo.Text, W1.Text, H1.Text, OuterQty.Text, DoubleGlazed.IsChecked == true, W2.Text, H2.Text, InnerQty.Text, OuterGlass.Text, InnerGlass.Text);
    private void Error(Exception ex) { AppDiagnostics.Log("Handled error", ex); StudioDialog.Error(this, ex.Message); }
    private bool Confirm(string text) => StudioDialog.Confirm(this, "Please confirm", text);

    private void Form_Changed(object sender, TextChangedEventArgs e) { if (_ready && !_loadingForm) PreviewForm(); }
    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready || _loadingForm) return;
        InnerFields.IsEnabled = DoubleGlazed.IsChecked == true;
        PreviewForm();
    }
    private void PreviewForm()
    {
        _previewRow = null;
        PreviewSource.Text = _editing == null ? "Live form · not saved to batch" : $"Editing order {_editing.OrderNo} · unsaved form";
        try
        {
            _previewEntry = ReadForm();
            ValidationMessage.Text = "";
            RenderPreview();
        }
        catch (FormatException ex)
        {
            _previewEntry = null;
            PreviewHost.Content = null;
            CopyPreviewText.Text = "";
            PreviewDetails.Text = "Complete the entry to see its sticker.";
            ValidationMessage.Text = ex.Message;
        }
    }
    private void ShowEntry(BatchRow row)
    {
        _previewRow = row;
        _previewCopy = 1;
        _previewEntry = row.Entry;
        ValidationMessage.Text = "";
        PreviewSource.Text = $"Selected order {row.OrderNo} · {row.Source}";
        RenderPreview();
    }
    private void RenderPreview()
    {
        if (_previewEntry == null) return;
        InnerPreviewOption.IsEnabled = _previewEntry.IsDouble;
        if (!_previewEntry.IsDouble && PreviewPane.SelectedIndex == 1) PreviewPane.SelectedIndex = 0;
        var label = PreviewPane.SelectedIndex == 1 ? _previewEntry.Inner! : _previewEntry.Outer;
        _previewCopy = Math.Clamp(_previewCopy, 1, label.Qty);
        var rowNumber = _previewRow != null ? _batch.IndexOf(_previewRow) + 1 : _editing != null ? _batch.IndexOf(_editing) + 1 : _batch.Count + 1;
        label = label with { EntryNumber = Math.Max(1, rowNumber), CopyIndex = _previewCopy, Numbering = NumberStyle };
        var view = new LabelView(label, _company, _layouts.For(FormatOverride ?? LayoutLibrary.FormatFor(label)));
        PreviewHost.Content = view;
        CopyPreviewText.Text = $"Copy {_previewCopy} of {label.Qty} · {label.QuantityText}";
        ValidationMessage.Text = string.Join("\n", view.Warnings.Take(3));
        PreviewDetails.Text = $"{(string.IsNullOrEmpty(label.Pane) ? "SINGLE GLASS" : label.Pane)}  ·  {label.Size} mm  ·  Qty {label.Qty}\n{label.GlassType}";
    }
    private void PreviewPane_Changed(object sender, SelectionChangedEventArgs e) { if (_ready) RenderPreview(); }
    private void Preview_Click(object sender, RoutedEventArgs e) => PreviewForm();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var row = new BatchRow(ReadForm(), _editing?.Source ?? "Manual entry");
            var editing = _editing != null;
            if (_editing != null) _batch[_batch.IndexOf(_editing)] = row;
            else
            {
                if (_batch.Count >= LabelCsv.MaxLabels) throw new FormatException("A batch can contain at most 10,000 entries.");
                _batch.Add(row);
            }
            EndEdit();
            BatchGrid.SelectedItem = row;
            BatchGrid.ScrollIntoView(row);
            ShowEntry(row);
            RefreshBatch();
            Status.Text = editing ? "Entry and its pane labels updated." : $"Entry added with {(row.Entry.IsDouble ? "outer + inner stickers" : "one sticker")}. Select a row to preview it.";
        }
        catch (FormatException ex) { Error(ex); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BatchRow row) return;
        if (_editing != null && !Confirm("Discard the current unsaved edit and edit this entry?")) return;
        _editing = row;
        var entry = row.Entry;
        var outer = entry.Outer;
        _loadingForm = true;
        try
        {
            OrderNo.Text = outer.OrderNo; Specification.Text = entry.Specification;
            ReferenceNo.Text = outer.ReferenceNo; CustomerName.Text = outer.CustomerName; JobNo.Text = outer.JobNo;
            W1.Text = Number(outer.Width); H1.Text = Number(outer.Height); OuterQty.Text = Number(outer.Qty);
            DoubleGlazed.IsChecked = entry.IsDouble;
            W2.Text = entry.Inner is { } inner ? Number(inner.Width) : "";
            H2.Text = entry.Inner is { } innerH ? Number(innerH.Height) : "";
            InnerQty.Text = entry.Inner is { } innerQ ? Number(innerQ.Qty) : "";
            // Only populate overrides when they differ from the auto-derived spec, so editing Spec stays live.
            var parts = entry.Specification.Split('+', StringSplitOptions.TrimEntries);
            OuterGlass.Text = outer.GlassType == (entry.IsDouble && parts.Length == 3 ? parts[0] : entry.Specification) ? "" : outer.GlassType;
            InnerGlass.Text = entry.Inner == null || (parts.Length == 3 && entry.Inner.GlassType == parts[2]) ? "" : entry.Inner.GlassType;
            InnerFields.IsEnabled = entry.IsDouble;
        }
        finally { _loadingForm = false; }
        AddButton.Content = "Save changes";
        FormTitle.Text = "01   Edit glass entry";
        CancelEditButton.Visibility = Visibility.Visible;
        BatchGrid.SelectedItem = row;
        PreviewForm();
        OrderNo.Focus();
        Status.Text = "Editing an entry. Typing updates the preview; Save changes updates the batch.";
    }
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private void EndEdit()
    {
        _editing = null;
        AddButton.Content = "+  Add to batch";
        FormTitle.Text = "01   Compose your glass entry";
        CancelEditButton.Visibility = Visibility.Collapsed;
    }
    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        var row = _editing;
        EndEdit();
        if (row != null) ShowEntry(row);
        Status.Text = "Edit cancelled; batch unchanged. Form values remain available for a new entry.";
    }
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BatchRow row || !Confirm($"Remove order {row.OrderNo} and its pane labels?")) return;
        if (ReferenceEquals(_editing, row)) EndEdit();
        _batch.Remove(row);
        if (BatchGrid.SelectedItem is BatchRow selected) ShowEntry(selected); else PreviewForm();
        RefreshBatch();
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_batch.Count == 0 || !Confirm("Clear every batch entry? This cannot be undone.")) return;
        EndEdit(); _batch.Clear(); PreviewForm(); RefreshBatch();
        Status.Text = "Batch cleared. Form values retained.";
    }
    private void Batch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (BatchGrid.SelectedItem is BatchRow row) ShowEntry(row);
        RefreshBatch();
    }

    public IReadOnlyList<GlassLabel> GetPrintLabels()
    {
        var rows = _batch.Select((row, index) => (Row: row, Number: index + 1));
        if (PrintScope.SelectedIndex == 1) rows = rows.Where(item => ReferenceEquals(item.Row, BatchGrid.SelectedItem));
        return rows.SelectMany(item =>
        {
            IEnumerable<GlassLabel> labels = PrintLayers.SelectedIndex switch
            {
                1 => new[] { item.Row.Entry.Outer },
                2 => item.Row.Entry.Inner is { } inner ? new[] { inner } : Array.Empty<GlassLabel>(),
                _ => item.Row.Entry.Labels
            };
            return labels.Select(label => label with { EntryNumber = item.Number });
        }).ToArray();
    }
    private void RefreshBatch()
    {
        if (!_ready) return;
        BatchTitle.Text = $"{_batch.Count:N0} entries  ·  {_batch.Sum(row => row.Entry.IsDouble ? 2 : 1):N0} panes";
        EmptyHint.Visibility = _batch.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var labels = GetPrintLabels();
        var pages = PageCount(labels);
        PrintSummary.Text = $"{pages:N0} pages ready  ·  {labels.Count:N0} pane labels";
        PrintButton.IsEnabled = pages > 0;
    }
    private long PageCount(IReadOnlyList<GlassLabel> labels) => RepeatQuantity.IsChecked == true ? labels.Sum(label => (long)label.Qty) : labels.Count;
    private void PrintOptions_Changed(object sender, SelectionChangedEventArgs e) { if (_ready) { RefreshBatch(); RenderPreview(); } }
    private void Repeat_Changed(object sender, RoutedEventArgs e) { if (_ready) RefreshBatch(); }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel / CSV / TSV (*.xlsx;*.csv;*.tsv)|*.xlsx;*.csv;*.tsv|Excel workbook (*.xlsx)|*.xlsx|CSV UTF-8 (*.csv)|*.csv|Tab-separated (*.tsv)|*.tsv", Title = "Import glass entries" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new FormatException("Files must be 10 MB or smaller.");
            IReadOnlyList<string[]> rows;
            var source = Path.GetFileName(dialog.FileName);
            if (Path.GetExtension(dialog.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var workbook = new XlsxWorkbook(stream);
                var sheet = workbook.SheetNames.Count == 1 ? workbook.SheetNames[0] : StudioDialog.ChooseSheet(this, workbook.SheetNames);
                if (sheet == null) return;
                rows = workbook.ReadTable(sheet);
                source += $" · {sheet}";
            }
            else if (Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(dialog.FileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                rows = ImportMapping.ReadCsv(File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true)), out var delimiter);
                source += delimiter == ';' ? " · semicolon CSV" : delimiter == '\t' ? " · tab-separated" : " · comma CSV";
            }
            else throw new FormatException("Choose .xlsx, UTF-8 .csv or .tsv. Convert legacy .xls files to .xlsx first.");
            var preview = new ImportPreviewWindow(rows, source) { Owner = this };
            if (preview.ShowDialog() != true || preview.Entries is not { } entries) { Status.Text = "Import cancelled. Batch unchanged."; return; }
            AppendEntries(entries, source);
            Status.Text = $"Imported {entries.Count} entries from {source}. Click a row, then choose Outer or Inner in Live sticker.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or DecoderFallbackException or System.Xml.XmlException or ArgumentException or OverflowException or InvalidOperationException)
        {
            var reason = ex is DecoderFallbackException ? "This CSV is not valid UTF-8. Save As → CSV UTF-8 in Excel, then retry." : ex.Message;
            Status.Text = "Import failed; batch unchanged. " + reason;
            Error(new FormatException(reason + "\n\nSave the workbook in Excel before retrying. If it still fails, share a redacted sample and the diagnostic log: " + AppDiagnostics.LogPath, ex));
        }
    }

    public void AppendEntries(IReadOnlyList<GlassEntry> entries, string source)
    {
        if (_batch.Count + entries.Count > LabelCsv.MaxLabels) throw new FormatException("The combined batch exceeds 10,000 entries. Nothing was imported.");
        if (entries.Count == 0) return;
        var first = _batch.Count;
        foreach (var entry in entries) _batch.Add(new BatchRow(entry, source));
        BatchGrid.SelectedItem = _batch[first];
        BatchGrid.ScrollIntoView(_batch[first]);
        RefreshBatch();
    }
    private void Template_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "Ocean_Glass_Outer_Inner_Template.csv", DefaultExt = ".csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, EntryTable.Header + "\r\n", new UTF8Encoding(true)); Status.Text = $"Template saved: {dialog.FileName}. Open it in Excel and save as .xlsx or CSV UTF-8."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error(ex); }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var labels = GetPrintLabels();
        if (labels.Count == 0) return;
        if (_editing != null) { Error(new FormatException("Save or cancel the current edit before printing.")); return; }
        var pages = PageCount(labels);
        if (pages > 10000) { Error(new FormatException("A print job is limited to 10,000 pages. Use selected entry, reduce the batch, or turn off Qty copies.")); return; }
        var layoutWarnings = labels.SelectMany(label => new LabelView(label with { CopyIndex = label.Qty, Numbering = NumberStyle }, _company,
            _layouts.For(FormatOverride ?? LayoutLibrary.FormatFor(label))).Warnings).Distinct().Take(4).ToArray();
        if (layoutWarnings.Length > 0 && !Confirm("Some sticker fields may be clipped. Open Sticker Editor to fix them, or continue deliberately:\n\n" + string.Join("\n", layoutWarnings))) return;
        if (!Confirm($"Print {pages:N0} page(s) from {labels.Count:N0} chosen pane labels?\n\nOnly saved batch values are printed. Set driver Copies to 1. Test a selected entry before sending a full batch.")) return;
        try
        {
            var dialog = new PrintDialog { UserPageRangeEnabled = false, SelectedPagesEnabled = false, CurrentPageEnabled = false };
            dialog.PrintTicket.PageMediaSize = new PageMediaSize(LabelView.LabelWidth, LabelView.LabelHeight);
            dialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
            dialog.PrintTicket.CopyCount = 1;
            if (dialog.ShowDialog() != true) return;
            var ticket = dialog.PrintQueue.MergeAndValidatePrintTicket(dialog.PrintQueue.DefaultPrintTicket, dialog.PrintTicket).ValidatedPrintTicket;
            dialog.PrintTicket = ticket;
            var stock = ticket.PageMediaSize;
            var landscape = ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape;
            if (!PrintStock.MatchesLabel(stock?.Width, stock?.Height, landscape))
            {
                string Mm(double? value) => value.HasValue ? (value.Value * 25.4 / 96).ToString("0.0", CultureInfo.InvariantCulture) : "unknown";
                if (!StudioDialog.Confirm(this, "Printer stock compatibility",
                    $"The driver reports {Mm(stock?.Width)} × {Mm(stock?.Height)} mm ({ticket.PageOrientation?.ToString() ?? "unspecified orientation"}). It did not verify the 100 × 70 mm label.\n\nSome Zebra drivers report only a named stock or omit custom dimensions. If you already configured the correct stock, you may continue using the driver settings.\n\nThe app keeps the label at 100 × 70 mm without fit-to-page scaling. Incorrect driver settings can clip, rotate, or offset the print. Cancel to adjust Printing Preferences, or Continue to send this job. Test one label first.")) return;
            }
            if (ticket.CopyCount.GetValueOrDefault(1) != 1)
            {
                Error(new FormatException("Set driver Copies to 1. Use 'Print Qty copies of each pane' in the app for quantity-based printing. Nothing was sent."));
                return;
            }
            dialog.PrintDocument(new LabelPaginator(labels, RepeatQuantity.IsChecked == true, _company, _layouts, FormatOverride, NumberStyle), "Ocean Glass outer / inner label batch");
            Status.Text = $"Sent {pages:N0} pages to {dialog.PrintQueue.FullName}. Check the Windows queue and the physical output.";
        }
        catch (Exception ex) when (ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException)
        { Error(ex); }
    }
    private void BuildInfo_Click(object sender, RoutedEventArgs e) => StudioDialog.Info(this, "Running build", AppBuildInfo.Describe());
    private void PreviousCopy_Click(object sender, RoutedEventArgs e) { _previewCopy = Math.Max(1, _previewCopy - 1); RenderPreview(); }
    private void NextCopy_Click(object sender, RoutedEventArgs e) { _previewCopy++; RenderPreview(); }
    private ThemeStudioWindow? _themeStudio;
    private void ThemeStudio_Click(object sender, RoutedEventArgs e)
    {
        if (_themeStudio != null)
        {
            _themeStudio.Show();
            if (_themeStudio.WindowState == WindowState.Minimized) _themeStudio.WindowState = WindowState.Normal;
            _themeStudio.Activate();
            return;
        }
        _themeStudio = new ThemeStudioWindow(StudioServices.Theme) { Owner = this };
        _themeStudio.Closed += (_,_) => _themeStudio = null;
        _themeStudio.Show();
    }
    private void Theme_Changed(object? sender, ThemeChangedEventArgs e)
    {
        ThemeState.Text = e.Kind switch
        {
            ThemeChangeKind.Preview => $"{e.Name} · LIVE UNSAVED · Theme Studio to save/revert",
            ThemeChangeKind.Saved => $"{e.Name} · Saved and active · {DateTime.Now:T}",
            ThemeChangeKind.Reverted => $"{e.Name} · Preview reverted",
            _ => $"{e.Name} · Active theme"
        };
    }
    private void StickerEditor_Click(object sender, RoutedEventArgs e)
    {
        var format = FormatOverride ?? (_previewEntry?.IsDouble == true ? StickerFormat.DGU : StickerFormat.SGU);
        var editor = new StickerEditorWindow(_layouts, _company, StudioServices.LayoutPath, format) { Owner = this };
        if (editor.ShowDialog() == true && editor.SavedLibrary is { } saved)
        {
            _layouts = saved;
            RenderPreview();
            Status.Text = "Sticker formats saved. Preview and printer now use these same layouts.";
        }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_batch.Count > 0 && !Confirm("Close Label Studio? The current batch is not saved and will be lost.")) e.Cancel = true;
    }
}
