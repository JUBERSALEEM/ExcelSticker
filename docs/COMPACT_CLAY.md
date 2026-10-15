# Compact clay workspace — v0.6.0

> **Superseded material/shape behavior in v0.7.0:** Directional inset-mask relief is now the default; the angle slider stays enabled in both modes; radii are bounded to avoid capsules; full-page wash and square table/paper frames were added. Follow [Rectangular clay](RECTANGULAR_CLAY.md) for the current workflow. The notes below record the v0.6.0 implementation.

## Reference interpretation and research

The six supplied reference images share a material language: opaque lavender/mint/peach/blue panels, a colored center, rounded corners, contained inner highlights/shades, and a soft outer shadow. Their charts, shopping products, music navigation and oversized showcase buttons are not features of this label maker. The application keeps its Compose → Import → Preview → Print workflow instead of copying an unrelated dashboard.

The published CSS recipes describe rounded pastel surfaces with inner and outer shadows. Albert Walicki shows a tinted outer shadow plus inset shade; OpenReplay demonstrates opposing inset highlight/shade layers and stresses hierarchy/contrast. [4](https://albertwalicki.com/blog/how-to-create-claymorphism-using-css) [3](https://blog.openreplay.com/implementing-claymorphism-with-css/)

These are visual recipes, **not one standardized “exact claymorphism pattern.”** WPF has no CSS `box-shadow: inset` equivalent. This engine uses cached vector perimeter bands with smooth falloffs plus a decorative-only outer effect. It is a WPF approximation, not pixel-identical CSS or a verified replica of a supplied image. We do not adopt the articles' broad claims that claymorphism automatically solves accessibility.

## Material changes

- **Balanced lighting is the default.** Complete perimeter bands have the same thickness and shading on all sides. There is no left-to-right color wash across the card center.
- **Fill-tinted relief.** Highlights blend toward white from the surface color; shade blends toward the theme shadow color, rather than drawing a black/white bevel across the whole surface.
- **Circular corners.** True quarter-circle arcs replace quadratic approximations. Adjacent radii normalize by one shared scale when necessary; inner bands use the normalized outer shape before insetting.
- **Linked corners.** Top left controls all four radii while Link all four corners is on. Turn it off to restore independent controls. The other three sliders are disabled while linked.
- **Raised versus inset.** Main cards use a raised profile. Outer/inner glass panels use the new `InnerPlate` template with reversed relief and no heavy outer shadow. Text boxes use smaller inset wells.
- **Separate elevation tiers.** Cards retain a soft shadow; buttons, dropdowns and small controls use a smaller shadow. Text remains outside effect-bearing visuals.
- **Directional lighting is optional.** It biases the perimeter shading; it does not replace the colored center. The angle slider is disabled in Balanced mode. Balanced inner relief is symmetric; its outer shadow still has a downward grounding offset.
- **Pastel roles.** Lavender marks Compose, peach marks Batch, blue marks Preview, mint marks Print. Nested panes use mint/blue. Dark themes receive restrained tints; Reduced effects removes the tints and relief.
- **Contrast checks include all four panel fills.** The rim report now includes opacity; it no longer reports the contrast of an opaque border that is actually drawn translucent. Soft rims are decorative, not a promise of 3:1 functional boundaries. Keyboard outlines remain solid; High contrast uses full-opacity borders.
- **Cache invalidation.** Material drawings are cached per size and invalidated by material property changes. Borrowed brushes are cloned before freezing the drawing. Updating a fill must not leave a stale surface.

## Compact sizing

Density is shared across the application instead of being implemented as a `ScaleTransform` on the window. Typography stays at its configured size and the label remains 100 × 70 mm.

| Metric | Previous default | New fresh-theme Compact default |
| --- | ---: | ---: |
| Card padding | 20 DIP | 12.8 DIP |
| Control vertical padding | 10 DIP | 5.6 DIP |
| Body type | 14 DIP | 13 DIP |
| Secondary type | 12 DIP | 12 DIP |
| Main section headings | 18 DIP | 16 DIP |
| Two-line batch row | 60 DIP | 49 DIP |
| Card radius | 24 DIP | 20 DIP, linked |
| Shadow blur / offset | 20 / 5 DIP | 14 / 3 DIP |
| Initial application window | 1460 × 940 DIP | 1360 × 860 DIP |

**Compact / Comfortable / Spacious** use spacing multipliers 0.8 / 1 / 1.15. Changing density does not multiply fonts. The new fresh-theme font defaults are listed separately above; saved font choices are preserved.

Shared resources control card/plate padding, button/field spacing, workspace margins, table headers and row heights. Local oversized padding overrides were removed from the main cards and editor/import cards. The native-size preview column reserves room for its paper, padding and scrollbar rather than shrinking the paper. Studio inspectors/action bars deliberately keep stable compact metrics so a density experiment cannot make Save unreachable. Native Windows file/printer dialogs remain OS-controlled.

Sticker Studio initially uses 150% **editor zoom** in its smaller window; the user can still choose 100/150/200/300%. This does not change stored element dimensions or printing.

## Apply the reference-inspired look

1. Update and build v0.6.0 using `Build-and-Run.cmd`.
2. Open **Theme Studio → Presets → Pastel clay** for coral actions and the reference-inspired palette.
3. Choose **Density: Compact**, **EdgeLighting: Balanced**, and **Link all four corners**.
4. Adjust **Pastel surface strength**, **Inset feathering**, **Inner highlight**, **Inner shade**, and **Decorative rim opacity** as needed. Dots/Grid are optional *canvas textures*, separate from the clay edge treatment.
5. Inspect the actual application, then **Save theme**.

Existing preference files are not deleted or automatically rewritten. Missing new density/lighting fields adopt Compact/Balanced defaults. Legacy files keep their palette and independent-corner intent (pastel roles/linking are initially off for those files); select the new preset if you want the complete reference palette. Saving writes the new options into the existing version-1 JSON schema. Cancel still restores the opening/last-saved theme.

## Verification and visual review

Added Windows checks cover:

- Effective padding/row-height changes, unchanged font scaling and physical preview dimensions.
- Linked/unlinked corners, proportional circular geometry, and reduced-effects plate/control resources.
- Legacy JSON behavior without rewriting settings.
- All preset text contrast against the new role fills; opacity-aware rim reporting.
- **Actual WPF raster samples** comparing left/right/top/bottom edges and opposite corners at 96/144/192 DPI, on square and wide cards.
- Preserved center fill, live cache invalidation, and the actual outer-glass plate template.

`Build-and-Run.cmd` sets a test-only output path and runs these checks along with the existing save/reload, import, and printing-layout tests. It writes native WPF review PNGs under ignored **`artifacts/visual-review/`**:

- `application-compact.png`
- `theme-studio-compact.png`
- `balanced-clay-96dpi.png`, `balanced-clay-144dpi.png`, `balanced-clay-192dpi.png`

These are produced by the Windows application/tests, not generated illustrations or browser mockups. The test temporarily previews Pastel clay and reverts it afterward. It uses an isolated settings directory, not the user's saved preferences. Direct test runs can opt in by setting `OCEANGLASS_VISUAL_REVIEW` to a writable folder.

**Executed here:** C# syntax parsing, XML parsing, Git whitespace checks, and an independent numerical check of preset text contrast. **Not executed here:** .NET compilation, the WPF suite/raster assertions, actual screenshot capture, or visual sign-off on Windows. This Linux sandbox has no .NET SDK/WPF runtime. Review the generated PNGs and test mouse/keyboard states and monitor scaling on Windows before considering the appearance verified. DPI raster samples alone are not a substitute for actual multi-monitor interaction or accessibility testing.
