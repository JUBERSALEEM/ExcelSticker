using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExcelSticker.Core;
using Microsoft.Win32;
using ExcelSticker.Theming;
using ExcelSticker.Wpf.Studio;

namespace ExcelSticker.Wpf;

public partial class MainWindow : Window
{
    // Wrapper identity keeps duplicate Excel rows independently editable/removable.
    public sealed class BatchRow(GlassEntry entry, CuttingRow sourceRow, string source = "Manual entry")
    {
        public GlassEntry Entry { get; } = entry;
        public CuttingRow SourceRow { get; } = sourceRow;
        public string Source { get; } = source;
        public string OrderNo => Entry.Outer.OrderNo;
        public string Specification => Entry.Specification;
        public string OuterSize => Entry.Outer.Size;
        public string InnerSize => Entry.Inner?.Size ?? "Single glass";
        public string CustomerName => Entry.Outer.CustomerName;
        public string Quantities => $"{Entry.Outer.Qty} / {Entry.Inner?.Qty.ToString(CultureInfo.InvariantCulture) ?? "—"}";
    }

    private readonly ObservableCollection<BatchRow> _batch = new();
    public IReadOnlyList<BatchRow> BatchRows => _batch;
    private BatchRow? _selectedBatchRow;
    private readonly List<string> _cuttingIssues=new();
    private readonly CompanyProfile _company;
    private CuttingRow? _editing;
    private GlassEntry? _previewEntry;
    private LayoutLibrary _layouts = new();
    private int _previewCopy = 1;
    private BatchRow? _previewRow;
    private StickerFormat? FormatOverride => LayoutFormat.SelectedIndex switch { 1 => StickerFormat.NormalJobOrder, 2 => StickerFormat.SGU, 3 => StickerFormat.DGU, _ => null };
    private const NumberingStyle NumberStyle = NumberingStyle.EntryCopy;
    private bool _ready;
    private bool _loadingForm;
    private bool _samplePreview;
    private bool _preparedSample;
    private bool _refreshingSampleJob;
    private bool _choosingExample;
    private bool _sending;
    private CancellationTokenSource? _printCancellation;
    private readonly StickerPrinter _printer=new();

    public MainWindow() : this(null) { }

    public MainWindow(CompanyProfile? companyOverride)
    {
        _ = StudioServices.Theme;
        InitializeComponent();InputEditing.Install(this);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save,(s,e)=>SaveExcel_Click(s,e)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save,Key.S,ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Print,(s,e)=>OpenPrintPreview_Click(s,e)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Print,Key.P,ModifierKeys.Control));
        JobPreview.RefreshRequested+=(_,_)=>Print_Click(this,new RoutedEventArgs());
        JobPreview.PrinterRequested+=(_,_)=>ConfigurePrinter();
        JobPreview.PrintRequested+=PrintPreview_Requested;
        JobPreview.CancelRequested+=(_,_)=>{if(_printCancellation is {} cancellation){cancellation.Cancel();Status.Text="Cancelling…";}};
        JobPreview.DriverModeChanged+=(_,_)=>ChangePrinterMode();
        Closed+=(_,_)=>_printer.Dispose();
        Title = "LABLE STUDIO — " + AppBuildInfo.Short;
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
        try { _layouts = LayoutLibrary.Load(StudioServices.LayoutPath); LogoImages.ValidateLibrary(_layouts); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
        { _layouts=new LayoutLibrary(); settingsWarning = "Saved layouts could not be loaded; using defaults. " + ex.Message; }
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
        CuttingView.Changed += (_,_) => SyncCutting();
        CuttingView.SelectionChanged += (_,_) =>
        {
            _choosingExample=true;try{PreviewExample.SelectedIndex=0;}finally{_choosingExample=false;}
            SyncCutting(PrintScope.SelectedIndex==1);
            if(JobPreview.Job!=null && _preparedSample!=_samplePreview)JobPreview.Invalidate("Live source changed between sample and cutting work.");
        };
        _ready = true;
        SyncCutting();
        Status.Text = settingsWarning ?? "Ready. Add a specification and cutting rows, or paste/import Excel. Expand Stickers & printing when needed. Five blank rows are ready. Use Save Excel (Ctrl+S) to keep your cutting list.";
    }

    private GlassEntry ReadForm() => GlassEntry.Create(_editing!=null?EntryOrderNo.Text:OrderNo.Text, Specification.Text, ReferenceNo.Text, _editing!=null?EntryCustomerName.Text:CustomerName.Text,
        _editing!=null?EntryJobNo.Text:JobNo.Text, W1.Text, H1.Text, OuterQty.Text, DoubleGlazed.IsChecked == true, W2.Text, H2.Text, InnerQty.Text, OuterGlass.Text, InnerGlass.Text);
    private void Error(Exception ex) { AppDiagnostics.Log("Handled error", ex); StudioDialog.Error(this, ex.Message); }
    private bool Confirm(string text) => StudioDialog.Confirm(this, "Please confirm", text);

    private void Form_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _loadingForm) return;
        if(sender==OrderNo || sender==CustomerName || sender==JobNo)SyncCutting();
        else PreviewForm();
    }
    private void SyncCutting(bool invalidate=true)
    {
        if(!_ready)return;
        if(invalidate)JobPreview.Invalidate("Cutting data or job metadata changed.");
        _batch.Clear();_cuttingIssues.Clear();
        foreach(var source in CuttingView.Sheet.ActiveRows)
        {
            try {_batch.Add(new BatchRow(source.ToEntry(OrderNo.Text,CustomerName.Text,JobNo.Text),source,source.Source));}
            catch(FormatException ex){_cuttingIssues.Add($"Row {source.Serial}: {ex.Message}");}
        }
        if(_editing!=null && !CuttingView.Sheet.Rows.Contains(_editing))EndEdit();
        _selectedBatchRow=_batch.FirstOrDefault(r=>ReferenceEquals(r.SourceRow,CuttingView.SelectedRow));
        if(_editing!=null)PreviewForm();
        else if(PreviewExample.SelectedIndex>0)ShowSample(SampleIsDgu);
        else if(_selectedBatchRow!=null)ShowEntry(_selectedBatchRow);
        else if(CuttingView.SelectedRow==null || CuttingView.SelectedRow.IsEmpty)ShowSample(SampleIsDgu);
        else
        {
            PreviewBadge.Text="INCOMPLETE";_samplePreview=false;_previewRow=null;_previewEntry=null;PreviewHost.Content=null;CopyPreviewText.Text="";
            PreviewSource.Text="Select a valid cutting row to preview its sticker.";
            PreviewDetails.Text="Enter Order No and Customer for stickers. Imported row metadata is retained.";
            ValidationMessage.Text=_cuttingIssues.FirstOrDefault()??"";
        }
        RefreshBatch();
    }
    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready || _loadingForm) return;
        InnerFields.IsEnabled = DoubleGlazed.IsChecked == true;
        PreviewForm();
    }
    private void PreviewForm()
    {
        PreviewBadge.Text="UNSAVED";_samplePreview=false;
        _choosingExample=true;try{PreviewExample.SelectedIndex=0;}finally{_choosingExample=false;}
        JobPreview.Invalidate("Cutting entry details are being edited.");
        _previewRow = null;
        PreviewSource.Text = _editing == null ? "Live form · not saved to batch" : $"Editing row {_editing.Serial} · unsaved form";
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
        RefreshBatch();
    }
    private void ShowEntry(BatchRow row)
    {
        PreviewBadge.Text="LIVE";_samplePreview=false;
        _previewRow = row;
        _previewCopy = 1;
        _previewEntry = row.Entry;
        ValidationMessage.Text = "";
        PreviewSource.Text = $"Selected order {row.OrderNo} · {row.Source}";
        RenderPreview();
    }
    private bool SampleIsDgu=>FormatOverride==StickerFormat.DGU || (FormatOverride!=StickerFormat.SGU && PreviewExample.SelectedIndex==2);
    private void ShowSample(bool dgu)
    {
        PreviewBadge.Text="SAMPLE";_samplePreview=true;_previewRow=null;_previewCopy=1;
        _previewEntry=dgu?LiveStickerSamples.Dgu():LiveStickerSamples.Sgu();
        PreviewSource.Text=$"SAMPLE · {(dgu?"DGU outer / inner":"SGU")} · 300 × 300 mm glass. Ready to preview or test print; no cutting entry needed.";
        RenderPreview();
    }
    private void PreviewExample_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!_ready || _choosingExample)return;
        JobPreview.Invalidate("Live sticker source changed.");
        _choosingExample=true;
        try{LayoutFormat.SelectedIndex=PreviewExample.SelectedIndex switch {1=>2,2=>3,_=>0};}
        finally{_choosingExample=false;}
        SyncCutting(false);RefreshSampleJobIfOpen();
    }
    private void RefreshSampleJobIfOpen()
    {
        if(!_samplePreview || JobPreview.Job==null || !PrintPreviewSection.IsExpanded || _sending || _refreshingSampleJob || _editing!=null)return;
        _refreshingSampleJob=true;
        try{PreparePrintPreview();}
        catch(Exception ex) when(ex is FormatException or InvalidOperationException or ArgumentException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException)
        {JobPreview.Invalidate("Sample preview could not be refreshed.");Status.Text=ex.Message;}
        finally{_refreshingSampleJob=false;}
    }
    private void PrintPreview_Expanded(object sender,RoutedEventArgs e)
    {
        if(_ready && ReferenceEquals(e.OriginalSource,PrintPreviewSection) && JobPreview.IsStale)RefreshSampleJobIfOpen();
    }
    private void FollowLivePage()
    {
        if(_sending || JobPreview.IsStale || JobPreview.Job is not {} job || PreviewHost.Content is not LabelView live)return;
        for(var i=0;i<job.PageCount;i++)if(job.LabelAt(i)==live.Label){JobPreview.ShowPage(i);return;}
    }
    private int NextManualSerial()
    {
        var groups=CuttingView.Sheet.Groups;
        var group=groups.FirstOrDefault(g=>g.Specification==_previewEntry?.Specification && g.GlassType==(_previewEntry?.IsDouble==true?"DGU":"SGU"));
        if(group==null && groups.Count==1 && groups[0].Rows.All(r=>r.IsEmpty) && string.IsNullOrWhiteSpace(groups[0].Specification))group=groups[0];
        if(group==null)return CuttingView.Sheet.Rows.Count()+1;
        return group.Rows.FirstOrDefault(r=>r.IsEmpty)?.Serial ?? groups.TakeWhile(g=>g!=group).Sum(g=>g.Rows.Count)+group.Rows.Count+1;
    }
    private void RenderPreview()
    {
        if (_previewEntry == null) return;
        InnerPreviewOption.IsEnabled = _previewEntry.IsDouble;
        if (!_previewEntry.IsDouble && PreviewPane.SelectedIndex == 1) PreviewPane.SelectedIndex = 0;
        var label = PreviewPane.SelectedIndex == 1 ? _previewEntry.Inner! : _previewEntry.Outer;
        _previewCopy = RepeatQuantity.IsChecked==true?Math.Clamp(_previewCopy, 1, label.Qty):1;
        PreviousCopyButton.IsEnabled=_previewCopy>1;NextCopyButton.IsEnabled=RepeatQuantity.IsChecked==true && _previewCopy<label.Qty;
        var rowNumber = _samplePreview ? 1 : _previewRow?.SourceRow.Serial ?? _editing?.Serial ?? NextManualSerial();
        label = label with { EntryNumber = Math.Max(1, rowNumber), CopyIndex = _previewCopy, Numbering = NumberStyle };
        var view = new LabelView(label, _company, _layouts.For(FormatOverride ?? LayoutLibrary.FormatFor(label)));
        PreviewHost.Width=view.Width;PreviewHost.Height=view.Height;
        LiveLogoStatus.Text=view.Layout.Elements.Any(e=>e.Visible && e.Field=="Logo" && e.ImagePng.Length>0)?"Company logo included. Open Adjust to move or resize fields and logo.":"No company logo yet. Open Adjust → Import / replace logo to add yours. Preview is read-only; edit positions in Sticker Studio.";
        LiveStockSize.Text=$"{view.Layout.WidthMm:0.##} × {view.Layout.HeightMm:0.##} mm · actual print stays monochrome";
        PreviewHost.Content = view;
        CopyPreviewText.Text = $"Copy {_previewCopy} of {label.Qty} · {label.QuantityText}";
        ValidationMessage.Text = string.Join("\n", view.Warnings.Take(3));
        PreviewDetails.Text = $"{view.Layout.Format} · {(string.IsNullOrEmpty(label.Pane) ? "SINGLE GLASS" : label.Pane)}  ·  {label.Size} mm  ·  Qty {label.Qty}\n{label.GlassType}";
    }
    private void PreviewPane_Changed(object sender, SelectionChangedEventArgs e) { if (_ready) {RenderPreview();if(_preparedSample && _samplePreview)FollowLivePage();} }
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        PreviewForm();StickersSection.IsExpanded=true;
        Dispatcher.BeginInvoke(new Action(()=>StickerPreviewCard.BringIntoView(new Rect(0,0,StickerPreviewCard.ActualWidth,32))));
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var entry=ReadForm();var old=_editing;CuttingRow? added=null;
            var source=_editing?.Source??"Manual entry";
            void KeepFormInheritance(CuttingRow row)
            {
                row.InnerQty=entry.IsDouble?InnerQty.Text.Trim():"";
                row.W2=entry.IsDouble && !string.IsNullOrWhiteSpace(W2.Text)?W2.Text.Trim():"0";
                row.H2=entry.IsDouble && !string.IsNullOrWhiteSpace(H2.Text)?H2.Text.Trim():"0";
            }
            CuttingView.Sheet.Change(()=>
            {
                if(old!=null && old.Group?.Specification==entry.Specification && old.Group.GlassType==(entry.IsDouble?"DGU":"SGU"))
                {
                    var updated=CuttingRow.FromEntry(entry,source);KeepFormInheritance(updated);updated.Price=old.Price;old.CopyValues(updated);added=old;
                }
                else
                {
                    var price=old==null?"":old.Rate.ToString(CultureInfo.InvariantCulture);
                    old?.Group?.Rows.Remove(old);
                    added=CuttingView.Sheet.AddEntries(new[]{entry},source)[0];KeepFormInheritance(added);added.Price=price;
                }
            });
            EndEdit();CuttingView.SelectRow(added!);
            Status.Text="Entry saved to the cutting sheet. Pricing and pane labels updated.";
        }
        catch (FormatException ex) { Error(ex); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var row=(sender as FrameworkElement)?.DataContext as CuttingRow ?? CuttingView.SelectedRow;
        if(row?.Group==null){Status.Text="Select a cutting row first.";return;}
        if(_editing!=null && !Confirm("Discard the current unsaved detail edit?"))return;
        ManualEntrySection.IsExpanded=true;_editing=row;
        static bool Zero(string value)=>string.IsNullOrWhiteSpace(value) || (decimal.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n) && n==0);
        _loadingForm=true;
        try
        {
            EntryOrderNo.Text=row.HasOwnMetadata||row.OrderNo.Length>0?row.OrderNo:OrderNo.Text;
            EntryCustomerName.Text=row.HasOwnMetadata||row.CustomerName.Length>0?row.CustomerName:CustomerName.Text;
            EntryJobNo.Text=row.HasOwnMetadata||row.JobNo.Length>0?row.JobNo:JobNo.Text;
            Specification.Text=row.Group.Specification;ReferenceNo.Text=row.GlassRef;
            W1.Text=row.W1;H1.Text=row.H1;OuterQty.Text=row.Qty;
            DoubleGlazed.IsChecked=row.Group.GlassType=="DGU";
            var inherited=Zero(row.W2)&&Zero(row.H2);
            W2.Text=inherited?"":row.W2;H2.Text=inherited?"":row.H2;InnerQty.Text=row.InnerQty;
            OuterGlass.Text=row.OuterGlass;InnerGlass.Text=row.InnerGlass;InnerFields.IsEnabled=DoubleGlazed.IsChecked==true;
        }
        finally{_loadingForm=false;}
        EntryMetadataOverride.Visibility=Visibility.Visible;CuttingView.IsEnabled=false;
        AddButton.Content="Save changes";FormTitle.Text="Edit selected cutting row";CancelEditButton.Visibility=Visibility.Visible;
        PreviewForm();RefreshBatch();EntryOrderNo.Focus();
        Status.Text="Editing row details and optional pane overrides. Sheet defaults are unchanged; Save changes commits the edit.";
    }
    private void EndEdit()
    {
        _editing = null;CuttingView.IsEnabled=true;
        EntryMetadataOverride.Visibility=Visibility.Collapsed;
        AddButton.Content = "+  Add to batch";
        FormTitle.Text = "Manual glass entry";
        CancelEditButton.Visibility = Visibility.Collapsed;
    }
    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        var row = _editing;
        EndEdit();
        if (row != null) CuttingView.SelectRow(row);
        Status.Text = "Edit cancelled; batch unchanged. Form values remain available for a new entry.";
    }
    private bool HasCuttingWork => new[]{OrderNo.Text,CustomerName.Text,JobNo.Text}.Any(v=>!string.IsNullOrWhiteSpace(v)) || CuttingView.Sheet.ActiveRows.Any() || CuttingView.Sheet.Groups.Count>1 || CuttingView.Sheet.Groups.Any(g=>!string.IsNullOrWhiteSpace(g.Specification) || g.BasePrice!="0" || g.GlassType!="SGU");
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if(!HasCuttingWork || !Confirm("Clear all specifications and cutting rows? Save Excel first if needed."))return;
        EndEdit();CuttingView.Sheet.Change(()=>{CuttingView.Sheet.Groups.Clear();CuttingView.Sheet.Groups.Add(CuttingGroup.Blank());});
        Status.Text="Cutting sheet cleared.";
    }

    public IReadOnlyList<GlassLabel> GetPrintLabels()
    {
        if(_cuttingIssues.Count>0)return Array.Empty<GlassLabel>();
        var rows = _batch.Select(row => (Row: row, Number: row.SourceRow.Serial));
        if (PrintScope.SelectedIndex == 1) rows = rows.Where(item => ReferenceEquals(item.Row.SourceRow, CuttingView.SelectedRow));
        return rows.SelectMany(item =>
        {
            IEnumerable<GlassLabel> labels = PrintLayers.SelectedIndex switch
            {
                1 => new[] { item.Row.Entry.Outer },
                2 => item.Row.Entry.Inner is { } inner ? new[] { inner } : Array.Empty<GlassLabel>(),
                _ => item.Row.Entry.Labels
            };
            return labels.Select(label => label with { EntryNumber = item.Number, Numbering = NumberStyle });
        }).ToArray();
    }
    private void RefreshBatch()
    {
        if (!_ready) return;
        BatchTitle.Text = $"{CuttingView.Sheet.Groups.Count:N0} specification groups · {CuttingView.Sheet.Rows.Count():N0} cutting rows";
        CuttingPrintOptions.Visibility=_samplePreview?Visibility.Collapsed:Visibility.Visible;
        if(_samplePreview && _previewEntry!=null && _editing==null)
        {
            var samples=_previewEntry.Labels.ToArray();
            PrintSummary.Text=$"SAMPLE · {PageCount(samples):N0} sticker(s) · no cutting entry required";
            PrintButton.IsEnabled=true;return;
        }
        var labels = GetPrintLabels();
        var pages = PageCount(labels);
        PrintSummary.Text = _cuttingIssues.Count>0 ? $"Printing blocked: {_cuttingIssues.Count} incomplete row(s). {_cuttingIssues[0]}" : $"{pages:N0} pages ready  ·  {labels.Count:N0} pane labels";
        if(_editing!=null)PrintSummary.Text="Save or cancel the entry-details edit before printing.";
        PrintButton.IsEnabled = pages > 0 && _editing==null;
    }
    private long PageCount(IReadOnlyList<GlassLabel> labels) => RepeatQuantity.IsChecked == true ? labels.Sum(label => (long)label.Qty) : labels.Count;
    private void PrintOptions_Changed(object sender, SelectionChangedEventArgs e)
    {
        if(!_ready || _choosingExample)return;
        JobPreview.Invalidate("Print scope, pane filter or layout format changed.");
        if(_samplePreview && sender==LayoutFormat)
        {
            _choosingExample=true;
            try{if(LayoutFormat.SelectedIndex is 2 or 3 && PreviewExample.SelectedIndex>0)PreviewExample.SelectedIndex=LayoutFormat.SelectedIndex==3?2:1;}
            finally{_choosingExample=false;}
            ShowSample(SampleIsDgu);
        }
        else RenderPreview();
        RefreshBatch();RefreshSampleJobIfOpen();
    }
    private void Repeat_Changed(object sender, RoutedEventArgs e)
    {if(_ready){JobPreview.Invalidate("Quantity expansion changed.");RenderPreview();RefreshBatch();RefreshSampleJobIfOpen();}}

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if(_editing!=null){Status.Text="Save or cancel the current entry-details edit before importing.";return;}
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
                if(workbook.IsCuttingPage(sheet))
                {
                    var saved=CuttingWorkbook.Read(workbook,sheet);
                    if(HasCuttingWork && !Confirm("Open this cutting workbook and replace the current list? Save Excel first if you need to keep the current work."))return;
                    RestoreCuttingWorkbook(saved);Status.Text="Opened cutting workbook: "+source+". Visible Excel inputs, groups, rates and stored pane metadata restored.";return;
                }
                rows = workbook.ReadTable(sheet);
                source += $" · {sheet}";
            }
            else if (Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(dialog.FileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                rows = ImportMapping.ReadCsv(File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true)), out var delimiter);
                source += delimiter == ';' ? " · semicolon CSV" : delimiter == '\t' ? " · tab-separated" : " · comma CSV";
            }
            else throw new FormatException("Choose .xlsx, UTF-8 .csv or .tsv. Convert legacy .xls files to .xlsx first.");
            var cuttingHeader=CuttingTable.FindHeader(rows);
            if(cuttingHeader>=0)
            {
                var groups=CuttingTable.Read(rows,cuttingHeader);var count=groups.Sum(g=>g.Rows.Count);
                var blankStart=CuttingView.Sheet.Groups.Count==1 && CuttingView.Sheet.Groups[0].Rows.All(r=>r.IsEmpty) && string.IsNullOrWhiteSpace(CuttingView.Sheet.Groups[0].Specification);
                CuttingView.Sheet.CheckCapacity(count-(blankStart?CuttingView.Sheet.Rows.Count():0));
                if(!Confirm($"Append {count} cutting rows in {groups.Count} specification groups from {source}?\nPrices are AED per m². Incomplete rows will remain flagged; labels require Order No and Customer."))return;
                CuttingView.Sheet.Change(()=>
                {
                    if(CuttingView.Sheet.Groups.Count==1 && CuttingView.Sheet.Groups[0].Rows.All(r=>r.IsEmpty) && string.IsNullOrWhiteSpace(CuttingView.Sheet.Groups[0].Specification))CuttingView.Sheet.Groups.Clear();
                    foreach(var group in groups){foreach(var row in group.Rows)row.Source=source;CuttingView.Sheet.Groups.Add(group);}
                });
                CuttingView.SelectRow(groups[0].Rows[0]);Status.Text=$"Imported {count} cutting rows, including prices. Review dimensions and rates before printing.";return;
            }
            var preview = new ImportPreviewWindow(rows, source) { Owner = this };
            if (preview.ShowDialog() != true || preview.Entries is not { } entries) { Status.Text = "Import cancelled. Batch unchanged."; return; }
            AppendEntries(entries, source);
            Status.Text = $"Imported {entries.Count} entries from {source}. Select a cutting row and expand Stickers & printing. Legacy label imports do not carry prices; set group rates or import the cutting template.";
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
        var added=CuttingView.Sheet.AddEntries(entries,source);
        if(added.Count>0)CuttingView.SelectRow(added[0]);
    }
    public void RestoreCuttingWorkbook(CuttingWorkbookData saved)
    {
        EndEdit();_loadingForm=true;
        try
        {
            OrderNo.Text=saved.OrderNo;CustomerName.Text=saved.CustomerName;JobNo.Text=saved.JobNo;
            CuttingView.Sheet.Change(()=>
            {
                CuttingView.Sheet.Groups.Clear();
                foreach(var source in saved.Sheet.Groups)
                {
                    var group=new CuttingGroup {GlassType=source.GlassType,Specification=source.Specification,BasePrice=source.BasePrice};
                    foreach(var row in source.Rows)group.Rows.Add(row.Copy());CuttingView.Sheet.Groups.Add(group);
                }
                if(CuttingView.Sheet.Groups.Count==0)CuttingView.Sheet.Groups.Add(CuttingGroup.Blank());
            });
        }
        finally{_loadingForm=false;}
        SyncCutting();var first=CuttingView.Sheet.ActiveRows.FirstOrDefault();if(first!=null)CuttingView.SelectRow(first);
    }
    private void SaveExcel_Click(object sender,RoutedEventArgs e)
    {
        if(_editing!=null){Status.Text="Save or cancel the current entry-details edit before saving Excel.";return;}
        CuttingView.CommitEdits();
        var dialog=new SaveFileDialog {Filter="Excel cutting workbook (*.xlsx)|*.xlsx",DefaultExt=".xlsx",FileName="Lable_Studio_Cutting.xlsx"};
        if(dialog.ShowDialog(this)!=true)return;
        try {CuttingWorkbook.Save(dialog.FileName,CuttingView.Sheet,OrderNo.Text,CustomerName.Text,JobNo.Text);Status.Text="Excel cutting list saved: "+dialog.FileName+". Re-import this workbook to continue editing.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException){Error(ex);}
    }
    private void Template_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog {Filter="Excel cutting workbook (*.xlsx)|*.xlsx",DefaultExt=".xlsx",FileName="Lable_Studio_Blank_Cutting.xlsx"};
        if(dialog.ShowDialog(this)!=true)return;
        try {var blank=new CuttingSheet();blank.Groups.Add(CuttingGroup.Blank());CuttingWorkbook.Save(dialog.FileName,blank,"","","");Status.Text="Blank formatted Excel template saved with five input rows.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException){Error(ex);}
    }

    private void ExportCutting_Click(object sender,RoutedEventArgs e)
    {
        if(_editing!=null){Status.Text="Save or cancel the current entry-details edit before exporting.";return;}
        CuttingView.CommitEdits();
        var dialog=new SaveFileDialog {Filter="Cutting CSV (*.csv)|*.csv",FileName="Lable_Studio_Cutting.csv"};
        if(dialog.ShowDialog(this)!=true)return;
        try {File.WriteAllText(dialog.FileName,CuttingTable.Export(CuttingView.Sheet,OrderNo.Text,CustomerName.Text,JobNo.Text),new UTF8Encoding(true));Status.Text="Cutting CSV exported, including prices and per-row metadata. Incomplete values are retained.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Error(ex);}
    }

    private void SyncCuttingForPrint()
    {
        var row=_previewRow?.SourceRow;var copy=_previewCopy;
        CuttingView.CommitEdits();SyncCutting(false);
        if(row!=null && ReferenceEquals(row,_previewRow?.SourceRow)){_previewCopy=copy;RenderPreview();}
    }
    public LabelPaginator PreparePrintPreview()
    {
        if(_sending)throw new InvalidOperationException("Wait for the current print operation to finish.");
        // An explicit live sample is its own source, not a fabricated cutting row. Do not re-read/reset it from the sheet.
        if(!_samplePreview)SyncCuttingForPrint();
        if(_editing!=null)throw new FormatException("Save or cancel the current entry-details edit before preparing a print job.");
        var sample=_samplePreview && _previewEntry!=null;
        if(!sample && _cuttingIssues.Count>0)throw new FormatException("Complete the cutting rows before printing. "+_cuttingIssues[0]);
        IReadOnlyList<GlassLabel> labels=sample?_previewEntry!.Labels.Select(l=>l with {EntryNumber=1,Numbering=NumberStyle}).ToArray():GetPrintLabels();
        if(labels.Count==0)throw new FormatException("No cutting stickers match this scope. Select a sample, or change the cutting scope / pane filter.");
        if(PageCount(labels)>10000)throw new FormatException("A job is limited to 10,000 sticker pages. Reduce the scope or turn off Qty copies.");
        var job=new LabelPaginator(labels,RepeatQuantity.IsChecked==true,_company,_layouts,FormatOverride,NumberStyle);
        var scope=PrintScope.SelectedIndex==1?$"selected cutting row {CuttingView.SelectedRow?.Serial}":"all cutting rows";
        var layers=PrintLayers.SelectedIndex switch {1=>"outer / single only",2=>"inner only",_=>"outer + inner"};
        _preparedSample=sample;
        JobPreview.LoadJob(job,sample?$"SAMPLE · {_previewEntry!.Specification} · 300 × 300 mm glass · test stickers, not cutting work":$"{scope} · {layers} · {(RepeatQuantity.IsChecked==true?"all Qty copies":"one copy per pane")}");
        FollowLivePage();
        StickersSection.IsExpanded=true;PrintPreviewSection.IsExpanded=true;
        try{if(_printer.TryPrepare(job.PageSize) && _printer.Review is {} review)JobPreview.SetPrinter(review);}
        catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException)
        {AppDiagnostics.Log("Prepare printer summary",ex);JobPreview.ClearPrinter();}
        Status.Text=JobPreview.CanPrint?"Preview ready. Choose Print current or Print all.":"Change printer setup or sticker size before printing.";
        return job;
    }
    private void OpenPrintPreview_Click(object sender,RoutedEventArgs e)=>Print_Click(sender,e);
    private void Print_Click(object sender,RoutedEventArgs e)
    {
        if(_sending)return;
        try
        {
            PreparePrintPreview();
            Dispatcher.BeginInvoke(new Action(()=>PrintPreviewSection.BringIntoView(new Rect(0,0,PrintPreviewSection.ActualWidth,32))));
        }
        catch(Exception ex) when(ex is FormatException or InvalidOperationException or ArgumentException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException){Error(ex);}
    }
    private void ConfigurePrinter()
    {
        if(_sending)return;
        if(JobPreview.Job==null){Status.Text="Prepare the print preview first.";return;}
        try
        {
            if(_printer.Configure(JobPreview.Job.PageSize) && _printer.Review is {} review)
            {JobPreview.SetPrinter(review);Status.Text=review.BlockReason??"Printer ready. Choose Print current or Print all.";}
        }
        catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException){Error(ex);}
    }
    private void ChangePrinterMode()
    {
        if(_sending || JobPreview.Job==null)return;
        try
        {
            _printer.SetWindowsDriverOnly(JobPreview.WindowsDriverOnly,JobPreview.Job.PageSize);
            if(_printer.Review is {} review)JobPreview.SetPrinter(review);
        }
        catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException)
        {if(_printer.Review is {} review)JobPreview.SetPrinter(review);Error(ex);}
    }
    private async void PrintPreview_Requested(object? sender,PreviewPrintRequestEventArgs e)
    {
        if(_sending)return;
        if(!_preparedSample)SyncCuttingForPrint();
        if(!JobPreview.CanPrint || _editing!=null){Status.Text="Refresh the preview before printing.";return;}
        var job=JobPreview.Job!;var submitted=false;
        using var cancellation=new CancellationTokenSource();_printCancellation=cancellation;
        _sending=true;JobPreview.SetBusy(true);
        var noun=_preparedSample?"sample sticker":"sticker";
        var action=e.Kind switch {PreviewPrintKind.Current=>$"current {noun}",PreviewPrintKind.All=>$"all {e.Count:N0} {noun}s",_=>$"{e.Count:N0} {noun}s (pages {e.FirstPage+1}–{e.FirstPage+e.Count})"};
        try
        {
            if(!_printer.EnsureConfigured(job.PageSize)){Status.Text="Print cancelled.";return;}
            if(_printer.Review is {} review)
            {
                JobPreview.SetPrinter(review);
                if(review.BlockReason!=null){Status.Text=review.BlockReason;return;}
            }
            var pages=job.SelectPages(e.FirstPage,e.Count);
            void Validate()
            {
                if(JobPreview.IsStale || !ReferenceEquals(job,JobPreview.Job))throw new InvalidOperationException("The cutting list or sticker options changed. Refresh the preview before printing.");
            }
            Status.Text=$"Printing {action}…";
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            var printer=await _printer.SendAsync(pages,cancellation.Token,
                (done,total)=>Status.Text=$"Printing {action}… preparing {done:N0} / {total:N0}",Validate,
                ()=>{submitted=true;Status.Text=$"Printing {action}… sending to printer";});
            Status.Text=$"Sent {action} to {printer}.";
        }
        catch(OperationCanceledException){Status.Text=submitted?"Print cancelled. Some stickers may already have printed; check the printer before retrying.":"Print cancelled. No stickers sent.";}
        catch(Exception ex) when(ex is PrintSystemException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException or System.Windows.Xps.XpsException or IOException or UnauthorizedAccessException)
        {
            Status.Text=submitted?"Printing stopped. Check the printer queue before retrying; some stickers may have printed.":"No stickers sent. "+ex.Message;
            AppDiagnostics.Log("Print stickers",ex);
            StudioDialog.Info(this,"Could not print",Status.Text+(submitted?"\n\n"+ex.Message:""));
        }
        finally{_printCancellation=null;_sending=false;JobPreview.SetBusy(false);}
    }
    private void ToggleStickers_Click(object sender,RoutedEventArgs e)
    {
        StickersSection.IsExpanded=!StickersSection.IsExpanded;
        if(StickersSection.IsExpanded)Dispatcher.BeginInvoke(new Action(()=>StickersSection.BringIntoView(new Rect(0,0,StickersSection.ActualWidth,32))));
    }
    private void BuildInfo_Click(object sender, RoutedEventArgs e) => StudioDialog.Info(this, "Running build", AppBuildInfo.Describe());
    private void PreviousCopy_Click(object sender, RoutedEventArgs e) { _previewCopy = Math.Max(1, _previewCopy - 1); RenderPreview();if(_preparedSample && _samplePreview)FollowLivePage(); }
    private void NextCopy_Click(object sender, RoutedEventArgs e) { _previewCopy++; RenderPreview();if(_preparedSample && _samplePreview)FollowLivePage(); }
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
    private void StickerEditor_Click(object sender,RoutedEventArgs e)=>OpenStickerEditor(false);
    private void ImportLogo_Click(object sender,RoutedEventArgs e)=>OpenStickerEditor(true);
    private void OpenStickerEditor(bool importLogo)
    {
        if(_sending){Status.Text="Finish or cancel printing before opening Sticker Studio.";return;}
        var format = FormatOverride ?? (_previewEntry?.IsDouble == true ? StickerFormat.DGU : StickerFormat.SGU);
        var editor = new StickerEditorWindow(_layouts, _company, StudioServices.LayoutPath, format,importLogo) { Owner = this };
        if (editor.ShowDialog() == true && editor.SavedLibrary is { } saved)
        {
            _layouts = saved;
            JobPreview.Invalidate("Sticker layouts changed.");
            JobPreview.ClearPrinter();
            RenderPreview();RefreshSampleJobIfOpen();
            Status.Text = _samplePreview?"Sticker formats saved. Live and sample print previews updated.":"Sticker formats saved. Refresh the cutting print preview to use the new layout.";
        }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if(_sending){e.Cancel=true;Status.Text="Finish or cancel printing before closing.";return;}
        if ((_editing!=null || HasCuttingWork) && !Confirm("Close LABLE STUDIO? The cutting sheet is in memory only. Save Excel before closing to keep your work.")) e.Cancel = true;
    }
}
