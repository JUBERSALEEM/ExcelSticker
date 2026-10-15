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
    private readonly ComboBox _zoom=new() {ItemsSource=new[]{"100%","150%","200%","300%","Fit sticker"},SelectedIndex=4,Width=110};
    private readonly ScrollViewer _viewport=new() {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly Dictionary<string,TextBox> _geometryInputs=new();
    private readonly Dictionary<string,string> _geometryInitialText=new();
    private StickerElement? _geometryElement;
    private StickerFormat _geometryFormat;
    private bool _applyingGeometry;
    private double _displayedStockWidth=double.NaN,_displayedStockHeight=double.NaN;
    private string _initialStockWidthText="",_initialStockHeightText="";
    private readonly TextBox _stockWidth=new(),_stockHeight=new();
    private readonly CheckBox _scaleContents=new() {Content=new TextBlock {Text="Scale field boxes to new stock",TextWrapping=TextWrapping.Wrap},IsChecked=true};
    private readonly CheckBox _allLogos=new() {Content=new TextBlock {Text="Import logo into all formats",TextWrapping=TextWrapping.Wrap},IsChecked=true};
    private Button? _undoButton, _redoButton;
    private string? _selectedId;
    private bool _refreshing,_dirty,_saved,_dragging;
    private Point _start;
    private double _startX,_startY,_startW,_startH;
    public LayoutLibrary? SavedLibrary {get;private set;}
    private StickerFormat ActiveFormat=>(StickerFormat)(_format.SelectedItem??StickerFormat.NormalJobOrder);
    private StickerLayout Layout=>_draft.For(ActiveFormat);
    private StickerElement? Selected=>Layout.Elements.FirstOrDefault(e=>e.Id==_selectedId);
    public StickerEditorWindow(LayoutLibrary library,CompanyProfile company,string savePath,StickerFormat format=StickerFormat.DGU,bool importLogoOnOpen=false)
    {
        InputEditing.Install(this);
        _draft=library.Clone();LogoImages.ValidateLibrary(_draft);foreach(var layout in _draft.Layouts)layout.UpgradeLegacyNumbering();_company=company;_savePath=savePath;
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
        _undoButton=AddAction(topActions,"Undo",Undo);_redoButton=AddAction(topActions,"Redo",Redo);AddAction(topActions,"Simple pattern (keep logos)",ApplySimplePattern);
        AddAction(topActions,"Import JSON",Import);AddAction(topActions,"Export all",Export);AddAction(topActions,"Export format",ExportFormat);
        AddAction(topActions,"Save all formats",()=>{CommitForSave();LogoImages.ValidateLibrary(_draft);_draft.Save(_savePath);SavedLibrary=_draft.Clone();_saved=true;DialogResult=true;},true);
        AddAction(topActions,"Cancel",Close);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);footer.Children.Add(_warnings);footer.Children.Add(_status);root.Children.Add(footer);
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(240)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(285)});root.Children.Add(grid);
        var left=new StackPanel();var leftCard=new ClayCard {Content=new ScrollViewer {Content=left,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Margin=new Thickness(6,6,12,12)};leftCard.SetResourceReference(MarginProperty,"ClayCardMargin");grid.Children.Add(leftCard);
        left.Children.Add(new TextBlock {Text="FORMAT LIBRARY",FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,8)});left.Children.Add(_format);left.Children.Add(_pane);
        left.Children.Add(new TextBlock {Text="STICKER STOCK · all formats",FontWeight=FontWeights.Bold,Margin=new Thickness(0,8,0,6)});
        left.Children.Add(new TextBlock {Text="Width (mm)"});left.Children.Add(_stockWidth);
        left.Children.Add(new TextBlock {Text="Height (mm)"});left.Children.Add(_stockHeight);
        System.Windows.Automation.AutomationProperties.SetName(_stockWidth,"Sticker stock width in millimeters");
        System.Windows.Automation.AutomationProperties.SetName(_stockHeight,"Sticker stock height in millimeters");
        left.Children.Add(_scaleContents);AddAction(left,"Apply stock size",ApplyStock);
        left.Children.Add(new TextBlock {Text="20–300 mm. Match the physical roll/driver. Uncheck scaling to keep field positions; clipping is rejected.",TextWrapping=TextWrapping.Wrap,FontSize=11});
        left.Children.Add(_allLogos);AddAction(left,"Import / replace logo…",ImportLogo);
        left.Children.Add(new TextBlock {Text="PNG / JPEG / BMP → embedded grayscale logo. Select a logo to move, size, hide or delete it.",TextWrapping=TextWrapping.Wrap,FontSize=11});
        left.Children.Add(new TextBlock {Text="Fields · list order is print order",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});left.Children.Add(_elements);
        var field=new ComboBox {ItemsSource=StickerLayout.Fields.Where(f=>f!="TotalQty").ToArray(),SelectedItem="CustomText",Margin=new Thickness(0,12,0,12)};left.Children.Add(field);System.Windows.Automation.AutomationProperties.SetName(field,"New data field");
        AddAction(left,"+ Add field",()=>Change(()=>{if(Layout.Elements.Count>=60)throw new FormatException("Maximum 60 fields.");var element=new StickerElement {Field=(string)field.SelectedItem,Width=Math.Min(45,Layout.WidthMm-8),Height=(string)field.SelectedItem=="Line" ? 0.4 : 6};Layout.Elements.Add(element);_selectedId=element.Id;}));
        var operations=new WrapPanel();left.Children.Add(operations);
        AddAction(operations,"Duplicate",()=>{if(Selected is {} item)Change(()=>{if(Layout.Elements.Count>=60)throw new FormatException("Maximum 60 fields.");var clone=item.Clone();clone.Id=Guid.NewGuid().ToString("N");clone.X=Math.Min(Layout.WidthMm-clone.Width,clone.X+2);clone.Y=Math.Min(Layout.HeightMm-clone.Height,clone.Y+2);clone.Locked=false;Layout.Elements.Add(clone);_selectedId=clone.Id;});});
        AddAction(operations,"Delete",Delete);
        AddAction(operations,"Bring front",()=>Reorder(true));AddAction(operations,"Send back",()=>Reorder(false));
        var middle=new DockPanel {Margin=new Thickness(4,6,12,12)};Grid.SetColumn(middle,1);grid.Children.Add(middle);
        var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);middle.Children.Add(toolbar);toolbar.Children.Add(_zoom);toolbar.Children.Add(_guides);toolbar.Children.Add(_snap);toolbar.Children.Add(_gridSize);
        _stage.Children.Add(_render);_stage.Children.Add(_overlay);
        _viewport.Content=_stage;_viewport.SetResourceReference(BackgroundProperty,"ClaySelection");middle.Children.Add(_viewport);
        _viewport.SizeChanged+=(_,_)=>{if(_zoom.SelectedIndex==4)UpdateZoom();};
        var inspector=new ClayCard {Content=new ScrollViewer {Content=_properties,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Margin=new Thickness(4,6,6,12)};inspector.SetResourceReference(MarginProperty,"ClayCardMargin");Grid.SetColumn(inspector,2);grid.Children.Add(inspector);
        _format.SelectedItem=format;
        _format.SelectionChanged+=(_,_)=>
        {
            if(_refreshing)return;
            if(!TryApplyGeometry()){_refreshing=true;_format.SelectedItem=_geometryFormat;_refreshing=false;return;}
            _selectedId=null;Refresh();
        };
        _pane.SelectionChanged+=(_,_)=>Render();
        _zoom.SelectionChanged+=(_,_)=>UpdateZoom();_guides.Click+=(_,_)=>Render();_gridSize.SelectionChanged+=(_,_)=>Render();
        _elements.SelectionChanged+=(_,_)=>
        {
            if(_refreshing || _elements.SelectedItem is not StickerElement item)return;
            if(!TryApplyGeometry()){_refreshing=true;_elements.SelectedItem=Selected;_refreshing=false;return;}
            _selectedId=item.Id;_refreshing=true;_elements.SelectedItem=item;_refreshing=false;BuildInspector();Render();
        };
        _overlay.MouseMove+=DragMove;_overlay.MouseLeftButtonUp+=EndDrag;
        _overlay.LostMouseCapture+=(_,_)=>{if(_dragging){_dragging=false;Refresh();}};
        PreviewKeyDown+=Keys;
        Closing+=ClosingEditor;
        WindowPlacement.FitOnFirstShow(this);
        UpdateZoom();Refresh();
        if(importLogoOnOpen)ContentRendered+=ImportLogoOnFirstRender;
        void ImportLogoOnFirstRender(object? sender,EventArgs e)
        {
            ContentRendered-=ImportLogoOnFirstRender;
            Dispatcher.BeginInvoke(new Action(()=>{if(IsVisible)Safe(ImportLogo);}));
        }
    }
    private void Safe(Action action)
    {
        try{action();}
        catch(Exception ex) when(ex is FormatException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException){_status.Text=ex.Message;}
    }
    private void ReplaceLayout(StickerLayout layout)=>_draft.Layouts[_draft.Layouts.FindIndex(l=>l.Format==layout.Format)]=layout;
    private void Change(Action edit,bool rebuildInspector=true)
    {
        if(!_applyingGeometry)ApplyGeometry();
        var before=_draft.Clone();
        try{edit();_draft.Validate();}
        catch{_draft=before;Refresh();throw;}
        Remember(before);_dirty=true;
        if(rebuildInspector)Refresh();
        else
        {
            // Keep the clicked/focused inspector control alive when a numeric/text edit commits.
            Render();if(_undoButton!=null)_undoButton.IsEnabled=true;if(_redoButton!=null)_redoButton.IsEnabled=false;
            Title="Sticker Studio • Normal / SGU / DGU · unsaved";
        }
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
        if(_displayedStockWidth!=Layout.WidthMm || _displayedStockHeight!=Layout.HeightMm)
        {
            _stockWidth.Text=Layout.WidthMm.ToString("0.###",CultureInfo.InvariantCulture);_stockHeight.Text=Layout.HeightMm.ToString("0.###",CultureInfo.InvariantCulture);
            _displayedStockWidth=Layout.WidthMm;_displayedStockHeight=Layout.HeightMm;
            _initialStockWidthText=_stockWidth.Text;_initialStockHeightText=_stockHeight.Text;
        }
        _pane.IsEnabled=ActiveFormat==StickerFormat.DGU;
        _refreshing=false;BuildInspector();Render();
        _status.Text="Drag a field; drag its lower-right handle to resize. Arrows nudge 0.5 mm; Shift+arrows 2 mm. Ctrl+Z / Ctrl+Y. Positions are millimeters; fonts are points.";
    }
    private GlassLabel Sample()
    {
        var entry=ActiveFormat==StickerFormat.DGU?LiveStickerSamples.Dgu():LiveStickerSamples.Sgu();
        var label=entry.IsDouble && _pane.SelectedIndex==1?entry.Inner!:entry.Outer;
        return label with {Numbering=NumberingStyle.EntryCopy,CopyIndex=2};
    }
    private void Render(bool overlays=true)
    {
        _stage.Width=_overlay.Width=Layout.WidthMm*LabelView.UnitsPerMm;_stage.Height=_overlay.Height=Layout.HeightMm*LabelView.UnitsPerMm;
        var view=new LabelView(Sample(),_company,Layout);_render.Content=view;
        _warnings.Text=view.Warnings.Count==0?$"{Layout.WidthMm:0.##} × {Layout.HeightMm:0.##} mm • sample copy 1/2 • dashed safe area is 3 mm • guides and handles never print.":string.Join("\n",view.Warnings.Take(4));
        if(_zoom.SelectedIndex==4)UpdateZoom();
        if(!overlays)return;
        _overlay.Children.Clear();
        if(_guides.IsChecked==true)
        {
            var step=double.Parse((string)_gridSize.SelectedItem,CultureInfo.InvariantCulture)*LabelView.UnitsPerMm;
            for(double x=0;x<_stage.Width;x+=step)Guide(x,0,x,_stage.Height);
            for(double y=0;y<_stage.Height;y+=step)Guide(0,y,_stage.Width,y);
            var safe=new Rectangle {Width=(Layout.WidthMm-6)*LabelView.UnitsPerMm,Height=(Layout.HeightMm-6)*LabelView.UnitsPerMm,Stroke=Brushes.SteelBlue,StrokeThickness=.6,StrokeDashArray=new DoubleCollection{4,3},IsHitTestVisible=false};
            Canvas.SetLeft(safe,3*LabelView.UnitsPerMm);Canvas.SetTop(safe,3*LabelView.UnitsPerMm);_overlay.Children.Add(safe);
            for(var n=0;n<=Layout.WidthMm;n+=10){var text=new TextBlock {Text=n.ToString(CultureInfo.InvariantCulture),FontSize=9,Foreground=Brushes.Black,IsHitTestVisible=false};Canvas.SetLeft(text,n*LabelView.UnitsPerMm);Canvas.SetTop(text,-16);_overlay.Children.Add(text);}
            for(var n=10;n<=Layout.HeightMm;n+=10){var text=new TextBlock {Text=n.ToString(CultureInfo.InvariantCulture),FontSize=9,Foreground=Brushes.Black,IsHitTestVisible=false};Canvas.SetLeft(text,-24);Canvas.SetTop(text,n*LabelView.UnitsPerMm-6);_overlay.Children.Add(text);}
        }
        foreach(var item in Layout.Elements.Where(e=>e.Visible))
        {
            var box=new Border {Width=item.Width*LabelView.UnitsPerMm,Height=Math.Max(4,item.Height*LabelView.UnitsPerMm),Background=Brushes.Transparent,BorderBrush=item.Id==_selectedId?Brushes.DodgerBlue:Brushes.Transparent,BorderThickness=new Thickness(1),Cursor=item.Locked?Cursors.Arrow:Cursors.SizeAll,Tag=item.Id};
            Canvas.SetLeft(box,item.X*LabelView.UnitsPerMm);Canvas.SetTop(box,item.Y*LabelView.UnitsPerMm);_overlay.Children.Add(box);
            box.MouseLeftButtonDown+=(_,e)=>
            {
                e.Handled=true;if(!TryApplyGeometry())return;
                _selectedId=item.Id;_stage.Focus();Refresh();
                if(Selected is not {Locked:false} active)return;
                Remember(_draft.Clone());_start=Mouse.GetPosition(_stage);_startX=active.X;_startY=active.Y;_dragging=true;_overlay.CaptureMouse();
            };
        }
        if(Selected is {Visible:true,Locked:false} selected)
        {
            var handle=new Thumb {Width=14,Height=14,ToolTip="Drag to resize this field or logo",Cursor=Cursors.SizeNWSE,Background=Brushes.DodgerBlue};
            var handleFace=new FrameworkElementFactory(typeof(Border));handleFace.SetValue(Border.BackgroundProperty,Brushes.DodgerBlue);handleFace.SetValue(Border.BorderBrushProperty,Brushes.White);handleFace.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            handle.Template=new ControlTemplate(typeof(Thumb)){VisualTree=handleFace};
            Canvas.SetLeft(handle,(selected.X+selected.Width)*LabelView.UnitsPerMm-5);Canvas.SetTop(handle,(selected.Y+selected.Height)*LabelView.UnitsPerMm-5);_overlay.Children.Add(handle);
            handle.DragStarted+=(_,_)=>{Remember(_draft.Clone());_start=Mouse.GetPosition(_stage);_startW=selected.Width;_startH=selected.Height;};
            handle.DragDelta+=(_,_)=>
            {
                var delta=Mouse.GetPosition(_stage)-_start;
                selected.Width=Math.Clamp(Snap(_startW+delta.X/LabelView.UnitsPerMm),1,Math.Max(1,Layout.WidthMm-selected.X));
                selected.Height=Math.Clamp(Snap(_startH+delta.Y/LabelView.UnitsPerMm) ,.2,Math.Max(.2,Layout.HeightMm-selected.Y));
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
        selected.X=Math.Clamp(Snap(_startX+delta.X/LabelView.UnitsPerMm),0,Math.Max(0,Layout.WidthMm-selected.Width));selected.Y=Math.Clamp(Snap(_startY+delta.Y/LabelView.UnitsPerMm),0,Math.Max(0,Layout.HeightMm-selected.Height));
        _dirty=true;Render(false);
        foreach(var handle in _overlay.Children.OfType<Thumb>()){Canvas.SetLeft(handle,(selected.X+selected.Width)*LabelView.UnitsPerMm-5);Canvas.SetTop(handle,(selected.Y+selected.Height)*LabelView.UnitsPerMm-5);}
        foreach(var box in _overlay.Children.OfType<Border>().Where(b=>(string?)b.Tag==selected.Id)){Canvas.SetLeft(box,selected.X*LabelView.UnitsPerMm);Canvas.SetTop(box,selected.Y*LabelView.UnitsPerMm);}
    }
    private void EndDrag(object sender,MouseButtonEventArgs e){if(!_dragging)return;_dragging=false;_overlay.ReleaseMouseCapture();Refresh();}
    private void UpdateZoom()
    {
        var scale=_zoom.SelectedIndex==4?PreviewZoom.Fit(new Size(_stage.Width,_stage.Height),new Size(_viewport.ActualWidth,_viewport.ActualHeight),72)
            :double.Parse(((string)_zoom.SelectedItem).TrimEnd('%'),CultureInfo.InvariantCulture)/100;
        if(_stage.LayoutTransform is ScaleTransform current && Math.Abs(current.ScaleX-scale)<.000001)return;
        _stage.LayoutTransform=new ScaleTransform(scale,scale);
    }
    private void BuildInspector()
    {
        var wasRefreshing=_refreshing;_refreshing=true;
        try { BuildInspectorCore(); } finally { _refreshing=wasRefreshing; }
    }
    private void BuildInspectorCore()
    {
        _geometryInputs.Clear();_geometryInitialText.Clear();_geometryElement=null;
        _properties.Children.Clear();
        if(Selected is not {} element)return;
        _properties.Children.Add(new TextBlock {Text="FIELD INSPECTOR",FontSize=17,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12)});
        Choice("Field",StickerLayout.Fields.Where(f=>f!="TotalQty").ToArray());
        if(element.Field!="Logo"){Text("Prefix","Label prefix");Text("Text","Custom text");}
        _geometryElement=element;_geometryFormat=ActiveFormat;
        foreach(var name in new[]{"X","Y","Width","Height"})
        {
            var value=((double)typeof(StickerElement).GetProperty(name)!.GetValue(element)!).ToString("0.###",CultureInfo.InvariantCulture);
            var box=new TextBox {Text=value,IsEnabled=!element.Locked};
            System.Windows.Automation.AutomationProperties.SetName(box,name+" (mm)");
            box.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){e.Handled=true;Safe(ApplyGeometry);}};
            _geometryInputs.Add(name,box);_geometryInitialText.Add(name,value);Label(name+" (mm)");_properties.Children.Add(box);
        }
        var geometryActions=new WrapPanel();_properties.Children.Add(geometryActions);
        var apply=new Button {Content="Apply position / size",IsEnabled=!element.Locked};apply.Click+=(_,_)=>Safe(ApplyGeometry);geometryActions.Children.Add(apply);
        var reset=new Button {Content="Reset values"};reset.Click+=(_,_)=>{BuildInspector();_status.Text="Unapplied position/size values discarded.";};geometryActions.Children.Add(reset);
        _properties.Children.Add(new TextBlock {Text="Edit all four bounds together, then Apply or Enter. Save also applies them. Invalid bounds stay here for correction.",TextWrapping=TextWrapping.Wrap,FontSize=11});
        var moves=new WrapPanel();_properties.Children.Add(new TextBlock {Text="MOVE · 0.5 mm per click"});_properties.Children.Add(moves);
        foreach(var move in new[]{("←",-.5,0d),("↑",0d,-.5),("↓",0d,.5),("→",.5,0d)})
        {
            var button=new Button {Content=move.Item1,IsEnabled=!element.Locked};
            button.Click+=(_,_)=>Safe(()=>Change(()=>{element.X=Math.Clamp(element.X+move.Item2,0,Math.Max(0,Layout.WidthMm-element.Width));element.Y=Math.Clamp(element.Y+move.Item3,0,Math.Max(0,Layout.HeightMm-element.Height));}));moves.Children.Add(button);
        }
        if(element.Field=="Logo")
        {
            Toggle("KeepAspect");
            _properties.Children.Add(new TextBlock {Text=$"Logo rotation: {element.Rotation}°"});
            var rotate=new Button {Content="Rotate logo 90°",IsEnabled=!element.Locked};rotate.Click+=(_,_)=>Safe(()=>Change(()=>element.Rotation=(element.Rotation+90)%360));_properties.Children.Add(rotate);
            var replace=new Button {Content="Import / replace logo…"};replace.Click+=(_,_)=>Safe(ImportLogo);_properties.Children.Add(replace);
            var fit=new Button {Content="Fit box to image proportions",IsEnabled=!element.Locked};fit.Click+=(_,_)=>Safe(()=>FitLogo(element));_properties.Children.Add(fit);
            _properties.Children.Add(new TextBlock {Text="KeepAspect fits the whole logo without cropping or stretching. White background and grayscale are shared with printing.",TextWrapping=TextWrapping.Wrap});
        }
        if(element.Field!="Logo")
        {
            Numeric("FontSize","Font size (pt)");Choice("FontFamily",new[]{"Arial","Segoe UI","Calibri","Verdana","Tahoma"});Choice("Alignment",new[]{"Left","Center","Right"});
            foreach(var name in new[]{"Bold","Underline","Wrap","AutoFit"})Toggle(name);
            Numeric("Stroke","Line / box stroke (mm)");
        }
        Toggle("Visible");Toggle("Locked");
        var actions=new WrapPanel();_properties.Children.Add(actions);
        foreach(var name in new[]{"Left","Center X","Right","Top","Center Y","Bottom"})
        {
            var button=new Button {Content=name,FontSize=12,Padding=new Thickness(8),Margin=new Thickness(0,0,6,6)};
            button.Click+=(_,_)=>Safe(()=>{if(element.Locked)return;Change(()=>{switch(name){case"Left":element.X=Math.Min(3,Layout.WidthMm-element.Width);break;case"Center X":element.X=(Layout.WidthMm-element.Width)/2;break;case"Right":element.X=Math.Max(0,Layout.WidthMm-3-element.Width);break;case"Top":element.Y=Math.Min(3,Layout.HeightMm-element.Height);break;case"Center Y":element.Y=(Layout.HeightMm-element.Height)/2;break;case"Bottom":element.Y=Math.Max(0,Layout.HeightMm-3-element.Height);break;}});});actions.Children.Add(button);
        }
        void Label(string text)=>_properties.Children.Add(new TextBlock {Text=text,Margin=new Thickness(0,4,0,4)});
        void Commit(Action action){if(!_refreshing)Safe(()=>Change(action));}
        void Text(string property,string label)
        {
            Label(label);var info=typeof(StickerElement).GetProperty(property)!;var box=new TextBox {Text=(string)info.GetValue(element)!,MaxLength=property=="Prefix"?80:240,IsEnabled=!element.Locked};
            System.Windows.Automation.AutomationProperties.SetName(box,label);
            box.LostKeyboardFocus+=(_,_)=>{if(!_refreshing&&box.Text!=(string)info.GetValue(element)!)Safe(()=>Change(()=>info.SetValue(element,box.Text),false));};_properties.Children.Add(box);
        }
        void Numeric(string property,string label)
        {
            Label(label);var info=typeof(StickerElement).GetProperty(property)!;var box=new TextBox {Text=((double)info.GetValue(element)!).ToString("0.##",CultureInfo.InvariantCulture),IsEnabled=!element.Locked};
            System.Windows.Automation.AutomationProperties.SetName(box,label);
            box.LostKeyboardFocus+=(_,_)=>{if(_refreshing)return;Safe(()=>{if(!double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n))throw new FormatException("Use a dot decimal number for "+label);if(n!=(double)info.GetValue(element)!)Change(()=>info.SetValue(element,n),false);});};_properties.Children.Add(box);
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
    private bool HasPendingGeometry=>_geometryInputs.Any(pair=>pair.Value.Text!=_geometryInitialText[pair.Key]);
    private void ApplyGeometry()
    {
        if(_geometryElement==null || !HasPendingGeometry)return;
        var target=_geometryElement;var layout=_draft.For(_geometryFormat);
        if(target.Locked)throw new FormatException("Unlock the selected field before changing its position or size.");
        var values=new Dictionary<string,double>();
        foreach(var pair in _geometryInputs)
        {
            var property=typeof(StickerElement).GetProperty(pair.Key)!;
            if(pair.Value.Text==_geometryInitialText[pair.Key])values[pair.Key]=(double)property.GetValue(target)!;
            else if(double.TryParse(pair.Value.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n) && double.IsFinite(n))values[pair.Key]=n;
            else throw new FormatException("Enter a finite millimeter value for "+pair.Key+". Nothing was applied.");
        }
        // Validate the complete proposed rectangle before touching the live draft or rebuilding controls.
        var candidate=layout.Clone();var trial=candidate.Elements.Single(e=>e.Id==target.Id);
        void Set(StickerElement item){foreach(var pair in values)typeof(StickerElement).GetProperty(pair.Key)!.SetValue(item,pair.Value);}
        Set(trial);candidate.Validate();
        _applyingGeometry=true;
        try{Change(()=>Set(target));}finally{_applyingGeometry=false;}
        _status.Text="Position and size applied together. Save all formats to use these bounds in live preview and printing.";
    }
    private bool TryApplyGeometry()
    {
        try{ApplyGeometry();return true;}
        catch(FormatException ex){_status.Text=ex.Message;return false;}
    }
    private bool HasPendingStock
    {
        get
        {
            if(_stockWidth.Text==_initialStockWidthText && _stockHeight.Text==_initialStockHeightText)return false;
            return !double.TryParse(_stockWidth.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var width)
                || !double.TryParse(_stockHeight.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var height)
                || width!=Layout.WidthMm || height!=Layout.HeightMm;
        }
    }
    private void CommitForSave()
    {
        if(HasPendingStock)throw new FormatException("Apply stock size before saving/exporting. The typed paper dimensions have not been applied yet.");
        ApplyGeometry();
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
        Safe(()=>Change(()=>{item.X=Math.Clamp(item.X+dx,0,Math.Max(0,Layout.WidthMm-item.Width));item.Y=Math.Clamp(item.Y+dy,0,Math.Max(0,Layout.HeightMm-item.Height));}));e.Handled=true;
    }
    private void ApplySimplePattern()
    {
        Change(()=>ReplaceLayout(Layout.SimplePreset()));
        _status.Text="Simple pattern applied to this format. Stock and logos kept; check custom logo placement. Undo restores the previous design. Save all formats to activate.";
    }
    private void ApplyStock()
    {
        if(!double.TryParse(_stockWidth.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var width) || !double.TryParse(_stockHeight.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var height))throw new FormatException("Enter width and height in mm using a decimal point.");
        Change(()=>_draft.ResizeStock(width,height,_scaleContents.IsChecked==true));
        _status.Text="Stock size updated in all three formats. Save all formats to activate, then Print sticker.";
    }
    private void ImportLogo()
    {
        var dialog=new OpenFileDialog {Title="Import company logo",Filter="Logo images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"};
        if(dialog.ShowDialog(this)!=true)return;
        ApplyLogo(LogoImages.Import(dialog.FileName));
    }
    private void ApplyLogo(string data)
    {
        _=LogoImages.Decode(data);
        Change(()=>
        {
            foreach(var layout in _draft.Layouts.Where(l=>_allLogos.IsChecked==true || l.Format==ActiveFormat))
            {
                var logo=layout.Format==ActiveFormat && Selected?.Field=="Logo"?Selected:layout.Elements.FirstOrDefault(e=>e.Field=="Logo");
                if(logo?.Locked==true)throw new FormatException("Unlock the existing logo before replacing it.");
                if(logo==null)
                {
                    logo=new StickerElement {Field="Logo",X=layout.WidthMm*.04,Y=layout.HeightMm*3/70,Width=layout.WidthMm*.14,Height=layout.HeightMm*.1};
                    layout.Elements.Add(logo);
                    // Reserve space only in the known default header; never rearrange custom headers silently.
                    var header=layout.Elements.FirstOrDefault(e=>e.Field=="CompanyName" && !e.Locked && Math.Abs(e.X-layout.WidthMm*.04)<.01 && Math.Abs(e.Width-layout.WidthMm*.92)<.01);
                    if(header!=null){header.X=layout.WidthMm*.20;header.Width=layout.WidthMm*.76;}
                }
                logo.ImagePng=data;logo.Visible=true;
                if(layout.Format==ActiveFormat)_selectedId=logo.Id;
            }
        });
        _status.Text="Logo imported and embedded. Drag it, resize the blue handle, or enter X / Y / Width / Height. Save all formats to activate.";
    }
    private void FitLogo(StickerElement element)
    {
        if(LogoImages.Rotated(element.ImagePng,element.Rotation) is not {} image)return;
        Change(()=>
        {
            var ratio=(double)image.PixelHeight/image.PixelWidth;
            var width=Math.Min(element.Width,(Layout.HeightMm-element.Y)/ratio);
            element.Width=width;element.Height=width*ratio;element.KeepAspect=true;
        });
    }
    private void Import()
    {
        var dialog=new OpenFileDialog {Filter="Sticker layout / library (*.json)|*.json"};
        if(dialog.ShowDialog(this)!=true)return;
        if(new FileInfo(dialog.FileName).Length>10*1024*1024)throw new FormatException("Layout file is too large.");
        using var document=JsonDocument.Parse(File.ReadAllText(dialog.FileName));
        if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new FormatException("Layout JSON must be an object.");
        if(document.RootElement.TryGetProperty("Layouts",out _)){var imported=LayoutLibrary.Load(dialog.FileName);LogoImages.ValidateLibrary(imported);Change(()=>_draft=imported);}
        else {var imported=StickerLayout.Load(dialog.FileName);foreach(var logo in imported.Elements.Where(e=>e.Field=="Logo"))_=LogoImages.Decode(logo.ImagePng);Change(()=>ReplaceLayout(imported));_format.SelectedItem=imported.Format;}
    }
    private void ExportFormat()
    {
        CommitForSave();
        var dialog=new SaveFileDialog {Filter="Sticker format (*.json)|*.json",FileName=ActiveFormat+"-sticker.json"};
        if(dialog.ShowDialog(this)==true){Layout.Save(dialog.FileName);_status.Text="Single format exported. Import it without replacing the other two formats.";}
    }
    private void Export(){CommitForSave();var dialog=new SaveFileDialog {Filter="Sticker layout library (*.json)|*.json",FileName="ocean-glass-sticker-layouts.json"};if(dialog.ShowDialog(this)==true){_draft.Save(dialog.FileName);_status.Text="Layout library exported. Save all formats to activate these layouts in the app.";}}
    private void ClosingEditor(object? sender,CancelEventArgs e){if((_dirty || HasPendingGeometry || HasPendingStock)&&!_saved&&!StudioDialog.Confirm(this,"Discard layout edits?","These layout changes have not been saved. Continue to discard them?"))e.Cancel=true;}
}
