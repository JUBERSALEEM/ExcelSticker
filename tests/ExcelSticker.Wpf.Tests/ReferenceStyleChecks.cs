using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExcelSticker.Theming;
using ExcelSticker.Wpf;

internal static class ReferenceStyleChecks
{
    public static void Run(MainWindow main, string settingsRoot)
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var scope = new ResourceDictionary();
        var engine = new ThemeEngine(scope, Path.Combine(settingsRoot,"reference-styles.json"));
        Check(ReferenceStyles.All.Count==6 && ThemeTokens.Presets.Select(t=>t.Name).Distinct().Count()==ThemeTokens.Presets.Count,
            "All six reference recipes coexist with the previous presets without duplicate names");
        foreach (var reference in ReferenceStyles.All)
        {
            var theme = reference.Create(); theme.Validate();
            Check(ThemeEngine.ContrastPasses(theme), reference.Name + " checks all six surface roles");
            Check(ThemeEngine.PanelColors(theme).Distinct().Count()==6, reference.Name + " supplies six distinct colors");
            engine.Apply(theme); engine.Save(theme);
            Check(JsonSerializer.Serialize(ThemeEngine.Read(engine.StorePath))==JsonSerializer.Serialize(theme), reference.Name + " exports/imports every material and palette field");
            var first = reference.Create(); first.PaletteRose="#FFFFFF";
            Check(reference.Create().PaletteRose!=first.PaletteRose, "Reference recipes return independent editable copies");
            Check(((CornerRadius)scope["ClayPlateRadius"]).TopLeft==theme.PlateRadius, "Nested plate radius is independent of input radius");
            Check((scope["ClayPlateShadow"]!=null)==(theme.PlateRelief=="Raised"), "Only raised nested plates receive a cast shadow");
        }
        var edited=ReferenceStyles.All[0].Create(); edited.PageTreatment="Pastel wash"; engine.Apply(edited);
        var originalPage=scope["ClayCanvas"];
        edited.PaletteRose="#F8E3E9"; engine.Apply(edited);
        Check(!ReferenceEquals(originalPage,scope["ClayCanvas"]), "Palette-only edits invalidate the coordinated page wash");
        edited.ColorfulCards=false; engine.Apply(edited); originalPage=scope["ClayCanvas"];
        edited.Accent="#B03E54"; engine.Apply(edited);
        Check(!ReferenceEquals(originalPage,scope["ClayCanvas"]), "Accent-only edits invalidate monochrome page wash");
        edited.PlateRelief="Raised"; edited.ReducedEffects=true; engine.Apply(edited);
        Check(scope["ClayPlateShadow"]==null && (double)scope["ClayPlateInset"]==0, "Reduced effects also flattens raised plates");
        var invalid=edited.Clone(); invalid.PaletteYellow="not rgb";
        try { invalid.Validate(); throw new Exception("Bad palette accepted"); } catch(FormatException) { }
        invalid=edited.Clone(); invalid.SurfaceShadowTint=double.NaN;
        try { invalid.Validate(); throw new Exception("Nonfinite tint accepted"); } catch(FormatException) { }
        invalid=edited.Clone(); invalid.PlateRelief="Unknown";
        try { invalid.Validate(); throw new Exception("Bad relief accepted"); } catch(FormatException) { }

        var legacyPath=Path.Combine(settingsRoot,"reference-legacy.json");
        const string legacyJson="{\"Name\":\"Older theme\",\"InputRadius\":8}";
        File.WriteAllText(legacyPath,legacyJson);
        var migrated=ThemeEngine.Read(legacyPath);
        Check(migrated.PlateRadius==8 && migrated.PlateRelief=="Inset" && migrated.SurfaceShadowTint==0,
            "Legacy themes preserve their input-derived plate radius and untinted shadows");
        Check(File.ReadAllText(legacyPath)==legacyJson, "New palette defaults do not rewrite legacy files");

        // A material leaf owns its clone; shared theme effects and brushes must not be mutated/frozen.
        var shared=new DropShadowEffect { Color=Colors.Purple, Direction=315, Opacity=.3 };
        var leaf=new ClayChrome { Style=null, Fill=Brushes.PaleGreen, SurfaceShadowTint=.75, Shadow=shared };
        var green=((DropShadowEffect)leaf.Effect).Color;
        Check(leaf.Effect.IsFrozen && !shared.IsFrozen && shared.Color==Colors.Purple, "Tinted shadow clones are frozen, not borrowed effects");
        leaf.Fill=Brushes.Pink;
        Check(((DropShadowEffect)leaf.Effect).Color!=green, "Live fill changes retint the actual effect");
        leaf.SurfaceShadowTint=0;
        Check(((DropShadowEffect)leaf.Effect).Color==Colors.Purple, "Zero tint preserves the legacy global shadow color");
        leaf.Shadow=null;
        Check(leaf.Effect==null, "Clearing the shadow source removes the derived effect");

        void Flush() { main.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); main.UpdateLayout(); }
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Click((Button)main.FindName("ThemeStudioButton")); Flush();
        var studio=main.OwnedWindows.OfType<ThemeStudioWindow>().Single();
        Button Action(string name) => Descendants(studio).OfType<Button>().Single(b=>b.Name==name);
        Button Tile(string id) => Descendants(studio).OfType<Button>().Single(b=>Equals(b.Tag,"Reference:"+id));
        var original=((SolidColorBrush)((ClayCard)main.FindName("EntryCard")).Background).Color;
        try
        {
            Check(Descendants(studio).OfType<Button>().Count(b=>b.Tag is string tag && tag.StartsWith("Reference:"))==6, "Six reference tiles are keyboard-usable buttons in the real Studio");
            var live=Descendants(studio).OfType<CheckBox>().Single(c=>c.Name=="LivePreviewToggle");
            live.IsChecked=false; Click(Tile("clayshop")); Flush();
            Check(((SolidColorBrush)((ClayCard)main.FindName("EntryCard")).Background).Color==original,
                "Choosing a reference respects Live preview off");
            live.IsChecked=true;
            foreach (var reference in ReferenceStyles.All)
            {
                Click(Tile(reference.Id)); Flush();
                var expected=reference.Create();
                var roles=new[] { ("EntryCard",expected.PaletteLavender), ("OuterFields",expected.PaletteMint),
                    ("BatchCard",expected.PalettePeach), ("StickerPreviewCard",expected.PaletteBlue),
                    ("PrintSettingsCard",expected.PaletteYellow), ("InnerFields",expected.PaletteRose) };
                foreach (var (name,hex) in roles)
                    Check(((SolidColorBrush)((ClayCard)main.FindName(name)).Background).Color==ThemeTokens.ParseColor(hex), reference.Name+" updates actual "+name);
                var plate=(ClayCard)main.FindName("OuterFields"); plate.ApplyTemplate();
                var chrome=(ClayChrome)plate.Template.FindName("PlateSurface",plate);
                Check(chrome.Relief==(expected.PlateRelief=="Raised" ? ClayRelief.Raised : ClayRelief.Inset), "Nested plate topology follows the selected style");
                Check((chrome.Effect!=null)==(expected.PlateRelief=="Raised"), "Actual nested plate shadow follows its topology");
                var card=(ClayCard)main.FindName("EntryCard");card.ApplyTemplate();
                var surface=(ClayChrome)card.Template.FindName("Surface",card);
                var color=ClayChrome.TintedShadow(ThemeTokens.ParseColor(expected.Shadow),ThemeTokens.ParseColor(expected.PaletteLavender),expected.SurfaceShadowTint);
                Check(((DropShadowEffect)surface.Effect).Color==color, "Actual card shadow matches its surface hue");
                Check(((Border)main.FindName("StickerPaperFrame")).CornerRadius==new CornerRadius(0), "Reference styles never round the sticker paper");
                Save(main,"reference-"+reference.Id+"-application.png");
                Save(studio,"reference-"+reference.Id+"-studio.png");
            }
            Descendants(studio).OfType<TextBox>().Single(t=>Equals(t.Tag,"PaletteRose")).Text="#F8E3E9";
            Click(Action("ApplyThemePreviewButton")); Flush();
            Check(((SolidColorBrush)((ClayCard)main.FindName("InnerFields")).Background).Color==ThemeTokens.ParseColor("#F8E3E9"),
                "Custom palette fields edit the real inner-glass surface immediately");
            var reduced=Descendants(studio).OfType<CheckBox>().Single(c=>Equals(c.Tag,"ReducedEffects"));
            reduced.IsChecked=true; reduced.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Click(Action("ApplyThemePreviewButton")); Flush();
            Check(Descendants(studio).OfType<ClayChrome>().All(c=>c.Inset==0 && c.Effect==null),
                "Reduced effects flattens the gallery thumbnails, inspector and laboratory too");
            // Persist and reopen behavior is also exercised independently by ThemeRegressionChecks.
            Click(Action("RevertThemeButton")); Flush();
            Check(((SolidColorBrush)((ClayCard)main.FindName("EntryCard")).Background).Color==original, "Revert restores the opening application palette after reference changes");
        }
        finally { studio.Close(); Flush(); }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);yield return child;
            foreach(var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Save(Window window,string name)
    {
        var folder=Environment.GetEnvironmentVariable("OCEANGLASS_VISUAL_REVIEW"); if(string.IsNullOrEmpty(folder))return;
        window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(folder);using var stream=File.Create(Path.Combine(folder,name));encoder.Save(stream);
    }
}
