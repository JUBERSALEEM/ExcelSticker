# Sculpted clay material overhaul — v0.8.0

## Reference analysis and source audit

The supplied ProGlass screenshot (15 October 2026) has a **convex rolled shoulder**, bright upper-left glints, a darker red underside, a flat readable center and soft detached shadows. Increasing `CornerRadius` alone cannot create that surface. Its capsule silhouettes are deliberately **not** copied: this application retains the previously requested bounded rounded rectangles, square batch table and square sticker paper.

The v0.7 path had three material limitations:

1. `ClayChrome.DrawDirectionalRelief` subtracted translated silhouettes. Those feathered bands followed corners, but had no surface normal, light elevation or specular reflection. Balanced mode used nested outline bands.
2. `ThemeEngine` silently limited `InsetDepth` to compact padding minus three DIP. Asking for broader relief could therefore make no visible difference.
3. Cards/buttons had one directional shadow only. Theme Studio offered direction and intensity, but no fullness, gloss, elevation or independent control bevel. Its tiny swatches were poor corner-inspection specimens.

## New renderer

`ClayLightingModel` replaces the old masks/bands in **both** lighting modes:

- Find the nearest straight boundary or circular corner arc and its outward XY normal.
- Convert inward boundary distance into a quarter-round bevel profile: `phi = (distance / width)^Curvature * pi/2`.
- Construct a 3D normal with horizontal component `cos(phi)` and face-facing component `sin(phi)`.
- Compute directional diffuse lighting and a view-facing half-vector specular lobe. Subtract the flat-face baseline so the interior keeps its exact base color.
- Mix shade toward the chosen shadow hue, and highlight toward a fill-derived near-white. Recessed plates/inputs reverse the horizontal normal. Pressed buttons use recessed relief and lose lift.
- Balanced mode supplies an omnidirectional studio rim, symmetric on all sides. Azimuth still controls its cast shadow; elevation/fullness/gloss change its material profile.

This is **2.5D shaded decoration**, not a 3D mesh, physically calibrated renderer, or a promise of pixel-identical browser rendering. Lighting uses RGB color mixing, not a full physically based material system.

`ClayBevelRenderer` draws an opaque vector base with eight small frozen bitmap patches: four corner patches and four one-pixel-wide edge strips. The center is not rasterized. Patches are regenerated only when material/size/DPI changes; `ClayChrome` caches the frozen drawing. Patch resolution follows visual DPI up to 4×; with the existing 32-DIP radius limit and 24-DIP bevel cap, no corner patch exceeds **128×128 pixels**. Large windows do not allocate full-window material textures. Vector geometry supplies the shared silhouette/clip/focus outline. Gradient or translucent caller-supplied fills retain flat brush rendering rather than being replaced by opaque material pixels.

This hybrid rendering replaces the previous all-vector material technique. At normal 96 DPI, four maximum-size corners use roughly 16 KB of pixel data per material, before WPF overhead; 400% uses roughly 256 KB. WPF drop shadows can allocate their own intermediate surfaces beyond this patch budget. No runtime performance benchmark has been run here.

## Composition and layout

`Themes/Clay.xaml` now composes raised cards/buttons from:

1. A flat decorative silhouette with a wide, zero-offset ambient shadow.
2. The shaded material silhouette with the directional cast shadow.
3. The independent content presenter.
4. A separate keyboard-focus outline where appropriate.

Neither shadow leaf is an ancestor of text. Disabled/pressed buttons hide ambient lift. Reduced effects removes both shadow types and all material depth/gloss, including the inspector. Inputs and inner plates remain recessed, not floating.

Card bevel width is independent of density. Effective card padding is at least `bevel + 3`; control padding is at least `control bevel + 2`. Broad bevels therefore take space explicitly rather than shading the text bed. Geometry caps material depth at 24% of the short side for tiny surfaces. The Studio inspector uses a compact bevel cap while the laboratory and real application use the full material. Explicit padding overrides in consumer controls still require visual review.

All v0.7 square-paper/table and radius constraints remain. The engine changes neither label dimensions nor the printer/paginator/layout model. The preview column still reserves the native 100×70 mm sheet plus effective card padding.

## Theme Studio and presets

Use **Theme Studio → Apply coral 3D reference to entire app → Save theme**. Alternatively select **Coral sculpted** or import `examples/themes/coral-sculpted.json`.

The preset uses coral surfaces, dark ink, a warm red shadow, 28-DIP card radii, an 18-DIP card bevel, a 6-DIP control bevel, 135° azimuth and 40° elevation. Linked card corners retain rectangular proportions. A monochrome palette now gets distinct coordinated page-wash stops rather than four identical colors. Four larger material specimens make curved shoulders easier to inspect. Existing saved preferences are never silently replaced with coral.

New controls:

| Token | Range | Purpose |
|---|---|---|
| `InsetDepth` | 0–24 DIP | Card bevel width; existing JSON name retained |
| `ControlBevelWidth` | 0–10 DIP | Independent compact control bevel |
| `LightElevation` | 15–80° | Light height above the surface |
| `Curvature` | 0.5–2 | Fullness of the rounded shoulder |
| `Gloss` | 0–1 | Specular sheen strength |
| `GlossFocus` | 4–96 | Broad-to-tight specular lobe |
| `AmbientLift` | 0–1 | Ambient opacity multiplier on the outer shadow opacity |
| `Shade` | 0–0.7 | Extended range for a pronounced tinted underside |

Light direction remains editable through 0–360°. Width, curvature and gloss are distinct from silhouette radius and cast-shadow offset. A zero outer-shadow opacity also makes ambient lift invisible.

JSON remains version 1, with additive fields and validated defaults for old files. Existing explicit `InsetDepth` values are retained. Save/revert/close session semantics, atomic persistence, and import/export paths are unchanged. New values participate in the same live resource pipeline as the actual application, not a disconnected preview renderer.

## Verification status and acceptance

Checked here: C# syntax trees, XML well-formedness, resource publication wiring, example JSON/palette calculations, analytic lighting sanity checks, and unchanged print/toolchain files. These are **not** C# compilation, XAML compilation, WPF execution or visual approval. The sandbox has no .NET SDK/Windows desktop runtime.

Windows regression coverage was extended for new JSON fields/defaults/ranges, compact padding reservations, separate ambient resources, analytic material changes, actual rendered angle/elevation/gloss changes, eight bounded patches, and Studio-to-application bindings. Existing symmetry, exact center color, text/effect separation, square empty batch, label size, save/revert and import tests remain.

Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
$env:OCEANGLASS_VISUAL_REVIEW = "$PWD/artifacts/visual-review"
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Review `application-coral-sculpted.png`, `theme-studio-coral-sculpted.png` and the material images; these are produced only by the Windows test run, not fabricated in this sandbox. Verify 100%, 150%, 200% and mixed-monitor DPI, fractional patch joins, independent/small corners, pointer/keyboard states, scrolling performance, and reduced effects. RenderTargetBitmap DPI tests alone are not real per-monitor validation. Compare against the supplied reference before accepting appearance. Contrast guard checks the flat text backgrounds, not every bevel pixel or every accessibility requirement.

SDK policy, the canonical solution, inactive workflow location, and the 100×70 mm printing contract remain unchanged.
