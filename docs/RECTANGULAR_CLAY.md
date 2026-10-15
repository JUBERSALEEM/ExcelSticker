# Rectangular layout and curved clay material — v0.7.0

> The shape/layout fixes below remain current. The v0.7 inset-mask material is superseded by the [v0.8 sculpted material engine](SCULPTED_CLAY.md).

## Screenshot diagnosis

There was no literal `999` radius in the checked-in application. Two real mechanisms could still make a capsule:

- The empty batch DataGrid could measure to just its column-header height inside a wrapper using the editable input radius.
- Radius controls allowed up to 64 DIP. The renderer previously normalized oversized radii to half the short side, creating circular ends. Native Border-based outlines could also disagree with the decorative chrome.

The batch now has a **square rectangular frame**, a minimum 156-DIP table body, and its empty message inside that frame. The header uses the batch card's peach palette, not a mismatching rounded outline. It no longer relies on a large radius to decorate an almost-empty strip.

## Shape policy

- Adjustable card corners: **0–32 DIP**.
- Buttons: **0–10 DIP**; inputs: **0–8 DIP**.
- Decorative geometry also caps each radius to **one quarter of the short side**, preserving straight edges even when a small control inherits a larger radius.
- Slider thumbs, checkboxes and scrollbar thumbs are rounded rectangles, not circles. Focus outlines use the same bounded geometry as the surface.
- The sticker **paper frame is square, white, centered and has zero padding**. Its enclosing `StickerPreviewCard` retains independent adjustable card rounding. Paper remains exactly 100 × 70 mm; no theme scale is applied to label output.
- Copy controls have less spacing; empty validation text no longer reserves an unnecessary gap.

Oversized radii in saved theme files are capped at read time without discarding the palette or rewriting the file. Saving explicitly writes the safe values. Invalid nonfinite/negative values remain validation errors. This is a compatibility correction, not a silent deletion of user preferences.

## How the references are applied

The supplied labeled progress card shows a rounded rectangular silhouette, a softly lit upper edge, a tinted darker underside and a cast shadow. The component sheet also contains circles and pill controls, but the latest requirement explicitly rejects those shapes. We retain the sculpted pastel **material**, not those circular component silhouettes.

Directional relief now uses **opposing, feathered inset masks formed from the rounded silhouette itself**. The offset masks follow both straight edges and curved corners. Highlight color is mixed with the fill; shade is mixed with the configured shadow color. The center is not replaced with a white gradient. Raised and recessed surfaces reverse these offsets. Only decorative chrome receives outer shadow effects; text stays in sibling visuals.

This is a 2D WPF approximation of a curved 3D material, not a 3D mesh or a claim of pixel-identical rendering to the reference images. Balanced mode retains symmetric perimeter bands as an alternative.

## Lighting is no longer gated by Balanced mode

The Light direction slider is always editable, **0–360°**. `135°` is only the starting value (upper-left light), not a fixed angle.

- **Directional:** angle changes inner curved-edge lighting and cast shadows.
- **Balanced:** inner rim strength stays equal; angle still rotates the cast shadow.
- **Reduced effects:** inner relief and shadows are intentionally removed, so no lighting effect will be visible.

The former hard-coded 270° Balanced shadow and the disabled angle slider are removed. A directional east/west raster regression checks actual edge and corner pixel changes, not merely a property value.

## Full-page palette

**PageTreatment = Pastel wash** applies a restrained full-window blend of the panel palette through the shared Window background. `Solid` remains available. Optional Grid/Speckles textures are separate transparent layers on the main/studio workspace roots, so enabling texture does not replace the page palette. Old Dots texture data migrates to tiny rectangular Speckles rather than circular decorations. File/printer dialogs remain native Windows UI.

Page colors are included in the contrast guard. Reduced effects uses a solid page and disables texture. Existing palette choices remain saved as before; the app does not silently switch a user's theme.

To apply the complete reference-inspired configuration:

1. Open **Theme Studio**.
2. Click **Apply reference clay to entire page** (or select **Pastel clay**).
3. Keep **Compact**, **Directional**, **Pastel wash**, and linked card corners.
4. Move **Light direction** to inspect the actual application's light/shadow response.
5. Click **Save theme**.

The new example `examples/themes/pastel-clay.json` contains the same preset. All distributed example themes were updated to the current compact shape/lighting settings.

## Regression and visual review

New/updated Windows checks cover:

- Empty batch rectangular body and internal empty message.
- Safe radius limits and legacy radius/texture migration without rewriting source preferences.
- Same-shape focus and surface geometry; existing hit tests remain in place.
- Square paper inside a rounded sticker card, with unchanged physical preview dimensions.
- Enabled angle slider in Balanced mode and actual main-application cast-shadow changes.
- Directional edge/corner pixel changes and preserved center fill.
- Separate full-page palette/texture resources, plus a Solid option.

The Windows build helper also emits `empty-batch-panel.png`, `clay-light-east.png` and `clay-light-west.png` under ignored `artifacts/visual-review/`, alongside the existing application/studio/DPI snapshots. These are **native WPF renders**, generated when you run the tests on Windows; no screenshots were generated in the Linux sandbox.

Validation here is limited to C# syntax, XML/JSON structure, resource references, static layout checks and numerical preset contrast checks. **No .NET compilation, WPF execution or visual acceptance has been performed in this environment.** Run `Build-and-Run.cmd`, inspect the new images, and verify empty/populated tables and mouse/keyboard states on Windows. The solution, SDK resolution policy and print renderer are unchanged by this repair.
