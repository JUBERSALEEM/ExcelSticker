using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExcelSticker.Theming;
using ExcelSticker.Wpf;

internal static class ThemeRegressionChecks
{
    public static void Run(MainWindow main, string settingsRoot)
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        void Flush() { main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); main.UpdateLayout(); }
        Button StudioAction(ThemeStudioWindow studio, string name) => Children(studio).OfType<Button>().Single(b => b.Name == name);
        T Field<T>(ThemeStudioWindow studio, string tag) where T : FrameworkElement => Children(studio).OfType<T>().Single(c => Equals(c.Tag, tag));
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var open = (Button)main.FindName("ThemeStudioButton");
        Click(open); Flush();
        var studio = main.OwnedWindows.OfType<ThemeStudioWindow>().Single();
        Require(main.IsEnabled, "Theme Studio is modeless; the application remains interactive");
        Click(open);
        Require(main.OwnedWindows.OfType<ThemeStudioWindow>().Count() == 1, "Repeated open reuses one theme session");
        Field<ComboBox>(studio, "Preset").SelectedItem = "Ocean"; Flush();
        Field<ComboBox>(studio, "EdgeLighting").SelectedItem = "Balanced";
        Require(Field<Slider>(studio, "LightAngle").IsEnabled, "Light direction remains editable in Balanced mode");
        Field<Slider>(studio, "LightAngle").Value = 45;
        Click(StudioAction(studio, "ApplyThemePreviewButton"));
        Require(((System.Windows.Media.Effects.DropShadowEffect)main.FindResource("ClayShadow")).Direction == 225, "Changing the studio angle moves the actual application shadow");
        Field<Slider>(studio, "TopLeft").Value = 29;
        var save = StudioAction(studio, "SaveThemeButton");
        Require(save.IsEnabled, "Valid Save remains enabled during debounced live rendering");
        Click(StudioAction(studio, "ApplyThemePreviewButton")); Flush();
        Require(Children(main).OfType<ClayCard>().First().CornerRadius.TopLeft == 29, "Studio changes reach the actual main application card");
        Require(((TextBlock)main.FindName("ThemeState")).Text.Contains("LIVE UNSAVED"), "Application explicitly distinguishes preview from saved theme");
        Require(save.InputHitTest(new Point(3, save.ActualHeight / 2)) != null, "Save button has a full hit-test surface");
        Click(save); Flush();
        var store = Path.Combine(settingsRoot, "theme.json");
        Require(File.Exists(store) && ThemeEngine.Read(store).TopLeft == 29, "Clicking Save actually persists the chosen theme");
        Require(studio.IsVisible && studio.HasSaved, "Save reports success without closing the editor");
        Require(Children(studio).OfType<TextBlock>().Single(t => t.Name == "ThemeStatusMessage").Text.StartsWith("SAVED"), "Successful Save feedback survives dispatcher processing");
        Require(((TextBlock)main.FindName("ThemeState")).Text.Contains("Saved and active"), "Application confirms immediate saved state");
        Require(new ThemeEngine(new ResourceDictionary(), store).Current.TopLeft == 29, "A new engine reloads the saved theme");
        Field<Slider>(studio, "TopLeft").Value = 31;
        Click(StudioAction(studio, "ApplyThemePreviewButton"));
        studio.Close(); Flush();
        Require(Children(main).OfType<ClayCard>().First().CornerRadius.TopLeft == 29, "Closing restores the last save, not the pre-save opening theme");
        Click(open); Flush(); studio = main.OwnedWindows.OfType<ThemeStudioWindow>().Single();
        var savedJson = File.ReadAllText(store);
        Field<TextBox>(studio, "Name").Text = "";
        Require(!StudioAction(studio, "SaveThemeButton").IsEnabled, "Invalid draft disables Save immediately with an explanation");
        Click(StudioAction(studio, "ApplyThemePreviewButton"));
        Require(File.ReadAllText(store) == savedJson, "Invalid draft never alters the persisted theme");
        Click(StudioAction(studio, "RevertThemeButton")); Flush();
        Require(Field<TextBox>(studio, "Name").Text == "Ocean", "Revert restores fields as well as resources");
        Click(StudioAction(studio, "InspectApplicationButton"));
        Require(!studio.IsVisible && main.IsEnabled, "Inspect application exposes the working screen without losing the draft");
        Click(open); Flush();
        Require(studio.IsVisible && main.OwnedWindows.OfType<ThemeStudioWindow>().Count() == 1, "Hidden studio reopens with the same session");
        Click(StudioAction(studio, "ApplyReferenceClayButton")); Flush();
        Require(Field<TextBox>(studio, "Name").Text == "Coral sculpted", "One-click reference selects the sculpted coral material");
        Field<Slider>(studio, "LightElevation").Value = 55;
        Field<Slider>(studio, "Gloss").Value = .7;
        Click(StudioAction(studio, "ApplyThemePreviewButton")); Flush();
        var materialCard = Children(main).OfType<ClayCard>().First(); materialCard.ApplyTemplate();
        var materialChrome = (ClayChrome)materialCard.Template.FindName("Surface", materialCard);
        Require(materialChrome.LightElevation == 55 && materialChrome.Gloss == .7 && materialChrome.Inset == 18,
            "New Studio controls bind to the real application material, not only the laboratory");
        Require(((ClayChrome)materialCard.Template.FindName("Ambient", materialCard)).Effect != null,
            "Application cards have a separate decorative ambient shadow leaf");
        Field<ComboBox>(studio, "Preset").SelectedItem = "Porcelain"; Flush();
        Click(StudioAction(studio, "SaveThemeButton")); studio.Close(); Flush();

        var input = (TextBox)main.FindName("OrderNo"); input.ApplyTemplate();
        var chrome = Children(input).OfType<ClayChrome>().First();
        Require(chrome.Inset == (double)main.FindResource("ClayInputInset"), "Input depth overrides the default chrome depth");
        Require(chrome.ReadLocalValue(ClayChrome.LineWidthProperty) == DependencyProperty.UnsetValue,
            "Chrome defaults do not shadow template state triggers with local values");
        var primary = (Button)main.FindName("AddButton");
        primary.IsEnabled = false; Flush();
        Require(((SolidColorBrush)primary.Background).Color == ((SolidColorBrush)main.FindResource("ClaySurface")).Color,
            "Disabled primary uses a readable neutral surface");
        primary.IsEnabled = true;

        var resources = new ResourceDictionary();
        var engine = new ThemeEngine(resources, Path.Combine(settingsRoot, "session-tests", "theme.json"));
        var snapshot = engine.Current; snapshot.TopLeft = 63;
        Require(engine.Current.TopLeft == new ThemeTokens().TopLeft, "Current returns a defensive snapshot");
        var revisions = new List<ThemeChangeKind>(); engine.Changed += (_, args) => revisions.Add(args.Kind);
        using (var session = engine.BeginEdit())
        {
            try { engine.BeginEdit(); throw new Exception("Second theme session was allowed"); }
            catch (InvalidOperationException) { }
            var unchangedInk = resources["Ink"];
            var draft = engine.Current; draft.TopLeft = 19; session.Preview(draft);
            Require(ReferenceEquals(unchangedInk, resources["Ink"]), "Geometry-only edits reuse unchanged frozen text brushes");
            var revision = engine.Revision; draft.Text = draft.Surface;
            try { session.Preview(draft); throw new Exception("Invalid contrast was allowed"); }
            catch (FormatException) { }
            Require(engine.Revision == revision && engine.Current.TopLeft == 19, "Invalid preview publishes no partial resource changes");
            session.Revert(); Require(engine.Current.TopLeft == new ThemeTokens().TopLeft, "Cancel-only session rolls back to opening theme");
            draft = engine.Current; draft.TopLeft = 20; session.Save(draft);
            draft.TopLeft = 22; session.Preview(draft);
        }
        Require(engine.Current.TopLeft == 20 && ThemeEngine.Read(engine.StorePath).TopLeft == 20, "Save advances the rollback point atomically");
        Require(revisions.Contains(ThemeChangeKind.Preview) && revisions.Contains(ThemeChangeKind.Saved), "Engine publishes observable preview/save revisions");

        // A directory at the destination reliably fails replacement even under administrator accounts.
        var blocked = Path.Combine(settingsRoot, "blocked-theme.json"); Directory.CreateDirectory(blocked);
        var blockedEngine = new ThemeEngine(new ResourceDictionary(), blocked);
        var failureStudio = new ThemeStudioWindow(blockedEngine) { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
        failureStudio.Show(); Flush();
        Field<Slider>(failureStudio, "TopLeft").Value = 30;
        Click(StudioAction(failureStudio, "ApplyThemePreviewButton"));
        Click(StudioAction(failureStudio, "SaveThemeButton")); Flush();
        Require(!failureStudio.HasSaved && failureStudio.IsVisible, "Failed Save retains the draft and keeps the studio open");
        Require(Children(failureStudio).OfType<TextBlock>().Single(t => t.Name == "ThemeStatusMessage").Text.Contains("Action failed"), "Save I/O failure is visible, not silently accepted");
        Require(StudioAction(failureStudio, "SaveThemeButton").IsEnabled, "Failed Save can be retried");
        failureStudio.Close();
        Require(blockedEngine.Current.TopLeft == new ThemeTokens().TopLeft && Directory.Exists(blocked), "Failed Save never advances rollback or destroys destination");
        Require(!Directory.EnumerateFiles(settingsRoot, "*.tmp", SearchOption.AllDirectories).Any(), "Theme persistence cleans temporary files");
    }
    private static IEnumerable<DependencyObject> Children(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Children(child)) yield return descendant;
        }
    }
}
