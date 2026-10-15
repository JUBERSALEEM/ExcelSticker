using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Wpf.Studio;

internal static class CuttingWorkspaceChecks
{
    public static void Run(MainWindow main)
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
        void Flush(){main.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);main.UpdateLayout();}
        var view=(CuttingWorkspace)main.FindName("CuttingView");var group=view.Sheet.Groups[0];
        var grid=Children(view).OfType<DataGrid>().First();
        Check(grid.Columns.Take(11).Select(c=>c.Header?.ToString()).SequenceEqual(CuttingTable.Columns),"Cutting table exposes the eleven requested columns in order");
        Check(!grid.IsReadOnly && grid.SelectionUnit==DataGridSelectionUnit.CellOrRowHeader && grid.ClipboardCopyMode==DataGridClipboardCopyMode.IncludeHeader,"Cutting table is editable and supports native rectangular copying");
        Check(group.Rows.Count==5 && group.Rows.All(r=>r.IsEmpty) && group.ValidationSummary=="","Five blank starter rows are editable without validation warnings");
        group.Specification="CLEAR";group.BasePrice="170";
        CuttingTable.Paste(view.Sheet,group,"11\t1050\t2127\t0\t0\t2\t170\n4\t854\t1981\t0\t0\t2\t170");Flush();
        Check(group.TotalPrice==1334.50m && main.GetPrintLabels().Count==0,"Cutting prices work without inventing required label metadata");
        ((TextBox)main.FindName("OrderNo")).Text="CUT-1";((TextBox)main.FindName("CustomerName")).Text="Factory";
        view.SelectRow(group.Rows[0]);Flush();
        Check(main.GetPrintLabels().Count==2,"Complete cutting rows feed the existing printer model");
        group.Rows[0].W1="1200";Flush();
        Check(((LabelView)((ContentControl)main.FindName("PreviewHost")).Content).Label.Width==1200,"Inline dimension edits update the selected live sticker");
        var draft=new CuttingRow {W1="100"};group.Rows.Add(draft);Flush();
        Check(!((Button)main.FindName("PrintButton")).IsEnabled && main.GetPrintLabels().Count==0,"An incomplete row blocks printing rather than disappearing from the job");
        group.Rows.Remove(draft);group.GlassType="DGU";group.Specification="GREY + AIR + CLEAR";Flush();
        Check(main.GetPrintLabels().Count==4 && main.GetPrintLabels()[1].Width==1200,"DGU zero inner dimensions reuse the outer dimensions");
        grid.CurrentCell=new DataGridCellInfo(group.Rows[0],grid.Columns[2]);grid.Focus();grid.BeginEdit();Flush();
        var editor=Children(grid).OfType<TextBox>().FirstOrDefault(t=>t.IsKeyboardFocused);
        Check(editor!=null && editor.SelectionLength==editor.Text.Length,"Cell edit focuses its text box and selects its value for replacement");
        var active=editor==null?null:Parents(editor).OfType<DataGridCell>().FirstOrDefault();
        Check(active!=null && Equals(active.Background,main.FindResource("ClaySurface")),"Editing cell uses a neutral surface, not a solid-blue selection box");
        grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
        var section=(Expander)main.FindName("StickersSection");var owned=main.OwnedWindows.Count;
        ((Button)main.FindName("StickersButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
        Check(section.IsExpanded && main.OwnedWindows.Count==owned,"Stickers toolbar button opens an inline section, not a popup");
        Check(Math.Abs(((ContentControl)main.FindName("PreviewHost")).Width-LabelView.LabelWidth)<.001,"Inline sticker section retains native physical preview size");
        group.Specification="LAMI + COMPLEX + EXTRA + CLEAR";Flush();
        ((Button)main.FindName("EditEntryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!view.IsEnabled && !((Button)main.FindName("PrintButton")).IsEnabled,"Advanced details can repair an unprintable row without conflicting inline edits");
        ((TextBox)main.FindName("EntryOrderNo")).Text="OWN-1";((TextBox)main.FindName("EntryCustomerName")).Text="Own customer";
        ((TextBox)main.FindName("OuterGlass")).Text="LAMINATED";((TextBox)main.FindName("InnerGlass")).Text="CLEAR";
        ((Button)main.FindName("AddButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
        Check(view.IsEnabled && group.Rows[0].OrderNo=="OWN-1" && ((TextBox)main.FindName("OrderNo")).Text=="CUT-1" && ((TextBox)main.FindName("CustomerName")).Text=="Factory","Entry details preserve independent metadata and do not overwrite sheet defaults");
        group.Rows[0].Qty="3";group.Rows[0].W1="1300";
        Check(group.Rows[0].ToEntry("","","").Inner!.Qty==3 && group.Rows[0].ToEntry("","","").Inner!.Width==1300,"Saving details retains intentionally inherited inner quantities and sizes");
        ((Button)main.FindName("EditEntryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((TextBox)main.FindName("EntryOrderNo")).Text="UNSAVED";
        ((Button)main.FindName("CancelEditButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
        Check(group.Rows[0].OrderNo=="OWN-1" && view.IsEnabled,"Cancelling entry details leaves source data unchanged");
        using(var workbookStream=new System.IO.MemoryStream())
        {
            CuttingWorkbook.Write(workbookStream,view.Sheet,"CUT-1","Factory","");workbookStream.Position=0;
            using var workbook=new XlsxWorkbook(workbookStream);var restored=CuttingWorkbook.Read(workbook,"Cutting list");
            main.RestoreCuttingWorkbook(restored);Flush();
            Check(view.Sheet.Rows.Count()==5 && view.Sheet.ActiveRows.First().OrderNo=="OWN-1" && ((TextBox)main.FindName("OrderNo")).Text=="CUT-1","Opening a saved workbook restores slots, row metadata and sheet defaults");
            Check(!ReferenceEquals(view.Sheet.Groups[0],restored.Sheet.Groups[0]),"Opened workbook does not share live collection/event ownership with the staging document");
        }
        ((Expander)main.FindName("ManualEntrySection")).IsExpanded=false;section.IsExpanded=false;
        view.Sheet.Change(()=>{view.Sheet.Groups.Clear();view.Sheet.Groups.Add(CuttingGroup.Blank());});
        ((TextBox)main.FindName("OrderNo")).Text="";((TextBox)main.FindName("CustomerName")).Text="";
        Flush();Check(!view.Sheet.ActiveRows.Any(),"Cutting checks leave an empty sheet for existing workflow tests");
    }
    private static IEnumerable<DependencyObject> Parents(DependencyObject node)
    {while((node=VisualTreeHelper.GetParent(node))!=null)yield return node;}
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);yield return c;foreach(var n in Children(c))yield return n;}}
}
