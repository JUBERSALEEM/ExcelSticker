# Cutting-first workspace — v0.12.0

## Main screen

The default main screen is now a cutting sheet, not a label composition dashboard. The industrial-flat default, application icon and optional themes remain.

Each specification card has:

- Glass type: **SGU** or **DGU**.
- Shared specification and a base rate in **AED per m²**.
- An editable table in the requested order: **Sr No, Glass Ref, W1, H1, W2, H2, Qty, SQM, Total SQM, Price, Total Price**.
- Add row, paste rows, delete row and remove specification actions.
- Group quantity/area/value totals; a grand total below all groups.

Home, Clear sheet and Add specification create **five blank input rows** in a rectangular table (minimum 216 DIP). These unused slots show no zero quantities, calculated dashes or validation errors. Single-click enters edit mode and selects the old value; subsequent clicks place the caret normally. Tab/keyboard navigation works, and Shift+click selects a range. Cell selection is a subtle tint; editing uses a neutral surface with a thin focus border rather than Windows solid blue. Serial numbers and computed columns are read-only. For started rows, blank prices display/inherit the group's base rate; typing a price overrides only that row. Clearing that override restores inheritance. Zero is a valid explicit free rate.

The requested screenshot is used as the information/layout reference, not as a reason to invent optimization, stock nesting, surcharge, tax or quote features. There is no optimization engine in this change.

## Sticker section: inline accordion, not a popup

**Stickers / printing** in the toolbar opens or closes an inline expandable section on the same page and brings its header into view. Its disclosure arrow also works directly. It starts collapsed.

This section contains:

- The live native-size sticker preview and outer/inner/copy controls.
- Format, fixed glass-serial/copy numbering, print scope and layer selection.
- Inline **Prepare print preview** with paginated actual print pages, current/all/range printing and reviewed printer settings.
- Marked SGU/DGU live examples when no usable row is selected; examples never create cutting or print data.
- Sticker layout editor access.
- A nested **Manual entry / advanced pane overrides** section for the previous form workflow and complex specifications.

Selecting a cutting row updates the preview even while the section is collapsed. Inline edits change its labels. **Edit selected entry details** also accepts incomplete rows, so missing metadata or complex DGU glass overrides can be corrected. While that detail edit is open, inline sheet editing and printing are blocked until Save/Cancel to avoid conflicting edits. The sheet's order/customer/job defaults are not overwritten by editing imported entry metadata.

The sticker itself remains square, white, monochrome and exactly **100×70 mm**. See [Print preview](PRINT_PREVIEW.md) for the v0.13.0 snapshot/driver-review workflow. As of v0.11.1, every stock format has a separate **SR NO** field and numbered **QTY**. Sr No 1 / Qty 4 prints `1/1, 1/2, 1/3, 1/4`; selecting row 7 alone still prints `7/1…`. DGU panes retain their independent quantities and each pane starts at copy 1. Total-only and copy/total choices are no longer exposed by the app. Older Core numbering enum values remain for compatibility.

Layout library schema remains version 1; individual sticker layouts now use version 2. Old layouts upgrade in memory: `TotalQty` becomes `Quantity`, the old standard order-number box is split to accommodate `GlassSrNo`, or vacant space is used for custom designs. Existing fields are not overlaid. A dense custom layout that cannot fit a serial produces a preview/print warning; add/show `GlassSrNo` in Sticker Studio or reset that format. Saving formats persists changes with the existing backup behavior; loading alone never rewrites the file.

## Calculation rules

The pricing convention is deliberately explicit:

```text
SQM         = round(W1 × H1 / 1,000,000, 3, away from zero)
Total SQM   = SQM × Qty
Total Price = round(Total SQM × Price per m², 2, away from zero)
```

This matches the reference's first three rows:

| Ref | W1 | H1 | Qty | SQM | Total SQM | Rate | Value AED |
|---|---:|---:|---:|---:|---:|---:|---:|
| 11 | 1050 | 2127 | 2 | 2.233 | 4.466 | 170 | 759.22 |
| 4 | 854 | 1981 | 2 | 1.692 | 3.384 | 170 | 575.28 |
| 7 | 1003 | 1857 | 2 | 1.863 | 3.726 | 170 | 633.42 |
| **Total** | | | **6** | | **11.576** | | **1,967.92** |

DGU sales area is the finished outer unit (W1/H1), **not** the sum of both panes. W2/H2 are used for inner stickers. Both zero or both blank mean “reuse W1/H1”; only one missing/zero dimension is invalid. SGU cannot have inner dimensions/overrides. Inner quantities from older imports are preserved independently, even when originally equal to outer Qty and can be edited in entry details; hover a row to inspect this override.

Dimensions must be >0 and ≤100,000 mm to be ready; Qty is an integer 1–10,000; rates are nonnegative and capped at 1,000,000,000. Numeric input uses a decimal point without thousands separators. Incomplete/zero rows can remain on the sheet. Their calculated cells show a dash, they are excluded from totals, and totals are visibly marked **INCOMPLETE**. Missing specification or label metadata may still permit numeric area calculation, but never permits printing.

Unused blank rows are ignored. Printing is blocked if **any started** cutting row cannot become a valid label entry—even when selected-entry print scope is chosen. Invalid rows are not silently dropped. Order/customer metadata is not invented from example values: enter sheet defaults or retain the metadata imported on each row. An explicitly imported blank job number remains blank.

## Excel copy/paste

- **Ctrl+C:** native DataGrid copy of selected cells/rows, with headers. A currently edited text box retains normal text-copy behavior.
- **Ctrl+V:** select the first destination cell, then paste a rectangular block. Existing rows are overwritten within that block; extra rows append to the same group.
- **Paste rows:** fill the trailing blank slots, then append as needed, with a block to that specification. Without headers use either:
  - Seven inputs: `Glass Ref, W1, H1, W2, H2, Qty, Price` (tab-separated from Excel), or
  - All eleven displayed columns.
- A header row containing W1/H1 supports named-column mapping. Native copied partial headers are recognized when they match the destination columns. Computed values and supplied serial numbers are ignored/recalculated.
- A blank final action column from native whole-row copying is accepted.
- The entire block is staged and checked before mutation. Malformed numbers, duplicate/unknown headings, ragged records or capacity violations reject the whole paste. Empty/zero numeric cells remain incomplete drafts rather than being fabricated as valid values.
- Paste limit: 2 MB; sheet limit: 10,000 rows. There is no multi-step sheet undo stack in this release. Export before a large replacement paste.

## Files and preserving work

### Save/open the formatted Excel page

**Save Excel** (or **Ctrl+S**) opens a Save dialog for a real `.xlsx` workbook—no Excel installation or external application packages required. **Blank Excel template** produces the same layout with five blank input rows.

The visible **Cutting list** sheet has:

- A LABLE STUDIO title, common order/customer/job fields and measurement/currency units.
- A glass-type/specification heading and base rate above each specification table.
- Exactly the eleven requested visible columns, not a repeated flat table of label metadata.
- White editable inputs, shaded calculations, aligned numbers, subtle borders, and green group/grand totals.
- Area/value formulas with cached values, the same three-decimal area rounding and two-decimal totals, frozen job headings, and an A4 landscape print area excluding internal data.

**Import Excel / CSV** recognizes this workbook and stages the entire read before changing anything. If the current list has work, it asks before replacing it. The current visible Excel inputs are authoritative, not a stale hidden copy of dimensions/prices. Calculated/serial cells are recomputed on import. Edited base rates continue to apply to inherited-price rows; manually replacing a Price formula with a number creates a row override. Clearing Price restores inheritance.

Blank rows, empty/duplicate specification cards, raw draft input, group base rates and common metadata survive this save/open path. Separate inner quantities, pane overrides and original per-row metadata are stored in a very-hidden `_StudioData` sheet, linked by a hidden row-ID column. This metadata is deliberately **not** repeated across the visible page. Global Excel job headings apply to rows using defaults; imported row-specific metadata retains its own values.

Keep the hidden ID column/metadata sheet and the eleven-column layout intact. Hidden is **not encrypted**: the workbook contains those additional details, so review it before sharing. Deleting rows in Excel can leave their old metadata in the helper sheet until the app reopens and re-saves the workbook. Rows copied in Excel retain their metadata; new rows without IDs use the common defaults. To extend a table in Excel, insert/copy rows inside its body so formulas travel with the inputs. Do not delete/move the specification/rate header rows. This is an editable grouped worksheet, not an Excel Table/ListObject or stock optimizer. Import restores cutting data, not arbitrary Excel formatting/macros; re-saving uses the app’s standard page template.

The app does not evaluate arbitrary Excel formulas. Input formulas other than its own inheritance formula need cached results saved by Excel; calculated area/total caches are ignored on import. Excel's normal numeric precision limits still apply. Opening the workbook in a spreadsheet app recalculates its formulas; the app separately validates all started rows before label printing. Dense sheets still need a Windows/Excel performance review.

Saving uses a temporary file and replacement only after successful writing/size checks, so a failed save does not overwrite the existing file. Limits remain 10,000 cutting rows and safe workbook size limits; a cell is limited to Excel's 32,767 characters. Data is **not autosaved**. Close warnings remain conservative even after saving.

### Legacy file compatibility

The existing `.xlsx`, UTF-8 CSV and TSV readers remain for flat files. Legacy label-only files use the mapping preview and do not carry prices; set group rates afterward. Flat cutting CSVs keep the dedicated price-aware importer. These imports append entries rather than replacing the list, and initial unused rows are reused/replaced instead of becoming false incomplete entries.

**Stickers / printing → Advanced CSV export** retains the older interchange schema. This is not the preferred save/reopen format: unused rows and empty cards are omitted; effective prices become explicit; malformed numeric drafts or empty specifications must be corrected before re-importing CSV. Formula-looking text is escaped as literal text. Use **Save Excel** for the full cutting-list round trip.

A reference sample is in `examples/cutting-reference.csv`. Import it to inspect the price calculations. Its sample order/customer values are demonstration data, not production metadata.

## Architecture

- `CuttingSheet`, `CuttingGroup`, `CuttingRow` (Core): editable source data, stable row identities, notifications, decimal calculations and label conversion.
- `CuttingWorkbook` (Core): formatted Open XML workbook writing, hidden metadata, staged reading and atomic file saves.
- `InputEditing` (WPF): scoped select-all-on-focus behavior without blocking later caret placement.
- `CuttingTable` (Core): clipboard rectangle staging and cutting CSV interchange; no WPF/clipboard dependency.
- `Studio/CuttingWorkspace` (WPF): grouped cards, editable grids, paste commands, selection and feedback.
- Main window: inline disclosure sections, metadata defaults and a derived validated adapter for the existing label/print pipeline.

Rows with identical values keep different identities. Batch changes/pastes are batched into one document refresh. DataGrid rows are virtualized inside each group; the group-card list is not virtualized. Large sheets or many groups still need Windows performance testing.

## Verification status

Executed here: C# syntax parsing, XML well-formedness, XAML event/resource wiring checks, column ordering, collapsed inline sections, unchanged physical-preview geometry, independent decimal calculations against the screenshot and `git diff --check`.

Added Core checks for five-row startup semantics, reusable blanks, formatted workbook package structure, full save/open round trips, visible Excel edits, uncached calculated formulas, inherited-price edits, newly inserted rows and failed-save preservation. WPF checks cover five visible starters, editor select-all and neutral focus styling. These executable checks require Windows/.NET and were not run here. Earlier tests cover reference pricing, atomic paste, partial-cell identity, full-grid headers, ignored computed values, inherited rates, incomplete rows, CSV round trips, inner quantities/overrides, duplicate identity, reference aliases and invalid input. Added WPF coverage for cutting-first startup, the eleven editable/readonly columns, live preview updates, print blocking, DGU fallback, advanced-edit metadata isolation/cancel/inheritance and opening the inline section without creating a window. Existing import/label/paginator/theme checks were updated for the new workspace.

**Not run in this sandbox:** .NET compilation, executable Core/WPF tests, native visual checks, actual clipboard interaction or printer output. There is no .NET SDK/Windows runtime here. Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Manually test 100/150/200% DPI, minimum-window scrolling, keyboard cell edits, Excel block paste/rejection, file import/export and one physical label. Review totals and metadata before a production print job. The workflow remains inactive and the SDK selection policy is unchanged.
