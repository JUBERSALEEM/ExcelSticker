# Cutting-first workspace — v0.11.0

## Main screen

The default main screen is now a cutting sheet, not a label composition dashboard. The industrial-flat default, application icon and optional themes remain.

Each specification card has:

- Glass type: **SGU** or **DGU**.
- Shared specification and a base rate in **AED per m²**.
- An editable table in the requested order: **Sr No, Glass Ref, W1, H1, W2, H2, Qty, SQM, Total SQM, Price, Total Price**.
- Add row, paste rows, delete row and remove specification actions.
- Group quantity/area/value totals; a grand total below all groups.

The empty table retains a 156-DIP rectangular body. Serial numbers and computed columns are read-only. Blank row prices display/inherit the group's base rate; typing a price overrides only that row. Clearing that override restores inheritance. Zero is a valid explicit free rate.

The requested screenshot is used as the information/layout reference, not as a reason to invent optimization, stock nesting, surcharge, tax or quote features. There is no optimization engine in this change.

## Sticker section: inline accordion, not a popup

**Stickers / printing** in the toolbar opens or closes an inline expandable section on the same page and brings its header into view. Its disclosure arrow also works directly. It starts collapsed.

This section contains:

- The live native-size sticker preview and outer/inner/copy controls.
- Format, numbering, print scope, layer selection and printer action.
- Sticker layout editor access.
- A nested **Manual entry / advanced pane overrides** section for the previous form workflow and complex specifications.

Selecting a cutting row updates the preview even while the section is collapsed. Inline edits change its labels. **Edit selected entry details** also accepts incomplete rows, so missing metadata or complex DGU glass overrides can be corrected. While that detail edit is open, inline sheet editing and printing are blocked until Save/Cancel to avoid conflicting edits. The sheet's order/customer/job defaults are not overwritten by editing imported entry metadata.

The sticker itself remains square, white, monochrome and exactly **100×70 mm**. Its renderer, layout library, printer-stock handling and quantity numbering are unchanged.

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

Printing is blocked if **any** cutting row cannot become a valid label entry—even when selected-entry print scope is chosen. Invalid rows are not silently dropped. Order/customer metadata is not invented from example values: enter sheet defaults or retain the metadata imported on each row. An explicitly imported blank job number remains blank.

## Excel copy/paste

- **Ctrl+C:** native DataGrid copy of selected cells/rows, with headers. A currently edited text box retains normal text-copy behavior.
- **Ctrl+V:** select the first destination cell, then paste a rectangular block. Existing rows are overwritten within that block; extra rows append to the same group.
- **Paste rows:** append a block to that specification. Without headers use either:
  - Seven inputs: `Glass Ref, W1, H1, W2, H2, Qty, Price` (tab-separated from Excel), or
  - All eleven displayed columns.
- A header row containing W1/H1 supports named-column mapping. Native copied partial headers are recognized when they match the destination columns. Computed values and supplied serial numbers are ignored/recalculated.
- A blank final action column from native whole-row copying is accepted.
- The entire block is staged and checked before mutation. Malformed numbers, duplicate/unknown headings, ragged records or capacity violations reject the whole paste. Empty/zero numeric cells remain incomplete drafts rather than being fabricated as valid values.
- Paste limit: 2 MB; sheet limit: 10,000 rows. There is no multi-step sheet undo stack in this release. Export before a large replacement paste.

## Files and preserving work

The existing `.xlsx`, UTF-8 CSV/TSV file reader remains. **Blank cutting template** now exports the new cutting schema, which includes the group type/specification, prices, optional per-row order/customer/job metadata, inner quantity and pane overrides.

- New cutting-format files are fully parsed/staged before an append confirmation; prices are retained. They can be converted to `.xlsx` in Excel.
- Older label-only files still use the existing header/column-mapping preview. Their schema does not carry pricing: set group rates afterward or use the cutting template. `Glass Ref` now works as a legacy reference-column alias too.
- New-format imports append specification cards in row order. Contiguous rows with the same type/specification share a card; adjacent identical cards can combine on CSV re-import. Repeated imports do not deduplicate rows, and may add another card with the same specification intentionally.
- **Export cutting CSV** retains the row inputs, effective rates, calculated values and per-row metadata. Calculated cells are recomputed on re-import. Empty specification cards without rows are not represented in a row-based CSV.
- Formula-looking text is exported with an Excel literal-text prefix, not as executable formulas. The cutting importer recognizes this escape; an original leading apostrophe is doubled on export and restored on import.
- Draft text is preserved in export, but invalid numeric text or an empty specification must be corrected before importing that CSV. Export is not a native project/autosave format.
- Data is still **in memory only**. Closing with cutting rows/specifications warns about losing work; export explicitly to retain it. Export does not remove the close warning.

A reference sample is in `examples/cutting-reference.csv`. Import it to inspect the price calculations. Its sample order/customer values are demonstration data, not production metadata.

## Architecture

- `CuttingSheet`, `CuttingGroup`, `CuttingRow` (Core): editable source data, stable row identities, notifications, decimal calculations and label conversion.
- `CuttingTable` (Core): clipboard rectangle staging and cutting CSV interchange; no WPF/clipboard dependency.
- `Studio/CuttingWorkspace` (WPF): grouped cards, editable grids, paste commands, selection and feedback.
- Main window: inline disclosure sections, metadata defaults and a derived validated adapter for the existing label/print pipeline.

Rows with identical values keep different identities. Batch changes/pastes are batched into one document refresh. DataGrid rows are virtualized inside each group; the group-card list is not virtualized. Large sheets or many groups still need Windows performance testing.

## Verification status

Executed here: C# syntax parsing, XML well-formedness, XAML event/resource wiring checks, column ordering, collapsed inline sections, unchanged physical-preview geometry, independent decimal calculations against the screenshot and `git diff --check`.

Added Core tests for reference pricing, atomic paste, partial-cell identity, full-grid headers, ignored computed values, inherited rates, incomplete rows, CSV round trips, inner quantities/overrides, duplicate identity, reference aliases and invalid input. Added WPF coverage for cutting-first startup, the eleven editable/readonly columns, live preview updates, print blocking, DGU fallback, advanced-edit metadata isolation/cancel/inheritance and opening the inline section without creating a window. Existing import/label/paginator/theme checks were updated for the new workspace.

**Not run in this sandbox:** .NET compilation, executable Core/WPF tests, native visual checks, actual clipboard interaction or printer output. There is no .NET SDK/Windows runtime here. Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Manually test 100/150/200% DPI, minimum-window scrolling, keyboard cell edits, Excel block paste/rejection, file import/export and one physical label. Review totals and metadata before a production print job. The workflow remains inactive and the SDK selection policy is unchanged.
