# Simple printing and ZT411 sizing — v0.15.0

**v0.17.0 workflow update:** [Print directly from the live sticker and choose/discover the printer](PRINTER_SERVICE.md). The paginated preview is optional. Installed Windows queues and verified direct-IP ZT411 connections share this sizing/graphics implementation.

## For the operator

1. Prepare the sticker preview; select the desired format (Automatic, SGU, DGU or Normal Job Order) and quantity options.
2. **Print current** prints the sticker on screen. **Print all (N)** prints all N stickers in that prepared batch. **Print range** remains available.
3. Choose a printer in the visible selector above the live card. The app remembers it. **Print sticker** sends one displayed label without preparing a preview. **Printer setup…** focuses the selector without sending a job.
4. Status reads “Printing current sticker…” / “Printing N stickers…”, then “Sent … to [printer].” Sent means the Windows spooler accepted the job, not that hardware completion was verified.

The ordinary screen shows the sticker type, sticker dimensions, printer and buttons. Driver diagnostics, optional boundary guides and Windows-driver compatibility mode are under the collapsed **Advanced** section. In v0.16.0, standalone **SAMPLE** jobs can be test printed without entering cutting data. Open sample jobs refresh when sample format/source/Qty options change; production data changes still require **Refresh preview**, so old cutting work cannot print as new work.

## Why the screenshot showed 104 × 50.8 mm

The screenshot's **ZDesigner ZT411-203dpi EPL** queue reported a **104 × 50.8 mm Windows paper preset**, while the layout was **100 × 70 mm**. That reported height is not a fixed limit of the ZT411. Shrinking 70 mm content into 50.8 mm or simply dismissing the warning would not fix physical sizing.

A recognized ZT411 driver now uses direct ZPL raster output through the existing Windows queue, including when its driver name ends in EPL. The printer must accept ZPL (or automatic language recognition); the app does not silently change its language. If it is deliberately locked to EPL, enable ZPL on the printer, or select **Use Windows driver…** in Advanced and set matching stock in that driver's properties. Queue submission alone cannot detect an incompatible active printer language.

Other printers continue to use WPF/Windows printing. Detection uses the **installed driver model**, never just a user-editable printer queue name. The resolution must be known from the model name or ticket; conflicting/unsupported reported resolutions block direct output rather than guessing. The chosen printer is remembered in `%LOCALAPPDATA%/OceanGlass/printer.json` (or the existing `OCEANGLASS_DATA_DIR` override). A missing saved queue asks for selection again; it does not silently redirect labels to another printer.

## Zebra specifications used

Official ZT411 technical specifications [1]:

| Property | ZT411 |
|---|---|
| Maximum **printable** width | **104 mm**, for 203/300/600 dpi |
| Native dot grids | 203 dpi: **8 dots/mm**; 300 dpi: **12 dots/mm**; 600 dpi: **24 dots/mm** |
| Media web width, including liner | 25.4–114 mm tear-off/cutter; 25.4–108 mm peel/rewind |
| Minimum label length | 12.7 mm tear-off/peel/rewind; 25.4 mm cutter |
| Maximum non-continuous label + liner length | 991 mm at 203/300 dpi; 508 mm at 600 dpi |
| Languages | ZPL / ZPL II; EPL available at 203 dpi only |

These are different measurements: **114 mm media width does not mean 114 mm printable width**, and continuous-media print-length specifications are not die-cut label limits. The app retains its general 20–300 mm stock editor; ZT411 printing rejects widths over 104 mm. The loaded liner, gap/mark, cutter mode, ribbon and calibration must still be appropriate to the selected label. The app's minimum height of 20 mm is not a claim that 20 mm labels work in cutter mode.

For the default **100 × 70 mm** layout:

| Head | Width × height in printer dots |
|---|---|
| 203 dpi / 8 dots/mm | **800 × 560** |
| 300 dpi / 12 dots/mm | **1200 × 840** |
| 600 dpi / 24 dots/mm | **2400 × 1680** |

Native dots/mm are used, not nominal dpi divided by 25.4. Fractional sizes round to the nearest dot (half away from zero); physical dimensions can differ by at most half a dot per axis.

## Implementation and boundaries

- `LabelPaginator` still owns the immutable layout/company/label snapshot and already-expanded copy sequence. Current/range map to those original pages; no second quantity expansion.
- `ZebraRasterizer` renders the same unattached `LabelView` at 203.2 / 304.8 / 609.6 bitmap dpi, composites onto white, converts to gray and deterministically dithers to one bit. Logos, Unicode text, field placement, size and rotation come from the WPF renderer, not printer-resident fonts. The preview control supports this final monochrome raster when supplied a known profile. In the v0.17.0 direct workflow, the normal live view remains the same layout's vector artwork; output is rasterized from its immutable snapshot.
- Each label has its own `^XA…^XZ`. `^JMA` requests the native dot grid; matching `^MUD,200,200` parameters turn off format conversion. Normal orientation, mirror/reverse, home, shift and top offset are specified so prior formatting offsets do not move the raster. These are transient printer formatting settings, not a save to permanent configuration.
- `^PW` sets width in dots [2]. `^LLheight,Y` precedes the first field separator and explicitly applies the requested length to continuous **and gap/mark** media [3]. It does **not** switch media tracking to continuous. This setting can remain until another job changes it or the printer restarts; no `^JUS` permanent save is sent. The operator must load the matching roll and calibrate when changing stock.
- `^GFA` fields use MSB-first bytes, black=1, zero right-padding and two ASCII hex characters per byte. Tall/high-resolution images are banded at whole rows, each at most 99,999 bytes [4]. `^PQ1,0,0,N` sends one label per already-expanded page [5]. No user text is interpolated into printer commands.
- ZPL controls paper geometry and quantity, bypassing the EPL driver's old media preset, scaling, copy count, N-up and printable-area report. No guessed driver boundary is drawn for this path. Speed, darkness, ribbon/thermal method, media sensing mode and permanent calibration are not changed.
- The full selected ZPL batch is rendered **before** opening a spool job, one page at a time with UI yields, cancellation/stale checks and a 512 MiB temporary-file limit. The temp file is removed on success/error/cancellation; no 10,000-page bitmap cache. Too-large jobs ask for a smaller range rather than sending a partial selection.
- Windows RAW submission uses `OpenPrinterW → StartDocPrinterW (RAW) → StartPagePrinter → WritePrinter → EndPagePrinter → EndDocPrinter`, on a worker thread [6], with a separate Start/EndPage pair per label and recorded byte boundaries (no giant single Windows page). Partial writes are continued at the correct byte offset. Failure/cancellation attempts `AbortPrinter`, then closes the handle; there is **no automatic fallback or retry** that could duplicate already-printed labels. Cancellation after submission cannot promise zero labels. A blocking spooler call must return before cancellation can be observed.
- Generic/compatibility printing requests actual size, portrait, one copy, one page per sheet and one-sided output. Driver-validated known paper-size/scaling/copy/N-up/duplex conflicts block with a specific correction, not a confirmation checklist. Unreported vendor settings remain Advanced diagnostics. WPF's synchronous Windows submission can still block during driver UI (for example a PDF save dialog); the new asynchronous RAW transport applies only to ZT411.

## Verification and acceptance

**Not hardware-certified.** This development environment has no .NET SDK/Windows runtime or physical ZT411. Static syntax/XML/event-wiring and patch checks are not a successful build or printer test. The following regression sources were added, but must run on Windows before production:

- `ZebraLabelChecks`: model/resolution rejection, 8/12/24-dot dimensions, width limits, bit polarity/padding, stripe reassembly through maximum supported stock, invariant numeric commands, quantity, partial writes and cancellation/failure without retries.
- `ZebraPrintingChecks`: final preview/output bit equality at all three grids, logo polarity/placement, original `7/4` numbering, screen-zoom isolation, Windows fallback and stale-job protection.
- Updated existing preview/stock tests: print enabled before printer selection, Advanced collapsed, guide opt-in, unchanged current/all/range counts and no extra stock-approval gate.

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Physical checks: on the reported **ZT411-203dpi EPL** queue, leave the driver at 104 × 50.8 mm, load/calibrate actual **100 × 70 mm** stock with ZPL enabled, and print current on copy `7/4`. Verify one 100 × 70 mm layout, its logo/text and original number. Then test an outer-4/inner-2 batch (6 stickers), range 4–6 (3 stickers), custom supported stock and 104 mm width; 105+ mm must stop without spooling. Check continuous/gap/mark modes independently, 300/600 dpi hardware where available, remembered/missing queues, cancelled selection, offline queue, cancellation before/during RAW submission and Advanced Windows mode. Never infer physical success just from “Sent”.

## Sources researched 2026-10-15

1. [1](https://www.zebra.com/content/dam/zebra_dam/en/tech-specs/zt411-tech-specs-en-us.pdf) — official ZT411 specifications, native grids, printable/media dimensions and languages.
2. [2](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-pw.html) — print width in dots, hardware clamping.
3. [3](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-ll.html) — label length, second parameter Y for gap/mark, placement before first field separator, persistence until changed/restarted.
4. [4](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-gf.html) — graphic byte counts, 99,999-byte limits and ASCII hex.
5. [5](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-pq.html) — print quantity and replication.
6. [6](https://learn.microsoft.com/en-us/windows/win32/printdocs/writeprinter) — RAW settings in the printer language, blocking spooler writes.
7. [7](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-zpl-commands/r-zpl-mu.html) — matching base/desired parameters disable format conversion.
8. [8](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-zpl-adv-techn-advanced-techniques/r-zpl-adv-techn-set-dotsmillimeter.html) — native dot grids / `^JM`.
