# LABLE STUDIO

A native **C# / WPF Windows application** for Zebra glass labels, with a normal industrial flat default, optional clay themes and Theme Studio, drag-and-drop Sticker Studio, Excel/CSV imports, outer/inner glass entries, numbered copies, direct ZT411 printing and Windows-driver compatibility. No browser, Excel installation, or third-party application packages are required.

## Direct Print sticker + printer discovery (v0.17.0)

1. Choose the named printer above the live sticker. **Refresh printers** lists installed Windows USB/network queues. The selected name is also shown in the main toolbar.
2. Click **Print sticker** (toolbar or live card), or **Ctrl+P**. It sends exactly one displayed sticker, including its current copy number. **No prepared print-preview page is required.**
3. **Print all (N)** sends the current batch directly. The old paginated preview is optional, for inspecting batches/ranges only.

**Scan local network** finds possible private-LAN port-9100 devices; it sends no print data. **Check & use IP** verifies a ZT411's model, head resolution, language and ID before selecting it. Each direct network print rechecks identity on its submission connection. Other models and USB devices use installed Windows printers. Missing/offline/unknown devices are never silently replaced with another printer. This is an in-app service using Windows/.NET APIs, not a third-party print page or a separately installed Windows service.

See [printer service, connection setup and acceptance tests](docs/PRINTER_SERVICE.md). **Windows compilation, real discovery and physical printing remain unverified here.** No claim of physical success is made from a spool/network “Sent” message.

## Live sample → print preview without entry (v0.16.0)

- **Preview batch (optional)** accepts the selected live sample directly. No manual entry, customer/order defaults or Excel import is needed for a sample job.
- SGU sample TYPE: **6mm HD Grey Glass**. DGU sample TYPE: **6mm HD Grey Glass + 12mm ASP + 6mm Clear Glass**. No explanatory “Thickness”, “Color” or “Airspacer” text is added.
- Sample **glass dimensions are 300 × 300 mm**, including both DGU panes. Physical label paper is unchanged (default **100 × 70 mm**); 300 mm is not a ZT411 paper-width request.
- The selected sample format updates both live and open job previews. Live pane/copy navigation selects the matching job page, rather than restarting at the first outer sticker. Sticker Studio uses the same sample definitions.
- **SAMPLE** jobs can be test printed with current/all/range and remain separate from cutting data and production label selection. SGU retains four sample copies; DGU retains four outer/two inner copies to demonstrate numbering. With Qty expansion off, each pane has one page.
- Manual-entry/pane overrides and Excel/CSV tools belong to the cutting area, not the sticker section. Cutting-only scope filters are hidden while using samples. Real jobs retain validation and stale-snapshot safeguards.

See [sample and production preview workflow](docs/PRINT_PREVIEW.md). **Static checks are not a Windows build or physical printer test; both remain required.**

## Simple Print current / Print all and ZT411 sizing (v0.15.0)

- **Print current** sends the displayed sticker; **Print all (N)** sends the prepared batch. No repeated warning/confirmation screen.
- Choose a printer in the visible selector; it is remembered. Connection setup is separate. Status says what is printing. Technical diagnostics are under collapsed **Advanced**.
- Recognized **ZT411** drivers (including the reported **203dpi EPL** driver) use direct ZPL output sized from the layout, bypassing the old **104 × 50.8 mm** driver preset. **100 × 70 mm = 800 × 560 dots** at 203 dpi. The printer must accept ZPL and have matching stock loaded/calibrated.
- Supports the ZT411's 8/12/24 dots/mm grids; rejects widths beyond **104 mm**, without shrinking. Logos and original row/copy numbers are retained by the shared renderer and printer rasterizer.
- Other printers retain Windows-driver output; an Advanced Windows compatibility option is available. Stale/sample protections remain. Cancellation after submission cannot undo labels already printed.

See [simple printing](docs/PRINT_PREVIEW.md) and [ZT411 specifications, implementation and acceptance checks](docs/ZT411_PRINTING.md). **Windows build/runtime and physical printing are not verified in this sandbox.**

## Preview and adjustment access improvements (v0.14.1)

- **Print preview** is now directly on the cutting-page toolbar. With an empty list it prepares the marked sample job instead of an error; with a selected real row it prepares the cutting job. As of v0.17.0, **Ctrl+P prints the live sticker directly** instead.
- **Logo / sticker layout** is also on that toolbar. The live card has **Import company logo…**, which opens Sticker Studio and its local image picker directly.
- Both the print-job preview and Sticker Studio default to **Fit sticker**. Large/custom paper stays fully visible; fixed 100–300% zoom remains available and never changes printed size.
- **Apply position / size** commits X, Y, Width and Height together. Invalid rectangles stay editable without partially moving the field. Save/export includes pending valid bounds and refuses to silently ignore unapplied paper dimensions.

Current/all/range printing, SGU/DGU samples, embedded logo move/resize/rotation, custom stock and stale-job safeguards remain in place. See [the full workflow](docs/LOGOS_AND_STOCK.md). **Windows execution and physical printer verification are still required.**

## Adjustable sticker size and imported logos (v0.14.0)

Open **Logo / sticker layout** on the cutting-page toolbar (also **Stickers / printing → Adjust size / move fields…**) directly from the live sticker card.

- Change physical **width / height in mm** (20–300; default 100 × 70), with optional field-box scaling. All formats share one stock size, and preview/paginator/driver review use it consistently.
- **Import your logo** from PNG, JPEG or BMP, into the active format or all three. It is embedded in layout JSON and rendered in white-backed grayscale—no dependency on its original file location.
- Drag to move, use a visible resize handle, enter exact X/Y/width/height, or use visible arrow buttons. Logos support aspect preservation, 90° rotation, alignment, hide/lock, duplicate/delete and undo/redo.
- The previous **actual paginated preview**, **current/all/range printing**, original copy numbering and default **SGU/DGU samples** remain available. Saved size/logo edits invalidate the old job so it cannot be printed accidentally.
- No company logo is invented: import yours once. The live card clearly identifies when none is configured.

See [logo/stock workflow, research and verification limits](docs/LOGOS_AND_STOCK.md). **Windows build, UI/image-codec tests and physical printer verification remain required.**

## Actual print-job preview and sample stickers (introduced in v0.13.0)

- **Stickers / printing → Preview batch (optional)** prepares an inline, paginated snapshot from the selected live sample or saved cutting rows. It does not send a print job.
- Browse the actual vector pages at the saved stock size (default **100 × 70 mm**) in spool order, including SGU/DGU/Job Order formats, outer/inner panes and expanded quantity copies. Navigate, jump to a page, or zoom the screen without scaling the output.
- **Print current sticker** sends one physical preview page. **Print all preview pages** sends the prepared scope. **Print range** uses inclusive physical page numbers. Original row/copy numbers are preserved—even if the chosen page is `7/4`.
- Choose **Print current**, **Print all** or **Print range**. Use the visible printer selector; it is remembered. Printer setup focuses that selector. Diagnostics/guides are optional under **Advanced** (simplified in v0.15.0).
- Changing cutting data, metadata, layout, scope, pane filters or quantity mode marks the snapshot **OUT OF DATE** and prevents sending until refreshed. Direct ZT411 jobs prepare the selected pages before submission and can be cancelled. No final warning confirmation is required.
- The live card starts with a marked **SGU sample** instead of blank paper. Choose **DGU example** and outer/inner to inspect both panes. Samples never enter the cutting sheet or production label selection; v0.16.0 permits explicitly marked standalone sample/test jobs. Selecting a real row resumes real live preview; incomplete real rows remain visibly invalid.

See [Print preview, driver research and Windows acceptance checks](docs/PRINT_PREVIEW.md). Preview matches the app's submitted page content and geometry; it cannot guarantee driver/feed calibration or physical completion. **Compilation, executable WPF tests and physical printer checks still need Windows verification.**

## Ready-to-edit cutting sheet and Excel save (v0.12.0)

- Home and each new specification start with **five blank editable rows**. Unused rows are ignored by totals and printing; partially filled rows remain flagged.
- **Single-click an input cell to edit and select its value.** Later clicks can position the caret. Tab navigation and Shift+click range selection remain available.
- Cells use a subtle selection tint, a thin focus border and a neutral editor—not the full Windows blue selection box.
- **Save Excel / Ctrl+S** saves a genuine formatted `.xlsx`: job heading, specification sections, the eleven cutting columns, input cells, area/price formulas, group/grand totals and landscape print settings.
- **Import Excel / CSV** recognizes the saved workbook and restores the list, including blanks, drafts, prices and per-pane metadata. Saved-workbook opening replaces the current list after confirmation; legacy flat-file imports still append.
- **Blank Excel template** saves the same clean page with five starter rows. Advanced CSV export is still available under **Stickers / printing**.

The extra label metadata is kept in hidden workbook storage, not extra visible table columns. Keep that storage and the original column arrangement intact for complete round trips. See [Cutting workspace](docs/CUTTING_WORKSPACE.md) for details and verification limits. No .NET/Windows/Excel execution was available in this sandbox; run `Build-and-Run.cmd` and test one workbook before production use.

## Glass serial and sticker QTY (v0.11.1)

Stickers now include **SR NO** from the cutting row, separately from Glass Ref and Order No. QTY uses **glass Sr No / copy**: row 1 with Qty 4 prints **1/1, 1/2, 1/3, 1/4**; row 7 with Qty 2 prints **7/1, 7/2**, including selected-only printing. Preview and printing use the same rule. The total-only and copy/total choices have been removed from the app.

All three stock formats include the new serial field. Old layout files upgrade in memory without being overwritten: legacy TotalQty fields become numbered QTY, and a serial is added where it fits. Dense custom layouts show a missing-serial warning rather than overwriting other fields; use **Stickers / printing → Sticker layout editor → GlassSrNo**, or reset the format, then **Save all formats**. The sticker remains 100×70 mm. Windows build/runtime verification is still required.

## Cutting-first workspace (v0.11.0)

The main page now shows specification-grouped cutting tables: **Sr No · Glass Ref · W1 · H1 · W2 · H2 · Qty · SQM · Total SQM · Price · Total Price**. Add rows/specifications, edit cells, copy with Ctrl+C, paste Excel blocks with Ctrl+V, and export cutting CSV with pricing and metadata.

Use **Stickers / printing** to open the inline expandable section for the live preview, printer settings and layout editor. It is collapsed by default—not a popup. Industrial flat remains the default appearance.

Rates are **AED/m²**. Unit SQM is rounded to three decimals before multiplying by quantity, matching the supplied reference; invalid rows remain visible and block printing. See [Cutting workspace usage, formulas and verification](docs/CUTTING_WORKSPACE.md), and try `examples/cutting-reference.csv`.

## Factory-ready default and application icon (v0.10.0)

The application is now **LABLE STUDIO** (`LableStudio.exe`), with a navy/teal label icon embedded in the executable and displayed in the window and header.

**New installations start in Industrial flat:** neutral white panels, a solid gray workspace, blue actions, crisp borders and no clay shadows, gloss or background patterns. Theme Studio is optional. Existing saved themes/layouts are retained; to return to normal styling, choose **Theme Studio → Use factory flat theme → Save theme**.

The configured company printed on stickers is unchanged. See [Branding, factory defaults and verification](docs/BRANDING_AND_FACTORY.md).

## Six reference styles (v0.9.0)

**Clayhub · Puffy studio · Daily progress · Clayboard · Clayshop · Clayplay** are now complete editable recipes in Theme Studio, alongside Coral sculpted and the existing presets.

Open **Theme Studio → Six reference styles → select a tile → Save theme**. With Live preview enabled, the actual application changes—not just a sample card. The recipes include six editable surface colors, per-surface tinted bevels/shadows, raised or recessed inner plates, and their own gloss, corners and spacing. Sticker paper remains square and monochrome.

See [Reference style pack, mappings and verification](docs/REFERENCE_STYLES.md). Native Windows compilation and visual verification are still required; no Windows screenshots are claimed from this sandbox.

## Sculpted clay engine overhaul (v0.8.0)

The material renderer now uses **curved-surface normals, directional diffuse light and specular sheen** instead of translated inset masks. Corners have a rolled shoulder, fill-tinted underside and separate soft ambient/cast shadows. Text remains outside effects; sticker paper and the batch table remain square.

Use **Theme Studio → Apply coral 3D reference to entire app → Save theme**. New controls cover card/control bevel width, fullness, light elevation, sheen/focus and ambient lift. Larger laboratory specimens and the actual application use the same renderer. Existing saved themes are retained until you explicitly change/save them.

See [Sculpted clay analysis, architecture and verification](docs/SCULPTED_CLAY.md). The implementation has static checks, but still requires a Windows build, regression run and comparison against the supplied screenshot; no native visual result has been verified in this sandbox.

## Rectangular layout and curved clay repair (v0.7.0)

The empty batch now stays a rectangular table instead of collapsing into a capsule. Small controls no longer become circles; oversized saved radii are capped safely. The sticker is a square white sheet inside its separately rounded card. The angle slider works in both modes. Its original inset-mask material is superseded by v0.8.0 above. A full-page **Pastel wash** completes the palette.

For the earlier palette, select **Theme Studio → Pastel clay → Save theme**. The preset selects Compact, Directional lighting and Pastel wash; **135° is editable**, not locked. See [Rectangular clay repair](docs/RECTANGULAR_CLAY.md) for the source diagnosis, shape policy, screenshot checks and Windows verification limits.

## SDK resolution fix (v0.6.1)

If Visual Studio reports **NETSDK1141** with an installed SDK such as **10.0.400**, update this checkout. `global.json` now requests **8.0.100 or a compatible newer stable SDK** using `rollForward: major`, rather than requiring the 8.0.4xx feature band. The build helper accepts stable SDKs 8 and newer too. An installed .NET 8 SDK is preferred; otherwise selection can advance to SDK 9, 10, or later.

The projects still target **net8.0 / net8.0-windows**. Running the app/tests requires **.NET 8 Windows Desktop Runtime**, even when compiling with SDK 10. Use a Visual Studio release compatible with the SDK selected on your machine. See [SDK recovery](docs/UPDATE_AND_BUILD.md#netsdk1141-sdk-resolution-error).

## Compact, reference-inspired clay (v0.6.0)

The workspace now uses smaller cards/controls, colored pastel panels, real inset outer/inner plates, circular linked corners, and **Balanced** four-edge relief instead of a card-wide white gradient. Select **Theme Studio → Pastel clay → Compact → Directional**, then **Save theme** for the full reference-inspired look. Existing saved palettes are not silently replaced.

Density changes spacing throughout the workspace, not the physical 100 × 70 mm sticker. Windows builds now generate native review images in `artifacts/visual-review/`. See [Compact clay implementation and verification](docs/COMPACT_CLAY.md) for the source research, sizing table, migration behavior, new tests, and Windows verification limits.

## Theme and toolchain repair (v0.5.0)

- **Theme Studio is modeless:** live changes can be inspected in the main app, whose header distinguishes unsaved preview from saved/active theme.
- **Save is explicit:** validates immediately, reports write failures and the path, persists without closing, and makes subsequent Cancel/Close restore the most recent save.
- **Control states corrected:** chrome defaults no longer override template triggers; hover/pressed/disabled/focus states and input depth use proper WPF precedence.
- **Build setup (updated in v0.6.1):** `global.json` prefers .NET SDK 8 and allows newer stable major versions; `.vsconfig` declares the desktop workload. Use an IDE compatible with the selected SDK and the canonical `.sln`. The .NET 8 desktop runtime is separately required to run the app/tests.

See [Theme and solution review](docs/THEME_AND_SOLUTION_REVIEW.md) for findings, behavior, setup, verification limits, and the required Windows acceptance checks. Different major Visual Studio versions can still update their own version metadata; no file changes are hidden or automatically discarded.

## Fix for build/import/update confusion (v0.4.1)

The `CS0029` sticker-editor error is fixed (`BorderThickness = new Thickness(1)`). Clay controls now have hit-testable padding/thumb surfaces. The app displays its version/commit and provides a diagnostics badge so an old EXE is distinguishable from the latest build.

**Do not re-clone for every update.** Follow [Update and build recovery](docs/UPDATE_AND_BUILD.md), then run **Build-and-Run.cmd**. It rebuilds the explicitly named `ExcelSticker.sln`, runs regression checks, and only launches after success. Never choose Visual Studio's **Launch old version** following a build error.

Import now includes a source/header/range/column-mapping preview, explicit metadata fallbacks, and validation before any batch mutation. Standard/Strict `.xlsx`, UTF-8 comma/semicolon CSV, and TSV are supported. See the recovery guide for exact steps and diagnostics.

## Open and run on Windows

Requirements: Windows 10/11, a stable .NET SDK **8 or newer**, **Windows Desktop Runtime 8**, a Visual Studio version compatible with the selected SDK and the **.NET desktop development** workload, and the Zebra Windows driver for physical printing. For SDK 8.0.4xx use Visual Studio 2022 17.11+; newer SDKs may require newer Visual Studio releases.

1. Open `ExcelSticker.sln`.
2. In **Solution Explorer**, right-click **ExcelSticker.Wpf** → **Set as Startup Project**. Its name should be bold.
3. Press **F5**.

Or run from the repository root:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project src/ExcelSticker.Wpf
```

**Verification status:** WPF runs only on Windows. This development sandbox has no .NET SDK or Windows runtime. Static C#/XAML checks are possible here, but compilation, executable regression tests, desktop appearance, and physical printer output still need Windows verification. The workflow template is inactive until restored as described below.

## Theme Studio — separate claymorphism engine

Open **Theme Studio** in the header. The standalone `ExcelSticker.Theming` WPF library has no dependency on the label maker or its Core project.

- Industrial flat by default; optional palettes with HEX/RGB color controls: Porcelain, Pastel clay, Coral sculpted, Ocean, Mint, Lavender, Graphite, High contrast, and six reference-style presets.
- Four independent card corners, button/input radii, border line visibility/width/color.
- Light direction/elevation, rounded bevel width/fullness, tinted shade, specular sheen/focus and layered cast/ambient shadows.
- Card/control padding, font family and sizes, subtle vector speckles/grid patterns.
- Contrast report/guard, keyboard focus states, reduced effects.
- Save locally, import/export versioned JSON, reset to the opening theme, and cancel/revert.

**Text clarity changes:** decorative effects now live on chrome-only sibling visuals, not around text-bearing containers. UI layout rounding, display text metrics, pixel snapping, and a per-monitor DPI manifest are enabled. The main sticker preview no longer auto-shrinks through a Viewbox. Physical print text stays in its own monochrome renderer and is not affected by theme colors or UI fonts. These changes still need visual testing on Windows.

Read [the research and engine architecture](docs/CLAY_ENGINE.md). Shareable preset files are in `examples/themes/`.

## Sticker Editor — separate Normal, SGU, and DGU formats

Open **Logo / sticker layout** on the cutting-page toolbar (also **Stickers / printing → Adjust size / move fields…**):

- Drag fields and resize with a corner handle; edit exact mm position/size.
- Set point size, font, alignment, wrapping, bold/underline, prefix, and custom text.
- Import a company logo, change paper dimensions, or add data fields, divider lines, and outline boxes; duplicate, hide, lock, delete, and reorder.
- Rulers, safe-area guide, 0.5/1/2 mm snapping, zoom, keyboard nudging, alignment commands, and undo/redo.
- Separate **NormalJobOrder**, **SGU**, and **DGU** samples/layout slots. DGU samples include outer and inner views.
- Import/export a single format or the full library; Save all formats applies the same layout to preview and print.

Select **Automatic · SGU / DGU** in the main print panel, or override with Normal job order / SGU / DGU. Samples are in `examples/layouts/`. See [Sticker Studio usage and limits](docs/STICKER_STUDIO.md).

## Numbered quantity copies

**Qty copies are ON by default.** Fixed **Glass Sr No / copy** numbering prints Qty 4 as `1/1`, `1/2`, `1/3`, `1/4` for entry 1. Entry 2 starts `2/1`. DGU outer and inner each get their own sequence, with the pane marker identifying them.

Every stock sticker separately shows **SR NO**. QTY contains only the glass serial/copy pair, not a repeated total or copy/total. The underlying quantity is unchanged. Preview arrows switch between numbered copies. The serial uses the current cutting row position, not a permanent job identifier.

If Qty copies is turned off, only one sticker per chosen pane is printed even when numbering is selected. Set driver Copies to **1** to avoid repeating the entire numbered job.

Theme and layout preferences persist separately in `%LOCALAPPDATA%\OceanGlass\theme.json` and `sticker-layouts.json`. **Cutting data is not autosaved. Use Save Excel to save/reopen it; advanced CSV export is also available.**

## Outer and inner glass

Each batch entry represents one source row, with either a single-glass sticker or an **outer + inner** pair.

| Field | Meaning |
|---|---|
| W1 / H1 | Outer glass width / height in millimeters |
| Outer Qty | Quantity printed on the outer label |
| W2 / H2 | Inner glass width / height in millimeters |
| Inner Qty | Quantity printed on the inner label |
| Spec | `outer glass + spacer + inner glass` |
| Glass overrides | Optional explicit outer/inner material names for complex specs |

**Your source-data example** (quantity totals; printed Qty text follows the selected numbering mode):

```text
Spec: 6MM HD GREY FT GLASS + 12MM ASP + 6MM CLEAR GLASS
W1: 2550     H1: 2650     Qty: 4

OUTER → 6MM HD GREY FT GLASS   2550.0 × 2650.0 mm   QTY: 4
INNER → 6MM CLEAR GLASS       2550.0 × 2650.0 mm   QTY: 4
```

- Leave **both** W2 and H2 blank to reuse W1 and H1. If either is supplied, both are required.
- Blank Inner Qty uses Outer Qty. Set it separately when quantities differ.
- The spacer (such as `12MM ASP`) does **not** receive a glass sticker.
- Standard double-glass specs must contain exactly three nonempty `+`-separated parts. For laminated/complex specs, supply **both** material overrides instead of relying on automatic splitting. This avoids silently dropping intermediate glass layers.
- Turn off **Outer + inner glass** for a single-glass manual entry and enter its material in Spec.
- Each double-glass sticker includes an explicit **OUTER** or **INNER** marker. Single-glass labels have no pane marker.
- Sample form values use the example above; the batch starts empty.

## Live sticker preview and editing

- Typing in the form updates the preview automatically. Incomplete/invalid input clears the sticker and shows an inline error instead of leaving stale values visible.
- Import Excel or CSV, then **click any batch row**: the preview immediately switches to that saved entry. The source filename/worksheet and order are shown above the sticker.
- Choose **Outer glass** or **Inner glass** in the preview selector. This selection follows subsequent row clicks; single-glass rows disable Inner and switch to Outer automatically.
- Selecting a row previews it; it does **not** overwrite the manual form. Choose **Edit** to load the entry into the form, then **Save changes** to update the batch.
- **Preview form** switches from the selected-row preview back to the live form. Unsaved form values are never silently printed.
- Add, edit, cancel edit, remove, and clear are supported. Identical imported rows remain independently editable.
- Cutting data is **not autosaved**. **Save Excel** retains the list; **Import Excel / CSV** reopens the formatted workbook. Closing with data still prompts for confirmation, even after saving.

## Excel and CSV import

Use **Import Excel / CSV**. Formatted LABLE STUDIO cutting workbooks restore their grouped page and defaults directly, after a replacement confirmation when there is current work. Other `.xlsx` files are read directly, without installing Excel. Workbooks with multiple worksheets show a sheet picker. Choose the header in the import preview; it is suggested from the first 50 parsed rows. Review column mapping and the last included row, validate, then confirm Import. The import appends entries, validates the complete file first, and adds nothing if validation fails.

Try these included files:

- `examples/outer-inner-labels.xlsx`: two worksheets, including your double-glass example and a row with different inner dimensions/quantity.
- `examples/outer-inner-labels.csv`: the same double-glass examples in CSV.
- `examples/sample-labels.csv`: the original single-glass template, still supported.

For older flat-file imports, the following CSV headings remain supported. The current **Blank Excel template** button instead creates the formatted cutting page:

```csv
OrderNo,Spec,W1,H1,Qty,W2,H2,OuterQty,InnerQty,ReferenceNo,CustomerName,JobNo,OuterGlass,InnerGlass
7494/71,6MM HD GREY FT GLASS + 12MM ASP + 6MM CLEAR GLASS,2550,2650,4,,,,,W6* F5,FUJSENG,,,
7494/72,6MM HD GREY FT GLASS + 12MM ASP + 6MM CLEAR GLASS,2550,2650,4,2540,2640,,2,W7,FUJSENG,J-100,,
```

### Column rules

- Required headings: **OrderNo, Spec, W1, H1, CustomerName**, plus **Qty or OuterQty**. Required values must be populated.
- **OuterQty** overrides Qty when provided. **InnerQty** overrides the resulting outer quantity when provided.
- ReferenceNo, JobNo, W2, H2, InnerQty, OuterGlass, and InnerGlass are optional columns/values.
- A row is treated as outer/inner when Spec contains `+`, or any inner field (W2, H2, InnerQty, InnerGlass) is filled. Otherwise it is single glass.
- Column order is flexible. Headings ignore capitalization, spaces, and punctuation. Common aliases are supported: `GlassType`/`Specification` → Spec; `Width`/`Width (mm)` → W1; `Height`/`Height (mm)` → H1; `Qty1`/`QtyOuter` → OuterQty; `Qty2`/`QtyInner` → InnerQty; `Order`, `Customer`, `Ref`, and `Job` are also accepted.
- Duplicate headings/aliases are rejected, so do not include both `Width` and `W1`. Extra named columns are ignored.
- The import preview detects comma, semicolon, or tab delimiters. Dot decimals are the default; explicitly enable comma decimals for applicable files. Thousands separators are not supported. Standard quoted commas, escaped quotes, UTF-8 BOM, and Unicode are supported. Text fields cannot contain line breaks.

### Excel notes and limits

- Standard and Strict Open XML `.xlsx` are supported. Convert legacy `.xls`, `.xlsb`, `.xlsm`, or encrypted workbooks to an unencrypted `.xlsx` file or UTF-8 CSV first.
- Format identifiers (order/reference/job) as **Text** to preserve leading zeros and avoid Excel date conversion. Import reads underlying values, not number-format masks. Numeric display formats such as `00000` do not add leading zeros on import.
- Formulas are **not executed**. Saved/cached results are read; missing results or Excel error cells are rejected. Recalculate/save in Excel, or paste values, before importing. Stale formula caches cannot be detected.
- Title rows are supported via header selection. Use a rectangular data table; merged/duplicate headings may require explicit mapping. Hidden/filtered rows are still imported. Select the worksheet you want in the picker.
- 10 MB input file; XLSX uncompressed parts at most 64 MB; at most 256 columns; 10,000 data rows/entries; 10,000 pages per print job. XLSX blank rows after the header count toward the worksheet row limit.
- Qty is 1–10,000. Dimensions must be greater than zero and at most 100,000 mm. Dimensions display to one decimal place. Optional auto-fit stops at 6 pt and reports text that still does not fit; enlarge or wrap such fields before printing.

## Printing and the driver-stock warning

1. Install the matching printer driver, load matching stock and calibrate the printer. Default layout: **100 × 70 mm**.
2. Under **Stickers / printing**, choose the scope, panes, format and Qty expansion. Print directly, or click **Print preview** for optional batch inspection. **Ctrl+P** prints the live sticker.
3. **Print current** sends the displayed sticker. **Print all (N)** sends that prepared batch; **Print range** uses inclusive page numbers. Original row/copy numbers are preserved.
4. Choose the printer in the visible selector; it is remembered. **Refresh printers** discovers installed USB/network queues, without spooling. There is no repeated warning confirmation.

Recognized ZT411 drivers use **direct ZPL**, including the reported 203 dpi EPL queue when the device accepts ZPL. Width/height come from the saved layout, not the driver's old paper preset. The app uses native 8/12/24 dots/mm, checks the **104 mm** print-width limit and renders the same logo/text raster shown in preview. Other printers use Windows WPF printing; known incompatible settings get a specific correction. Technical details and Windows compatibility mode are under **Advanced**.

The loaded roll, printer language and media calibration must still match the job. “Sent” means spool submission, not verified physical completion. Cancellation cannot undo stickers already printed; failed jobs are not automatically retried. See [workflow](docs/PRINT_PREVIEW.md) and [ZT411 research/limits](docs/ZT411_PRINTING.md). Printed entries remain in the cutting list; changed jobs require Refresh preview.

## Company details

Edit `src/ExcelSticker.Wpf/company.json` before building, or `company.json` beside the published executable, then restart. It controls company name, address, phone, website, and email. **Verify the sample details, especially the telephone number, before production printing**; they were transcribed from a small screenshot. Invalid JSON produces a warning and falls back to sample values. Label data is not sent to an external service.

## Troubleshooting the Visual Studio class-library message

“A project with an Output Type of Class Library cannot be started directly” is a **startup selection error**, not a compiler error. `ExcelSticker.Core` intentionally remains a library; **ExcelSticker.Wpf** is the executable (`OutputType=WinExe`, `UseWPF=true`).

Open `ExcelSticker.sln`, then **Solution Explorer → right-click ExcelSticker.Wpf → Set as Startup Project → F5**. Or use **solution Properties → Common Properties → Startup Project → Single startup project → ExcelSticker.Wpf**. Do not select a test runner or change Core into an executable.

The WPF project is first in the solution for fresh sessions, but existing Visual Studio settings may retain an old selection after pulling. To bypass that selection:

```powershell
dotnet run --project src/ExcelSticker.Wpf/ExcelSticker.Wpf.csproj
```

## Create a distributable Windows application

```powershell
dotnet publish src/ExcelSticker.Wpf -c Release -r win-x64 --self-contained true -o artifacts/LableStudio
```

Copy the **entire output folder** to a Windows x64 machine and run `LableStudio.exe`. This portable self-contained build does not need a separate .NET installation. It is not an MSI installer.

### Activate the GitHub Actions workflow

`workflow-templates/windows-build.yml` builds, runs checks, and packages the application. It is stored outside `.github/workflows/` because the current Arena GitHub connection lacks permission to modify active workflows. **The template is inactive in its current location.**

Move it into **`.github/workflows/` beneath the project root** (the folder containing `ExcelSticker.sln`), not directly beside the solution. From PowerShell in that folder:

```powershell
New-Item -ItemType Directory -Force -Path .github/workflows | Out-Null
git mv workflow-templates/windows-build.yml .github/workflows/windows-build.yml
git commit -m "Enable Windows build workflow"
git push origin arena/4ed12585-excelsticker
```

Push using your own GitHub connection with workflow permissions. The restricted Arena connection will still reject an active workflow. The final location is:

```text
ExcelSticker/
├── ExcelSticker.sln
└── .github/
    └── workflows/
        └── windows-build.yml
```

Open the repository's **Actions** tab and look for **Windows build and package**. It runs on pushes and pull requests; manual **Run workflow** becomes available when the workflow reaches the default branch. Restoring it does not merge this working branch into `main`. After a successful run, download `LableStudio-win-x64` and extract the complete artifact ZIP.

## Tests and manual checks

Dependency-free executable regression runners return a nonzero exit code on failure. Use `dotnet run`, not `dotnet test`:

```powershell
# Any OS with the .NET 8 SDK
dotnet run --project tests/ExcelSticker.Core.Tests -c Release

# Windows only; no print job is sent
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release
```

Core checks cover import/validation, per-entry/per-pane numbering, print-plan limits, layout schema validation, independent template copies, and JSON round trips. Windows checks also cover SGU/DGU demo isolation, current/all/range commands, stale-job guards, print-ticket scaling/copy safeguards, physical pagination, saved company/layout snapshots, preset contrast/resource updates, theme fallback, and keeping effects out of text ancestors. They do not replace manual drag/resize, high-DPI, or physical-printer tests.

Before production use on Windows:

1. Import each sheet in `examples/outer-inner-labels.xlsx`, click between rows, and switch Outer/Inner. Confirm material, dimensions, customer/order, and quantity.
2. Change W2/H2 and Inner Qty, preview, save, and verify the batch. Cancel an edit and confirm saved values remain unchanged.
3. Check print scopes/layers and page totals with Qty copies on/off.
4. Test first printer selection/cancel, remembered selection, current/all/range and cancellation before/during submission. Print one outer and one inner label on matching stock and measure the output.
5. Verify the clay workspace at 100%, 150%, and 200% display scaling, keyboard navigation, and readable long-text stickers.

## Layout

- `src/ExcelSticker.Core`: entry/label models, CSV/XLSX readers, numbered print plans, validated physical-layout data, and stock classification.
- `src/ExcelSticker.Theming`: standalone clay engine, shared styles, palette/contrast logic, Theme Studio, and RGB picker.
- `src/ExcelSticker.Wpf`: business UI, Sticker Studio, shared label renderer, printer integration, and per-monitor DPI manifest.
- `tests/`: core regression and Windows smoke runners.
- `examples/`: glass CSV/XLSX files, theme presets, and separate Normal/SGU/DGU layout JSON files.
- `docs/`: research rationale, engine API, and studio usage/limits.
- `workflow-templates/`: inactive Windows CI workflow; restore to `.github/workflows/` to activate.
