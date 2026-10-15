# Get the correct branch and run the correct build

## One-time recovery from the old build

1. Close every LABLE STUDIO window (and any older Ocean Glass window). Close Visual Studio before updating files.
2. In the **existing checkout**, run:

   ```powershell
   git branch --show-current
   git remote -v
   git fetch origin arena/4ed12585-excelsticker
   git status --short
   ```

   The branch must be `arena/4ed12585-excelsticker` and the remote must be this repository. If you are on another branch, select the existing working branch in Visual Studio's **Git Repository** window first. Fetch downloads commits; it does not update the files you are building.
3. If there are local changes, inspect them before pulling. Commit/stash changes you need. If a solution edit is only unwanted IDE metadata, review it in Visual Studio Git Changes and undo **that file only**. Do not discard your source or `company.json` changes blindly.
4. Once clean, update:

   ```powershell
   git pull --ff-only origin arena/4ed12585-excelsticker
   git log -1 --oneline
   ```

5. Run **`Build-and-Run.cmd`** from the checkout. It cleans, rebuilds **ExcelSticker.sln**, runs the core and Windows smoke checks, and launches the exact resulting EXE **only if everything succeeds**. It refuses to kill a running app because its batch might contain unsaved data. It never launches a previous binary after failure.
6. The app should now show **v0.14.0** with its build configuration and Git commit in the title/header. Click the diagnostics badge for the source branch, compilation UTC time, loaded assembly, application folder, settings folder, and log path. Select the diagnostic text and use Ctrl+C to copy it.

After this initial recovery, **`Update-Checkout.cmd`** performs a safe fetch/fast-forward update. It refuses another branch, local changes, or divergent history rather than resetting/deleting anything. You do not need to clone repeatedly.

If a new clone is necessary, choose a **new empty folder**, not your existing checkout:

```powershell
git clone --branch arena/4ed12585-excelsticker --single-branch https://github.com/JUBERSALEEM/ExcelSticker.git ExcelSticker-clean
cd ExcelSticker-clean
.\Build-and-Run.cmd
```

The helper does not install SDKs or sign you into GitHub. Install a stable **.NET SDK 8 or newer**, with a compatible Visual Studio version and the **.NET desktop development** workload (`.vsconfig`). `global.json` prefers SDK 8 and permits newer stable major versions when needed; it no longer requires 8.0.4xx. Running the app/tests also needs **Windows Desktop Runtime 8**. If fetch fails, inspect the remote/network and sign into GitHub through Visual Studio. Do not share credentials.

## Why `.sln` / `.slnx` keeps appearing in changes

- **ExcelSticker.sln is the canonical solution.** Do not regenerate it with `dotnet new sln` on every clone.
- Modern SDK-project type identifiers, a stable solution GUID, the original project GUIDs, and explicit CRLF solution line endings reduce avoidable rewriting.
- Different Visual Studio versions can still rewrite their version metadata. This alone does **not** mean application functionality changed.
- Some newer IDE/SDK workflows create or convert a `.slnx` solution. A private `ExcelSticker.slnx` is ignored here; scripts always explicitly build **ExcelSticker.sln**. Open that file, not a converted copy or a duplicate checkout.
- No build step writes to the solution or project files. Generated assembly/build metadata goes into ignored `obj/` outputs.
- Do not commit `.vs/`, `.suo`, `bin/`, or `obj/` files.

In Visual Studio: **File → Open → Project/Solution → ExcelSticker.sln**, then right-click **ExcelSticker.Wpf → Set as Startup Project**. Do not start Core, Theming, or a test runner.

Under **Tools → Options → Projects and Solutions → Build and Run**, choose the equivalent of **Do not launch** when build errors occur. Never accept **Launch old version**. The previous `CS0029` prevented a new executable from being produced, so running an older successful build could make updates appear absent.

## Import troubleshooting

The Import button now opens a mapping preview after reading the file/sheet:

1. Choose `.xlsx`, UTF-8 `.csv`, or `.tsv`. Save changes in Excel first; unsaved Excel cells are not imported.
2. For multiple sheets, choose a worksheet.
3. Choose the header row (auto-suggested from the first 50 parsed rows). Title rows no longer force the whole worksheet to one column.
4. Check the source-column mapping. Duplicate headings are not silently guessed. Select different source columns explicitly.
5. Set the last included parsed row to exclude totals/notes/footer rows.
6. If OrderNo or CustomerName is absent, enter an **explicit file-wide fallback**. The app does not copy the sample customer/order into your file silently.
7. Comma, semicolon, and tab delimiters are detected. For comma decimal values, explicitly select the comma-decimal checkbox; thousands separators are not supported.
8. Click **Validate preview**, inspect the mapped labels, then **Import N entries**. Validation/cancel leaves the batch unchanged.

Both standard and Strict Open XML `.xlsx` are accepted. Legacy `.xls`, binary `.xlsb`, macro `.xlsm`, and encrypted files must be converted first. Formula cells need saved cached results. Map arbitrary vendor headings rather than renaming an entire workbook. The blank template remains the easiest starting point.

Clay button padding and scrollbar thumbs now have actual hit-test surfaces, so clicks/drags no longer depend on hitting text or a thin border.

## Diagnostics and verification

- Build logs: `artifacts/build.log` and `artifacts/build.binlog`.
- App errors reported through the normal error dialog are logged locally under `%LOCALAPPDATA%\OceanGlass\logs\app.log` (rotated at 2 MB).
- Theme/layout preferences live outside Git. Re-cloning does not reset those preferences, nor restore an in-memory batch.
- UI regression tests use a temporary settings directory and sample company, not your saved theme/layout/company details. They create off-screen windows to exercise real template layout and hit testing.
- `OCEANGLASS_DATA_DIR` is an optional settings/log-directory override used by these tests. Normal users should leave it unset.
- Build provenance needs git on PATH. Source ZIPs or machines without git show `unavailable` rather than inventing a commit. Local uncommitted edits can change the executable without changing the recorded commit.

**This Linux development sandbox has no .NET SDK or WPF runtime.** Static checks were performed here; the executable test runners, clean build, manual file import and printer behavior still require Windows verification. The Windows workflow remains an inactive template because the Arena GitHub connection cannot add active workflows. The local helper does not depend on GitHub Actions permissions.

If the issue persists, send the exact version/commit from the running app, the **first** build error or import message, and a small redacted workbook. Logs/binlogs can contain local paths: review them before sharing. A screenshot saying only “import does not work” cannot establish which workbook or binary is failing.

For the new compact workspace and reference-inspired material, select Theme Studio → Apply reference clay to entire page, then Save theme. See [Compact clay](COMPACT_CLAY.md). Existing palette choices are retained until you change them.

## NETSDK1141: SDK resolution error

The earlier `global.json` used `8.0.400` plus `latestPatch`. That rejected a machine with SDK **10.0.400** but no SDK 8.0.4xx. This was an overly restrictive repository setting, not a problem in `Microsoft.NET.Sdk.DefaultItems.targets`. Do not edit installed SDK targets.

The corrected policy is:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "major",
    "allowPrerelease": false
  }
}
```

`major` preserves preference for an available compatible .NET 8 SDK, then allows advancing to a newer major. It is not `latestMajor`, which would always prefer the highest installed major. No project is retargeted to .NET 10, and runtime major-version roll-forward is not enabled.

Close Visual Studio, preserve/review local changes, pull the current branch, then run from the checkout:

```powershell
dotnet --list-sdks
dotnet --version
.\Build-and-Run.cmd
```

If an old `global.json` still blocks your initial update, apply the JSON above to that file locally. If you also have the old build helper, update it before using `Build-and-Run.cmd`; the old helper separately rejected SDK 10. Do not discard other local edits just to pull.

If CLI resolution works but Visual Studio still cannot load the projects, reopen the solution and check whether that Visual Studio release supports the **selected SDK feature band**. Update the IDE, or install a supported .NET 8 SDK alongside SDK 10. Installing only Windows Desktop Runtime 8 enables running net8 applications; it does not install SDK 8 or fix an IDE/SDK compatibility mismatch.

The build helper accepts stable SDK major versions >= 8, but still checks for **Windows Desktop Runtime 8** before running the app/tests. That is a distinct requirement and not the NETSDK1141 pinning error. SDK selection was statically reviewed here; this environment cannot execute `dotnet` or Visual Studio.

The v0.7.0 geometry/lighting repair adds rectangular table and paper frames, bounded control radii, and a full-page palette. See [Rectangular clay](RECTANGULAR_CLAY.md). The SDK fallback introduced in v0.6.1 remains unchanged.

## v0.8 sculpted material verification

After rebuilding, use **Theme Studio → Apply coral 3D reference to entire app → Save theme**. See [Sculpted clay](SCULPTED_CLAY.md) for new material controls, compatibility and the Windows visual acceptance checklist. Older saved themes retain their chosen palette and bevel width.

## v0.9 reference style pack

After rebuilding, open **Theme Studio → Six reference styles**, select a tile, and **Save theme**. Older saved palettes remain active until explicitly changed. See [Reference styles](REFERENCE_STYLES.md) for all six mappings and Windows screenshot/test instructions.

## v0.10 application rename and factory default

The rebuilt executable is **LableStudio.exe**. Update old shortcuts; the build helper launches the new filename. New installations start in **Industrial flat**. Existing preferences remain in the compatibility folder and can be reset explicitly with **Theme Studio → Use factory flat theme → Save theme**. See [Branding and factory default](BRANDING_AND_FACTORY.md) for icon integration and Windows checks.

## v0.11 cutting-first workspace

After rebuilding, the main page is the cutting list. **Stickers / printing** expands the live sticker and printer controls inline. Use the new cutting CSV template to retain prices; legacy label imports still have their mapping preview. See [Cutting workspace](CUTTING_WORKSPACE.md) for paste formats, calculation rules and verification.

## v0.11.1 glass serial / copy correction

Rebuild with **Build-and-Run.cmd**. Stock stickers now show a separate glass **SR NO** and only **Sr No / copy** in QTY (`1/1` through `1/4` for row 1, Qty 4). Older stock layouts upgrade automatically in memory. A full custom layout without space for `GlassSrNo` warns instead of overwriting artwork: add the field or reset that format in Sticker Studio, then **Save all formats**. Native Core/WPF tests still require a Windows build; only static checks were executable in the coding sandbox.

## v0.12 five starter rows / formatted Excel persistence

After rebuilding, Home has five editable blank rows. Single-click selects an input value for replacement; selection no longer fills the edit cell with Windows blue. **Save Excel / Ctrl+S** and **Blank Excel template** write the grouped `.xlsx` page. Reopen it through **Import Excel / CSV**; complete workbook import replaces the current list after confirmation. Advanced flat CSV export moved under **Stickers / printing**. See [Cutting workspace](CUTTING_WORKSPACE.md) for workbook round-trip/hidden-data details. Run the Core and WPF checks plus a real Excel edit/save/open test before production use; native execution was unavailable in the coding sandbox.
