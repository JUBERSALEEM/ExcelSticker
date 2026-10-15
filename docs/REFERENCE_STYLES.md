# Six reference styles — v0.9.0

## What is implemented

The six supplied images are translated into **selectable application-wide theme recipes**, not six unrelated analytics, commerce or music applications. Their visual language is applied to the existing label workflow, Theme Studio and shared controls. No chart data, storefront products, playback buttons or decorative bitmap icons are inserted into the business UI.

| Supplied reference | Studio preset | Material and palette mapping |
|---|---|---|
| `clay hub.jpeg` | **Clayhub** | Lavender entry, mint plate, peach batch and blue preview; moderately rolled highlights, subtle edge lines |
| `clay pattern edge effect.jpeg` | **Puffy studio** | Broad inflated shoulders, stronger gloss, candy mint/peach/sky/butter/rose accents, soft lift |
| `round card clay.jpeg` | **Daily progress** | Saturated lavender, mint recess, broad luminous shoulder, 32-DIP card radius, humanist Calibri text |
| Roadmap / Clayboard image | **Clayboard** | Pale nested panels, shallower bevels, low gloss, small cast shadow and raised inner cards |
| Shop / Clayshop image | **Clayshop** | More saturated six-color tiles, coral actions and raised inner cards |
| Music / Clayplay image | **Clayplay** | Violet/mint/blue palette, coral actions, raised inner cards and moderate glossy relief |

Each recipe defines the palette, material, shadows, typography, corner geometry and nested-panel relief together. The screenshot's round avatars/pills are **not** copied because the standing application requirement is bounded rounded rectangles. Calibri/Segoe UI use installed Windows fonts; no unlicensed font or image bundle is embedded. Primary-action text uses the existing contrast-selected ink rather than copying low-contrast white text blindly.

## Apply and edit

1. Rebuild and open **Theme Studio**.
2. Expand **Six reference styles · apply a complete look** beneath the presets/coral action, and select a tile. All six also appear in the preset dropdown.
3. With **Live preview in application** on, the actual app updates. With it off, selection changes only the draft; use **Apply preview now** or enable Live preview.
4. Use **Inspect application** to inspect the working label screen. Reopen Studio without losing the draft.
5. **Save theme** persists the selected style and edits. Revert/close retain the existing saved/opening-theme semantics.

The gallery uses miniature renderings from each recipe, not screenshots claiming to show a verified Windows build. Its color chips are decorative. The larger six labeled specimens in the live laboratory use the real active application resources. Reduced effects also flattens gallery specimens.

Existing saved themes are **not** automatically replaced. `Coral sculpted`, Pastel clay and all previous presets remain available. Shareable JSON files for all fourteen presets are in `examples/themes/`.

## Engine changes beyond presets

### Six editable surface roles

The old engine mixed four hard-coded pastel colors into every theme. The new additive JSON fields are `PaletteLavender`, `PaletteMint`, `PalettePeach`, `PaletteBlue`, `PaletteYellow`, and `PaletteRose`. Each accepts an opaque `#RRGGBB` value and has a Studio color picker.

| Resource | Real application surface |
|---|---|
| `ClayLavender` | Entry form |
| `ClayMint` | Outer glass plate |
| `ClayPeach` | Batch panel and table header |
| `ClayBlue` | Sticker's outer preview card |
| `ClayYellow` | Print settings |
| `ClayRose` | Inner glass plate |

`ColorfulCards` and `SurfaceTint` continue to control their blending. Disable colorful surfaces to use a monochrome palette. Reduced effects retains the existing flat-surface policy. Text contrast validation now includes all six colors and their page-wash/texture variants. Page-brush caching now invalidates on palette changes **and** accent changes; an accent-only edit previously left a monochrome wash stale.

### Per-surface shadows

`SurfaceShadowTint` (0–1) blends the global shadow color toward a darker version of each individual fill. The same hue function drives the analytic bevel shading and cast/ambient shadows, so mint cards no longer have to borrow the exact lavender shadow of a neighboring card.

`ClayChrome.Shadow` takes an immutable source `DropShadowEffect`. The decorative leaf owns a frozen tinted clone in its `Effect` property. Fill/tint changes rebuild the clone without mutating shared resources. Pressed/disabled template triggers clear the **Shadow source**, so a subsequent live fill change cannot resurrect a shadow that should be disabled. Text remains a sibling, never inside an effect-bearing ancestor. Callers supplying a raw `Effect` without a `Shadow` source remain supported.

### Raised versus recessed nested plates

`PlateRelief` selects `Inset` or `Raised`; `PlateRadius` allows 0–16 DIP independently from input corners. The shared `InnerPlate` style and actual W1/H1 and W2/H2 surfaces use these resources. Raised plates get a compact cast shadow; recessed plates reverse their surface normals and have no outer shadow. Reduced effects clears both relief depth and lift.

The v0.8 curved-normal material, DPI-aware bounded corner patches, editable lighting and crisp sibling text remain the underlying renderer. This is a 2.5D material approximation, not a pixel-identical recreation of the reference artwork.

## Compatibility and scope

- JSON remains version 1 with additive validated fields; absent fields get safe defaults.
- `SurfaceShadowTint` defaults to zero, preserving earlier global shadow behavior.
- Older theme files lacking `PlateRadius` inherit their migrated input radius at read time. No file is rewritten until Save.
- Palette recipes return independent copies; edits do not mutate a built-in recipe.
- Square batch/paper frames, retained empty-table body and **100×70 mm** print geometry are unchanged.
- No changes to Core label data, import handling, paginator, solution/SDK policy or inactive workflow location.
- The label preview itself stays white/monochrome, even inside a colorful preview card.

## Verification

Executed in the sandbox: C# syntax parsing, XML well-formedness, dynamic-resource publication checks, six-role/square-frame/physical-preview structural assertions, fourteen example palette calculations, and `git diff --check`.

Minimum flat-background body / secondary contrast for the new recipes:

| Preset | Body | Secondary |
|---|---:|---:|
| Clayhub | 11.70:1 | 6.46:1 |
| Puffy studio | 10.06:1 | 5.55:1 |
| Daily progress | 9.38:1 | 5.18:1 |
| Clayboard | 11.99:1 | 6.62:1 |
| Clayshop | 8.78:1 | 4.84:1 |
| Clayplay | 8.33:1 | 4.60:1 |

These are token calculations, not accessibility certification or measured glyph contrast on a Windows screen.

`ReferenceStyleChecks` adds Windows coverage for all recipes, round-trip serialization, independent copies, palette/page-cache invalidation, shadow clone ownership/live recoloring/removal, raised/recessed plate behavior, actual Studio gallery selection with Live on/off, six real application surfaces, custom palette editing, reduced-effects gallery behavior and revert. Existing material/print/save checks remain.

**Not executed here:** C#/XAML compilation, WPF tests, native screenshots or visual comparison. This sandbox has no .NET SDK/Windows desktop runtime. Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
$env:OCEANGLASS_VISUAL_REVIEW = "$PWD/artifacts/visual-review"
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

The new test writes `reference-<style-id>-application.png` and `reference-<style-id>-studio.png` for all six presets only when run with that environment variable. Inspect at 100%, 150%, 200% and mixed-monitor DPI. Check small-window scrolling, nested-card clipping, effect cost, keyboard focus, pointer states, monochrome paper, and a physical test label. Do not treat syntax checks or the gallery miniatures as Windows visual approval.
