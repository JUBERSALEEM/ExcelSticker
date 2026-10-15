# Logo import, sticker size and layout movement — updated for v0.15.0

**v0.19.0:** [Company details, reliable input commits and non-destructive logo darkness/contrast](COMPANY_AND_LOGO_EDITING.md). Studio company settings save with the library and override startup defaults. Existing layouts remain intact.


## Where to adjust a sticker

Open **Logo / sticker layout** on the cutting-page toolbar, or **Stickers / printing → Adjust size / move fields…** in the live card. **Import company logo…** in the live card opens the editor and its file picker directly. This opens the existing Sticker Studio editor; live/job previews stay inline and read-only. The editor shows the same physical label renderer as printing, with editor-only guides, selections and resize handles on a separate layer.

### Change paper width and height

The left sidebar has **Sticker stock · all formats**, **Width (mm)**, **Height (mm)** and **Apply stock size**. Defaults remain **100 × 70 mm**; allowed dimensions are **20–300 mm**. These are physical paper dimensions, not glass W1/H1 and not screen-card corner radii.

- **Scale field boxes to new stock** on: scale every field's position and box dimensions, including logos and locked fields. Font point sizes remain unchanged; normal auto-fit/warnings still apply, with the existing 6 pt floor. Minimum boxes remain 1 mm wide / 0.2 mm high. Review small-stock legibility before printing.
- Off: retain positions and dimensions. If anything would be outside the paper, reject the entire operation rather than clipping or silently moving it.
- Stock size is common to SGU, DGU and Normal Job Order so a mixed batch never requires incompatible media within one queue submission. Layouts remain independently editable. A single-format JSON import must match the library stock size; resize the library first if necessary.
- **Simple pattern (keep logos)** restores the simplified default field arrangement scaled to the current stock, retaining imported logos and their geometry. It affects only the selected format, supports Undo and needs Save all formats to activate. Existing saved layouts are never replaced automatically. Check custom logo placement for overlaps.
- Apply, undo/redo, import/export and Save all formats preserve stock dimensions. Valid typed paper dimensions apply with **Apply stock size** or Save/export; invalid values block saving rather than being silently ignored. Save invalidates the old prepared job and its printer review. Refresh the preview; v0.15.0 reuses the chosen printer and applies the new size automatically.

The live sticker is scrollable for larger stock. The job preview and editor default to **Fit sticker**, adapting screen zoom to the available viewport; 100/150/200/300% fixed zoom is still available. Fit scaling lives on UI wrappers, never on the printed label. Print-job paper, pagination, page-range wrappers, printable-area overlay and printer-ticket request all use the saved dimensions. The printer must actually support/load the selected stock: the editor's range is **not** a claim that a Zebra or any specific printer supports all sizes. Hardware calibration is still required. ZT411 direct output supports up to **104 mm printable width**, using the saved height and native 8/12/24 dots/mm. See [ZT411 printing](ZT411_PRINTING.md).

### Import your company logo

Click **Import / replace logo…** and choose a local **PNG, JPEG or BMP**. No upload or remote URL is involved. **Import logo into all formats** is checked by default; uncheck it to change only the active format. Replacing a selected logo keeps its position/size/rotation. An existing locked logo must be unlocked first.

- Logos are normalized to white-backed grayscale PNG for monochrome label output. Transparent backgrounds become white; color logos become grayscale. This avoids a colorful preview of a monochrome print. Review pale colors/very fine lines on a real test sticker.
- The normalized bytes are embedded in layout JSON, not a path to the original image. Moving/deleting the source file or transferring exported layouts does not break the logo.
- Input limit: 8 MB and 16 megapixels. Normalization downsamples to a longest side of at most 1000 pixels, preserving aspect ratio. Embedded limit: 512 KB per PNG; at most three logo/image payloads per format. An exceptionally complex image may exceed the normalized size limit and must be simplified first.
- Imported JSON PNGs are bounded to 1200 × 1200 pixels and decoded/checked before activation. PNG header validation in Core is followed by WPF image decoding; a recognizable header alone is not treated as a usable image.
- SVG, animated/multipage images and externally linked images are not supported. Export a static PNG instead.
- The app does **not** invent a company logo or substitute the LABLE STUDIO application icon. Until you import your own, the live card explicitly says no company logo is configured. SGU/DGU demonstration data remains separate from cutting work; v0.16.0 allows explicitly marked standalone sample/test jobs.

A first import reserves logo space in the recognized default company header. Custom headers are not silently rearranged. Overlapping logo/field boxes, missing logo data and potentially low image resolution produce renderer/preflight warnings.

### Move and resize fields or logos

1. Select a field on the editor canvas or in the fields list.
2. **Drag its body to move** it. Drag its **blue lower-right handle** to resize its box. The handle has explicit visible chrome, rather than depending on a platform Thumb template.
3. Enter **X, Y, Width, Height (mm)** in the inspector for precise placement, then **Apply field changes** or Enter. All four bounds validate together: enlarging a field while moving it left no longer fails on an invalid intermediate rectangle. Save/export and other edits commit pending valid text, font and bounds. Invalid input remains editable and blocks selection changes until corrected or explicitly discarded with **Reset values**. Untouched high-precision coordinates are not rounded to the displayed precision. Click an input to select its value for replacement.
4. Use visible **← ↑ ↓ →** buttons for 0.5 mm moves, or keyboard arrows with canvas/list focus (Shift+arrows: 2 mm). Snap spacing is independently selectable for pointer movement. Fields stay inside the current paper, not a hard-coded 100 × 70 rectangle.
5. Logo **KeepAspect** fits the whole image without stretching/cropping. Turn it off only if intentional stretching is wanted. **Fit box to image proportions** adjusts the bounding box; **Rotate logo 90°** cycles through 0/90/180/270 degrees inside the box.
6. Alignment, visibility, locking, front/back order, duplication, deletion and undo/redo apply to logos as well as fields. Editing a logo does not modify source image files.
7. **Save all formats**, then inspect the real print-job preview. **Print current** tests the shown physical page; **Print all** and **Print range** retain the expanded original row/copy numbers. Neither page selection nor image import regenerates copy numbering.

Layout schema is now version **3** (library schema remains 1). Versions 1/2 load with 100 × 70 mm defaults and preserve custom fields; migration is in memory until Save. Keep the automatic `.bak` backup if you need to use an older build, which will not understand version 3.

## Research decisions

The v0.14.1 fit-to-view wrapper uses `LayoutTransform` because WPF layout must account for the scaled screen content. The print page itself remains untransformed. Microsoft distinguishes this layout-aware behavior from post-layout `RenderTransform`: [1](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/transforms-overview).

- Microsoft [BitmapImage guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-use-a-bitmapimage): bounded decode dimensions reduce image memory; setting one decode dimension preserves source aspect ratio. Import uses a local stream, OnLoad caching and frozen image sources, so source files can close immediately.
- Microsoft [Thumb resize example](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-resize-a-canvas-by-using-a-thumb): DragStarted/DragDelta/DragCompleted supply movement, but the application must explicitly move handles and resize content. The editor converts pointer coordinates to physical mm through its zoomed canvas and constrains them to the active stock.
- Microsoft [Canvas](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/canvas) and [DocumentPaginator](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.documentpaginator?view=windowsdesktop-8.0): field positioning and printer-page geometry are separate concerns. Zoom and editor guides remain outside the submitted page. See [print-preview research](PRINT_PREVIEW.md) for the reviewed print-ticket/scaling behavior.

## Verification and acceptance

Added Core/WPF regression source covers stock limits, atomic rejection, mixed-stock rejection, old-schema migration, clone independence, payload/rotation validation, white-backed grayscale conversion, source-file deletion, rotation, saved-logo round trips, custom page/range geometry, original copy numbering, printer-stock matching, cleared printer-size summaries, stock undo/redo and visible logo rotation/move controls. v0.14.1 adds coverage for fit scale/viewport containment, blank-sheet preview entry, joint geometry transactions, one-step undo, invalid-input retention, selection blocking, untouched coordinate precision and unapplied-stock save blocking.

**Not executed in this sandbox:** .NET build, executable tests, Windows image codecs/UI, drag capture and physical printing. No Windows runtime or .NET SDK is installed here. Static parsing and source review do not establish runtime correctness. Run `Build-and-Run.cmd` on Windows, or the build/test commands in [PRINT_PREVIEW.md](PRINT_PREVIEW.md).

Before production: import a real transparent/color logo, delete the original and reopen saved layouts; check all three formats and DGU panes; move/resize at 100/150/200% Windows DPI and editor zoom; toggle aspect/rotation/lock/visibility; resize stock with scaling on/off and undo; check minimum-window scrollbars; refresh old prepared jobs; print one current page and a short range on matching stock; measure geometry and inspect logo contrast/resolution. Confirm imported malformed/oversized images are rejected without altering saved layouts.
