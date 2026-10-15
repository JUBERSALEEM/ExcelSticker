using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ExcelSticker.Theming;

/// <summary>A reusable studio shipped with the theme engine, not with the business UI.</summary>
public sealed class ThemeStudioWindow : Window
{
    private readonly ThemeEngine _engine;
    private readonly ThemeEditSession _session;
    private readonly Button _save;
    private readonly CheckBox _live = new() { Name = "LivePreviewToggle", Content = "Live preview in application", IsChecked = true };
    private ThemeTokens _draft;
    private readonly StackPanel _fields=new();
    private readonly TextBlock _status=new() { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(6,8,6,12) };
    private readonly TextBlock _contrast=new() { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,12,0,12) };
    private readonly DispatcherTimer _timer=new() { Interval=TimeSpan.FromMilliseconds(80) };
    public bool HasSaved => _session.HasSaved;
    public ThemeStudioWindow(ThemeEngine engine)
    {
        _engine=engine; _session=engine.BeginEdit(); _draft=_session.Baseline;
        // Standalone hosts can supply a local resource scope; app hosts already inherit it.
        if (!ReferenceEquals(engine.Resources, Application.Current?.Resources)) Resources.MergedDictionaries.Add(engine.Resources);
        Title="Clay Engine • Theme Studio"; Width=1120;Height=840;MinWidth=850;MinHeight=620;
        SetResourceReference(StyleProperty,typeof(Window)); WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel(); root.SetResourceReference(MarginProperty,"ClayWorkspaceMargin"); root.SetResourceReference(Panel.BackgroundProperty,"ClayCanvasTexture"); Content=root;
        var heading=new TextBlock { Text="Theme Studio  /  Clay Engine", FontSize=24,FontWeight=FontWeights.Bold,Margin=new Thickness(8,0,0,18) };
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        _status.Name = "ThemeStatusMessage";
        System.Windows.Automation.AutomationProperties.SetLiveSetting(_status, System.Windows.Automation.AutomationLiveSetting.Assertive);
        footer.Children.Add(new ScrollViewer { Content = _status, MaxHeight = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        StableEditorMetrics(footer); StableEditorMetrics(_fields);
        var actions=new WrapPanel();footer.Children.Add(actions);
        Button AddAction(string name,Action run,bool primary=false)
        {var b=new Button {Content=name};if(primary)b.SetResourceReference(StyleProperty,"Primary");b.Click+=(_,_)=>Safe(run);actions.Children.Add(b);return b;}
        AddAction("Import JSON",Import);AddAction("Export JSON",Export);
        AddAction("Revert changes", () => { _timer.Stop(); _draft=_session.Baseline; BuildFields(); _session.Revert(); ValidateDraft(false); }).Name = "RevertThemeButton";
        AddAction("Close / discard unsaved",Close).Name = "CloseThemeButton";
        _save=AddAction("Save theme",SaveTheme,true); _save.Name="SaveThemeButton";
        _save.ToolTip="Save and apply immediately. Stays open; closing later only discards changes made after this save.\n"+engine.StorePath;
        var liveActions = new WrapPanel { Margin = new Thickness(0,0,0,8) };
        DockPanel.SetDock(liveActions, Dock.Top); root.Children.Add(liveActions);
        StableEditorMetrics(liveActions);
        liveActions.Children.Add(_live);
        var apply = new Button { Content = "Apply preview now", Name = "ApplyThemePreviewButton", Margin = new Thickness(12,0,10,0) };
        apply.Click += (_,_) => { _timer.Stop(); ValidateDraft(true); }; liveActions.Children.Add(apply);
        var inspect = new Button { Content = "Inspect application", Name = "InspectApplicationButton", ToolTip = "Hide the studio temporarily. Reopen it with Theme Studio in the main window; your draft stays available." };
        inspect.Click += (_,_) => { if (Owner != null) { Hide(); Owner.Activate(); } }; liveActions.Children.Add(inspect);
        _live.Checked += (_,_) => ValidateDraft(true);
        _live.Unchecked += (_,_) => { _timer.Stop(); _session.Revert(); ValidateDraft(false); };
        var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(400)});columns.ColumnDefinitions.Add(new ColumnDefinition());root.Children.Add(columns);
        var scroll=new ScrollViewer {Content=_fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(0,0,12,0)};columns.Children.Add(scroll);
        var preview=new StackPanel();Grid.SetColumn(preview,1);columns.Children.Add(new ScrollViewer {Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(columns.Children[1],1);
        SizeChanged += (_,_) =>
        {
            var compact = ActualWidth < 980;
            columns.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(400);
            columns.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            columns.Children[1].Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        };
        var demo=new StackPanel();var card=new ClayCard {Content=demo};card.SetResourceReference(MarginProperty,"ClayCardMargin");card.SetResourceReference(Control.BackgroundProperty,"ClayLavender");preview.Children.Add(card);
        demo.Children.Add(new TextBlock {Text="Live component laboratory",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
        demo.Children.Add(new TextBlock {Text="The whole app updates while you experiment. Text sits above decorative chrome, never inside a shadow effect.",TextWrapping=TextWrapping.Wrap});
        var swatches = new WrapPanel { Margin = new Thickness(0,8,0,8) };
        foreach (var key in new[]{"ClayLavender","ClayMint","ClayPeach","ClayBlue"})
        {
            var chip = new ClayCard { Width=54,Height=30,Margin=new Thickness(0,0,8,4),CornerRadius=new CornerRadius(10),ToolTip=key };
            chip.SetResourceReference(Control.BackgroundProperty,key);swatches.Children.Add(chip);
        }
        demo.Children.Add(swatches);
        var plate = new ClayCard { Content = new TextBlock { Text = "Inset plate · matched four-edge relief", TextWrapping = TextWrapping.Wrap } };
        plate.SetResourceReference(StyleProperty,"InnerPlate");plate.SetResourceReference(Control.BackgroundProperty,"ClayMint");demo.Children.Add(plate);
        demo.Children.Add(_contrast);
        demo.Children.Add(new TextBox {Text="Crisp text • 7494/71 • 2550 × 2650",Margin=new Thickness(0,6,0,12)});
        demo.Children.Add(new ComboBox {ItemsSource=new[]{"SGU · single glass","DGU · outer + inner","Normal job order"},SelectedIndex=0});
        demo.Children.Add(new CheckBox {Content="Clearly visible selected state",IsChecked=true});
        var buttons=new WrapPanel();var primary=new Button {Content="Primary action"};primary.SetResourceReference(StyleProperty,"Primary");buttons.Children.Add(primary);buttons.Children.Add(new Button {Content="Secondary action"});demo.Children.Add(buttons);
        var disabled=new Button {Content="Disabled control",IsEnabled=false};demo.Children.Add(disabled);
        preview.Children.Add(new ClayCard {Content=new TextBlock {Text="Keyboard focus\nTab through the controls to inspect the focus outlines.\n\nSaved themes are local JSON data. Export them to share a theme; no XAML or code is loaded.\n\nContrast guard checks normal and secondary text against canvas, surface, selection and pastel panel fills. This is not a complete accessibility audit.",TextWrapping=TextWrapping.Wrap},Margin=new Thickness(6,4,6,8)});
        _timer.Tick+=(_,_)=>{_timer.Stop();ValidateDraft(_live.IsChecked == true);};
        Closed+=(_,_)=>{_timer.Stop();_session.Dispose();};
        WindowPlacement.FitOnFirstShow(this);
        BuildFields();ValidateDraft(false);
    }
    private static void StableEditorMetrics(FrameworkElement panel)
    {
        // The inspector must stay usable while the application/laboratory previews extreme density.
        panel.Resources["ClayControlPadding"] = new Thickness(9, 5, 9, 5);
        panel.Resources["ClayFontSize"] = 13d;
        panel.Resources["ClaySmallFontSize"] = 12d;
        TextElement.SetFontSize(panel, 13);
        TextElement.SetFontFamily(panel, new FontFamily("Segoe UI"));
    }
    private void Report(string message, bool error = false)
    {
        _status.Text = message;
        _status.SetResourceReference(TextBlock.ForegroundProperty, error ? "ClayDanger" : "Ink");
    }
    private void Safe(Action action)
    {
        try { action(); }
        catch(Exception ex) when(ex is FormatException or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Report("Action failed: " + ex.Message + "\nTheme file: " + _engine.StorePath + "\nYour draft is retained. Correct the issue and retry, or export to another writable folder.", true); }
    }
    private void QueuePreview()
    {
        _timer.Stop();
        // Validate synchronously so a valid Save click never gets swallowed during the debounce.
        // Only the expensive resource/render publication is delayed.
        if (ValidateDraft(false)) _timer.Start();
    }
    private bool ValidateDraft(bool apply)
    {
        try
        {
            _draft.Validate();
            var ratios=ThemeEngine.Ratios(_draft);
            _contrast.Text=$"Minimum body contrast: {ratios.Body:0.00}:1\nSecondary contrast: {ratios.Secondary:0.00}:1\nRim contrast (after opacity): {ThemeEngine.EdgeContrast(_draft):0.00}:1\nTargets: 4.5:1 text; 3:1 functional boundaries. Soft rims are decorative; focus outlines stay solid. This is not an accessibility certification.";
            ThemeEngine.ValidateForApply(_draft);
            if (apply) _session.Preview(_draft);
            _save.IsEnabled=true;
            Report(apply ? "LIVE · unsaved preview is visible in the application now. Save theme to keep it, or close to restore the last saved/opening theme."
                : "Valid draft. Apply preview now, enable Live preview, or Save theme. Closing discards changes since the last save.");
            return true;
        }
        catch (FormatException ex)
        {
            _save.IsEnabled=false;
            Report(ex.Message + "\nSave is disabled; the last valid preview is unchanged.", true);
            return false;
        }
    }
    private void SaveTheme()
    {
        _timer.Stop();
        if (!ValidateDraft(false)) return;
        _session.Save(_draft);
        Report($"SAVED at {DateTime.Now:T} · Applied to the application immediately.\n{_engine.StorePath}\nYou can keep editing. Close will restore this saved version, not the theme from before this save.");
    }
    private void BuildFields()
    {
        _timer.Stop();
        _fields.Children.Clear();
        void Heading(string text)=>_fields.Children.Add(new TextBlock {Text=text,FontWeight=FontWeights.Bold,FontSize=17,Margin=new Thickness(0,12,0,12)});
        Heading("Presets & identity");
        var presets=new ComboBox {ItemsSource=ThemeTokens.Presets.Select(t=>t.Name).ToArray(),SelectedItem=_draft.Name, Tag="Preset"};
        presets.SelectionChanged+=(_,_)=>{if(presets.SelectedItem is string name){_draft=ThemeTokens.Presets.First(t=>t.Name==name).Clone();BuildFields();ValidateDraft(_live.IsChecked == true);}};_fields.Children.Add(presets);
        var reference = new Button { Content="Apply reference clay to entire page", Name="ApplyReferenceClayButton" };
        reference.Click+=(_,_)=>{_draft=ThemeTokens.Presets.First(t=>t.Name=="Pastel clay").Clone();BuildFields();ValidateDraft(_live.IsChecked==true);};
        _fields.Children.Add(reference);
        Text("Name","Theme name");
        Heading("Global workspace density"); Choice("Density",new[]{"Compact","Comfortable","Spacious"});
        _fields.Children.Add(new TextBlock { Text="Compact reduces card padding, control spacing and table rows. It never scales the printed label.", TextWrapping=TextWrapping.Wrap });
        Heading("Surface palette · #RRGGBB");
        foreach(var property in new[]{"Canvas","Surface","Accent","Text","Muted","Edge","Shadow"})Text(property,property);
        Toggle("ColorfulCards","Pastel card surfaces");Number("SurfaceTint","Pastel surface strength",0,1,.05);
        Heading("Clay relief & equal corners"); Choice("EdgeLighting",new[]{"Directional","Balanced"});
        _fields.Children.Add(new TextBlock { Text="Directional sculpts the rounded edge from the selected light angle. Balanced keeps an equal rim; its cast shadow still follows the angle. Reduced effects removes both.", TextWrapping=TextWrapping.Wrap });
        Toggle("UniformCorners","Link all four corners (top left controls all)");
        foreach(var p in new[]{"TopLeft","TopRight","BottomRight","BottomLeft"})Number(p,p=="TopLeft"?"Linked radius / top left":p,0,32,1);
        Number("ButtonRadius","Button radius",0,10,1);Number("InputRadius","Input radius",0,8,1);
        Heading("Borders & sculpted depth");Toggle("ShowBorders","Show border lines");Number("BorderWidth","Border line width",0,4,.5);Number("RimOpacity","Decorative rim opacity",0,1,.05);
        Number("ShadowBlur","Outer shadow softness",0,48,1);Number("ShadowDepth","Elevation",0,20,1);Number("ShadowOpacity","Outer shadow opacity",0,.6,.01);
        Number("LightAngle","Light direction · degrees",0,360,5);Number("InsetDepth","Inset feathering",0,12,1);Number("Highlight","Inner highlight",0,.8,.02);Number("Shade","Inner shade",0,.4,.01);
        Heading("Typography & density");Choice("FontFamily",new[]{"Segoe UI","Arial","Calibri","Verdana","Tahoma"});Number("FontSize","Body font size",12,20,1);Number("SmallFontSize","Secondary font size",11,18,1);
        Number("CardPadding","Card padding",8,36,1);Number("ControlPadding","Control padding",4,18,1);
        Heading("Full-page color & texture");Choice("PageTreatment",new[]{"Pastel wash","Solid"});Choice("Pattern",new[]{"None","Speckles","Grid"});Number("PatternSize","Pattern spacing",8,64,1);Number("PatternOpacity","Pattern opacity",0,.15,.01);
        Toggle("ReducedEffects","Reduced effects / flat surfaces");Toggle("ContrastGuard","Contrast guard (4.5:1 text)");
    }
    private PropertyInfo Property(string name)=>typeof(ThemeTokens).GetProperty(name)!;
    private void Text(string name,string label)
    {
        _fields.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,0,0,4)});
        var box=new TextBox {Text=(string)Property(name).GetValue(_draft)!,MaxLength=name=="Name"?80:7, Tag=name};
        System.Windows.Automation.AutomationProperties.SetName(box,label);
        box.TextChanged+=(_,_)=>{Property(name).SetValue(_draft,box.Text);QueuePreview();};
        if(name=="Name"){_fields.Children.Add(box);return;}
        var row=new DockPanel();
        var swatch=new Button {Content="Pick",Width=64,Margin=new Thickness(0,0,8,14),ToolTip="Choose RGB color"};
        swatch.Click+=(_,_)=>Safe(()=>{var picker=new ColorPickerWindow(box.Text.Length == 7 && box.Text[0] == '#' && box.Text.Skip(1).All(Uri.IsHexDigit) ? box.Text : "#808080"){Owner=this};if(picker.ShowDialog()==true)box.Text=picker.SelectedHex;});
        DockPanel.SetDock(swatch,Dock.Right);row.Children.Add(swatch);row.Children.Add(box);_fields.Children.Add(row);
    }
    private void Number(string name,string label,double min,double max,double step)
    {
        var value=(double)Property(name).GetValue(_draft)!;var title=new TextBlock {Text=$"{label}: {value:0.##}"};_fields.Children.Add(title);
        var slider=new Slider {Minimum=min,Maximum=max,Value=value,TickFrequency=step,SmallChange=step,LargeChange=step*5, Tag=name};
        slider.IsEnabled = !(name is "TopRight" or "BottomRight" or "BottomLeft") || !_draft.UniformCorners;
        // Direction always controls the cast shadow; never disable this slider in Balanced mode.
        System.Windows.Automation.AutomationProperties.SetName(slider,label);
        slider.ValueChanged+=(_,_)=>{Property(name).SetValue(_draft,slider.Value);title.Text=$"{label}: {slider.Value:0.##}";QueuePreview();};_fields.Children.Add(slider);
    }
    private void Toggle(string name,string label)
    {
        var box=new CheckBox {Content=label,IsChecked=(bool)Property(name).GetValue(_draft)!, Tag=name};
        box.Click+=(_,_)=>{Property(name).SetValue(_draft,box.IsChecked==true);if(name=="UniformCorners")BuildFields();QueuePreview();};_fields.Children.Add(box);
    }
    private void Choice(string name,string[] values)
    {
        var box=new ComboBox {ItemsSource=values,SelectedItem=Property(name).GetValue(_draft), Tag=name};
        System.Windows.Automation.AutomationProperties.SetName(box,name);
        box.SelectionChanged+=(_,_)=>{if(box.SelectedItem is string value){Property(name).SetValue(_draft,value);QueuePreview();}};_fields.Children.Add(box);
    }
    private void Import()
    {
        var dialog=new OpenFileDialog {Filter="Clay theme (*.json)|*.json"};if(dialog.ShowDialog(this)!=true)return;
        _draft=ThemeEngine.Read(dialog.FileName);BuildFields();ValidateDraft(_live.IsChecked == true);
    }
    private void Export()
    {
        var dialog=new SaveFileDialog {Filter="Clay theme (*.json)|*.json",FileName="ocean-glass-theme.json"};if(dialog.ShowDialog(this)==true){_engine.Save(_draft,dialog.FileName);_status.Text="Theme exported. Save theme to also persist it as your app preference.";}
    }
}
