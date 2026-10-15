# Print preview and simple printing — v0.16.0

## Factory workflow

1. Expand **Stickers / printing** and choose a sample/format, or select a valid cutting row. Samples need **no manual entry or Excel import**. Cutting scope and layer filters appear only for cutting data.
2. Click **Print preview** on the toolbar or press **Ctrl+P**. The in-page preview lists the actual prepared stickers. An empty sheet prepares a clearly labelled **SAMPLE** SGU/DGU test job. It can be previewed or test printed without filling Order No/Customer.
3. Browse pages. **Print current** sends only the displayed sticker. **Print all (N)** sends all N stickers of the prepared scope. **Print range** uses inclusive 1-based page numbers.
4. On first print, select the printer in the Windows dialog; the requested action then continues. Subsequent clicks use the remembered printer. **Printer setup…** changes the printer separately and does not send any stickers.

**No recurring send confirmation or technical-warning checklist.** Status says “Printing current sticker…” or “Printing all N stickers…”, then “Sent … to [printer].” A real problem has one actionable error; diagnostics and the optional driver boundary are under **Advanced**. Real cutting changes still require Refresh. Sample format/Qty/source changes rebuild an open sample job automatically; live sample pane/copy navigation follows its matching page. Saved sample layout/logo edits refresh an open sample job too. Refresh is not a second print permission step.

Example: row 7, outer Qty 4 + inner Qty 2 produces six pages: outer `7/1…7/4`, then inner `7/1…7/2`. Current on page 4 sends one outer `7/4`; range 4–6 sends outer `7/4`, inner `7/1`, inner `7/2`. It does not restart copy numbering. Qty expansion off prepares one page per included pane.

## Actual-pattern rendering

`LabelPaginator` snapshots immutable expanded labels plus cloned company/layout settings. Preview and print use the same `LabelView` renderer. With a recognized ZT411 selected, preview shows the **same final monochrome printer-resolution raster** that direct output encodes. Other printers use the original vector paginator. Current/range delegate to the snapshot without rebuilding the print plan.

- Default stock **100 × 70 mm**, with saved custom dimensions shared across SGU/DGU/Normal formats.
- Imported logos, Unicode text, font/field positions, rotation, pane details and source-row/copy numbering are retained.
- Screen-only **Fit sticker** or 100/150/200/300% zoom, paper frame, theme effects and boundary guides never enter print output. Actual size on a monitor is not a physical ruler guarantee.
- Only one preview page is retained. Direct jobs render/release one output page at a time, into a bounded temporary spool file. 10,000-page job limit; 512 MiB direct-output file limit. Larger direct jobs ask for a smaller range.
- Cutting metadata/row/quantity/layout/format/filter changes mark the job **OUT OF DATE**, leaving it visible but nonprintable until refreshed. Selected-row changes invalidate selected-only jobs, not all-row jobs. Print preparation checks for changes/cancellation before spooling.
- `LiveStickerSamples` supplies the live card, sample paginator and Sticker Studio. SGU TYPE is `6mm HD Grey Glass`; DGU TYPE is `6mm HD Grey Glass + 12mm ASP + 6mm Clear Glass`. Both DGU panes have **300 × 300 mm glass dimensions** and the requested complete assembly on the sample TYPE line. This does not change real production pane-material parsing.
- These are **glass**, not physical paper, dimensions. Saved paper size remains unchanged (100 × 70 mm by default); the ZT411 104 mm print-width check remains intact.
- Samples are standalone, explicitly identified test jobs. They never populate cutting data or `GetPrintLabels()` production selection. A sample is allowed even if an unrelated cutting draft is incomplete. Selecting incomplete real work instead still blocks production preparation; it cannot silently print a sample as that row.
- Changing the live source between samples and cutting work invalidates the prior job. Explicitly choosing a sample while the job preview is open replaces it with a clearly marked sample job; nothing is sent automatically. Sample preparation does not reset its selected pane/copy by re-reading the sheet. Production preparation also preserves the displayed copy when it still belongs to the selected row and requested scope. Selecting another valid production row preserves an all-rows snapshot, as before.
- Manual entry/pane overrides and CSV/Excel tools are in the cutting area, outside the sticker section. Sample mode hides unrelated cutting-scope/layer controls. The format/source controls agree on SGU versus DGU; Normal Job Order and Automatic remain available.

## Printer selection, sizing and compatibility

See [ZT411 research, implementation and physical acceptance](ZT411_PRINTING.md).

Recognized ZT411 drivers use direct ZPL RAW output through the selected Windows queue. This explicitly sets the saved stock's width/height in native dots and quantity one per expanded page, avoiding the screenshot's legacy EPL driver **104 × 50.8 mm** preset when the layout is **100 × 70 mm**. ZT411 max printable width is **104 mm**. Printer language must accept ZPL; matching physical media and sensor calibration still matter. Printer selection and the optional Windows compatibility preference are remembered locally.

Other printers, or **Advanced → Use Windows driver…**, use WPF `PrintDocument`. The app requests the saved paper dimensions, portrait, actual size, one copy, one page per sheet, one-sided and monochrome. A merged driver's known paper-size/scaling/copy/N-up/duplex conflict blocks with a short correction. Unreported settings are Advanced diagnostics, not a repeated confirmation. Standard Print Schema `Custom` X/Y and Fit options remain inspected—not just the X-only `PageScalingFactor` getter.

A driver printable-area guide is opt-in and only uses real finite reported margins on matching stock. Unknown boundaries are not invented. Direct ZT411 output does not use the legacy driver's margins or scaling. Neither path verifies loaded stock, hardware completion or roll sensing. **Sent** means spool submission, not physical completion.

Cancel before direct submission sends nothing. Cancel/failure after submission attempts to abort the job, but some labels may already have printed. There is no automatic retry/fallback. Native RAW writes run off the UI thread; Windows-driver submission still follows WPF's synchronous API, including any driver save dialog.

## Research/API references

- [ZT411 specifications and command research](ZT411_PRINTING.md#sources-researched-2026-10-15).
- [Microsoft PrintDialog](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/documents/how-to-display-print-dialog): selection versus `PrintDocument` submission.
- [WPF PrintDialog source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/PrintDialog.cs): paginator/ticket passed to XPS writer.
- [PrintTicket implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/ReachFramework/PrintConfig/PrtTicket_Public_Simple.cs) and [PageScaling](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/ReachFramework/PrintConfig/PageScaling.cs): clone, copy, N-up, CustomSquare/Custom X/Y behavior.
- [PageImageableArea](https://learn.microsoft.com/en-us/dotnet/api/system.printing.printcapabilities.pageimageablearea?view=windowsdesktop-8.0): nullable origin/extents in DIP.
- [DocumentPage](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Documents/DocumentPage.cs): disposable temporary page visuals.

## Verification

This sandbox has **no .NET SDK or Windows runtime**. Static syntax/XML/event-wiring/diff checks are possible; compilation, executable regression tests, native dialogs and physical output remain unverified. No test print has been sent. The workflow remains an inactive template as requested.

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
$env:OCEANGLASS_VISUAL_REVIEW="$PWD/artifacts/visual-review"
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Tests cover original serial/pane/copy sequences, immutable snapshots, current/all/range selection, standalone sample jobs, live/job format and inner-copy synchronization, 300 × 300 glass versus unchanged paper dimensions, sample isolation from incomplete drafts, stale/busy states, stock changes, Advanced/guide defaults, raster/preview equality, logo placement, native dot grids, graphic-band byte counts and partial/cancelled writes. Tests never send a physical print.

On Windows, check first printer selection/cancel, remembered queue after restart, Printer setup alone sending nothing, missing/offline printers, changed stock, invalid width, exact current/all/range counts, mixed SGU/DGU and Normal formats, 100/150/200% display scaling, PDF driver dialogs, and cancel before/during a large direct job. Measure a physical 100 × 70 mm label and validate the printer's language, media sensor and calibration before a production run.

### v0.16.0 sample acceptance

On an empty sheet, select SGU then DGU under Sticker format. Prepare preview with blank Order/Customer: no entry dialog or cutting-row error should appear. Check the exact TYPE strings above and 300 × 300 glass dimensions in live, prepared and Studio previews. With the DGU sample, choose INNER and copy 2: the prepared job should select page 6 (`1/2`), including after preparing again. Turn Qty copies off: both views should show inner copy 1 (page 2 of 2). Change sample formats with the job open: it updates without a separate refresh or sending anything. Check sample current/all/range printing only on matching physical stock. Samples must not alter Excel exports, cutting totals, row count or dirty-work state. Switching to an incomplete real row must invalidate the old sample and retain production validation. Windows execution/physical output remain unverified in this sandbox.
