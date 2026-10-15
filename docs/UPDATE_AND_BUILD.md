# Get the correct branch and run the correct build

## One-time recovery from the old build

1. Close every Ocean Glass window. Close Visual Studio before updating files.
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
6. The app should now show **v0.5.0** with its build configuration and Git commit in the title/header. Click the diagnostics badge for the source branch, compilation UTC time, loaded assembly, application folder, settings folder, and log path. Select the diagnostic text and use Ctrl+C to copy it.

After this initial recovery, **`Update-Checkout.cmd`** performs a safe fetch/fast-forward update. It refuses another branch, local changes, or divergent history rather than resetting/deleting anything. You do not need to clone repeatedly.

If a new clone is necessary, choose a **new empty folder**, not your existing checkout:

```powershell
git clone --branch arena/4ed12585-excelsticker --single-branch https://github.com/JUBERSALEEM/ExcelSticker.git ExcelSticker-clean
cd ExcelSticker-clean
.\Build-and-Run.cmd
```

The helper does not install SDKs or sign you into GitHub. Install a stable **.NET SDK 8.0.4xx** and Visual Studio 2022 **17.11+** with the **.NET desktop development** workload (`.vsconfig`). `global.json` deliberately selects this feature band; newer major SDKs alone do not satisfy it. Running the app/tests also needs **Windows Desktop Runtime 8**. If fetch fails, inspect the remote/network and sign into GitHub through Visual Studio. Do not share credentials.

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
