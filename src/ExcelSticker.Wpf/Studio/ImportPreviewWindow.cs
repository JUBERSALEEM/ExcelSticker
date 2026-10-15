using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExcelSticker.Core;
using ExcelSticker.Theming;
namespace ExcelSticker.Wpf.Studio;

/// <summary>A preview/validation step; never mutates the batch on cancel or error.</summary>
public sealed class ImportPreviewWindow : Window
{
    private sealed record Column(int Index, string Name);
    private readonly IReadOnlyList<string[]> _rows;
    private readonly ComboBox _header = new();
    private readonly TextBox _last = new();
    private readonly CheckBox _comma = new() { Content = "Comma decimals (not thousands)", Margin = new Thickness(0,8,0,8) };
    private readonly StackPanel _mapping = new();
    private readonly Dictionary<string, ComboBox> _columns = new();
    private readonly Dictionary<string, TextBox> _defaults = new();
    private readonly DataGrid _raw = MakeGrid(), _preview = MakeGrid();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,12,4,12) };
    private readonly Button _import = new() { Content = "Import entries", IsEnabled = false };
    private bool _updating;
    public IReadOnlyList<GlassEntry>? Entries { get; private set; }
    public ImportPreviewWindow(IReadOnlyList<string[]> rows, string source)
    {
        if (rows.Count == 0) throw new FormatException("The file is empty."); _rows = rows;
        Title = "Import preview — " + source; Width = 1260; Height = 840; MinWidth = 920; MinHeight = 640;
        SetResourceReference(StyleProperty, typeof(Window)); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var title = new TextBlock { Text = "Map columns • validate • import", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(6,0,0,14) };
        DockPanel.SetDock(title,Dock.Top); root.Children.Add(title);
        var footer = new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer); footer.Children.Add(_status);
        var actions = new WrapPanel(); footer.Children.Add(actions);
        var validate = new Button { Content = "Validate preview" }; validate.Click += (_,_) => Validate(); actions.Children.Add(validate);
        _import.SetResourceReference(StyleProperty,"Primary"); _import.Click += (_,_) => { if (Validate()) DialogResult = true; }; actions.Children.Add(_import);
        actions.Children.Add(new Button { Content = "Cancel", IsCancel = true });
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) }); body.ColumnDefinitions.Add(new ColumnDefinition()); root.Children.Add(body);
        var options = new StackPanel(); body.Children.Add(new ClayCard { Content = new ScrollViewer { Content = options, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Padding = new Thickness(16), Margin = new Thickness(4,6,16,12) });
        options.Children.Add(new TextBlock { Text = "Header row (parsed preview)", FontWeight = FontWeights.Bold });
        _header.ItemsSource = rows.Take(50).Select((row,i) => $"{i+1}: {string.Join(" | ",row.Where(s=>!string.IsNullOrWhiteSpace(s)).Take(3))}").ToArray();
        _header.SelectedIndex = ImportMapping.SuggestHeader(rows); options.Children.Add(_header);
        options.Children.Add(new TextBlock { Text = "Last parsed row to include (1-based)" });
        _last.Text = rows.Count.ToString(CultureInfo.InvariantCulture); options.Children.Add(_last); options.Children.Add(_comma);
        options.Children.Add(new TextBlock { Text = "COLUMN MAPPING", FontWeight = FontWeights.Bold, Margin = new Thickness(0,12,0,10) }); options.Children.Add(_mapping);
        options.Children.Add(new TextBlock { Text = "FALLBACKS FOR MISSING/BLANK METADATA", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold, Margin = new Thickness(0,14,0,8) });
        options.Children.Add(new TextBlock { Text = "Optional file-wide values. No sample customer/order is applied automatically.", TextWrapping = TextWrapping.Wrap });
        foreach(var field in new[]{"OrderNo","CustomerName","ReferenceNo","JobNo"}) {
            options.Children.Add(new TextBlock {Text=field}); var box=new TextBox {MaxLength=field=="CustomerName"?80:field=="ReferenceNo"?60:40};
            System.Windows.Automation.AutomationProperties.SetName(box,field+" fallback");box.TextChanged+=(_,_)=>Invalidate();_defaults[field]=box;options.Children.Add(box);
        }
        var right=new Grid(); right.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());right.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(right,1);body.Children.Add(right);
        right.Children.Add(new TextBlock {Text="SOURCE DATA · first 50 rows after header",FontWeight=FontWeights.Bold,Margin=new Thickness(4,8,0,10)});
        Grid.SetRow(_raw,1);right.Children.Add(_raw);
        var heading=new TextBlock {Text="LABEL ENTRIES · validate to preview (first 50)",FontWeight=FontWeights.Bold,Margin=new Thickness(4,16,0,10)};Grid.SetRow(heading,2);right.Children.Add(heading);Grid.SetRow(_preview,3);right.Children.Add(_preview);
        _header.SelectionChanged+=(_,_)=>BuildMapping();_last.TextChanged+=(_,_)=>Invalidate();_comma.Click+=(_,_)=>Invalidate();
        System.Windows.Automation.AutomationProperties.SetName(_header,"Header row");System.Windows.Automation.AutomationProperties.SetName(_last,"Last included parsed row");
        _raw.AutoGeneratingColumn+=(_,e)=>{if(_raw.ItemsSource is DataView view && view.Table?.Columns[e.PropertyName] is {} col)e.Column.Header=col.Caption;};
        WindowPlacement.FitOnFirstShow(this);BuildMapping();Validate();
    }
    private static DataGrid MakeGrid()
    {
        var grid=new DataGrid {IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeight=36,ColumnWidth=new DataGridLength(145),Margin=new Thickness(4)};
        grid.SetResourceReference(BackgroundProperty,"ClaySurface");grid.SetResourceReference(ForegroundProperty,"Ink");grid.SetResourceReference(DataGrid.RowBackgroundProperty,"ClaySurface");grid.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty,"ClaySelection");return grid;
    }
    private void BuildMapping()
    {
        if(_header.SelectedIndex<0)return;
        _updating=true;_columns.Clear();_mapping.Children.Clear();var header=_rows[_header.SelectedIndex];var width=_rows.Max(row=>row.Length);
        var choices=new[]{new Column(-1,"(Not mapped)")}.Concat(Enumerable.Range(0,width).Select(i=>new Column(i,$"Column {i+1}: {(i<header.Length?header[i]:"(no heading)")}"))).ToArray();
        var suggested=ImportMapping.SuggestColumns(header);
        foreach(var field in ImportMapping.Fields) {
            _mapping.Children.Add(new TextBlock {Text=field,Margin=new Thickness(0,3,0,4)});
            var combo=new ComboBox {ItemsSource=choices,DisplayMemberPath=nameof(Column.Name),SelectedItem=choices.First(c=>c.Index==suggested[field])};
            System.Windows.Automation.AutomationProperties.SetName(combo,field+" source column");combo.SelectionChanged+=(_,_)=>Invalidate();_columns[field]=combo;_mapping.Children.Add(combo);
        }
        var data=new DataTable();
        for(var i=0;i<width;i++)data.Columns.Add("Column"+(i+1),typeof(string)).Caption=$"{i+1}: {(i<header.Length?header[i]:"")}";
        foreach(var row in _rows.Skip(_header.SelectedIndex+1).Take(50))data.Rows.Add(Enumerable.Range(0,width).Select(i=>(object)(i<row.Length?row[i]:"")).ToArray());
        _raw.ItemsSource=data.DefaultView;_updating=false;Invalidate();
    }
    private void Invalidate() {if(_updating)return;Entries=null;_import.IsEnabled=false;_preview.ItemsSource=null;_status.Text="Review header, columns and range, then Validate preview. Nothing has been added to the batch.";}
    private bool Validate()
    {
        try {
            if(!int.TryParse(_last.Text,out var last))throw new FormatException("Last row must be a whole number.");
            Entries=ImportMapping.Import(_rows,_header.SelectedIndex,last,_columns.ToDictionary(p=>p.Key,p=>(p.Value.SelectedItem as Column)?.Index??-1),_defaults.ToDictionary(p=>p.Key,p=>p.Value.Text),_comma.IsChecked==true);
            _preview.ItemsSource=Entries.Take(50).Select(e=>new{Order=e.Outer.OrderNo,Customer=e.Outer.CustomerName,Spec=e.Specification,OuterSize=e.Outer.Size,OuterQty=e.Outer.Qty,InnerSize=e.Inner?.Size??"Single",InnerQty=e.Inner?.Qty}).ToArray();
            _import.Content=$"Import {Entries.Count:N0} entries";_import.IsEnabled=true;_status.Text=$"Validated {Entries.Count:N0} entries. Check the mapped values, then Import. Existing batch entries will be retained.";return true;
        }catch(FormatException ex){Entries=null;_import.IsEnabled=false;_preview.ItemsSource=null;_status.Text=ex.Message;return false;}
    }
}
