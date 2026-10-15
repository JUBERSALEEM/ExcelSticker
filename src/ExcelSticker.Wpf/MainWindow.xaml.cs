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
    private bool _choosingExample;
    private bool _sending;
    private bool _cancelPrint;
    private readonly StickerPrinter _printer=new();

    public MainWindow() : this(null) { }

    public MainWindow(CompanyProfile? companyOverride)
    {
        _ = StudioServices.Theme;
        InitializeComponent();InputEditing.Install(this);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save,(s,e)=>SaveExcel_Click(s,e)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save,Key.S,ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Print,(s,e)=>Print_Click(s,e)));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Print,Key.P,ModifierKeys.Control));
        JobPreview.RefreshRequested+=(_,_)=>Print_Click(this,new RoutedEventArgs());
        JobPreview.PrinterRequested+=(_,_)=>ConfigurePrinter();
        JobPreview.PrintRequested+=PrintPreview_Requested;
        JobPreview.CancelRequested+=(_,_)=>_cancelPrint=true;
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
        else if(PreviewExample.SelectedIndex>0)ShowSample(PreviewExample.SelectedIndex==2);
        else if(_selectedBatchRow!=null)ShowEntry(_selectedBatchRow);
        else if(CuttingView.SelectedRow==null || CuttingView.SelectedRow.IsEmpty)ShowSample(false);
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
        if(_editing!=null)JobPreview.Invalidate("An entry-details edit is in progress.");
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
    private void ShowSample(bool dgu)
    {
        PreviewBadge.Text="SAMPLE";_samplePreview=true;_previewRow=null;_previewCopy=1;
        _previewEntry=dgu?LiveStickerSamples.Dgu():LiveStickerSamples.Sgu();
        PreviewSource.Text=$"SAMPLE · {(dgu?"DGU outer / inner":"SGU")} demonstration only — not in the cutting list and never printed.";
        RenderPreview();
    }
    private void PreviewExample_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(_ready && !_choosingExample)SyncCutting(false);
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
        _previewCopy = Math.Clamp(_previewCopy, 1, label.Qty);
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
    private void PreviewPane_Changed(object sender, SelectionChangedEventArgs e) { if (_ready) RenderPreview(); }
    private void Preview_Click(object sender, RoutedEventArgs e) => PreviewForm();

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
        StickersSection.IsExpanded=true;ManualEntrySection.IsExpanded=true;_editing=row;
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
        var labels = GetPrintLabels();
        var pages = PageCount(labels);
        PrintSummary.Text = _cuttingIssues.Count>0 ? $"Printing blocked: {_cuttingIssues.Count} incomplete row(s). {_cuttingIssues[0]}" : $"{pages:N0} pages ready  ·  {labels.Count:N0} pane labels";
        if(_editing!=null)PrintSummary.Text="Save or cancel the entry-details edit before printing.";
        PrintButton.IsEnabled = pages > 0 && _editing==null;
    }
    private long PageCount(IReadOnlyList<GlassLabel> labels) => RepeatQuantity.IsChecked == true ? labels.Sum(label => (long)label.Qty) : labels.Count;
    private void PrintOptions_Changed(object sender, SelectionChangedEventArgs e) { if (_ready) { JobPreview.Invalidate("Print scope, pane filter or layout format changed."); RefreshBatch(); RenderPreview(); } }
    private void Repeat_Changed(object sender, RoutedEventArgs e) { if (_ready) { JobPreview.Invalidate("Quantity expansion changed."); RefreshBatch(); } }

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

    public LabelPaginator PreparePrintPreview()
    {
        if(_sending)throw new InvalidOperationException("Wait for the current print operation to finish.");
        CuttingView.CommitEdits();SyncCutting(false);
        if(_editing!=null)throw new FormatException("Save or cancel the current entry-details edit before preparing a print job.");
        if(_cuttingIssues.Count>0)throw new FormatException("Complete the cutting rows before printing. "+_cuttingIssues[0]);
        var labels=GetPrintLabels();
        if(labels.Count==0)throw new FormatException("No saved cutting labels match this scope. Samples are preview-only. Add a complete row, or change the scope/pane filter.");
        if(PageCount(labels)>10000)throw new FormatException("A job is limited to 10,000 sticker pages. Reduce the scope or turn off Qty copies.");
        var job=new LabelPaginator(labels,RepeatQuantity.IsChecked==true,_company,_layouts,FormatOverride,NumberStyle);
        var scope=PrintScope.SelectedIndex==1?$"selected cutting row {CuttingView.SelectedRow?.Serial}":"all cutting rows";
        var layers=PrintLayers.SelectedIndex switch {1=>"outer / single only",2=>"inner only",_=>"outer + inner"};
        JobPreview.LoadJob(job,$"{scope} · {layers} · {(RepeatQuantity.IsChecked==true?"all Qty copies":"one copy per pane")}");
        StickersSection.IsExpanded=true;PrintPreviewSection.IsExpanded=true;
        Status.Text="Print preview prepared. Browse the actual pages, choose printer settings, then print current, range or all. Nothing has been sent.";
        return job;
    }
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
        if(JobPreview.Job==null || JobPreview.IsStale){Status.Text="Prepare or refresh the print preview before choosing printer settings.";return;}
        var size=JobPreview.Job.PageSize;
        StudioDialog.Info(this,"Printer settings only",$"Choose {size.Width/LabelView.UnitsPerMm:0.##} × {size.Height/LabelView.UnitsPerMm:0.##} mm stock, Actual size / 100%, Copies 1, one page per sheet and one-sided printing.\n\nThe Windows dialog's Print/OK button ONLY applies settings here; no sticker is sent. Review the result in the app, then use Print current, Print range or Print all.");
        try
        {
            if(_printer.Configure(size) && _printer.Review is {} review)
            {JobPreview.SetPrinter(review);Status.Text=review.BlockReason??"Printer settings reviewed. Inspect the preview and boundary guide before sending pages.";}
        }
        catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException){Error(ex);}
    }
    private async void PrintPreview_Requested(object? sender,PreviewPrintRequestEventArgs e)
    {
        if(_sending)return;
        CuttingView.CommitEdits();SyncCutting(false);
        if(!JobPreview.CanPrint || _editing!=null){Status.Text="Refresh the preview and review printer settings before printing. Nothing was sent.";return;}
        var job=JobPreview.Job!;var review=_printer.Review;
        if(review==null){Status.Text="Choose printer settings first.";return;}
        _cancelPrint=false;_sending=true;JobPreview.SetBusy(true);
        try
        {
            var pages=job.SelectPages(e.FirstPage,e.Count);
            var warnings=new List<string>();var warningPages=0;
            // Inspect every requested page with the same renderer, yielding so large jobs stay responsive.
            for(var i=0;i<pages.PageCount;i++)
            {
                using var page=pages.GetPage(i);
                var issues=JobPreview.WarningsFor((LabelView)page.Visual);
                if(issues.Count>0)
                {
                    warningPages++;
                    if(warnings.Count<8)warnings.Add($"Page {e.FirstPage+i+1}: "+string.Join("; ",issues.Take(2)));
                }
                if(i%25==0)
                {
                    Status.Text=$"Checking print pages {i+1:N0} / {pages.PageCount:N0}…";
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    if(_cancelPrint)throw new OperationCanceledException();
                    if(JobPreview.IsStale || !ReferenceEquals(job,JobPreview.Job))throw new InvalidOperationException("The cutting list or print options changed during review. Refresh the preview. Nothing was sent.");
                }
            }
            if(JobPreview.IsStale || !ReferenceEquals(review,_printer.Review))throw new InvalidOperationException("The job or printer settings changed. Review again before sending.");
            var range=e.Count==1?$"physical page {e.FirstPage+1}":$"physical pages {e.FirstPage+1}–{e.FirstPage+e.Count}";
            var caution=string.Join("\n",review.Warnings);
            if(warningPages>0)caution+=$"\n\n{warningPages} requested page(s) have layout/boundary warnings:\n"+string.Join("\n",warnings);
            if(!Confirm($"Send {e.Count:N0} sticker(s), {range} of {job.PageCount:N0}?\nOriginal row/copy numbering is preserved.\n\n{review.Description}\n\n{caution}\n\nOnly these reviewed snapshot pages will be sent. Test one sticker first. Continue?")){Status.Text="Print cancelled. Nothing was sent.";return;}
            if(JobPreview.IsStale)throw new InvalidOperationException("Preview is out of date. Nothing was sent.");
            var printer=_printer.Send(pages);
            Status.Text=$"Sent {e.Count:N0} sticker pages to {printer} ({range}). Check the Windows queue and physical output.";
        }
        catch(OperationCanceledException){Status.Text="Print review cancelled. Nothing was sent.";}
        catch(Exception ex) when(ex is PrintSystemException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.Xml.XmlException or System.Windows.Xps.XpsException or IOException or UnauthorizedAccessException){Status.Text="Printing did not complete. Review the error and check the Windows queue before retrying.";Error(ex);}
        finally{_sending=false;JobPreview.SetBusy(false);}
    }
    private void ToggleStickers_Click(object sender,RoutedEventArgs e)
    {
        StickersSection.IsExpanded=!StickersSection.IsExpanded;
        if(StickersSection.IsExpanded)Dispatcher.BeginInvoke(new Action(()=>StickersSection.BringIntoView(new Rect(0,0,StickersSection.ActualWidth,32))));
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
        if(_sending){Status.Text="Finish or cancel the print checks before opening Sticker Studio.";return;}
        var format = FormatOverride ?? (_previewEntry?.IsDouble == true ? StickerFormat.DGU : StickerFormat.SGU);
        var editor = new StickerEditorWindow(_layouts, _company, StudioServices.LayoutPath, format) { Owner = this };
        if (editor.ShowDialog() == true && editor.SavedLibrary is { } saved)
        {
            _layouts = saved;
            JobPreview.Invalidate("Sticker layouts changed.");
            JobPreview.ClearPrinter();
            RenderPreview();
            Status.Text = "Sticker formats saved. Preview and printer now use these same layouts.";
        }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if(_sending){e.Cancel=true;Status.Text="Finish or cancel the print checks before closing.";return;}
        if ((_editing!=null || HasCuttingWork) && !Confirm("Close LABLE STUDIO? The cutting sheet is in memory only. Save Excel before closing to keep your work.")) e.Cancel = true;
    }
}
