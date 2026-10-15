# Single EXE and print-path audit — v0.18.0

## Package on Windows

From the repository root, with a stable .NET SDK 8 or newer:

```powershell
dotnet publish .\src\ExcelSticker.Wpf\ExcelSticker.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o .\artifacts\single-exe
```

Output: **`artifacts\single-exe\LableStudio.exe`**. Copy that EXE to the factory Windows x64 PC; no separately installed .NET runtime is needed. Do **not** trim WPF. A Windows printer driver is still needed for installed USB/network queues. Native runtime components may extract to the user's temporary directory at launch; “single file” means one distribution file, not zero runtime files.

Alternatively run **`Publish-SingleExe.cmd`**. It publishes into a fresh `artifacts/single-exe-win-x64-<id>` folder, checks that the only file is `LableStudio.exe`, and prints its SHA256. Fresh staging avoids stale DLLs/JSON/PDBs from a previous publish. `-Runtime win-arm64` selects native ARM64 Windows. A Visual Studio `SingleExe.pubxml` publish profile is also included. The full command/helper explicitly applies the no-symbol properties to referenced projects too.

`src/ExcelSticker.Wpf/company.json` is now compiled into the EXE. Edit it **before publishing** to bundle your company details. An optional `company.json` beside the EXE overrides the bundled version, retaining ordinary editable installations. Invalid overrides produce an error instead of silently substituting another company. Single-file publish no longer requires that sidecar. Logs show `Environment.ProcessPath`, not the empty assembly location common to bundled assemblies.

Existing layouts, imported logos and themes remain in the local settings directory; they are **not automatically baked into a release EXE**. On another PC, export/import the layout library from Sticker Studio to carry your customized patterns and embedded logos. The EXE does not overwrite existing user settings.

## Concrete audit findings and changes

These are source-code findings, **not a diagnosis confirmed on your printer**:

| Finding | Correction |
| --- | --- |
| Recognizing “ZT411” in an installed driver name enabled raw ZPL, even for an EPL queue. A driver/model name does not verify the active device language; RAW bypasses translation. | Every installed Windows queue now uses its Windows driver. Removed Windows RAW transport and driver-mode toggles. ZPL is sent only through the separately verified direct-IP path. |
| A known media size had to equal the sticker size, rejecting larger paper/PDF carriers. | Actual-size stickers may use larger media. Too-small media, non-100% scaling, extra driver copies, N-up and duplex remain blocked. Size errors report actual and requested dimensions. |
| Level-2 queue enumeration asked for metadata from all queues, potentially waiting on unreachable shared servers. | Level-4 cached installed-name enumeration; driver/ticket metadata is queried only for the selected queue at submission. Discovery does not claim online status or USB-port identification. |
| Company JSON was loose content; copying only the EXE could silently fall back to hardcoded company details. | Embedded build-time profile, optional adjacent override, and conditional omission of the publish sidecar. |
| Four small contact-detail rows crowded the default 100 × 70 label; the glass description had only a single short row. | An 11-element default instead of 15: company, format/copy, wrapped glass description, large dimensions, order/serial, reference/pane/job and customer. Contact fields are still available in the editor. |
| Resetting the format also discarded imported logos. | Explicit **Simple pattern (keep logos)** action preserves paper size and every logo's data, position, size, visibility and rotation; one Undo restores the prior design. Only the selected format changes. |
| “Sent” did not reveal enough to diagnose routing/settings. | Logs now record destination, route, page count, requested size, validated driver media/orientation, block reason and submission return. “Sent” still does not mean physical completion. |

## Simple operator workflow

1. Select the named Windows printer once (USB or installed network queue).
2. Choose SGU/DGU/sample or the real cutting entry; click **Print sticker**. It prints the displayed copy, not an independently regenerated first copy. No batch preview or confirmation is required.
3. Use **Print all** for the batch. Numbering/scope settings are under collapsed **Batch options / numbering**.

**Existing saved designs deliberately stay unchanged.** To simplify one, open **Logo / sticker layout → choose format → Simple pattern (keep logos) → Save all formats**. Repeat for other formats if desired. Custom-positioned logos remain where you put them; inspect any overlap warnings before saving. No automatic migration erases your design.

If the driver still reports **104 × 50.8 mm** for a **100 × 70 mm** sticker, set **100 × 70 mm** once in **Windows printer preferences** and load/calibrate matching stock. The 50.8 mm value is a driver preset, not the ZT411's maximum label length. The app does not shrink 70 mm into 50.8 mm or pretend software can change the loaded roll. For non-Zebra/PDF printers, larger paper can carry the sticker at 100%, but actual imageable margins remain driver-dependent.

## Verification status and next evidence

No .NET SDK, Windows runtime or physical printer is available in this sandbox. **Compilation, automated executable tests, single-file launch and hardware printing have not been run here.** C# parsing/XML/event wiring and diff checks are static checks only. New regression sources cover larger/undersized/rotated media, simple defaults, stock/logo preservation plus Undo, embedded profiles and overrides. Existing samples, current-copy, batch/range and raster tests remain.

Run on Windows before distributing:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
.\Publish-SingleExe.cmd
```

Then:
- Copy **only** the resulting EXE to an empty folder and launch it. Check **v0.18.0**, company name, icon, SGU/DGU samples, saved layouts and settings. Test on a Windows PC without a separately installed .NET runtime.
- Select the installed EPL/ZPL queue, set real matching paper, and print one sample with no optional preview open. Confirm one correctly sized physical label, legible DGU wrapping and logo. Check copy `7/4`, then a small batch. Never infer hardware success from spool acceptance.
- Use a PDF/large-paper driver at 100%; it must not fail merely because paper exceeds 100 × 70. The PDF driver's own save dialog can still appear.
- Repeat with undersized media: no job should submit, and the status should identify both sizes. Offline queues must not trigger another printer or automatic retry.
- On the existing installation, exercise Simple pattern / Undo / Save and reopen; ensure no logo, custom stock or unrelated format was lost.

If printing still fails, provide the **exact inline status**, the **selected printer/driver name**, and the corresponding block from `%LOCALAPPDATA%\OceanGlass\logs\app.log` (or the diagnostics badge's log path). Review local paths/device IDs before sharing. Confirm whether the job appears in the Windows queue and whether a Windows printer test page prints. This distinguishes application rejection, queue/driver problems and hardware/media problems rather than guessing again.

Known limits: selected-queue metadata and WPF submission are synchronous and can block on a troublesome driver. Cancellation cannot interrupt every Windows driver call or undo output. Direct-LAN identity queries are firmware-dependent and still need real-device testing. Packaging does not fix a driver, printer language, offline spooler or stock calibration issue.
