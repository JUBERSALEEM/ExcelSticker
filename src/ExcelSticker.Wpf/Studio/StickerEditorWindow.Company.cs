using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf.Studio;

public sealed partial class StickerEditorWindow
{
    private readonly Dictionary<string,TextBox> _companyInputs=new();
    private readonly Expander _companyPanel=new() {Header="COMPANY DETAILS · all formats",IsExpanded=true};
    private bool _loadingCompany;
    private void BuildCompanyPanel(Panel parent)
    {
        var content=new StackPanel();_companyPanel.Content=content;parent.Children.Add(_companyPanel);
        foreach(var (property,label,limit) in new[]{("Name","Company name",120),("Address","Address",240),("Phone","Phone",80),("Website","Website",160),("Email","Email",160)})
        {
            content.Children.Add(new TextBlock {Text=label});
            var box=new TextBox {MaxLength=limit,TextWrapping=TextWrapping.Wrap};_companyInputs.Add(property,box);content.Children.Add(box);
            System.Windows.Automation.AutomationProperties.SetName(box,label);
            box.TextChanged+=(_,_)=>{if(_loadingCompany)return;_dirty=true;Title="Sticker Studio · unsaved company details";if(_undoButton!=null)_undoButton.IsEnabled=true;if(_redoButton!=null)_redoButton.IsEnabled=false;Render();};
            box.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){e.Handled=true;Safe(ApplyCompany);}};
        }
        var apply=new Button {Content="Apply company details"};apply.Click+=(_,_)=>Safe(ApplyCompany);content.Children.Add(apply);
        var reset=new Button {Content="Reset company inputs"};reset.Click+=(_,_)=>{SyncCompanyInputs();Render();};content.Children.Add(reset);
        content.Children.Add(new TextBlock {Text="Typing updates this preview. Apply / Enter creates an Undo step; Save also applies. Simple pattern shows only the name. Contact fields print when included in the layout. Text baked into an image must be changed in the source logo.",TextWrapping=TextWrapping.Wrap,FontSize=11});
        var contacts=new Button {Content="Company + contacts pattern",ToolTip="Replace this format with the contact-rich pattern. Keep stock and logos. Undo restores the previous design."};
        contacts.Click+=(_,_)=>Safe(()=>{ApplyCompany();Change(()=>ReplaceLayout(Layout.CompanyPreset()));_status.Text="Contact fields added in a contact-rich pattern for this format. Stock and logos retained. Undo restores the previous design; Save all formats activates it.";});content.Children.Add(contacts);
        SyncCompanyInputs();
    }
    private CompanyDetails ReadCompanyInputs()
    {
        var details=(_draft.Company??_company.ToDetails()).Clone();
        foreach(var (property,box) in _companyInputs)typeof(CompanyDetails).GetProperty(property)!.SetValue(details,box.Text.Trim());
        return details;
    }
    private bool HasPendingCompany=>JsonSerializer.Serialize(ReadCompanyInputs())!=JsonSerializer.Serialize(_draft.Company);
    private void SyncCompanyInputs()
    {
        _loadingCompany=true;
        try{foreach(var (property,box) in _companyInputs)box.Text=(string)typeof(CompanyDetails).GetProperty(property)!.GetValue(_draft.Company??_company.ToDetails())!;}
        finally{_loadingCompany=false;}
        if(_undoButton!=null)_undoButton.IsEnabled=_undo.Count>0;
        if(_redoButton!=null)_redoButton.IsEnabled=_redo.Count>0;
    }
    private void ApplyCompany()
    {
        var details=ReadCompanyInputs();details.Validate();
        if(HasPendingCompany)Change(()=>_draft.Company=details);
        SyncCompanyInputs();Render();
        _status.Text="Company details applied to all formats. Save all formats to update live stickers and printing.";
    }
    private void SaveDraft()
    {
        CommitForSave();LogoImages.ValidateLibrary(_draft);
        // One atomic library replacement: no separately saved company/layout half-state.
        _draft.Save(_savePath);SavedLibrary=_draft.Clone();_saved=true;
    }
    private void BuildLogoToneControls(StickerElement element)
    {
        _properties.Children.Add(new TextBlock {Text="LOGO DARKNESS · preview + print",FontWeight=FontWeights.Bold});
        SliderControl("Darkness",element.LogoDarkness,1,4,.1,value=>element.LogoDarkness=value);
        SliderControl("Contrast",element.LogoContrast,.5,3,.1,value=>element.LogoContrast=value);
        var mode=new CheckBox {Content="Solid black / white",IsChecked=element.LogoBlackWhite,IsEnabled=!element.Locked};
        mode.Click+=(_,_)=>Safe(()=>Change(()=>element.LogoBlackWhite=mode.IsChecked==true));_properties.Children.Add(mode);
        SliderControl("Black / white threshold",element.LogoThreshold,1,254,1,value=>element.LogoThreshold=(int)Math.Round(value),element.LogoBlackWhite);
        var reset=new Button {Content="Reset logo tones",IsEnabled=!element.Locked};
        reset.Click+=(_,_)=>Safe(()=>Change(()=>{element.LogoDarkness=1;element.LogoContrast=1;element.LogoBlackWhite=false;element.LogoThreshold=200;}));_properties.Children.Add(reset);
        var all=new Button {Content="Copy tones to all formats",ToolTip="Only logos with the same embedded source image are adjusted. Positions and other artwork are unchanged.",IsEnabled=!element.Locked};
        all.Click+=(_,_)=>Safe(()=>Change(()=>
        {
            foreach(var other in _draft.Layouts.SelectMany(l=>l.Elements).Where(e=>e.Field=="Logo" && e.ImagePng==element.ImagePng))
            {
                if(other.Locked)throw new FormatException("Unlock matching logos before applying tones to all formats.");
                other.LogoDarkness=element.LogoDarkness;other.LogoContrast=element.LogoContrast;other.LogoBlackWhite=element.LogoBlackWhite;other.LogoThreshold=element.LogoThreshold;
            }
        }));_properties.Children.Add(all);
        _properties.Children.Add(new TextBlock {Text="Increase Darkness for pale artwork. Black / white removes gray; higher threshold makes more pixels black. White backing stays white. Original embedded image is retained; Reset restores its imported appearance. This changes artwork, not printer heat/darkness settings.",TextWrapping=TextWrapping.Wrap,FontSize=11});
        void SliderControl(string label,double value,double min,double max,double step,Action<double> set,bool enabled=true)
        {
            var caption=new TextBlock {Text=$"{label}: {value:0.##}"};_properties.Children.Add(caption);
            var slider=new Slider {Minimum=min,Maximum=max,Value=value,TickFrequency=step,IsSnapToTickEnabled=true,SmallChange=step,LargeChange=step*5,IsEnabled=!element.Locked&&enabled};
            System.Windows.Automation.AutomationProperties.SetName(slider,"Logo "+label);
            slider.ValueChanged+=(_,_)=>{if(_refreshing)return;Safe(()=>Change(()=>set(slider.Value),false));caption.Text=$"{label}: {slider.Value:0.##}";};
            _properties.Children.Add(slider);
        }
    }
}
