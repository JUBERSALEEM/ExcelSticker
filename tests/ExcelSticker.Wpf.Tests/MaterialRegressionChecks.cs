using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExcelSticker.Theming;
using ExcelSticker.Wpf;

internal static class MaterialRegressionChecks
{
    public static void Run(MainWindow main, string settingsRoot)
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var scope = new ResourceDictionary();
        var engine = new ThemeEngine(scope, Path.Combine(settingsRoot, "material.json"));
        var token = new ThemeTokens();
        engine.Apply(token);
        var compactPadding = ((Thickness)scope["ClayCardPadding"]).Left;
        var compactRows = (double)scope["ClayRowHeight"];
        token.Density = "Comfortable"; engine.Apply(token);
        Check(((Thickness)scope["ClayCardPadding"]).Left > compactPadding && (double)scope["ClayRowHeight"] > compactRows,
            "Global density changes card padding and batch rows together");
        Check((double)scope["ClayFontSize"] == token.FontSize, "Density never scales text");
        token.TopLeft = 23; token.TopRight = 7; token.UniformCorners = true; engine.Apply(token);
        Check((CornerRadius)scope["ClayCardRadius"] == new CornerRadius(23), "Linked corners apply the same radius on all sides");
        token.UniformCorners = false; engine.Apply(token);
        Check(((CornerRadius)scope["ClayCardRadius"]).TopRight == 7, "Independent corners remain available explicitly");
        var fullRim = token.Clone(); fullRim.RimOpacity=1;
        Check(ThemeEngine.EdgeContrast(token) < ThemeEngine.EdgeContrast(fullRim), "Rim contrast accounts for the actual decorative opacity");
        token.ReducedEffects = true; engine.Apply(token);
        Check(scope["ClayControlShadow"] == null && (double)scope["ClayPlateInset"] == 0, "Reduced effects flattens controls and inner plates too");
        Check(scope["ClayAmbientShadow"] == null && scope["ClayControlAmbientShadow"] == null && (double)scope["ClayGloss"] == 0, "Reduced effects removes ambient lift and sheen");
        var normalized = ClayGeometry.Normalize(new Rect(0, 0, 100, 30), new CornerRadius(40));
        Check(normalized == new CornerRadius(7.5), "Oversized equal radii retain straight sides instead of becoming a capsule");
        Check(ClayGeometry.Rounded(new Rect(0, 0, 100, 30), normalized).IsFrozen, "Rounded geometry is frozen for reuse");
        foreach (var preset in ThemeTokens.Presets)
        {
            Check(ThemeEngine.ContrastPasses(preset), preset.Name + " contrast includes the pastel panel surfaces");
        }
        var legacy = Path.Combine(settingsRoot, "legacy-material.json");
        File.WriteAllText(legacy, "{\"Version\":1,\"Name\":\"Legacy\",\"TopLeft\":31,\"TopRight\":9,\"CardPadding\":20}");
        var migrated = ThemeEngine.Read(legacy);
        Check(migrated.Density == "Compact" && migrated.EdgeLighting == "Directional" && !migrated.UniformCorners && !migrated.ColorfulCards,
            "Legacy files acquire compact/directional defaults without overwriting palette or independent-corner intent");
        Check(File.ReadAllText(legacy).Contains("\"CardPadding\":20"), "Loading a legacy theme does not rewrite preferences");

        Check(migrated.LightElevation == 45 && migrated.ControlBevelWidth == 4 && migrated.Curvature == 1.2,
            "Version-one JSON receives safe material defaults without a schema break");
        var coral = ThemeTokens.Presets.First(t => t.Name == "Coral sculpted");
        engine.Apply(coral);
        Check(ThemeEngine.PageColors(coral).Distinct().Count()>1, "Monochrome coral has a genuine coordinated page wash");
        Check((double)scope["ClayInset"] == 18 && ((Thickness)scope["ClayCardPadding"]).Left >= 21,
            "Broad bevel is preserved in compact mode and text is padded beyond it");
        Check((double)scope["ClayControlInset"] == 6 && ((Thickness)scope["ClayControlPadding"]).Top >= 8,
            "Control bevel has independent depth and a flat text bed");
        Check(((System.Windows.Media.Effects.DropShadowEffect)scope["ClayAmbientShadow"]).ShadowDepth == 0,
            "Ambient lift is separate from the directional cast shadow");
        engine.Save(coral);
        var roundTrip = ThemeEngine.Read(engine.StorePath);
        Check(roundTrip.Gloss == coral.Gloss && roundTrip.LightElevation == coral.LightElevation && roundTrip.AmbientLift == coral.AmbientLift && roundTrip.ControlBevelWidth == 6,
            "New material controls survive JSON round trip");
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1d, 81d })
        {
            var bad = coral.Clone(); bad.LightElevation = invalid;
            try { bad.Validate(); throw new Exception("Invalid elevation accepted"); } catch (FormatException) { }
        }
        var settings = new ClayMaterialSettings(ThemeTokens.ParseColor(coral.Surface), ThemeTokens.ParseColor(coral.Shadow),
            18, 135, 40, .78, .58, .5, 24, 1.25, false, false);
        var analyticSize = new Size(160, 128); var analyticRadius = new CornerRadius(28);
        var baseline = new ClayLightingModel(analyticSize, analyticRadius, settings);
        var samples = new[] { new Point(80, 6), new Point(80, 11), new Point(80, 15), new Point(18, 18), new Point(6, 64), new Point(12, 12), new Point(80, 122) };
        foreach (var variation in new[] { settings with { LightElevation=75 }, settings with { Gloss=0 },
            settings with { GlossFocus=80 }, settings with { Curvature=.6 }, settings with { Recessed=true } })
        {
            var changed = new ClayLightingModel(analyticSize, analyticRadius, variation);
            Check(samples.Any(p => Difference(baseline.Sample(p.X,p.Y),changed.Sample(p.X,p.Y)) > 3),
                "Elevation, gloss, focus, curvature and recession each change curved-surface lighting");
            Check(changed.Sample(80,64) == settings.Fill, "Material changes preserve the exact flat-center color");
        }
        Check(ThemeEngine.Luminance(baseline.Sample(80,8)) > ThemeEngine.Luminance(baseline.Sample(80,120)),
            "Coral reference has a bright upper roll and a tinted underside");

        // Actual WPF raster tests, not a screenshot generated by a browser or image model.
        foreach (var dpi in new[] { 96d, 144d, 192d })
        foreach (var size in new[] { new Size(128, 128), new Size(400, 80) })
        {
            var chrome = new ClayChrome { Style=null, Fill=new SolidColorBrush(Color.FromRgb(220,203,250)),
                Radius=new CornerRadius(20), Inset=7, Highlight=.58, Shade=.22, LineWidth=0,
                Lighting=ClayLighting.Balanced, ShadeColor=Color.FromRgb(130,115,159) };
            chrome.Measure(size); chrome.Arrange(new Rect(size)); chrome.UpdateLayout();
            var image = Render(chrome, size, dpi);
            var w=image.PixelWidth; var h=image.PixelHeight; var depth=(int)Math.Round(2*dpi/96);
            var left=Pixel(image,depth,h/2); var right=Pixel(image,w-1-depth,h/2);
            var top=Pixel(image,w/2,depth); var bottom=Pixel(image,w/2,h-1-depth);
            Check(Difference(left,right)<=4 && Difference(top,bottom)<=4 && Difference(left,top)<=4,
                $"Balanced four-edge relief is symmetric at {dpi} DPI, {size.Width}×{size.Height} DIP");
            var corner=(int)Math.Round(8*dpi/96);
            Check(Difference(Pixel(image,corner,corner),Pixel(image,w-1-corner,h-1-corner))<=6,
                $"Opposite circular corners match at {dpi} DPI");
            Check(Difference(Pixel(image,w/2,h/2),Color.FromRgb(220,203,250))<=1, "Center retains the pastel fill, not a white gradient wash");
            chrome.Fill = Brushes.Coral;
            var updated=Render(chrome,size,dpi);
            Check(Difference(Pixel(updated,w/2,h/2),Colors.Coral)<=1, "Material cache invalidates when a live fill changes");
            if (size.Width == 128) Save(image,$"balanced-clay-{dpi:0}dpi.png");
        }
        // Rotating directional light must change actual curved-edge pixels, not just a token value.
        var sculpted = new ClayChrome { Style=null, Fill=new SolidColorBrush(Color.FromRgb(220,203,250)), Radius=new CornerRadius(20),
            Inset=8, Highlight=.7, Shade=.35, LineWidth=0, Lighting=ClayLighting.Directional, LightAngle=0 };
        var materialSize=new Size(128,128);sculpted.Measure(materialSize);sculpted.Arrange(new Rect(materialSize));sculpted.UpdateLayout();
        var east=Render(sculpted,materialSize,96);
        sculpted.LightAngle=180;
        var west=Render(sculpted,materialSize,96);
        Check(Difference(Pixel(east,2,64),Pixel(west,2,64))>3, "Changing light angle alters the actual left edge");
        Check(Difference(Pixel(east,125,64),Pixel(west,125,64))>3, "Changing light angle alters the actual right edge");
        Check(Difference(Pixel(east,8,8),Pixel(west,8,8))>2, "Directional surface normals shade the curved corner, not only the straight edge");
        Check(Difference(Pixel(east,64,64),Pixel(west,64,64))==0, "Directional relief does not wash out the material center");
        Save(east,"clay-light-east.png");Save(west,"clay-light-west.png");
        sculpted.Inset=18; sculpted.Radius=new CornerRadius(28); sculpted.LightAngle=135; sculpted.Gloss=.5;
        var beforeElevation=Render(sculpted,materialSize,96);
        sculpted.LightElevation=75;
        var afterElevation=Render(sculpted,materialSize,96);
        Check(Difference(Pixel(beforeElevation,64,8),Pixel(afterElevation,64,8))>3, "Elevation invalidates the actual chrome cache");
        sculpted.LightElevation=40; sculpted.Gloss=0; var matte=Render(sculpted,materialSize,96);
        sculpted.Gloss=1; var glossy=Render(sculpted,materialSize,96);
        Check(Enumerable.Range(2,14).Any(y=>Difference(Pixel(matte,64,y),Pixel(glossy,64,y))>3), "Gloss changes actual material pixels");
        var patches=Images(VisualTreeHelper.GetDrawing(sculpted)).Select(d=>(BitmapSource)d.ImageSource).ToArray();
        Check(patches.Length==8 && patches.All(p=>p.PixelWidth<=128 && p.PixelHeight<=128),
            "Material uses only eight bounded patches, never a full-window bitmap");
        Save(glossy,"sculpted-gloss.png");

        token.ReducedEffects=false;token.EdgeLighting="Balanced";token.LightAngle=90;engine.Apply(token);
        Check(((System.Windows.Media.Effects.DropShadowEffect)scope["ClayShadow"]).Direction==270, "Balanced shadow follows 90 degree light");
        token.LightAngle=270;engine.Apply(token);
        Check(((System.Windows.Media.Effects.DropShadowEffect)scope["ClayShadow"]).Direction==90, "Balanced shadow is not fixed to one direction");
        token.PageTreatment="Pastel wash";engine.Apply(token);
        Check(scope["ClayCanvas"] is LinearGradientBrush && scope["ClayCanvasTexture"] is Brush, "Full-page palette and optional texture are separate layers");
        token.PageTreatment="Solid";engine.Apply(token);
        Check(scope["ClayCanvas"] is SolidColorBrush, "Solid page remains an explicit option");
        File.WriteAllText(legacy,"{\"Name\":\"Oversized legacy\",\"TopLeft\":256,\"ButtonRadius\":64,\"InputRadius\":64,\"Pattern\":\"Dots\"}");
        var safe=ThemeEngine.Read(legacy);
        Check(safe.TopLeft==32 && safe.ButtonRadius==10 && safe.InputRadius==8 && safe.Pattern=="Speckles", "Legacy excessive rounding and circular texture migrate to rectangular policy");
        Check(File.ReadAllText(legacy).Contains("256"), "Migration leaves the original file intact until Save");
        var paper=(Border)main.FindName("StickerPaperFrame");
        var stickerCard=(ClayCard)main.FindName("StickerPreviewCard");
        Check(paper.CornerRadius==new CornerRadius(0) && paper.Padding==new Thickness(0), "Sticker paper has square corners and no wasteful padding");
        Check(stickerCard.CornerRadius.TopLeft>0, "The enclosing sticker card can still be rounded independently");
        var plate = (ClayCard)main.FindName("OuterFields"); plate.ApplyTemplate();
        Check(Descendants(plate).OfType<ClayChrome>().First().Relief==ClayRelief.Inset, "Outer glass uses real inset material, not a flat Border");
        var preview = (ContentControl)main.FindName("PreviewHost");
        Check(Math.Abs(preview.Width - LabelView.LabelWidth)<.001 && Math.Abs(preview.Height - LabelView.LabelHeight)<.001,
            "Compact workspace preserves the physical sticker preview size");

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OCEANGLASS_VISUAL_REVIEW")))
        {
            ((Button)main.FindName("ThemeStudioButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var studio=main.OwnedWindows.OfType<ThemeStudioWindow>().Single();
            try
            {
                main.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                Descendants(studio).OfType<ComboBox>().Single(c=>Equals(c.Tag,"Preset")).SelectedItem="Coral sculpted";
                main.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                main.UpdateLayout(); studio.UpdateLayout();
                Save(Render(main,new Size(main.ActualWidth,main.ActualHeight),96),"application-coral-sculpted.png");
                Save(Render(studio,new Size(studio.ActualWidth,studio.ActualHeight),96),"theme-studio-coral-sculpted.png");
            }
            finally { studio.Close(); }
        }
    }
    public static void VerifyEmptyBatch(MainWindow main)
    {
        var frame=(Border)main.FindName("BatchTableFrame");
        var table=(DataGrid)main.FindName("BatchGrid");
        var hint=(TextBlock)main.FindName("EmptyHint");
        if(table.Items.Count!=0 || frame.CornerRadius!=new CornerRadius(0) || table.ActualHeight<155 || hint.Parent!=frame.Child)
            throw new Exception("Empty batch must retain a rectangular table body with its empty state inside the frame.");
        Console.WriteLine("PASS empty batch does not collapse into a rounded header strip");
        Save(Render(frame,new Size(frame.ActualWidth,frame.ActualHeight),96),"empty-batch-panel.png");
    }
    private static IEnumerable<ImageDrawing> Images(Drawing? drawing)
    {
        if (drawing is ImageDrawing image) yield return image;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var nested in Images(child)) yield return nested;
    }
    private static RenderTargetBitmap Render(Visual visual, Size size, double dpi)
    {
        if(visual is UIElement element)element.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(size.Width*dpi/96),(int)Math.Ceiling(size.Height*dpi/96),dpi,dpi,PixelFormats.Pbgra32);
        image.Render(visual);return image;
    }
    private static Color Pixel(BitmapSource image,int x,int y)
    {
        var pixel=new byte[4];image.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);
        return Color.FromArgb(pixel[3],pixel[2],pixel[1],pixel[0]);
    }
    private static int Difference(Color a,Color b) => new[]{Math.Abs(a.R-b.R),Math.Abs(a.G-b.G),Math.Abs(a.B-b.B),Math.Abs(a.A-b.A)}.Max();
    private static void Save(BitmapSource image,string name)
    {
        var folder=Environment.GetEnvironmentVariable("OCEANGLASS_VISUAL_REVIEW");if(string.IsNullOrEmpty(folder))return;
        Directory.CreateDirectory(folder);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream=File.Create(Path.Combine(folder,name));encoder.Save(stream);
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
