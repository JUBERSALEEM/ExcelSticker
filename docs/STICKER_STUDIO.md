# Sticker Studio: layouts and numbered copies

Open **Sticker Editor** in the header. It is independent of **Theme Studio**.

## Three format slots

- **NormalJobOrder:** job-number-focused conventional label.
- **SGU:** single-glass format with SGU heading and job field.
- **DGU:** outer/inner format with DGU heading and pane marker.

Each starts with its own sample and is saved independently within the layout library. DGU sample selection switches between outer and inner material/dimensions. The samples are not batch entries and are never automatically sent to a printer.

In the main window, **Sticker format → Automatic** uses SGU for single glass and DGU for pane labels. **Normal job order**, **SGU**, and **DGU** are explicit job-wide overrides. Forcing SGU onto a DGU entry may omit a pane field in that format; inspect the result before production use.

## Editing

1. Choose a format and select an element on the canvas or in the field list.
2. Drag to move; use the lower-right handle to resize. Coordinates and dimensions are in **millimeters** on a fixed **100 × 70 mm** stock.
3. Set position/size exactly in the inspector. Numeric/text edits commit when the control loses focus. Invalid bounds are rejected, not silently saved.
4. Set font family, **point size**, bold, underline, alignment, wrapping, auto-fit, prefix, and custom text.
5. Add data-bound fields, custom text, divider lines, or outline boxes. Duplicate/delete, bring front/send back, hide, or lock a field.
6. Align to the 3 mm safe-area edges or center in the label. This guide is advisory; it is not a measurement of your printer's nonprintable region.
7. Use a 0.5/1/2 mm snap grid, rulers, and 100/150/200/300% editor zoom. Guides and handles do not print.
8. Arrow keys nudge a selected field by 0.5 mm; Shift+arrows move 2 mm. Use Ctrl+Z / Ctrl+Y for layout undo/redo when not editing text. Up to 50 history snapshots are kept.
9. **Save all formats** activates the layouts in the main preview and printer. Cancel/window close warns about unsaved layout edits.

The inspector and canvas support one selected element at a time; there is no multi-selection, image/logo upload, barcode generator, arbitrary rotation, or multi-stock page designer in this version.

## Text fitting and visibility

Each text field uses vector text. Optional auto-fit reduces the point size in 0.25 pt steps but never below **6 pt**. If the content still does not fit, the preview reports a warning and the print flow asks for confirmation. Enlarge the box, enable wrapping, shorten the text, or adjust the font instead of relying on unreadable text. Fields can intentionally overlap; there is no automatic collision solver. Use layering commands to control which field is drawn last. Locked fields cannot be dragged or edited until unlocked.

Available data includes company contacts, order, job, customer, reference, pane, material, full spec, size/individual dimensions, formatted Quantity, raw TotalQty, CopyNumber, and format heading. Prefixes are literal text. Custom text is not a template language and does not execute code.

## Quantity numbering

Default settings are **Print Qty copies = on** and **Entry / copy** numbering.

For the first batch entry, Qty 4:

```text
QTY: 1/1
QTY: 1/2
QTY: 1/3
QTY: 1/4
```

The first number is the entry's **current one-based position in the batch**; the second is the copy number. Entry 2 starts at `2/1`. Selecting only entry 3 still prints `3/1`, not `1/1`. Removing earlier entries renumbers subsequent entries; these are not permanent production IDs.

For DGU, numbering restarts independently for each pane. Qty 4 outer + Qty 4 inner produces 8 stickers: outer `1/1`–`1/4`, then inner `1/1`–`1/4`. The pane marker distinguishes them. An inner quantity of 2 instead produces inner `1/1`, `1/2`.

Other modes:

- **Copy / total:** `1/4`, `2/4`, `3/4`, `4/4`.
- **Total quantity only:** all four say `4`.

`Quantity` follows the chosen mode. `TotalQty` always shows the original numeric quantity. `CopyNumber` always shows `entry/copy`. Turning **Print Qty copies** off prints one copy per chosen pane, irrespective of numbering; it does not promise four pages. The preview arrows let you inspect each numbered copy. The sidebar page count is authoritative for the selected print scope/layers/copies setting.

The paginator creates separate immutable records per copy; it does not repeat one pre-rendered sticker four times. Layouts are cloned when the print job is created, so later edits cannot change pages already being spooled.

## Import/export and storage

Sample files:

- `examples/layouts/normal-job-order.json`
- `examples/layouts/sgu.json`
- `examples/layouts/dgu.json`
- `examples/layouts/all-formats.json`

**Import JSON** accepts either a single format (updates that format's slot) or an entire three-format library. Import changes the draft; use Save all formats to activate it. **Export format** writes the current slot; **Export all** writes the library. Imported JSON is validated: supported version/fields/fonts, unique element IDs, finite in-stock coordinates, 1–60 elements per layout, 6–36 pt font sizes, and bounded text lengths. It cannot load XAML or executable code.

Activated layouts are stored in `%LOCALAPPDATA%\OceanGlass\sticker-layouts.json`; the previous file is copied to `.bak` before replacement. A corrupt or incompatible saved library falls back to defaults with a status warning and is not overwritten merely by opening the app. Export backups before major changes. Theme settings, layouts, and in-memory batch data are separate.

## Production checklist

- Match the visible preview to the intended saved entry, pane, copy, and format.
- Resolve text-fit warnings for representative longest values.
- Test selected-entry / outer-only / copies-off for a one-sticker test.
- Configure actual 100 × 70 mm stock and driver Copies = 1.
- Print and measure one SGU, one DGU outer/inner pair, and one job-order sticker.
- Test restart persistence, Cancel rollback, import/export, and different Windows display scales.
