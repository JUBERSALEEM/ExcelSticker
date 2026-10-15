# Ocean Glass — Label Studio

A native **C# / WPF Windows application** for Zebra glass labels, with an independent claymorphism theme engine, Theme Studio, drag-and-drop Sticker Studio, Excel/CSV imports, outer/inner glass entries, numbered copies, and Windows-driver printing. No browser, Excel installation, or third-party application packages are required.

## Compact, reference-inspired clay (v0.6.0)

The workspace now uses smaller cards/controls, colored pastel panels, real inset outer/inner plates, circular linked corners, and **Balanced** four-edge relief instead of a card-wide white gradient. Select **Theme Studio → Pastel clay → Compact → Balanced**, then **Save theme** for the full reference-inspired look. Existing saved palettes are not silently replaced.

Density changes spacing throughout the workspace, not the physical 100 × 70 mm sticker. Windows builds now generate native review images in `artifacts/visual-review/`. See [Compact clay implementation and verification](docs/COMPACT_CLAY.md) for the source research, sizing table, migration behavior, new tests, and Windows verification limits.

## Theme and toolchain repair (v0.5.0)

- **Theme Studio is modeless:** live changes can be inspected in the main app, whose header distinguishes unsaved preview from saved/active theme.
- **Save is explicit:** validates immediately, reports write failures and the path, persists without closing, and makes subsequent Cancel/Close restore the most recent save.
- **Control states corrected:** chrome defaults no longer override template triggers; hover/pressed/disabled/focus states and input depth use proper WPF precedence.
- **Stable build setup:** `global.json` selects stable .NET SDK **8.0.4xx**; `.vsconfig` declares the desktop workload. Use **Visual Studio 2022 17.11+** and the canonical `.sln`. A newer major SDK alone is not sufficient.

See [Theme and solution review](docs/THEME_AND_SOLUTION_REVIEW.md) for findings, behavior, setup, verification limits, and the required Windows acceptance checks. Different major Visual Studio versions can still update their own version metadata; no file changes are hidden or automatically discarded.

## Fix for build/import/update confusion (v0.4.1)

The `CS0029` sticker-editor error is fixed (`BorderThickness = new Thickness(1)`). Clay controls now have hit-testable padding/thumb surfaces. The app displays its version/commit and provides a diagnostics badge so an old EXE is distinguishable from the latest build.

**Do not re-clone for every update.** Follow [Update and build recovery](docs/UPDATE_AND_BUILD.md), then run **Build-and-Run.cmd**. It rebuilds the explicitly named `ExcelSticker.sln`, runs regression checks, and only launches after success. Never choose Visual Studio's **Launch old version** following a build error.

Import now includes a source/header/range/column-mapping preview, explicit metadata fallbacks, and validation before any batch mutation. Standard/Strict `.xlsx`, UTF-8 comma/semicolon CSV, and TSV are supported. See the recovery guide for exact steps and diagnostics.

## Open and run on Windows

Requirements: Windows 10/11, stable .NET SDK 8.0.4xx (including Windows Desktop Runtime 8), Visual Studio 2022 (17.11 or newer) with **.NET desktop development**, and the Zebra Windows driver for physical printing.

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

- Live palettes with HEX/RGB color controls; Porcelain, Ocean, Mint, Lavender, Graphite, and High contrast presets.
- Four independent card corners, button/input radii, border line visibility/width/color.
- Light direction, outer shadow softness/elevation/opacity, feathered light/dark inset shading.
- Card/control padding, font family and sizes, subtle vector dots/grid patterns.
- Contrast report/guard, keyboard focus states, reduced effects.
- Save locally, import/export versioned JSON, reset to the opening theme, and cancel/revert.

**Text clarity changes:** decorative effects now live on chrome-only sibling visuals, not around text-bearing containers. UI layout rounding, display text metrics, pixel snapping, and a per-monitor DPI manifest are enabled. The main sticker preview no longer auto-shrinks through a Viewbox. Physical print text stays in its own monochrome renderer and is not affected by theme colors or UI fonts. These changes still need visual testing on Windows.

Read [the research and engine architecture](docs/CLAY_ENGINE.md). Shareable preset files are in `examples/themes/`.

## Sticker Editor — separate Normal, SGU, and DGU formats

Open **Sticker Editor** in the header:

- Drag fields and resize with a corner handle; edit exact mm position/size.
- Set point size, font, alignment, wrapping, bold/underline, prefix, and custom text.
- Add data fields, divider lines, and outline boxes; duplicate, hide, lock, delete, and reorder.
- Rulers, safe-area guide, 0.5/1/2 mm snapping, zoom, keyboard nudging, alignment commands, and undo/redo.
- Separate **NormalJobOrder**, **SGU**, and **DGU** samples/layout slots. DGU samples include outer and inner views.
- Import/export a single format or the full library; Save all formats applies the same layout to preview and print.

Select **Automatic · SGU / DGU** in the main print panel, or override with Normal job order / SGU / DGU. Samples are in `examples/layouts/`. See [Sticker Studio usage and limits](docs/STICKER_STUDIO.md).

## Numbered quantity copies

**Qty copies are now ON by default.** The default **Entry / copy** mode prints Qty 4 as `1/1`, `1/2`, `1/3`, `1/4` for entry 1. Entry 2 starts `2/1`. DGU outer and inner each get their own sequence, with the pane marker identifying them.

Also available: **Copy / total** (`1/4`, `2/4`, …) and **Total quantity only** (`4` on every copy). The underlying quantity is unchanged; add a `TotalQty` field if you want both values on the sticker. Preview arrows switch between numbered copies. The entry prefix uses the current batch row position, not a permanent job identifier.

If Qty copies is turned off, only one sticker per chosen pane is printed even when numbering is selected. Set driver Copies to **1** to avoid repeating the entire numbered job.

Theme and layout preferences persist separately in `%LOCALAPPDATA%\OceanGlass\theme.json` and `sticker-layouts.json`. **Batch entries remain in memory only.**

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
- Batch data is **in memory only**. Closing a populated batch prompts for confirmation. There is no save/reopen feature yet; keep your source workbook.

## Excel and CSV import

Use **Import Excel / CSV**. `.xlsx` files are read directly, without installing Excel. Workbooks with multiple worksheets show a sheet picker. Choose the header in the import preview; it is suggested from the first 50 parsed rows. Review column mapping and the last included row, validate, then confirm Import. The import appends entries, validates the complete file first, and adds nothing if validation fails.

Try these included files:

- `examples/outer-inner-labels.xlsx`: two worksheets, including your double-glass example and a row with different inner dimensions/quantity.
- `examples/outer-inner-labels.csv`: the same double-glass examples in CSV.
- `examples/sample-labels.csv`: the original single-glass template, still supported.

**Blank CSV template** downloads these headings; open it in Excel and save as `.xlsx` or **CSV UTF-8 (Comma delimited)**:

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

1. Install the Zebra ZT411 Windows driver, load the stock, and calibrate the printer.
2. Set a **100 mm wide × 70 mm high** custom stock in Windows **Printing Preferences**, with no fit-to-page scaling. Use your printer's correct DPI.
3. Choose **All batch entries** or **Selected entry only**.
4. Choose **Both outer + inner**, **Outer / single only**, or **Inner only**. Single-glass entries are skipped by Inner-only printing.
5. **Print Qty copies of each pane** starts on. Turn it off for a one-sticker-per-pane test. Choose the numbering mode and sticker format; page counts update immediately.
6. Set Windows driver **Copies = 1** so quantities are not multiplied twice.
7. Click **Print stickers**, select the queue, and check a test label before sending a large job.

For the 2550 × 2650, Qty 4 example: Both layers with copies **off** prints **2** stickers; with copies **on**, it prints **8** numbered stickers (4 outer + 4 inner). Choose Total quantity only if every sticker should show the raw quantity instead of a sequence. With OuterQty 4 and InnerQty 2, Both + Qty copies prints **6** stickers. To test one sticker, choose Selected entry, Outer only, and leave Qty copies off.

### “The driver did not confirm 100 × 70 mm”

Some Zebra drivers report a named stock, omit custom dimensions, or return rotated media dimensions. **An unverified size no longer automatically blocks printing.**

- Matching effective dimensions (including correctly rotated landscape stock) proceed normally.
- Otherwise, a **Printer stock compatibility** dialog displays the driver's reported dimensions/orientation.
- Choose **Cancel** to adjust Printing Preferences, or explicitly **Continue** if your stock is already configured and you want to send the job using the driver settings. Cancel is the default action.
- This is a compatibility override, not automatic calibration: incorrect stock/orientation/offsets can still clip, rotate, or misalign a label. Test one first.

Preview and printing share the same monochrome **100 × 70 mm** native layout. The app never scales the label to A4/Letter, nor adds clay shadows/colors to the print. App theme applies to the workspace and app-owned dialogs; Windows file/printer dialogs remain native. Printing uses the **Windows driver**, not raw ZPL. Spooler submission is not proof of physical completion. Printed entries remain in the batch.

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
dotnet publish src/ExcelSticker.Wpf -c Release -r win-x64 --self-contained true -o artifacts/OceanGlass-LabelMaker
```

Copy the **entire output folder** to a Windows x64 machine and run `OceanGlass.LabelMaker.exe`. This portable self-contained build does not need a separate .NET installation. It is not an MSI installer.

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

Open the repository's **Actions** tab and look for **Windows build and package**. It runs on pushes and pull requests; manual **Run workflow** becomes available when the workflow reaches the default branch. Restoring it does not merge this working branch into `main`. After a successful run, download `OceanGlass-LabelMaker-win-x64` and extract the complete artifact ZIP.

## Tests and manual checks

Dependency-free executable regression runners return a nonzero exit code on failure. Use `dotnet run`, not `dotnet test`:

```powershell
# Any OS with the .NET 8 SDK
dotnet run --project tests/ExcelSticker.Core.Tests -c Release

# Windows only; no print job is sent
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release
```

Core checks cover import/validation, per-entry/per-pane numbering, print-plan limits, layout schema validation, independent template copies, and JSON round trips. Windows checks cover previews, physical pagination, saved layout snapshots, preset contrast/resource updates, theme fallback, and keeping effects out of text ancestors. They do not replace manual drag/resize, high-DPI, or physical-printer tests.

Before production use on Windows:

1. Import each sheet in `examples/outer-inner-labels.xlsx`, click between rows, and switch Outer/Inner. Confirm material, dimensions, customer/order, and quantity.
2. Change W2/H2 and Inner Qty, preview, save, and verify the batch. Cancel an edit and confirm saved values remain unchanged.
3. Check print scopes/layers and page totals with Qty copies on/off.
4. Test both Cancel and Continue in the stock warning. Configure actual stock, print one outer and one inner label, and measure the output.
5. Verify the clay workspace at 100%, 150%, and 200% display scaling, keyboard navigation, and readable long-text stickers.

## Layout

- `src/ExcelSticker.Core`: entry/label models, CSV/XLSX readers, numbered print plans, validated physical-layout data, and stock classification.
- `src/ExcelSticker.Theming`: standalone clay engine, shared styles, palette/contrast logic, Theme Studio, and RGB picker.
- `src/ExcelSticker.Wpf`: business UI, Sticker Studio, shared label renderer, printer integration, and per-monitor DPI manifest.
- `tests/`: core regression and Windows smoke runners.
- `examples/`: glass CSV/XLSX files, theme presets, and separate Normal/SGU/DGU layout JSON files.
- `docs/`: research rationale, engine API, and studio usage/limits.
- `workflow-templates/`: inactive Windows CI workflow; restore to `.github/workflows/` to activate.
