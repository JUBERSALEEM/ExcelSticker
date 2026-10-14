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

namespace ExcelSticker.Wpf;

public partial class MainWindow : Window
{
    // Reference identity keeps identical batch rows independently editable/removable.
    public sealed class BatchRow(GlassLabel label)
    {
        public GlassLabel Label { get; } = label;
        public string OrderNo => Label.OrderNo;
        public string GlassType => Label.GlassType;
        public string Size => Label.Size;
        public string ReferenceNo => Label.ReferenceNo;
        public string CustomerName => Label.CustomerName;
        public int Qty => Label.Qty;
    }

    private readonly ObservableCollection<BatchRow> _batch = new();
    private readonly CompanyProfile _company;
    private BatchRow? _editing;

    public MainWindow()
    {
        InitializeComponent();
        try { _company = CompanyProfile.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException)
        {
            _company = new CompanyProfile();
            MessageBox.Show(this, $"Could not load company.json. Using the sample company details.\n\n{ex.Message}", "Company settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        BatchGrid.ItemsSource = _batch;
        ShowPreview(ReadForm());
        Status.Text = "Ready. Sample values are shown in the form; add them to the batch to print. Verify company.json before production use.";
    }

    private GlassLabel ReadForm() => GlassLabel.Create(OrderNo.Text, Qty.Text, GlassType.Text, ReferenceNo.Text,
        LabelWidth.Text, LabelHeight.Text, CustomerName.Text, JobNo.Text);

    private void ShowPreview(GlassLabel label) => PreviewHost.Content = new LabelView(label, _company);
    private void Error(Exception ex) => MessageBox.Show(this, ex.Message, "Ocean Glass Label Maker", MessageBoxButton.OK, MessageBoxImage.Warning);
    private bool Confirm(string text) => MessageBox.Show(this, text, "Ocean Glass Label Maker", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var label = ReadForm();
            var row = new BatchRow(label);
            var editing = _editing != null;
            if (_editing != null) _batch[_batch.IndexOf(_editing)] = row;
            else
            {
                if (_batch.Count >= LabelCsv.MaxLabels) throw new FormatException($"A batch can contain at most {LabelCsv.MaxLabels} rows.");
                _batch.Add(row);
            }
            EndEdit();
            BatchGrid.SelectedItem = row;
            BatchGrid.ScrollIntoView(row);
            ShowPreview(label);
            RefreshBatch();
            Status.Text = editing ? "Label updated in the batch." : "Label added to the batch.";
        }
        catch (FormatException ex) { Error(ex); }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try { ShowPreview(ReadForm()); Status.Text = "Preview updated. This does not change the batch; use Add label or Save changes."; }
        catch (FormatException ex) { Error(ex); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BatchRow row) return;
        if (_editing != null && !Confirm("Discard the current unsaved edit and edit this row?")) return;
        _editing = row;
        var label = row.Label;
        OrderNo.Text = label.OrderNo; Qty.Text = label.Qty.ToString(CultureInfo.InvariantCulture);
        GlassType.Text = label.GlassType; ReferenceNo.Text = label.ReferenceNo;
        LabelWidth.Text = label.Width.ToString(CultureInfo.InvariantCulture);
        LabelHeight.Text = label.Height.ToString(CultureInfo.InvariantCulture);
        CustomerName.Text = label.CustomerName; JobNo.Text = label.JobNo;
        AddButton.Content = "Save changes";
        FormTitle.Text = "Edit glass label";
        CancelEditButton.Visibility = Visibility.Visible;
        BatchGrid.SelectedItem = row;
        ShowPreview(label);
        OrderNo.Focus();
        Status.Text = "Editing a batch row. Save changes to apply the form values.";
    }

    private void EndEdit()
    {
        _editing = null;
        AddButton.Content = "Add label to list";
        FormTitle.Text = "Enter one glass label";
        CancelEditButton.Visibility = Visibility.Collapsed;
    }
    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_editing != null) ShowPreview(_editing.Label);
        EndEdit();
        Status.Text = "Edit cancelled. The batch is unchanged; form values remain available for a new label.";
    }
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BatchRow row) return;
        if (!Confirm($"Remove order {row.OrderNo} from the batch?")) return;
        if (ReferenceEquals(_editing, row)) EndEdit();
        _batch.Remove(row);
        RefreshBatch();
        Status.Text = "Label removed. Preview is not part of the batch unless added.";
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_batch.Count == 0 || !Confirm("Clear all labels in this batch? This cannot be undone.")) return;
        EndEdit(); _batch.Clear(); RefreshBatch();
        Status.Text = "Batch cleared. Form and preview retained for convenience.";
    }
    private void Batch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BatchGrid.SelectedItem is BatchRow row) ShowPreview(row.Label);
    }
    private void RefreshBatch()
    {
        BatchTitle.Text = $"Labels in this batch: {_batch.Count}   •   Pages: {PageCount():N0}";
        EmptyHint.Visibility = _batch.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PrintButton.IsEnabled = _batch.Count > 0;
    }
    private long PageCount() => RepeatQuantity.IsChecked == true ? _batch.Sum(row => (long)row.Qty) : _batch.Count;
    private void Repeat_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) RefreshBatch(); }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv", Title = "Import Excel CSV (UTF-8)" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new FormatException("CSV files must be 10 MB or smaller.");
            var labels = LabelCsv.Import(File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true)));
            if (_batch.Count + labels.Count > LabelCsv.MaxLabels) throw new FormatException($"The combined batch would exceed {LabelCsv.MaxLabels} rows. Nothing was imported.");
            foreach (var label in labels) _batch.Add(new BatchRow(label));
            BatchGrid.SelectedItem = _batch[^1];
            RefreshBatch();
            Status.Text = $"Imported {labels.Count} labels from {Path.GetFileName(dialog.FileName)}. Existing batch rows were retained.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or DecoderFallbackException) { Error(ex); }
    }
    private void Template_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "Ocean_Glass_Label_Template.csv", DefaultExt = ".csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, LabelCsv.Header + "\r\n", new UTF8Encoding(true));
            Status.Text = $"Blank UTF-8 CSV template saved to {dialog.FileName}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error(ex); }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_batch.Count == 0) return;
        if (_editing != null) { Error(new FormatException("Save or cancel the current edit before printing.")); return; }
        var pages = PageCount();
        if (pages > 10000) { Error(new FormatException("A print job is limited to 10,000 pages. Reduce the batch or turn off Qty copies.")); return; }
        if (!Confirm($"Print {pages:N0} label page(s) from the batch?\n\nThe current form/preview is not printed unless added to the batch. Driver copies should be set to 1.")) return;
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
            if (stock?.Width is not double width || stock.Height is not double height
                || Math.Abs(width - LabelView.LabelWidth) > 2 || Math.Abs(height - LabelView.LabelHeight) > 2
                || ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape)
            {
                Error(new FormatException("The selected driver did not confirm 100 × 70 mm portrait stock. No labels were sent. Configure a custom 100 × 70 mm stock in Windows Printing Preferences, then try again."));
                return;
            }
            if (ticket.CopyCount.GetValueOrDefault(1) != 1)
            {
                Error(new FormatException("Set Copies to 1 in the Windows print dialog. Use the app's Qty copies option for repeated labels. No labels were sent."));
                return;
            }
            dialog.PrintDocument(new LabelPaginator(_batch.Select(row => row.Label), RepeatQuantity.IsChecked == true, _company), "Ocean Glass label batch");
            Status.Text = $"Sent {pages:N0} label page(s) to {dialog.PrintQueue.FullName}. Check the Windows queue for completion.";
        }
        catch (Exception ex) when (ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException)
        { Error(ex); }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_batch.Count > 0 && !Confirm("Close the label maker? The current batch is not saved and will be lost.")) e.Cancel = true;
    }
}
