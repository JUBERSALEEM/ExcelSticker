using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ExcelSticker.Theming;

/// <summary>A reusable studio shipped with the theme engine, not with the business UI.</summary>
public sealed class ThemeStudioWindow : Window
{
    private readonly ThemeEngine _engine;
    private readonly ThemeTokens _original;
    private ThemeTokens _draft;
    private readonly StackPanel _fields=new();
    private readonly TextBlock _status=new() { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(6,8,6,12) };
    private readonly TextBlock _contrast=new() { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,12,0,12) };
    private readonly DispatcherTimer _timer=new() { Interval=TimeSpan.FromMilliseconds(80) };
    private bool _accepted;
    public ThemeStudioWindow(ThemeEngine engine)
    {
        _engine=engine; _original=engine.Current.Clone(); _draft=_original.Clone();
        Title="Clay Engine • Theme Studio"; Width=1120;Height=840;MinWidth=850;MinHeight=620;
        SetResourceReference(StyleProperty,typeof(Window)); WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel { Margin=new Thickness(24) }; Content=root;
        var heading=new TextBlock { Text="Theme Studio  /  Clay Engine", FontSize=26,FontWeight=FontWeights.Bold,Margin=new Thickness(8,0,0,18) };
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        footer.Children.Add(_status);var actions=new WrapPanel();footer.Children.Add(actions);
        Button AddAction(string name,Action run,bool primary=false)
        {var b=new Button {Content=name};if(primary)b.SetResourceReference(StyleProperty,"Primary");b.Click+=(_,_)=>Safe(run);actions.Children.Add(b);return b;}
        AddAction("Import JSON",Import);AddAction("Export JSON",Export);AddAction("Revert session",()=>{_draft=_original.Clone();BuildFields();Preview();});
        AddAction("Cancel",Close);AddAction("Save theme",()=>{_engine.Save(_draft);_engine.Apply(_draft);_accepted=true;Close();},true);
        var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(400)});columns.ColumnDefinitions.Add(new ColumnDefinition());root.Children.Add(columns);
        var scroll=new ScrollViewer {Content=_fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(0,0,12,0)};columns.Children.Add(scroll);
        var preview=new StackPanel();Grid.SetColumn(preview,1);columns.Children.Add(new ScrollViewer {Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(columns.Children[1],1);
        var demo=new StackPanel();var card=new ClayCard {Content=demo,Margin=new Thickness(14,8,14,20)};preview.Children.Add(card);
        demo.Children.Add(new TextBlock {Text="Live component laboratory",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
        demo.Children.Add(new TextBlock {Text="The whole app updates while you experiment. Text sits above decorative chrome, never inside a shadow effect.",TextWrapping=TextWrapping.Wrap});
        demo.Children.Add(_contrast);
        demo.Children.Add(new TextBox {Text="Crisp text • 7494/71 • 2550 × 2650",Margin=new Thickness(0,6,0,12)});
        demo.Children.Add(new ComboBox {ItemsSource=new[]{"SGU · single glass","DGU · outer + inner","Normal job order"},SelectedIndex=0});
        demo.Children.Add(new CheckBox {Content="Clearly visible selected state",IsChecked=true});
        var buttons=new WrapPanel();var primary=new Button {Content="Primary action"};primary.SetResourceReference(StyleProperty,"Primary");buttons.Children.Add(primary);buttons.Children.Add(new Button {Content="Secondary action"});demo.Children.Add(buttons);
        var disabled=new Button {Content="Disabled control",IsEnabled=false};demo.Children.Add(disabled);
        preview.Children.Add(new ClayCard {Content=new TextBlock {Text="Keyboard focus\nTab through the controls to inspect the focus outlines.\n\nSaved themes are local JSON data. Export them to share a theme; no XAML or code is loaded.\n\nContrast guard checks normal and secondary text against canvas, surface, and selection fills. This is not a complete accessibility audit.",TextWrapping=TextWrapping.Wrap},Margin=new Thickness(14,8,14,20)});
        _timer.Tick+=(_,_)=>{_timer.Stop();Safe(Preview);};
        Closing+=(_,_)=>{_timer.Stop();if(!_accepted)_engine.Apply(_original);};
        WindowPlacement.FitOnFirstShow(this);
        BuildFields();Preview();
    }
    private void Safe(Action action)
    {
        try { action(); }
        catch(Exception ex) when(ex is FormatException or IOException or UnauthorizedAccessException or JsonException or ArgumentException) {_status.Text=ex.Message;}
    }
    private void QueuePreview(){_timer.Stop();_timer.Start();}
    private void Preview()
    {
        _draft.Validate();
        var ratios=ThemeEngine.Ratios(_draft);
        _contrast.Text=$"Minimum body contrast: {ratios.Body:0.00}:1\nSecondary contrast: {ratios.Secondary:0.00}:1\nBorder contrast: {ThemeEngine.EdgeContrast(_draft):0.00}:1\nTargets: 4.5:1 text; 3:1 functional boundaries. Decorative strokes are not an accessibility certification.";
        if(_draft.ContrastGuard&&!ThemeEngine.ContrastPasses(_draft)){_status.Text="Contrast guard: choose darker/lighter text. The last valid preview is retained.";return;}
        _engine.Apply(_draft);_status.Text="Live preview · changes are not saved until Save theme. Closing or Cancel restores the opening theme.";
    }
    private void BuildFields()
    {
        _fields.Children.Clear();
        void Heading(string text)=>_fields.Children.Add(new TextBlock {Text=text,FontWeight=FontWeights.Bold,FontSize=17,Margin=new Thickness(0,12,0,12)});
        Heading("Presets & identity");
        var presets=new ComboBox {ItemsSource=ThemeTokens.Presets.Select(t=>t.Name).ToArray(),SelectedItem=_draft.Name};
        presets.SelectionChanged+=(_,_)=>{if(presets.SelectedItem is string name){_draft=ThemeTokens.Presets.First(t=>t.Name==name).Clone();BuildFields();Preview();}};_fields.Children.Add(presets);
        Text("Name","Theme name");
        Heading("Surface palette · #RRGGBB");
        foreach(var property in new[]{"Canvas","Surface","Accent","Text","Muted","Edge","Shadow"})Text(property,property);
        Heading("Independent card corners");
        foreach(var p in new[]{"TopLeft","TopRight","BottomRight","BottomLeft"})Number(p,p,0,64,1);
        Number("ButtonRadius","Button radius",0,64,1);Number("InputRadius","Input radius",0,64,1);
        Heading("Borders & sculpted depth");Toggle("ShowBorders","Show border lines");Number("BorderWidth","Border line width",0,4,.5);
        Number("ShadowBlur","Outer shadow softness",0,48,1);Number("ShadowDepth","Elevation",0,20,1);Number("ShadowOpacity","Outer shadow opacity",0,.6,.01);
        Number("LightAngle","Light direction",0,360,5);Number("InsetDepth","Inset feathering",0,12,1);Number("Highlight","Inner highlight",0,.8,.02);Number("Shade","Inner shade",0,.4,.01);
        Heading("Typography & density");Choice("FontFamily",new[]{"Segoe UI","Arial","Calibri","Verdana","Tahoma"});Number("FontSize","Body font size",12,20,1);Number("SmallFontSize","Secondary font size",11,18,1);
        Number("CardPadding","Card padding",8,36,1);Number("ControlPadding","Control padding",4,18,1);
        Heading("Pattern & accessibility");Choice("Pattern",new[]{"None","Dots","Grid"});Number("PatternSize","Pattern spacing",8,64,1);Number("PatternOpacity","Pattern opacity",0,.15,.01);
        Toggle("ReducedEffects","Reduced effects / flat surfaces");Toggle("ContrastGuard","Contrast guard (4.5:1 text)");
    }
    private PropertyInfo Property(string name)=>typeof(ThemeTokens).GetProperty(name)!;
    private void Text(string name,string label)
    {
        _fields.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,0,0,4)});
        var box=new TextBox {Text=(string)Property(name).GetValue(_draft)!,MaxLength=name=="Name"?80:7};
        System.Windows.Automation.AutomationProperties.SetName(box,label);
        box.TextChanged+=(_,_)=>{Property(name).SetValue(_draft,box.Text);QueuePreview();};
        if(name=="Name"){_fields.Children.Add(box);return;}
        var row=new DockPanel();
        var swatch=new Button {Content="Pick",Width=64,Margin=new Thickness(0,0,8,14),ToolTip="Choose RGB color"};
        swatch.Click+=(_,_)=>Safe(()=>{var picker=new ColorPickerWindow(box.Text){Owner=this};if(picker.ShowDialog()==true)box.Text=picker.SelectedHex;});
        DockPanel.SetDock(swatch,Dock.Right);row.Children.Add(swatch);row.Children.Add(box);_fields.Children.Add(row);
    }
    private void Number(string name,string label,double min,double max,double step)
    {
        var value=(double)Property(name).GetValue(_draft)!;var title=new TextBlock {Text=$"{label}: {value:0.##}"};_fields.Children.Add(title);
        var slider=new Slider {Minimum=min,Maximum=max,Value=value,TickFrequency=step,SmallChange=step,LargeChange=step*5};
        System.Windows.Automation.AutomationProperties.SetName(slider,label);
        slider.ValueChanged+=(_,_)=>{Property(name).SetValue(_draft,slider.Value);title.Text=$"{label}: {slider.Value:0.##}";QueuePreview();};_fields.Children.Add(slider);
    }
    private void Toggle(string name,string label)
    {
        var box=new CheckBox {Content=label,IsChecked=(bool)Property(name).GetValue(_draft)!};
        box.Click+=(_,_)=>{Property(name).SetValue(_draft,box.IsChecked==true);QueuePreview();};_fields.Children.Add(box);
    }
    private void Choice(string name,string[] values)
    {
        var box=new ComboBox {ItemsSource=values,SelectedItem=Property(name).GetValue(_draft)};
        System.Windows.Automation.AutomationProperties.SetName(box,name);
        box.SelectionChanged+=(_,_)=>{if(box.SelectedItem is string value){Property(name).SetValue(_draft,value);QueuePreview();}};_fields.Children.Add(box);
    }
    private void Import()
    {
        var dialog=new OpenFileDialog {Filter="Clay theme (*.json)|*.json"};if(dialog.ShowDialog(this)!=true)return;
        _draft=ThemeEngine.Read(dialog.FileName);BuildFields();Preview();
    }
    private void Export()
    {
        var dialog=new SaveFileDialog {Filter="Clay theme (*.json)|*.json",FileName="ocean-glass-theme.json"};if(dialog.ShowDialog(this)==true){_engine.Save(_draft,dialog.FileName);_status.Text="Theme exported. Save theme to also persist it as your app preference.";}
    }
}
