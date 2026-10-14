using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExcelSticker.Theming;

internal sealed class ColorPickerWindow : Window
{
    public string SelectedHex {get;private set;}
    public ColorPickerWindow(string initial)
    {
        var color=ThemeTokens.ParseColor(initial);SelectedHex=initial;
        Title="Clay Studio • RGB color";Width=420;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty,typeof(Window));
        var content=new StackPanel();Content=new ClayCard {Content=content,Margin=new Thickness(24)};
        var preview=new Border {Height=65,CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(color),Margin=new Thickness(0,0,0,12)};content.Children.Add(preview);
        var hex=new TextBlock {Text=initial,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12)};content.Children.Add(hex);
        var sliders=new Slider[3];var values=new[]{color.R,color.G,color.B};var names=new[]{"Red","Green","Blue"};
        for(var i=0;i<3;i++)
        {
            content.Children.Add(new TextBlock {Text=names[i]});
            var slider=new Slider {Minimum=0,Maximum=255,Value=values[i],TickFrequency=1,SmallChange=1,LargeChange=16};
            System.Windows.Automation.AutomationProperties.SetName(slider,names[i]);sliders[i]=slider;content.Children.Add(slider);
        }
        foreach(var slider in sliders)slider.ValueChanged+=(_,_)=>
        {
            var next=Color.FromRgb((byte)sliders[0].Value,(byte)sliders[1].Value,(byte)sliders[2].Value);
            SelectedHex=$"#{next.R:X2}{next.G:X2}{next.B:X2}";hex.Text=SelectedHex;preview.Background=new SolidColorBrush(next);
        };
        var actions=new WrapPanel();content.Children.Add(actions);
        var cancel=new Button {Content="Cancel",IsCancel=true};actions.Children.Add(cancel);
        var apply=new Button {Content="Use color",IsDefault=true};apply.SetResourceReference(StyleProperty,"Primary");apply.Click+=(_,_)=>DialogResult=true;actions.Children.Add(apply);
    }
}
