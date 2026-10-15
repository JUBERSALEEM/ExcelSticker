# Direct printing and printer discovery — v0.18.0

## Normal workflow

1. Open **Stickers / printing**. The printer selector loads installed Windows queues (USB, local network/TCP-IP/WSD and connected shared printers). Select the exact printer to use. It is remembered; its name is also visible beside the main toolbar's print button.
2. Select a real sticker or an SGU/DGU sample. Click **Print sticker**, on the toolbar or live card, or press **Ctrl+P**. This captures **one displayed label**, including the existing copy index, layout, logo and physical paper size. It does not require or open the paginated print preview.
3. **Print all (N)** captures the sample/production batch using the current scope, pane filter and quantity options, and sends it directly. Incomplete production batches still require correction; a valid individual live sticker can print independently.

No confirmation checklist, hidden automatic printer choice, or third-party print page. The existing **Preview batch (optional)** / page-range controls remain optional. They use the same explicitly selected destination. A missing saved Windows queue is not silently replaced by the default printer.

Status names the destination while printing, then reports **Sent**. That means submission to Windows/the TCP connection succeeded, **not** verified physical completion. Errors appear inline next to that printer. There is no automatic retry or fallback, which could duplicate labels after a partial submission. **Cancel printing** cannot undo stickers already printed.

## Finding printers

### Installed USB and network printers

**Refresh printers** uses Win32 `EnumPrintersW`, flags `PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS`, level 4. This reads cached queue names/shared attributes without requesting driver/port metadata from every offline remote server. The list does not guess USB ports or online status; an unplugged printer can still be listed. Driver/ticket metadata is obtained only for the explicitly selected queue at submission.

USB devices need an installed Windows printer driver. **Add a Windows / USB printer…** opens Windows Printers & scanners; it does not install a driver silently. Refresh afterward. **Windows printer preferences…** opens preferences for the selected queue only. These setup actions never submit a job.

Discovery is off the WPF thread. A 12-second UI wait is bounded, while a shared in-progress native scan is retained: refreshing again cannot spawn duplicate native RPC calls that are still blocked. Selecting another destination is explicit. Completed discovery does not change the destination of an in-flight print.

### Local network scan / direct IP

Under **Network printer / connection settings**:

- **Scan local network** checks TCP port 9100 on nearby private IPv4 addresses. It only uses active wired/Wi-Fi interfaces; each search stays within the interface subnet and its local /24. Total cap: 1,024 candidates, 24 concurrent connections, 400 ms each, 15-second overall UI cancellation. Public Internet, hostnames, IPv6 and whole large subnets are not scanned. A VPN/private address outside the nearby range can be entered explicitly.
- Scanning only opens/closes connections; it sends **no print data or test pages**. An open port is listed as an **unidentified candidate**, not a verified printer. A closed port does not prove there is no printer; IPP-only and other devices should use Windows discovery/installation.
- Enter/select an IP and click **Check & use IP**. Read-only Zebra SGD queries request `device.product_name`, `head.resolution.in_dpi`, `device.languages` and `device.unique_id`. No settings are changed. Only a reported ZT411 with supported resolution, an ID and a ZPL-compatible language is accepted for direct TCP printing. Unknown devices, other models, missing resolution and EPL-only mode are rejected rather than guessed. Use their installed Windows queue instead.
- Every network print rechecks model, ID, resolution and language on the **same connection that receives the job**. The bounded raster spool file is prepared first, so large jobs do not hold a printer connection idle. If an IP now identifies a different device or head, no sticker data is sent. This is reported device identity, not cryptographic authentication; use a trusted LAN/VPN, not an exposed public print port.
- Connection/identity checking has a 12-second deadline; each data write has a 10-second deadline. Cancellation closes the connection. Failure after bytes have been sent may leave some output already printed; the app does not resend it automatically.

A remembered network destination can be used independently of Windows queue discovery. Its persisted identity is **rechecked**, not treated as current proof of availability.

## Printing architecture

- `LiveStickerPaginator` is an immutable, **one-page** snapshot of the actual `LabelView`, with no quantity-plan rebuild. Copy `7/4` stays `7/4`, even if the user navigates elsewhere afterward.
- `CapturePrintBatch()` expands the existing pane/copy plan without loading or showing `PrintJobPreview`. Both direct actions work before any prepared job exists. Current printing does not depend on unrelated invalid cutting rows or a stale optional preview.
- `PrinterService` owns the selected destination and dispatches to the Windows queue or a verified LAN ZT411. Selection/checking/discovery never invokes send.
- `PrinterDiscovery` uses Win32 queue enumeration and bounded TCP reachability probing; no WPF `PrintQueue` objects cross discovery threads.
- `StickerPrinter.SelectQueue` opens the exact selected queue without `PrintDialog.ShowDialog`. All installed queues use the Windows driver, including EPL-named Zebra queues. There is no automatic raw ZPL or driver-mode choice. Preferences are refreshed before submission.
- Larger carrier paper is accepted at actual size; too-small paper and scaling/copy/N-up/duplex conflicts stop with an inline correction. No arbitrary paper-size equality gate or automatic shrinking.
- `ZebraPrintJob` prepares verified direct-TCP output one raster at a time, preserving order, width/height, logos and quantity one. Temporary data is limited to 512 MiB and removed in `finally`.
- Network I/O is asynchronous. Selected-queue inspection and WPF/Windows-driver submission still use synchronous APIs and can block or show driver UI such as a PDF save dialog.
- Destination preferences are local in `%LOCALAPPDATA%/OceanGlass/print-destination.json` (or the data-directory override). Legacy `printer.json` queue names are still read, but its old RAW/Windows mode flag is ignored. Refresh preserves all verified network entries.
- Diagnostics log destination/route, page count, requested stock, validated media/orientation and submission result. See [packaging and audit](SINGLE_EXE_AND_PRINT_AUDIT.md) for failure evidence and remaining limitations.

The sample glass size is still **300 × 300 mm**. Label paper remains the saved stock (default **100 × 70 mm**); ZT411 printable width remains capped at **104 mm**.

## Verification and limitations

This sandbox has no Windows runtime/printer access or .NET SDK. **No claim of a successful Windows build, USB discovery, actual LAN discovery or physical print is made.** Static C# syntax, XAML/event wiring and diff checks are not hardware tests. Regression sources cover single-page/copy snapshot preservation without a prepared job, direct batch capture, isolation from cutting data, private-address validation, subnet scan bounds, unsupported network identity/language and changed-device rejection. They do not submit jobs or probe a LAN. The application window's normal read-only Windows-queue enumeration may run when WPF tests show it.

Run on Windows:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

Manual acceptance: select a USB ZT411; verify the visible queue name; print one live sample while the optional preview has never been opened; test copy `7/4`, independent inner pane and a small Print all batch. Unplug the printer and check that status/submission is not mistaken for physical completion. Check unknown/missing queues without fallback. On a trusted LAN, verify scan candidates are not auto-selected, then Check & use IP, print one 100 × 70 mm label and measure it. Test wrong IP, a non-Zebra port-9100 listener, unsupported/EPL-only language, changed ID/head, cancellation/timeout before and after submission, and restart with a remembered network printer while the Windows spooler is unavailable. Inspect the real queue/output before retrying any interrupted job.

## References

- [Microsoft EnumPrinters](https://learn.microsoft.com/en-us/windows/win32/printdocs/enumprinters): flags, level-4 cached enumeration and blocking RPC considerations.
- [Microsoft PRINTER_INFO_4](https://learn.microsoft.com/en-us/windows/win32/printdocs/printer-info-4): native name/server/attributes layout.
- [Zebra resolution query](https://support.zebra.com/article/000032831): read-only `head.resolution.in_dpi` query and native dot grids.
- [Zebra SGD commands](https://docs.zebra.com/us/en/printers/software/zpl-pg/c-sgd-printer-commands.html): product, language and unique-ID variables.
- [Zebra command languages](https://docs.zebra.com/us/en/printers/industrial/zt211-industrial-printer-user-guide/c-zt4x1-configuration/r-changing-printer-settings/c-zt211-zt111-other-ways-to-change-printer-settings/r-zt2x0-language-settings.html): language variable/values; supported values vary by model/firmware, so the actual device is queried.
- [ZT411 physical sizing / rendering](ZT411_PRINTING.md).
