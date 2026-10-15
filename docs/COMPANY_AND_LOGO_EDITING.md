# Editable company details and logo tones — v0.19.0

## Findings

The old implementation had several separate problems, not just missing textboxes:

1. `CompanyProfile` was loaded once from an adjacent/bundled JSON file. Sticker Studio received that profile but offered no way to edit or persist it. Layout fields such as CompanyName and Email were dynamic bindings to that startup profile.
2. The inspector displayed a **Custom text** input for bound fields even though `LabelView.Resolve` ignores that property except on a CustomText element. Users could type into a control that had no effect on the sticker.
3. Prefix/font/stroke inputs depended on `LostKeyboardFocus`. Invalid input could show an error while leaving the previous model value available to Save. Geometry had staged validation, but other inputs did not share it.
4. Save rejected unapplied stock inputs instead of applying valid values.
5. Logo import composited transparency onto white and converted colors to grayscale. Pale colors or partially transparent artwork can therefore be very light, and there was no tonal adjustment. This is an artwork issue; actual print darkness also depends on hardware, ribbon/media and printer settings.

## Company editing

Open **Logo / sticker layout → COMPANY DETAILS · all formats**.

- Edit **Company name, Address, Phone, Website and Email**. Typing updates the Studio sample immediately without modifying the live application's saved profile.
- **Apply company details** or Enter validates and records one Undo step. Other details may be blank; company name is required. Values support Unicode, with one-line length limits shown by the input limits/validation.
- **Save all formats / Ctrl+S** applies pending field, company and valid stock inputs, then saves everything together. The live sticker updates immediately on returning to the main window. Open sample previews refresh; production batch previews are invalidated and must be refreshed, so they cannot accidentally submit the previous company.
- **Cancel** leaves the main application and disk unchanged. **Reset company inputs** discards only un-applied company input. Undo first discards pending company input, or undoes the last applied change when there is no pending company input. Redo restores applied changes.
- Existing saved layouts are not reset. The simple default intentionally shows only the company name. To include the contacts, explicitly choose **Company + contacts pattern** for the selected format, or add/move individual Address/Phone/Website/Email fields. The preset preserves stock, logos and logo adjustments; it changes only the selected format and supports Undo. Check custom logo overlaps before saving.
- Select a company field to edit its font, box size, alignment, wrapping, visibility and prefix in the inspector. **Edit company details** jumps to the matching shared value. **Custom text** is now offered only for CustomText elements; font controls appear only for text, and stroke controls only for lines/boxes. Order/customer/glass/quantity fields explain that their values come from cutting data, not a company setting.

A label can contain editable text **and** a bitmap logo. Text already baked into a PNG/JPEG/BMP cannot be edited as a company field: replace that image or use separate editable text elements. No OCR/font reconstruction is claimed.

## Reliable input commits

Text/prefix, font size, stroke and X/Y/Width/Height are staged as one validated field edit. Use **Apply field changes**, Enter, Save or Ctrl+S; no focus-loss event is required. Invalid text/numbers/bounds stay editable and block saving or changing selection rather than silently reverting to an older value. **Reset values** explicitly discards staged field inputs. Untouched high-precision imported values are retained.

Valid pending stock width/height now apply on Save using **Scale field boxes to new stock** as selected. Invalid stock or clipping stops Save. When a field must extend beyond the current paper, apply the larger stock first, then increase its field bounds. A failed Save never overwrites the saved library, although already-applied draft changes remain available for correction/Undo.

## Darker imported logos

Select a Logo element in the field list; scroll the inspector to **LOGO DARKNESS · preview + print**:

- **Darkness 1–4:** 1 preserves the imported grayscale appearance; higher values darken intermediate grays using a gamma curve.
- **Contrast 0.5–3:** 1 is neutral. Higher contrast separates light and dark details. Pure white backing and pure black are preserved at all settings.
- **Solid black / white:** removes gray after the adjustments. A higher **threshold (1–254)** makes more pixels black; this slider is enabled only in Solid black / white mode. Excessively high threshold can also darken compression noise, so inspect the result.
- **Reset logo tones:** restores neutral settings without another import.
- **Copy tones to all formats:** applies settings only to logos using the same embedded source image, not unrelated artwork. Locked matching logos block the transaction; unlock them first. Positions, dimensions, rotation and source images are not changed.

Adjustments are saved per logo, are undoable, and never rewrite the embedded normalized PNG. Grayscale decoding and adjusted-image caches are bounded. The same adjusted bitmap feeds Studio, live/optional previews, Windows-driver output and the verified direct-IP ZPL rasterizer. These controls do **not** send printer heat/darkness configuration commands and cannot recover missing pixels or an unreadably low-resolution source.

## Storage, single EXE and portability

Company data is an optional `Company` object inside `%LOCALAPPDATA%\OceanGlass\sticker-layouts.json` (or `OCEANGLASS_DATA_DIR`). It uses the existing temporary-file/backup/replace save path together with all three formats—no separately saved company/layout half-state. The application clones the editor result into its active company profile; prepared print jobs retain their existing immutable snapshots.

Startup precedence:
1. Company details saved in the Studio library.
2. Adjacent `company.json` when no Studio company exists.
3. Bundled `company.json` when neither exists.

This works with the single EXE and does not require writing beside the executable. Legacy libraries without `Company` and logos without tone properties still load with the existing defaults and neutral tones. **Export all / Import JSON** carries the company and all layouts, embedded logos and tones to another PC. Importing a legacy full library without company settings retains the current company. A single-format export carries its logo settings but not the shared company; use Export all for a complete company/template package. New full-library imports can change company details, which are visible in the draft and require Save to activate.

## Verification

New core regression sources cover company validation/clone/serialization/legacy loading, contact presets, neutral/dark/binary tone lookup, white preservation and invalid tone limits. New WPF regression sources cover live typed company updates, Undo/Redo, shared formats, staged font/prefix/stock saving, blocked invalid saves, grayscale/reset rendering, all-format tone copying, save/reopen, immutable print snapshots, ZPL raster enhancement and application restart precedence. Existing logo/stock tests now expect valid stock input to apply during Save.

Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Manual acceptance: change all five company fields, select the contacts pattern, resize the company field/font and stock, and save while focus is still in the last input. Verify live SGU/DGU/Job Order, reopen Studio and restart the app. Test Cancel, Undo/Redo, invalid name/font/stock, and Export all/import on another settings folder. Import a pale/transparent logo, adjust tones, reset and copy settings across formats, then print one label through the selected driver and (if available) verified LAN ZT411. Check actual legibility before a production batch.

**This sandbox has no .NET SDK, Windows runtime or physical printer. Only static source/XML checks are run here; compilation, executable regressions, visual interaction and physical print quality remain unverified.**
