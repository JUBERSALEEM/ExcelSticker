using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ExcelSticker.Core;
using ExcelSticker.Theming;
using Microsoft.Win32;

namespace ExcelSticker.Wpf.Studio;

/// <summary>Edits physical-mm layout data, never XAML. Editor-only handles/guides are not part of LabelView.</summary>
public sealed class StickerEditorWindow : Window
{
    private LayoutLibrary _draft;
    private readonly CompanyProfile _company;
    private readonly string _savePath;
    private readonly Stack<LayoutLibrary> _undo=new(),_redo=new();
    private readonly ListBox _elements=new() {MinHeight=180};
    private readonly StackPanel _properties=new();
    private readonly Canvas _stage=new() {Width=LabelView.LabelWidth,Height=LabelView.LabelHeight,Background=Brushes.White,Margin=new Thickness(30),Focusable=true};
    private readonly ContentControl _render=new();
    private readonly Canvas _overlay=new() {Width=LabelView.LabelWidth,Height=LabelView.LabelHeight,Background=Brushes.Transparent};
    private readonly TextBlock _status=new() {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,8,4,4)};
    private readonly TextBlock _warnings=new() {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,8,4,8)};
    private readonly ComboBox _format=new() {ItemsSource=Enum.GetValues<StickerFormat>(),SelectedIndex=0};
    private readonly ComboBox _pane=new() {ItemsSource=new[]{"Outer sample","Inner sample"},SelectedIndex=0};
    private readonly ComboBox _gridSize=new() {ItemsSource=new[]{"0.5","1","2"},SelectedIndex=1,Width=70,Margin=new Thickness(0,0,12,8)};
    private readonly CheckBox _snap=new() {Content="Snap (mm)",IsChecked=true,Margin=new Thickness(0,0,8,8)};
    private readonly CheckBox _guides=new() {Content="Grid + safe area",IsChecked=true,Margin=new Thickness(0,0,12,8)};
    private readonly ComboBox _zoom=new() {ItemsSource=new[]{"100%","150%","200%","300%"},SelectedIndex=1,Width=90};
    private Button? _undoButton, _redoButton;
    private string? _selectedId;
    private bool _refreshing,_dirty,_saved,_dragging;
    private Point _start;
    private double _startX,_startY,_startW,_startH;
    public LayoutLibrary? SavedLibrary {get;private set;}
    private StickerFormat ActiveFormat=>(StickerFormat)(_format.SelectedItem??StickerFormat.NormalJobOrder);
    private StickerLayout Layout=>_draft.For(ActiveFormat);
    private StickerElement? Selected=>Layout.Elements.FirstOrDefault(e=>e.Id==_selectedId);
    public StickerEditorWindow(LayoutLibrary library,CompanyProfile company,string savePath,StickerFormat format=StickerFormat.DGU)
    {
        _draft=library.Clone();foreach(var layout in _draft.Layouts)layout.UpgradeLegacyNumbering();_company=company;_savePath=savePath;
        System.Windows.Automation.AutomationProperties.SetName(_stage,"Sticker editor canvas. Select a field, then use arrow keys to move it.");
        System.Windows.Automation.AutomationProperties.SetName(_elements,"Sticker elements");
        System.Windows.Automation.AutomationProperties.SetName(_format,"Sticker format");
        System.Windows.Automation.AutomationProperties.SetName(_pane,"Sample pane");
        System.Windows.Automation.AutomationProperties.SetName(_zoom,"Editor zoom");
        System.Windows.Automation.AutomationProperties.SetName(_gridSize,"Snap spacing in millimeters");
        Title="Sticker Studio • Normal / SGU / DGU";Width=1280;Height=840;MinWidth=1050;MinHeight=680;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty,typeof(Window));
        var root=new DockPanel();root.SetResourceReference(MarginProperty,"ClayWorkspaceMargin");root.SetResourceReference(Panel.BackgroundProperty,"ClayCanvasTexture");Content=root;
        var top=new DockPanel {Margin=new Thickness(6,0,6,16)};DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        top.Children.Add(new TextBlock {Text="Sticker Studio",FontSize=26,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,24,0)});
        var topActions=new WrapPanel();top.Children.Add(topActions);
        Button AddAction(Panel panel,string label,Action action,bool primary=false)
        {
            var button=new Button {Content=label};if(primary)button.SetResourceReference(StyleProperty,"Primary");
            button.Click+=(_,_)=>Safe(action);panel.Children.Add(button);return button;
        }
        _undoButton=AddAction(topActions,"Undo",Undo);_redoButton=AddAction(topActions,"Redo",Redo);AddAction(topActions,"Reset format",()=>Change(()=>ReplaceLayout(StickerLayout.Default(ActiveFormat))));
        AddAction(topActions,"Import JSON",Import);AddAction(topActions,"Export all",Export);AddAction(topActions,"Export format",ExportFormat);
        AddAction(topActions,"Save all formats",()=>{_draft.Save(_savePath);SavedLibrary=_draft.Clone();_saved=true;DialogResult=true;},true);
        AddAction(topActions,"Cancel",Close);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);footer.Children.Add(_warnings);footer.Children.Add(_status);root.Children.Add(footer);
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(240)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(285)});root.Children.Add(grid);
        var left=new StackPanel();var leftCard=new ClayCard {Content=new ScrollViewer {Content=left,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Margin=new Thickness(6,6,12,12)};leftCard.SetResourceReference(MarginProperty,"ClayCardMargin");grid.Children.Add(leftCard);
        left.Children.Add(new TextBlock {Text="FORMAT LIBRARY",FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,8)});left.Children.Add(_format);left.Children.Add(_pane);
        left.Children.Add(new TextBlock {Text="Fields · list order is print order",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});left.Children.Add(_elements);
        var field=new ComboBox {ItemsSource=StickerLayout.Fields.Where(f=>f!="TotalQty").ToArray(),SelectedItem="CustomText",Margin=new Thickness(0,12,0,12)};left.Children.Add(field);System.Windows.Automation.AutomationProperties.SetName(field,"New data field");
        AddAction(left,"+ Add field",()=>Change(()=>{if(Layout.Elements.Count>=60)throw new FormatException("Maximum 60 fields.");var element=new StickerElement {Field=(string)field.SelectedItem,Height=(string)field.SelectedItem=="Line" ? 0.4 : 6};Layout.Elements.Add(element);_selectedId=element.Id;}));
        var operations=new WrapPanel();left.Children.Add(operations);
        AddAction(operations,"Duplicate",()=>{if(Selected is {} item)Change(()=>{if(Layout.Elements.Count>=60)throw new FormatException("Maximum 60 fields.");var clone=item.Clone();clone.Id=Guid.NewGuid().ToString("N");clone.X=Math.Min(100-clone.Width,clone.X+2);clone.Y=Math.Min(70-clone.Height,clone.Y+2);clone.Locked=false;Layout.Elements.Add(clone);_selectedId=clone.Id;});});
        AddAction(operations,"Delete",Delete);
        AddAction(operations,"Bring front",()=>Reorder(true));AddAction(operations,"Send back",()=>Reorder(false));
        var middle=new DockPanel {Margin=new Thickness(4,6,12,12)};Grid.SetColumn(middle,1);grid.Children.Add(middle);
        var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);middle.Children.Add(toolbar);toolbar.Children.Add(_zoom);toolbar.Children.Add(_guides);toolbar.Children.Add(_snap);toolbar.Children.Add(_gridSize);
        _stage.Children.Add(_render);_stage.Children.Add(_overlay);
        var scroll=new ScrollViewer {Content=_stage,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};scroll.SetResourceReference(BackgroundProperty,"ClaySelection");middle.Children.Add(scroll);
        var inspector=new ClayCard {Content=new ScrollViewer {Content=_properties,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Margin=new Thickness(4,6,6,12)};inspector.SetResourceReference(MarginProperty,"ClayCardMargin");Grid.SetColumn(inspector,2);grid.Children.Add(inspector);
        _format.SelectedItem=format;
        _format.SelectionChanged+=(_,_)=>{if(!_refreshing){_selectedId=null;Refresh();}};
        _pane.SelectionChanged+=(_,_)=>Render();
        _zoom.SelectionChanged+=(_,_)=>UpdateZoom();_guides.Click+=(_,_)=>Render();_gridSize.SelectionChanged+=(_,_)=>Render();
        _elements.SelectionChanged+=(_,_)=>{if(!_refreshing&&_elements.SelectedItem is StickerElement item){_selectedId=item.Id;BuildInspector();Render();}};
        _overlay.MouseMove+=DragMove;_overlay.MouseLeftButtonUp+=EndDrag;
        _overlay.LostMouseCapture+=(_,_)=>{if(_dragging){_dragging=false;Refresh();}};
        PreviewKeyDown+=Keys;
        Closing+=ClosingEditor;
        WindowPlacement.FitOnFirstShow(this);
        UpdateZoom();Refresh();
    }
    private void Safe(Action action)
    {
        try{action();}
        catch(Exception ex) when(ex is FormatException or IOException or UnauthorizedAccessException or JsonException or ArgumentException){_status.Text=ex.Message;}
    }
    private void ReplaceLayout(StickerLayout layout)=>_draft.Layouts[_draft.Layouts.FindIndex(l=>l.Format==layout.Format)]=layout;
    private void Change(Action edit)
    {
        var before=_draft.Clone();
        try{edit();_draft.Validate();}
        catch{_draft=before;Refresh();throw;}
        Remember(before);_dirty=true;Refresh();
    }
    private void Remember(LayoutLibrary before)
    {
        if(_undo.Count>=50){var keep=_undo.Take(49).Reverse().ToArray();_undo.Clear();foreach(var entry in keep)_undo.Push(entry);}
        _undo.Push(before);_redo.Clear();
    }
    private void Undo(){if(_undo.Count==0)return;_redo.Push(_draft.Clone());_draft=_undo.Pop();_dirty=true;Refresh();}
    private void Redo(){if(_redo.Count==0)return;_undo.Push(_draft.Clone());_draft=_redo.Pop();_dirty=true;Refresh();}
    private void Delete(){if(Selected is {} item&&!item.Locked)Change(()=>Layout.Elements.Remove(item));}
    private void Reorder(bool front){if(Selected is {} item&&!item.Locked)Change(()=>{Layout.Elements.Remove(item);Layout.Elements.Insert(front?Layout.Elements.Count:0,item);});}
    private void Refresh()
    {
        _refreshing=true;
        if(_undoButton!=null)_undoButton.IsEnabled=_undo.Count>0;
        if(_redoButton!=null)_redoButton.IsEnabled=_redo.Count>0;
        Title="Sticker Studio • Normal / SGU / DGU"+(_dirty?" · unsaved":"");
        if(Selected==null)_selectedId=Layout.Elements.FirstOrDefault()?.Id;
        _elements.ItemsSource=null;_elements.ItemsSource=Layout.Elements;_elements.DisplayMemberPath=nameof(StickerElement.DisplayName);_elements.SelectedItem=Selected;
        _pane.IsEnabled=ActiveFormat==StickerFormat.DGU;
        _refreshing=false;BuildInspector();Render();
        _status.Text="Drag a field; drag its lower-right handle to resize. Arrows nudge 0.5 mm; Shift+arrows 2 mm. Ctrl+Z / Ctrl+Y. Positions are millimeters; fonts are points.";
    }
    private GlassLabel Sample()
    {
        var inner=ActiveFormat==StickerFormat.DGU&&_pane.SelectedIndex==1;
        var material=ActiveFormat==StickerFormat.DGU?(inner?"6MM CLEAR GLASS":"6MM HD GREY FT GLASS"):ActiveFormat==StickerFormat.SGU?"6MM CLEAR LOW E":"8MM TEMPERED GLASS";
        return GlassLabel.Create("7494/71","4",material,"W6* F5",ActiveFormat==StickerFormat.DGU?(inner?"2540":"2550"):"316",ActiveFormat==StickerFormat.DGU?(inner?"2640":"2650"):"800","FUJSENG","JO-2026-1042")
            with{Pane=ActiveFormat==StickerFormat.DGU?(inner?"INNER":"OUTER"):"",Specification=ActiveFormat==StickerFormat.DGU?"6MM HD GREY FT GLASS + 12MM ASP + 6MM CLEAR GLASS":material,Numbering=NumberingStyle.EntryCopy,CopyIndex=2};
    }
    private void Render(bool overlays=true)
    {
        var view=new LabelView(Sample(),_company,Layout);_render.Content=view;
        _warnings.Text=view.Warnings.Count==0?"100 × 70 mm • sample copy 1/2 • dashed safe area is 3 mm • guides and handles never print.":string.Join("\n",view.Warnings.Take(4));
        if(!overlays)return;
        _overlay.Children.Clear();
        if(_guides.IsChecked==true)
        {
            var step=double.Parse((string)_gridSize.SelectedItem,CultureInfo.InvariantCulture)*LabelView.UnitsPerMm;
            for(double x=0;x<LabelView.LabelWidth;x+=step)Guide(x,0,x,LabelView.LabelHeight);
            for(double y=0;y<LabelView.LabelHeight;y+=step)Guide(0,y,LabelView.LabelWidth,y);
            var safe=new Rectangle {Width=94*LabelView.UnitsPerMm,Height=64*LabelView.UnitsPerMm,Stroke=Brushes.SteelBlue,StrokeThickness=.6,StrokeDashArray=new DoubleCollection{4,3},IsHitTestVisible=false};
            Canvas.SetLeft(safe,3*LabelView.UnitsPerMm);Canvas.SetTop(safe,3*LabelView.UnitsPerMm);_overlay.Children.Add(safe);
            for(var n=0;n<=100;n+=10){var text=new TextBlock {Text=n.ToString(CultureInfo.InvariantCulture),FontSize=9,Foreground=Brushes.Black,IsHitTestVisible=false};Canvas.SetLeft(text,n*LabelView.UnitsPerMm);Canvas.SetTop(text,-16);_overlay.Children.Add(text);}
            for(var n=10;n<=70;n+=10){var text=new TextBlock {Text=n.ToString(CultureInfo.InvariantCulture),FontSize=9,Foreground=Brushes.Black,IsHitTestVisible=false};Canvas.SetLeft(text,-24);Canvas.SetTop(text,n*LabelView.UnitsPerMm-6);_overlay.Children.Add(text);}
        }
        foreach(var item in Layout.Elements.Where(e=>e.Visible))
        {
            var box=new Border {Width=item.Width*LabelView.UnitsPerMm,Height=Math.Max(4,item.Height*LabelView.UnitsPerMm),Background=Brushes.Transparent,BorderBrush=item.Id==_selectedId?Brushes.DodgerBlue:Brushes.Transparent,BorderThickness=new Thickness(1),Cursor=item.Locked?Cursors.Arrow:Cursors.SizeAll,Tag=item.Id};
            Canvas.SetLeft(box,item.X*LabelView.UnitsPerMm);Canvas.SetTop(box,item.Y*LabelView.UnitsPerMm);_overlay.Children.Add(box);
            box.MouseLeftButtonDown+=(_,e)=>
            {
                _selectedId=item.Id;_stage.Focus();Refresh();e.Handled=true;
                if(Selected is not {Locked:false} active)return;
                Remember(_draft.Clone());_start=Mouse.GetPosition(_stage);_startX=active.X;_startY=active.Y;_dragging=true;_overlay.CaptureMouse();
            };
        }
        if(Selected is {Visible:true,Locked:false} selected)
        {
            var handle=new Thumb {Width=9,Height=9,Cursor=Cursors.SizeNWSE,Background=Brushes.DodgerBlue};
            Canvas.SetLeft(handle,(selected.X+selected.Width)*LabelView.UnitsPerMm-5);Canvas.SetTop(handle,(selected.Y+selected.Height)*LabelView.UnitsPerMm-5);_overlay.Children.Add(handle);
            handle.DragStarted+=(_,_)=>{Remember(_draft.Clone());_start=Mouse.GetPosition(_stage);_startW=selected.Width;_startH=selected.Height;};
            handle.DragDelta+=(_,_)=>
            {
                var delta=Mouse.GetPosition(_stage)-_start;
                selected.Width=Math.Clamp(Snap(_startW+delta.X/LabelView.UnitsPerMm),1,100-selected.X);
                selected.Height=Math.Clamp(Snap(_startH+delta.Y/LabelView.UnitsPerMm),.2,70-selected.Y);
                Canvas.SetLeft(handle,(selected.X+selected.Width)*LabelView.UnitsPerMm-5);Canvas.SetTop(handle,(selected.Y+selected.Height)*LabelView.UnitsPerMm-5);
                _dirty=true;Render(false);
            };
            handle.DragCompleted+=(_,_)=>Refresh();
        }
    }
    private void Guide(double x1,double y1,double x2,double y2)=>_overlay.Children.Add(new Line {X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=Brushes.LightSteelBlue,StrokeThickness=.25,IsHitTestVisible=false});
    private double Snap(double value){var step=double.Parse((string)_gridSize.SelectedItem,CultureInfo.InvariantCulture);return _snap.IsChecked==true?Math.Round(value/step)*step:value;}
    private void DragMove(object sender,MouseEventArgs e)
    {
        if(!_dragging||Selected is not {} selected)return;
        var delta=Mouse.GetPosition(_stage)-_start;
        selected.X=Math.Clamp(Snap(_startX+delta.X/LabelView.UnitsPerMm),0,100-selected.Width);selected.Y=Math.Clamp(Snap(_startY+delta.Y/LabelView.UnitsPerMm),0,70-selected.Height);
        _dirty=true;Render(false);
        foreach(var handle in _overlay.Children.OfType<Thumb>()){Canvas.SetLeft(handle,(selected.X+selected.Width)*LabelView.UnitsPerMm-5);Canvas.SetTop(handle,(selected.Y+selected.Height)*LabelView.UnitsPerMm-5);}
        foreach(var box in _overlay.Children.OfType<Border>().Where(b=>(string?)b.Tag==selected.Id)){Canvas.SetLeft(box,selected.X*LabelView.UnitsPerMm);Canvas.SetTop(box,selected.Y*LabelView.UnitsPerMm);}
    }
    private void EndDrag(object sender,MouseButtonEventArgs e){if(!_dragging)return;_dragging=false;_overlay.ReleaseMouseCapture();Refresh();}
    private void UpdateZoom(){var scale=double.Parse(((string)_zoom.SelectedItem).TrimEnd('%'),CultureInfo.InvariantCulture)/100;_stage.LayoutTransform=new ScaleTransform(scale,scale);}
    private void BuildInspector()
    {
        var wasRefreshing=_refreshing;_refreshing=true;
        try { BuildInspectorCore(); } finally { _refreshing=wasRefreshing; }
    }
    private void BuildInspectorCore()
    {
        _properties.Children.Clear();
        if(Selected is not {} element)return;
        _properties.Children.Add(new TextBlock {Text="FIELD INSPECTOR",FontSize=17,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12)});
        Choice("Field",StickerLayout.Fields.Where(f=>f!="TotalQty").ToArray());Text("Prefix","Label prefix");Text("Text","Custom text");
        foreach(var name in new[]{"X","Y","Width","Height"})Numeric(name,name+" (mm)");
        Numeric("FontSize","Font size (pt)");Choice("FontFamily",new[]{"Arial","Segoe UI","Calibri","Verdana","Tahoma"});Choice("Alignment",new[]{"Left","Center","Right"});
        foreach(var name in new[]{"Bold","Underline","Wrap","AutoFit","Visible","Locked"})Toggle(name);
        Numeric("Stroke","Line / box stroke (mm)");
        var actions=new WrapPanel();_properties.Children.Add(actions);
        foreach(var name in new[]{"Left","Center X","Right","Top","Center Y","Bottom"})
        {
            var button=new Button {Content=name,FontSize=12,Padding=new Thickness(8),Margin=new Thickness(0,0,6,6)};
            button.Click+=(_,_)=>Safe(()=>{if(element.Locked)return;Change(()=>{switch(name){case"Left":element.X=3;break;case"Center X":element.X=(100-element.Width)/2;break;case"Right":element.X=97-element.Width;break;case"Top":element.Y=3;break;case"Center Y":element.Y=(70-element.Height)/2;break;case"Bottom":element.Y=67-element.Height;break;}});});actions.Children.Add(button);
        }
        void Label(string text)=>_properties.Children.Add(new TextBlock {Text=text,Margin=new Thickness(0,4,0,4)});
        void Commit(Action action){if(!_refreshing)Safe(()=>Change(action));}
        void Text(string property,string label)
        {
            Label(label);var info=typeof(StickerElement).GetProperty(property)!;var box=new TextBox {Text=(string)info.GetValue(element)!,MaxLength=property=="Prefix"?80:240,IsEnabled=!element.Locked};
            System.Windows.Automation.AutomationProperties.SetName(box,label);
            box.LostKeyboardFocus+=(_,_)=>{if(!_refreshing&&box.Text!=(string)info.GetValue(element)!)Commit(()=>info.SetValue(element,box.Text));};_properties.Children.Add(box);
        }
        void Numeric(string property,string label)
        {
            Label(label);var info=typeof(StickerElement).GetProperty(property)!;var box=new TextBox {Text=((double)info.GetValue(element)!).ToString("0.##",CultureInfo.InvariantCulture),IsEnabled=!element.Locked};
            System.Windows.Automation.AutomationProperties.SetName(box,label);
            box.LostKeyboardFocus+=(_,_)=>{if(_refreshing)return;Safe(()=>{if(!double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n))throw new FormatException("Use a dot decimal number for "+label);if(n!=(double)info.GetValue(element)!)Change(()=>info.SetValue(element,n));});};_properties.Children.Add(box);
        }
        void Choice(string property,string[] choices)
        {
            Label(property);var info=typeof(StickerElement).GetProperty(property)!;var box=new ComboBox {ItemsSource=choices,SelectedItem=info.GetValue(element),IsEnabled=!element.Locked};
            System.Windows.Automation.AutomationProperties.SetName(box,property);
            box.SelectionChanged+=(_,_)=>{if(box.SelectedItem is string value&&value!=(string)info.GetValue(element)!)Commit(()=>info.SetValue(element,value));};_properties.Children.Add(box);
        }
        void Toggle(string property)
        {
            var info=typeof(StickerElement).GetProperty(property)!;var box=new CheckBox {Content=property,IsChecked=(bool)info.GetValue(element)!,IsEnabled=!element.Locked||property=="Locked"};box.Click+=(_,_)=>Commit(()=>info.SetValue(element,box.IsChecked==true));_properties.Children.Add(box);
        }
    }
    private void Keys(object sender,KeyEventArgs e)
    {
        if(Keyboard.FocusedElement is TextBox)return;
        if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Z){Undo();e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Y){Redo();e.Handled=true;return;}
        if(!(_stage.IsKeyboardFocusWithin||_elements.IsKeyboardFocusWithin))return;
        if(e.Key==Key.Delete){Safe(Delete);e.Handled=true;return;}
        if(Selected is not {Locked:false} item)return;
        var step=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?2:.5;
        var dx=e.Key==Key.Left?-step:e.Key==Key.Right?step:0;var dy=e.Key==Key.Up?-step:e.Key==Key.Down?step:0;
        if(dx==0&&dy==0)return;
        Safe(()=>Change(()=>{item.X=Math.Clamp(item.X+dx,0,100-item.Width);item.Y=Math.Clamp(item.Y+dy,0,70-item.Height);}));e.Handled=true;
    }
    private void Import()
    {
        var dialog=new OpenFileDialog {Filter="Sticker layout / library (*.json)|*.json"};
        if(dialog.ShowDialog(this)!=true)return;
        if(new FileInfo(dialog.FileName).Length>512*1024)throw new FormatException("Layout file is too large.");
        using var document=JsonDocument.Parse(File.ReadAllText(dialog.FileName));
        if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new FormatException("Layout JSON must be an object.");
        if(document.RootElement.TryGetProperty("Layouts",out _)){var imported=LayoutLibrary.Load(dialog.FileName);Change(()=>_draft=imported);}
        else {var imported=StickerLayout.Load(dialog.FileName);Change(()=>ReplaceLayout(imported));_format.SelectedItem=imported.Format;}
    }
    private void ExportFormat()
    {
        var dialog=new SaveFileDialog {Filter="Sticker format (*.json)|*.json",FileName=ActiveFormat+"-sticker.json"};
        if(dialog.ShowDialog(this)==true){Layout.Save(dialog.FileName);_status.Text="Single format exported. Import it without replacing the other two formats.";}
    }
    private void Export(){var dialog=new SaveFileDialog {Filter="Sticker layout library (*.json)|*.json",FileName="ocean-glass-sticker-layouts.json"};if(dialog.ShowDialog(this)==true){_draft.Save(dialog.FileName);_status.Text="Layout library exported. Save all formats to activate these layouts in the app.";}}
    private void ClosingEditor(object? sender,CancelEventArgs e){if(_dirty&&!_saved&&!StudioDialog.Confirm(this,"Discard layout edits?","These layout changes have not been saved. Continue to discard them?"))e.Cancel=true;}
}
