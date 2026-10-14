# Ocean Glass — Zebra ZT411 Label Maker

A native **C# / WPF Windows desktop application**, recreated from the supplied screenshot. No browser, WebView, Excel installation, or third-party application packages are required. The original HTML was unavailable, so behavior not visible in the screenshot is explicitly defined below.

## Open and run on Windows

Requirements: Windows 10/11, .NET 8 SDK, and (for physical printing) the installed Zebra ZT411 Windows driver. In Visual Studio 2022, install the **.NET desktop development** workload.

1. Open `ExcelSticker.sln`.
2. Set **ExcelSticker.Wpf** as the startup project.
3. Press **F5**.

Or use PowerShell in the repository root:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project src/ExcelSticker.Wpf
```

WPF only runs on Windows. The .NET SDK is not installed in the development sandbox, so the build and executable tests have **not been run there**. The included Windows CI workflow template is inactive until restored to `.github/workflows/` (see below). Once activated, it builds, runs checks, and publishes the application when pushed; no successful CI result is claimed until it runs.

## Features and behavior

- Manual entry: order, quantity, glass type, reference, dimensions in millimeters, customer, and optional job number.
- Sample form values match the reference; **the batch starts empty**.
- Add, edit/save, cancel edit, remove, and clear batch rows. Identical rows remain independent.
- Selecting a batch row previews it. **Update preview** shows the form without changing the batch.
- Preview and printing share the same native **100 × 70 mm** layout, including the company heading, contact block, order/quantity, glass, size, reference, optional job, and underlined customer name.
- Long single-line fields shrink to fit instead of overflowing. Check readability before printing very long values.
- UTF-8 CSV import appends to the batch. A malformed file or invalid row rejects the entire import without adding partial data.
- Download a blank Excel-compatible UTF-8 BOM CSV template.
- By default, **one label per batch row**; Qty is a printed field. Select **Print Qty copies of each row** to repeat pages instead.
- Windows printer selection; custom stock validation; pages generated on demand.
- Batch data lives only in memory. Clearing, removing, and closing a populated batch require confirmation. There is no batch save/reopen or XLSX import yet.

## Import from Excel

Download the template in the application, or use `examples/sample-labels.csv` as a working example:

```csv
OrderNo,Qty,GlassType,ReferenceNo,Width,Height,CustomerName,JobNo
7494/71,1,6MM CLEAR LOW E,W6* F5,316.0,800.0,FUJSENG,
```

Keep all eight headings, including optional columns. Column order is flexible; capitalization and spaces in headings are ignored. Extra columns are ignored, but headings must be unique. `ReferenceNo` and `JobNo` values may be blank. Order, glass type, and customer are required.

Use **Save As → CSV UTF-8 (Comma delimited)** in Excel. Delimiters must be commas, not semicolons; decimal separators must be dots, with no thousands separators. Standard quoted commas and escaped double quotes are supported. Text fields cannot contain line breaks. Format order/reference/job columns as **Text** in Excel to preserve leading zeros and prevent automatic date conversion.

Limits: Qty 1–10,000; dimensions greater than zero and at most 100,000 mm; 10 MB per CSV; 10,000 batch rows; 10,000 pages per print job. Dimensions display to one decimal place, as in the reference. Text lengths are limited to keep labels usable.

## Company details

Edit `src/ExcelSticker.Wpf/company.json` before building, or edit `company.json` beside the published executable and restart the app. It controls the company name, address, phone, website, and email. **These sample details were transcribed from a small screenshot; verify all details, especially the telephone number, before production printing.** The program reports invalid JSON and falls back to sample values. No data is sent to an external service.

## Zebra printing setup

1. Install the Zebra ZT411 Windows driver and load/calibrate your label stock.
2. In Windows **Printing Preferences**, configure stock to **100 mm wide × 70 mm high**, portrait, with no scaling/fit-to-page. Use the correct resolution for your printer (203/300/600 dpi); WPF uses physical units, not hard-coded printer pixels.
3. Add a single test label, select **Print labels**, and choose the Zebra queue in the Windows dialog.
4. Keep driver **Copies = 1**. Use the app's Qty copies checkbox for repeated labels.
5. Check the printed size, orientation, margins, and readability before sending a batch.

The application requests exact media dimensions and stops if the driver does not report a matching stock size/orientation. It does not silently scale onto A4/Letter. Some drivers require a named custom stock configured ahead of time. The printed layout has internal whitespace, but driver offsets/nonprintable margins still require physical calibration. This app uses the Windows driver, **not raw ZPL**. A successful submission means the job reached the Windows spooler, not that the printer physically completed it. Printer hardware has not been tested in the development sandbox.

Only saved batch rows are printed, not unadded form/preview values. Complete or cancel an edit before printing. Printed batches remain in the list until cleared.

## Create a distributable Windows application

```powershell
dotnet publish src/ExcelSticker.Wpf -c Release -r win-x64 --self-contained true -o artifacts/OceanGlass-LabelMaker
```

Copy the **entire output folder** to a Windows x64 machine and run `OceanGlass.LabelMaker.exe`. This self-contained build does not require a separate .NET installation. It is a portable folder, not an MSI installer.

The template `workflow-templates/windows-build.yml` performs the same publish step once activated. After activation and a successful build, download `OceanGlass-LabelMaker-win-x64` from the workflow's artifacts and extract the complete ZIP.

### Activate the GitHub Actions workflow

The workflow is stored in `workflow-templates/` because the current GitHub connection does not have permission to create or update active workflows. **GitHub Actions will not run this template in its current location.**

To activate it, move it into **`.github/workflows/` beneath the project root** (the folder containing `ExcelSticker.sln`), not directly beside the solution file. From PowerShell in the project root, run:

```powershell
New-Item -ItemType Directory -Force -Path .github/workflows | Out-Null
git mv workflow-templates/windows-build.yml .github/workflows/windows-build.yml
git add .github/workflows/windows-build.yml workflow-templates
git commit -m "Enable Windows build workflow"
git push origin arena/4ed12585-excelsticker
```

Run the push using your own GitHub connection with permission to modify workflows; the restricted Arena connection will still reject an active workflow. The resulting location must be:

```text
ExcelSticker/
├── ExcelSticker.sln
└── .github/
    └── workflows/
        └── windows-build.yml
```

Once pushed, open the repository's **Actions** tab and look for **Windows build and package**. It runs on pushes and pull requests. Manual **Run workflow** becomes available when the workflow is on the repository's default branch. Restoring the file does not itself merge this working branch into `main`.

## Tests

The regression runners have no third-party test dependencies; failures return a nonzero exit code. Run them using `dotnet run` (not `dotnet test`):

```powershell
# Can run on any OS with the .NET 8 SDK
dotnet run --project tests/ExcelSticker.Core.Tests -c Release

# Windows only; WPF startup, batch addition, pagination, native-size rendering
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release
```

Core checks cover validation, invariant decimal parsing, quoted CSV, BOM, Unicode, reordered headers, invalid/duplicate headers, malformed rows, atomic rejection, and batch limits. Windows smoke checks do not send anything to a printer. Manual testing is still needed for the print dialog, actual Zebra output, and high-DPI/multiple-monitor appearance.

## Layout

- `src/ExcelSticker.Core`: label model, validation, CSV parser.
- `src/ExcelSticker.Wpf`: native desktop UI, label renderer, company configuration, Windows printing.
- `tests/`: dependency-free regression and Windows smoke runners.
- `examples/`: importable sample data.
- `workflow-templates/`: inactive Windows CI workflow; move it to `.github/workflows/` to enable it.
