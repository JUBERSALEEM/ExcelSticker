# LABLE STUDIO branding and factory appearance — v0.10.0

## Application name

The requested spelling **LABLE STUDIO** is intentional. The window title, header, dialogs, diagnostics, Windows file metadata and print-job title use it. The built executable is now **`LableStudio.exe`**. Project/namespace names and the canonical `ExcelSticker.sln` stay stable to avoid unnecessary solution and source changes.

The build helper launches the new executable and checks for both the new process and an older `OceanGlass.LabelMaker` process from this checkout. It never terminates either process automatically. Portable publishing uses `artifacts/LableStudio/`; the inactive workflow template produces `LableStudio-win-x64` if manually restored later. The workflow remains inactive.

**Not renamed:** the company printed on labels. `company.json` and `CompanyProfile` still describe the configured factory (the sample is Ocean Glass Industry LLC). Application branding is independent of customer/company print data.

Existing preferences remain in `%LOCALAPPDATA%\OceanGlass\` and the `OCEANGLASS_DATA_DIR` override remains supported. Retaining this location avoids losing saved layouts or themes when the executable name changes. No user files are migrated, deleted or silently reset.

## Icon and visible logo

The user-selected generated icon is a navy label/barcode mark with a teal registration accent.

- Source artwork: `branding/lable-studio-source.png`.
- Transparent header logo: `src/ExcelSticker.Wpf/Assets/lable-studio.png` (256×256).
- Windows icon: `src/ExcelSticker.Wpf/Assets/lable-studio.ico` (16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels).
- `.csproj` `ApplicationIcon` embeds the ICO into the executable's Win32 resources for Explorer/shortcuts.
- WPF resource entries and an explicit window `Icon` URI provide the title-bar/taskbar icon.
- An actual `Image` in the header replaces the old “OG” text badge.

`python scripts/Make-Icons.py` regenerates both committed assets from the selected source, using Pillow as a **development-only** dependency. It removes only edge-connected white background, preserving the white label inside the navy tile. Building/running the Windows application does not require Python, Pillow or image-generation services.

Windows can cache shortcut icons. After rebuilding, launch `LableStudio.exe`, and recreate/re-pin an old shortcut if it still points to `OceanGlass.LabelMaker.exe`. The build does not alter desktop shortcuts or clear the user's shell cache.

## Normal industrial appearance is the default

`ThemeTokens.FactoryDefault` is the **Industrial flat** preset:

- Solid light-gray workspace, neutral white panels and navy text.
- Blue primary actions with contrast-selected text.
- Visible one-DIP borders and restrained 2–4 DIP corners.
- Compact spacing and Segoe UI typography.
- No bevel shading, gloss, cast/ambient shadows, colored pastel panels or background pattern.

Fresh installations and invalid-preference recovery start here. A valid explicitly saved theme remains respected, including an optional clay style. Theme Studio is not required for normal factory use.

To switch an existing installation back: **Theme Studio → Use factory flat theme → Save theme**. The action uses the normal validated live-preview/save/revert session. With Live preview disabled, it changes the draft until Apply or Save. Closing without saving preserves the last saved/opening theme. Factory defaults do not write a preference file merely by starting the application.

All fourteen earlier themes remain optional, bringing the catalog to fifteen presets. An explicit importable copy is `examples/themes/industrial-flat.json`.

The legacy `ThemeTokens` JSON property initializers remain unchanged intentionally: old partial version-one theme files must not acquire unrelated flat styling merely because the application defaults changed. Startup/recovery use the separate factory recipe.

## Verification

Checked in this sandbox:

- C# syntax, XML structure, theme/resource references and embedded-asset paths.
- ICO directory dimensions, frame decoding in Pillow and transparent PNG dimensions/corners.
- Factory preset color contrast, effect-free settings and all example JSONs.
- Build/publish path consistency, unchanged print/company data and `git diff --check`.

New Windows regression checks cover startup branding/logo loading, all embedded ICO frame sizes, actual flat startup chrome, saved optional-theme retention, corrupt/contrast-failing recovery, legacy JSON compatibility, and applying/saving/reloading the factory preset through the real Studio. Optional native capture: `artifacts/visual-review/lable-studio-industrial-flat.png`.

**Not verified here:** Windows compilation, WPF test execution, Explorer/taskbar display, native appearance or physical printing. This sandbox has no .NET SDK/Windows runtime. Run `Build-and-Run.cmd`, or:

```powershell
dotnet build ExcelSticker.sln -c Release
dotnet run --project tests/ExcelSticker.Core.Tests -c Release --no-build
$env:OCEANGLASS_VISUAL_REVIEW = "$PWD/artifacts/visual-review"
dotnet run --project tests/ExcelSticker.Wpf.Tests -c Release --no-build
```

On Windows, inspect Explorer, the title bar, Alt+Tab/taskbar and the header at 100%, 150% and 200% DPI. Confirm that a clean install is flat, a saved optional style survives restart, and printed labels still use the configured company and 100×70 mm dimensions.
