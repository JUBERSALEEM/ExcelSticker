# Actual print-job preview (v0.13.0)

## Factory workflow

1. Fill or import the cutting list. Keep Order No and Customer valid. Every started row must be valid; completely unused slots are ignored. Save/Cancel an entry-details edit first.
2. Expand **Stickers / printing**. Choose **All batch entries / Selected entry only**, pane filter, format and **Print all Qty copies**.
3. Click **Prepare print preview**, or press **Ctrl+P**. This commits inline edits and opens an **in-page** print-job preview—not a new window or a print submission. The header identifies the prepared scope and physical page count.
4. Browse the virtualized page list, First/Previous/Next/Last or Go. Each page identifies its row serial, copy, pane, dimensions, reference and order. Screen zoom is 100/150/200/300%; it is not printer scaling or a guarantee of physical ruler size on a monitor.
5. Choose **Printer settings / inspect**. The native dialog's Print/OK button **only applies settings**. No pages spool at this step. Cancel preserves the previous reviewed settings. Inspect the resulting driver media/orientation/scaling/copies and optional printable-area guide.
6. **Print current sticker (1)** sends just the displayed physical page. **Print all preview pages** means every page of this prepared scope, not necessarily the entire cutting sheet. **Print range** uses 1-based inclusive physical page numbers, not cutting-row serials.
7. The app checks all requested pages for text/field/boundary warnings, keeping only a small warning summary in memory. Large checks yield to the UI and offer **Cancel checks**. Data/option changes during checking abort the send. Confirm the exact count/range, printer and warnings before submission. A successful submission reports the queue; check the queue and physical output yourself.

Example: row 7, outer Qty 4 + inner Qty 2, both panes, Qty expansion on: pages 1–4 are outer `7/1…7/4`; pages 5–6 are inner `7/1…7/2`. Print current on page 4 sends **one** outer sticker `7/4`. Range 4–6 sends `7/4`, inner `7/1`, inner `7/2`. It does not renumber those three pages as copies 1–3. Qty expansion off prepares just one page per included pane, each with copy 1.

## Live samples versus printable jobs

The live card defaults to a clearly marked **SAMPLE / SGU** when no usable row is selected. The source selector also offers explicit SGU and DGU examples. DGU has outer 1200 × 1800 / Qty 4 and inner 1190 × 1790 / Qty 2. Outer/inner and copy navigation use the same `LabelView` renderer and saved layouts as real entries.

Examples exist only in `LiveStickerSamples`; they never enter `CuttingSheet`, `BatchRows`, `GetPrintLabels` or the prepared paginator. They do not create unsaved cutting work, enable printing or overwrite order/customer fields. Selecting a real cutting row automatically resumes its live preview. Selecting an incomplete started row shows its validation state and clears the invalid sticker rather than pretending a sample is that row. Explicitly choosing an example remains clearly labelled as demonstration data. A prepared real print job is independent of the live sample selector.

## What matches, and what cannot be guaranteed

- A prepared `LabelPaginator` snapshots expanded immutable label records, a cloned layout library and a cloned company profile. Page geometry is fixed at **100 × 70 mm = 377.9527559 × 264.5669291 WPF DIP**.
- Preview hosts `Job.GetPage(index).Visual`; printing delegates to the **same paginator**, with freshly rendered independent visuals. There is no screenshot export, approximate HTML preview, second layout engine or rebuilding of the quantity plan for current/range printing.
- Layout format overrides, source-row serials, pane quantities, custom positions/fonts, legacy layout upgrades and numbering all follow the existing actual renderer.
- The page list is virtualized; only one preview page visual is retained. Warning checks render/release one requested page at a time. There is a 10,000-page job limit. Large jobs still cost CPU and take time; preview is not a printer-emulation engine.
- Zoom, paper frame, list selection and red printable-area guides are **outside** the label visual. No UI theme, guide or screen scaling enters the selected paginator.
- A selected/range paginator maps indices into the already-expanded original snapshot. It never invokes `LabelPrintPlan.Build` again.
- Sheet/common metadata changes, entry-details editing, format/layout save, scope, pane filter or quantity mode invalidate the prepared job. Selection changes invalidate a selected-entry job, not an all-entry job. Stale pages remain visible with an **OUT OF DATE** warning and disabled print commands. Refresh explicitly replaces the job and returns to page 1. Theme/zoom/live-pane/demo navigation do not change print content.

**Physical limits:** printers may apply vendor-private scaling, offsets, orientation transforms, feed calibration, printable margins or DPI-specific font/rasterization behavior that WPF does not report. Paper feeding and hardware completion are outside app control. Test one sticker, measure it, and calibrate the stock before a production batch. This is an accurate preview of the app's submitted pages, not a guarantee that every driver/device reproduces them perfectly.

## Printer review and compatibility

`StickerPrinter.Configure` asks for 100 × 70 mm, portrait, copies 1, one page per sheet, 100% scaling, one-sided and monochrome. The chosen driver ticket is merged/validated, then cloned and retained along with the queue. `Send` uses that reviewed ticket and the selected paginator; it does not open a second settings dialog.

Known extra driver copies, N-up, duplex and non-100% scaling block printing. Standard Print Schema `PageScaling` is inspected in addition to the public `PageScalingFactor`: the public getter can return only X for non-square `Custom` scaling. Consequently X=100/Y=90 and standard Fit modes are blocked too; standard `None` means actual size. Missing/vendor-private scaling is warned, never assumed to be verified.

Unknown/unverified stock remains a deliberate compatibility path for named-stock/Zebra drivers. The inline review and final confirmation show unverified settings and explain clipping/offset risk. The app does not silently resize to A4/Letter or silently multiply copies. The driver's media/orientation is checked using the existing 2-DIP stock tolerance and landscape dimension handling.

`PrintCapabilities.PageImageableArea` provides paper-origin offsets and extent in DIP. Only finite, nonnegative origins and positive extents on verified stock produce an overlay. The outside area is tinted and the reported inside boundary dashed red. Layout boxes extending outside that boundary generate advisory warnings. Missing capabilities do not invent margins or automatically offset/scale label content. The guide cannot account for unreported vendor transformations.

## Research and API/source review

Reviewed Microsoft documentation and the open-source WPF implementation during this change:

- [Display a print dialog](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/documents/how-to-display-print-dialog): settings selection and `PrintDocument(DocumentPaginator, ...)` are separate operations; a delegating paginator supports page ranges.
- [DocumentPageView](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.documentpageview?view=windowsdesktop-8.0): WPF's paginated-page presentation model. This app hosts a fresh actual `DocumentPage.Visual` directly, avoiding a built-in viewer print command that could bypass scope/stale checks.
- [PageImageableArea](https://learn.microsoft.com/en-us/dotnet/api/system.printing.printcapabilities.pageimageablearea?view=windowsdesktop-8.0): origins/extents are 1/96-inch units and may be absent.
- [PageScalingFactor](https://learn.microsoft.com/en-us/dotnet/api/system.printing.printticket.pagescalingfactor?view=windowsdesktop-8.0): nullable percentage; null is not proof of 100%.
- [WPF PrintDialog source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/PrintDialog.cs): `PrintDocument` passes the paginator and print ticket to the XPS writer.
- [PrintTicket public implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/ReachFramework/PrintConfig/PrtTicket_Public_Simple.cs): typed `Clone()`, copy/N-up properties, scaling getter behavior and CustomSquare setter.
- [Print Schema scaling implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/ReachFramework/PrintConfig/PageScaling.cs): custom width/height and square scaling parameters.
- [DocumentPage source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Documents/DocumentPage.cs) and [Dispatcher source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/Dispatcher.cs): disposable temporary pages and dispatcher-yielding preflight on the WPF thread.

These are design/API references, not evidence of successful local compilation or printer tests.

## Verification status and Windows acceptance

This sandbox has **no .NET SDK or Windows runtime**. C# syntax-tree/XML/resource/event-wiring checks and diff checks are possible here. Compilation, executable regression tests, native dialog behavior, visual review and physical output remain **unverified**. No test print has been sent. The GitHub workflow is still an inactive template, as previously requested.

Added Windows regression source in `tests/ExcelSticker.Wpf.Tests/PrintPreviewChecks.cs` covers sample isolation, real/invalid selection, snapshot cloning, expanded order, current/all/range commands, range bounds, Qty/format behavior, 10,000-page bounds, screen-only zoom/guides, stale and busy guards, copy/N-up/duplex/scaling safeguards and selected-scope invalidation. It uses synthetic tickets and never calls the printer.

Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
$env:OCEANGLASS_VISUAL_REVIEW="$PWD/artifacts/visual-review"
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Then verify manually:

1. Fresh app: five empty cutting rows, default SGU sample, DGU outer/inner samples, no print eligibility; selecting an incomplete real row does not show a fake valid sticker.
2. A mixed SGU/DGU list: all/selected scope, every pane filter, Qty on/off and all three layout formats. Browse last/first/jump; compare row/copy numbers to expected spool order.
3. Prepare row 7 with 4 outer/2 inner copies; use current page 4, range 4–6 and all. Compare the actual output, not just counters. Keep driver copies at 1.
4. Change a row/default/format/layout/filter/selection during and after preparation. Confirm stale sends are blocked. Cancel large-page preflight; nothing should enter the queue.
5. Native settings dialog: Cancel, apply, switch printer, configure wrong stock, 90%/Fit, Copies 2, N-up 2 and duplex. Ensure reviewed settings/blocks appear and setup alone never spools.
6. Driver with no size/margin report: verify warnings and deliberate compatibility confirmation. Driver with known margins: inspect boundary guide and potential clipping warnings. Switching the guide or zoom must not change output geometry.
7. Check 100/150/200% Windows DPI, narrow/minimum window, keyboard page navigation and long order/customer/specification warnings. Preview must remain scrollable and legible.
8. Test one physical 100 × 70 mm sticker, measure dimensions/margins, then test DGU outer/inner and a small range before the full batch. Test an unavailable/offline queue and a PDF driver's save-dialog cancellation separately.
