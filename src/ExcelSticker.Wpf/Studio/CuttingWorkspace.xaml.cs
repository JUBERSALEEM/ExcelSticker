using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf.Studio;

public partial class CuttingWorkspace : UserControl
{
    public CuttingSheet Sheet { get; }=new();
    public CuttingRow? SelectedRow { get; private set; }
    public event EventHandler? Changed;
    public event EventHandler? SelectionChanged;
    private bool _selecting;
    public CuttingWorkspace()
    {
        InitializeComponent();DataContext=Sheet;
        Sheet.Groups.Add(new CuttingGroup());
        Sheet.Changed+=(_,_)=>{if(SelectedRow!=null&&!Sheet.Rows.Contains(SelectedRow))SelectedRow=null;Changed?.Invoke(this,EventArgs.Empty);};
    }
    public void SelectRow(CuttingRow row)
    {
        SelectedRow=row;_selecting=true;
        try{foreach(var grid in Grids(this)){grid.UnselectAllCells();if(ReferenceEquals(grid.DataContext,row.Group)){grid.SelectedItem=row;grid.ScrollIntoView(row);}}}
        finally{_selecting=false;}
        SelectionChanged?.Invoke(this,EventArgs.Empty);
    }
    private void SelectFrom(DataGrid grid,CuttingRow row)
    {
        if(_selecting)return;SelectedRow=row;
        _selecting=true;try{foreach(var other in Grids(this).Where(g=>g!=grid))other.UnselectAllCells();}finally{_selecting=false;}
        SelectionChanged?.Invoke(this,EventArgs.Empty);
    }
    private void Grid_SelectionChanged(object sender,SelectionChangedEventArgs e) { if(sender is DataGrid grid && grid.SelectedItem is CuttingRow row)SelectFrom(grid,row); }
    private void Grid_SelectedCellsChanged(object sender,SelectedCellsChangedEventArgs e) { if(sender is DataGrid grid && e.AddedCells.FirstOrDefault().Item is CuttingRow row)SelectFrom(grid,row); }
    private void Grid_Loaded(object sender,RoutedEventArgs e){if(sender is DataGrid grid && SelectedRow!=null && ReferenceEquals(grid.DataContext,SelectedRow.Group))grid.SelectedItem=SelectedRow;}
    public void CommitEdits(){foreach(var grid in Grids(this)){grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);}}
    private void Safe(Action action){try{action();}catch(Exception ex) when(ex is FormatException or ExternalException or InvalidOperationException){Feedback.Text=ex.Message;}}
    private void AddSpec_Click(object sender,RoutedEventArgs e)=>Sheet.Groups.Add(new CuttingGroup());
    private void AddRow_Click(object sender,RoutedEventArgs e)=>Safe(()=>{var group=(CuttingGroup)((FrameworkElement)sender).DataContext;Sheet.CheckCapacity(1);var row=new CuttingRow();group.Rows.Add(row);SelectRow(row);Feedback.Text="New row added. Fill dimensions and quantity; blank Price uses the group base rate.";});
    private void RemoveSpec_Click(object sender,RoutedEventArgs e)
    {
        var group=(CuttingGroup)((FrameworkElement)sender).DataContext;
        if(group.Rows.Count>0 && !StudioDialog.Confirm(Window.GetWindow(this),"Remove specification","Remove this specification and all its rows?"))return;
        Sheet.Groups.Remove(group);if(Sheet.Groups.Count==0)Sheet.Groups.Add(new CuttingGroup());
    }
    private void RemoveRow_Click(object sender,RoutedEventArgs e)
    {
        var row=(CuttingRow)((FrameworkElement)sender).DataContext;
        if(StudioDialog.Confirm(Window.GetWindow(this),"Remove row",$"Remove cutting row {row.Serial}?"))row.Group?.Rows.Remove(row);
    }
    private void Paste_Click(object sender,RoutedEventArgs e)=>Safe(()=>Paste((CuttingGroup)((FrameworkElement)sender).DataContext,null,1));
    private void Paste(CuttingGroup group,int? row,int column)
    {
        CommitEdits();var count=CuttingTable.Paste(Sheet,group,Clipboard.GetText(TextDataFormat.UnicodeText),row,column);
        Feedback.Text=$"Pasted {count} row(s). Calculated columns were recomputed.";
        if(count>0)SelectRow(group.Rows[row??(group.Rows.Count-count)]);
    }
    private void Grid_KeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key!=Key.V || (Keyboard.Modifiers&ModifierKeys.Control)==0)return;
        e.Handled=true;
        Safe(()=>{var grid=(DataGrid)sender;var group=(CuttingGroup)grid.DataContext;
            var row=grid.CurrentCell.Item as CuttingRow;
            Paste(group,row==null?null:group.Rows.IndexOf(row),grid.CurrentCell.Column?.DisplayIndex??1);});
    }
    private static IEnumerable<DataGrid> Grids(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is DataGrid grid)yield return grid;else foreach(var nested in Grids(child))yield return nested;}
    }
}
