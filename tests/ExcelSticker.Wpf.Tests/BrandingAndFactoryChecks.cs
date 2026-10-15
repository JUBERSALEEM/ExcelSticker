using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExcelSticker.Theming;
using ExcelSticker.Wpf;

internal static class BrandingAndFactoryChecks
{
    public static void Run(MainWindow main, string settingsRoot)
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        void Flush() { main.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); main.UpdateLayout(); }
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(main.Title.StartsWith("LABLE STUDIO") && typeof(MainWindow).Assembly.GetName().Name=="LableStudio", "Application title and executable assembly use the requested LABLE STUDIO brand");
        Check(main.Icon!=null && ((Image)main.FindName("ApplicationLogo")).Source is BitmapSource { PixelWidth:256 }, "Window icon and header logo load from embedded resources");
        using(var stream=Application.GetResourceStream(new Uri("pack://application:,,,/LableStudio;component/Assets/lable-studio.ico")).Stream)
        {
            var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            Check(new[]{16,20,24,32,40,48,64,128,256}.All(size=>decoder.Frames.Any(f=>f.PixelWidth==size && f.PixelHeight==size)), "Embedded icon includes small taskbar/title-bar and large Explorer sizes");
        }
        Check(((TextBlock)main.FindName("ThemeState")).Text.Contains("Industrial flat"), "A clean installation opens with the factory flat theme");
        Check(main.FindResource("ClayCanvas") is SolidColorBrush && main.TryFindResource("ClayShadow")==null && (double)main.FindResource("ClayInset")==0,
            "Factory UI has a solid background, no shadows and no bevel shading");
        Check(Descendants(main).OfType<ClayChrome>().All(c=>c.Effect==null && c.Inset==0), "Actual startup controls are flat, not just the preset data");
        Check(Descendants(main).OfType<ClayCard>().All(c=>c.Background is SolidColorBrush b && b.Color==Colors.White), "Factory panels are neutral white, not colored pastel tiles");

        var folder=Path.Combine(settingsRoot,"factory-checks");Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"theme.json");
        var fresh=new ThemeEngine(new ResourceDictionary(),path);
        Check(fresh.Current.Name=="Industrial flat" && !File.Exists(path), "Factory defaults do not write a preference file on first run");
        Check(ThemeEngine.EdgeContrast(ThemeTokens.FactoryDefault)>=3, "Factory functional boundaries have at least 3:1 token contrast");
        var factory=ThemeTokens.FactoryDefault;factory.ReducedEffects=false;
        Check(ThemeTokens.FactoryDefault.ReducedEffects, "Factory recipes are defensive copies");
        var chosen=ReferenceStyles.All[0].Create();fresh.Save(chosen);
        Check(new ThemeEngine(new ResourceDictionary(),path).Current.Name==chosen.Name, "An explicit saved clay choice survives the branding update");
        File.WriteAllText(path,"invalid json");
        var recovered=new ThemeEngine(new ResourceDictionary(),path);
        Check(recovered.Current.Name=="Industrial flat" && recovered.LoadWarning!=null && File.ReadAllText(path)=="invalid json", "Corrupt preferences recover to flat without destroying the original file");
        chosen.Text=chosen.Surface;File.WriteAllText(path,JsonSerializer.Serialize(chosen));
        recovered=new ThemeEngine(new ResourceDictionary(),path);
        Check(recovered.Current.Name=="Industrial flat" && recovered.LoadWarning!=null, "Contrast-failing preferences recover to factory flat too");
        File.WriteAllText(path,"{\"Name\":\"Legacy clay\"}");
        Check(!ThemeEngine.Read(path).ReducedEffects, "Legacy JSON material defaults are not silently reinterpreted as factory flat");

        Click((Button)main.FindName("ThemeStudioButton")); Flush();
        var studio=main.OwnedWindows.OfType<ThemeStudioWindow>().Single();
        Button Action(string name) => Descendants(studio).OfType<Button>().Single(b=>b.Name==name);
        try
        {
            Descendants(studio).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Preset")).SelectedItem="Clayhub"; Flush();
            Check(main.TryFindResource("ClayShadow")!=null, "Clay effects remain an optional selectable style");
            Click(Action("SaveThemeButton")); Flush();
            var store=Path.Combine(settingsRoot,"theme.json");
            Check(ThemeEngine.Read(store).Name=="Clayhub", "Optional clay style can still be saved");
            Click(Action("UseFactoryThemeButton")); Flush();
            Check(main.TryFindResource("ClayShadow")==null && Descendants(studio).OfType<ClayChrome>().All(c=>c.Inset==0 && c.Effect==null),
                "Factory action flattens the actual app, inspector, laboratory and reference thumbnails");
            Click(Action("SaveThemeButton")); Flush();
            Check(ThemeEngine.Read(store).Name=="Industrial flat" && new ThemeEngine(new ResourceDictionary(),store).Current.ReducedEffects,
                "Factory action persists and reloads through the normal Save workflow");
        }
        finally { studio.Close(); Flush(); }
        Check(main.TryFindResource("ClayShadow")==null, "Closing Studio retains the saved industrial appearance");
        var output=Environment.GetEnvironmentVariable("OCEANGLASS_VISUAL_REVIEW");
        if(!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            var image=new RenderTargetBitmap((int)Math.Ceiling(main.ActualWidth),(int)Math.Ceiling(main.ActualHeight),96,96,PixelFormats.Pbgra32);
            image.Render(main);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream=File.Create(Path.Combine(output,"lable-studio-industrial-flat.png"));encoder.Save(stream);
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);yield return child;
            foreach(var nested in Descendants(child))yield return nested;
        }
    }
}
