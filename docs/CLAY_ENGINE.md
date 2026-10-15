# Clay Engine: research, design decisions, and integration

## Research findings

This implementation is informed by the following sources, not by a claim that a visual trend alone guarantees usability:

1. **Shape and lighting.** Claymorphism uses an outer shadow plus light and dark inset shadows, with rounded geometry and a fill independent of the page background. Visual hierarchy and color contrast remain essential. [5](https://www.smashingmagazine.com/2022/03/claymorphism-css-ui-design-trend/)
2. **Why WPF text can blur.** Effects, opacity, clipping, and other intermediate render targets can change the background available to the text renderer and disable ClearType. Forcing ClearType everywhere is not a universal fix, especially on nonopaque backgrounds. [2](https://learn.microsoft.com/en-us/archive/blogs/llobo/new-wpf-features-cleartypehint)
3. **Pixel boundaries.** WPF's layout-rounding guidance recommends enabling rounding at the visual root to avoid inheriting fractional coordinates. Official API reference: <https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.uselayoutrounding?view=windowsdesktop-8.0>.
4. **Contrast.** WCAG's normal-text minimum is 4.5:1, with 3:1 for qualifying large text. Thin font rendering can still look faint even when the underlying colors pass, so contrast is necessary but not a complete visual test. Official explanation: <https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html>. Non-text guidance: <https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html>.

### Applied decisions

| Concern | Implementation | Tradeoff / verification |
|---|---|---|
| Sculpted material | Opaque fills, feathered light/dark inset rings, rounded geometry, isolated outer shadows | WPF geometry approximates the CSS inset recipe; it is not a CSS box-shadow implementation |
| Depth without fuzzy labels | `ClayChrome` is decorative-only; text/content presenters are sibling visuals | No text-bearing card or button is assigned an `Effect` |
| Typography | Display text metrics, root layout rounding, pixel snapping, explicit per-monitor DPI manifest | Still verify actual monitors at 100%, 150%, and 200% |
| Crisp sticker preview | No automatic `Viewbox` shrink in the main preview; no text-scaling viewboxes in sticker fields | Very long fields get a fit warning instead of shrinking below 6 pt |
| Palette | Semantic canvas, surface, text, muted, accent, border, shadow roles | Theme text guard evaluates concrete base/selected/pattern colors, not a whole-app accessibility audit |
| Focus | Contrasting keyboard outlines; selected states use a separate fill | Native Windows file and printer dialogs remain under OS control |
| Rich patterns | Subtle vector dots/grid, density and opacity controls | Patterns stay behind workspace surfaces, never behind printed text |
| Performance | Frozen brushes/effects, reusable resource dictionary, debounced studio updates, no continuous animation | A low-power/reduced-effects preset removes outer effects and inset depth |
| Print fidelity | Separate monochrome renderer and mm/pt layout model | Printer media/offsets still require hardware calibration |

## Separation of responsibilities

```text
ExcelSticker.Theming              Independent WPF library; no Core/Wpf app reference
 ├─ ThemeTokens                   Versioned JSON data, validation, presets
 ├─ ThemeEngine                   Token → frozen resources; contrast; storage
 ├─ ThemeEditSession              Single-editor preview/save/rollback transaction
 ├─ ClayChrome / ClayCard         Decorative rendering and content composition
 ├─ Themes/Clay.xaml              Shared control templates using DynamicResource
 ├─ ThemeStudioWindow             Live controls, import/export, save/revert
 └─ ColorPickerWindow             RGB sliders plus editable HEX in studio

ExcelSticker.Core
 ├─ StickerLayout / LayoutLibrary Physical-mm layout data and persistence
 └─ LabelPrintPlan                Numbered immutable per-copy label records

ExcelSticker.Wpf
 ├─ Studio/StickerEditorWindow    Drag/resize canvas and property inspector
 ├─ LabelView                     Shared print/preview renderer; no theme dependency
 └─ LabelPaginator                Physical-size pages with a layout snapshot
```

The theme engine cannot change label coordinates, paper size, print colors, or customer data. The sticker editor cannot change the app's theme. No imported XAML, scripts, arbitrary fonts, or shader binaries are executed.

## Studio controls

- **Palette:** canvas, surface, accent, primary/secondary text, border, shadow; HEX entry and RGB picker.
- **Geometry:** four independent card corners; button/input radii; border visibility and width.
- **Lighting:** direction, shadow blur/depth/opacity, inset feathering, highlight and shade strength.
- **Density:** card and control padding.
- **Typography:** supported UI font family, body size, secondary size.
- **Pattern:** None, Dots, Grid; spacing and intensity.
- **Accessibility:** text contrast guard, reported border contrast, visible focus, reduced effects, high-contrast preset.
- **Lifecycle:** live app-wide preview; Save theme; Cancel/revert; import/export JSON.

Inset depth is capped by content padding so edge shading does not encroach on text. Small controls and inset input wells use derived depth/light/shade tokens; reduced-effects mode removes their inset shading as well.

Presets: Porcelain, Ocean, Mint, Lavender, Graphite, High contrast. All built-in preset text colors are checked against the base surface, canvas, and selected fill. The border report targets 3:1 for functional boundaries; decorative strokes and disabled controls have different accessibility considerations. Turning off borders or the text guard is an explicit user choice, not an accessibility guarantee. The high-contrast preset is manually selected; automatic synchronization with the Windows high-contrast setting is not implemented.

Closing the studio restores the last successful save in the session, or the opening theme if nothing was saved. Save persists and applies immediately without closing. The studio is modeless in the main app; its header reports preview/saved state. Theme drafts with invalid values are not applied. When contrast guard is on, a failing palette retains the last valid live preview and cannot be saved. Import/export is data-only and size-limited. The app stores the chosen theme in `%LOCALAPPDATA%\OceanGlass\theme.json` using a temporary-file replacement; corrupt/unsupported settings fall back to defaults with a status warning.

## Reuse in another WPF app

Reference `ExcelSticker.Theming.csproj` and merge its styles:

```xml
<ResourceDictionary Source="/ExcelSticker.Theming;component/Themes/Clay.xaml" />
```

Initialize tokens before creating themed windows:

```csharp
var engine = new ThemeEngine(Application.Current.Resources, optionalSettingsPath);
engine.Apply(ThemeTokens.Presets.First(t => t.Name == "Ocean"));
new ThemeStudioWindow(engine) { Owner = mainWindow }.Show(); // Keep/reuse one instance per engine.
```

For a subclassed window, explicitly use the shared window style:

```xml
<Window Style="{StaticResource {x:Type Window}}" ...>
  <clay:ClayCard Style="{StaticResource Card}">
    <StackPanel><!-- Content is never a child of the shadow visual. --></StackPanel>
  </clay:ClayCard>
</Window>
```

The main window and both studios fit their initial size to the current monitor work area. On narrower windows the outer/inner input cards stack vertically. These accommodations still require actual multi-monitor/DPI testing.

The library includes templates for cards, buttons, text boxes, combo boxes/items, checkboxes, sliders, list items, scrollbars, tooltips, and table headers. Business-specific DataGrid columns stay in the app, but consume the shared tokens. OS dialogs are deliberately not reimplemented.

## Non-goals / remaining verification

This is an implemented editor/toolkit, not a claim of WCAG certification, perfect behavior on every DPI configuration, or a tested printer driver. There is no marketplace, plugin loader, animation timeline, CSS import, or image-based theme generator. The live preview and property controls need Windows interaction tests; actual paper output needs a calibrated printer. Research references are not a substitute for those tests.

For the v0.5.0 lifecycle, control-precedence repairs, toolchain setup and acceptance checklist, see [Theme and solution review](THEME_AND_SOLUTION_REVIEW.md).
