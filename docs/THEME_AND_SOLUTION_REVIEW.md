# v0.5.0 — Theme lifecycle and Visual Studio project review

## Findings and fixes

| Finding in source | Repair | Verification |
| --- | --- | --- |
| `ClayChrome` constructor set local resource expressions for shade, inset and line width. Local DP values outrank template triggers/values. | Resource defaults moved to an explicitly referenced chrome style. Template input-depth overrides and state triggers now take precedence. | Added effective input-depth/local-value regression checks; actual mouse/keyboard states still need Windows interaction testing. |
| Main window opened Theme Studio with `ShowDialog()`, blocking interaction with the app being previewed. | One modeless studio per main window, reusable after hide/minimize; Inspect application temporarily hides it without discarding the draft. | Added main-window enabled/single-instance/hide/reopen checks. |
| No observable application-level preview/save state; misleading “next launch” text. | Engine publishes change kind/revision. Main header displays LIVE UNSAVED, Saved and active, or Preview reverted independently of batch messages. | Added checks against the actual main-window card and status, not only dictionary values. |
| Save relied on a generic text error, then closed immediately on success. | Validity controls Save availability; errors show the target path and retry/export guidance. Save stays open and confirms success/time/path. Pending debounce cannot overwrite that result. | Added Save-button click, disk contents, fresh-engine reload, invalid draft, failed write and retry availability checks. |
| Rollback was one snapshot from window creation. This could not support save-and-continue safely. | `ThemeEditSession` advances its rollback point only after persistence succeeds. Close discards changes after the most recent save. Duplicate sessions are refused. | Added before-save/after-save rollback and blocked-destination checks. |
| `Current` returned mutable engine-owned data. All resources were recreated/published for every slider edit. | Defensive token snapshots; UI dispatcher ownership; validated resource preparation; unchanged frozen brushes/effects are reused; rendering is debounced, validation is immediate. | Added mutation isolation, no-partial-invalid-preview, observable revision and resource-reuse checks. |
| Some controls lacked distinct or consistent interaction states. | Semantic hover/pressed surfaces, contrast-preserving primary states, neutral disabled primary, mixed/disabled checkbox state, slider keyboard outline/vertical rail, combo density support. | Source/template review; manual mouse, keyboard and DPI matrix required. |
| Build stamping quoted a Windows directory ending in a backslash in the git command line. | Use the MSBuild Exec WorkingDirectory parameter instead of interpolating a quoted `git -C` path. | XML/source check; Windows build required. |
| Solution had hand-written space indentation and inconsistent configuration order; SDK and installer requirements were implicit. | Native-style BOM/CRLF/tab solution serialization, consistent project order/GUIDs, SDK feature-band selection, `.vsconfig`, explicit library output intent. | Static metadata checks; Windows IDE open/save round-trip still required. |

The exact cause of an individual failed save cannot be established from “not working” alone. The repairs cover concrete source defects and make validation, disk errors, and successful persistence distinguishable. No claim is made that a specific user's ACL/antivirus/settings file was reproduced here.

## Theme Studio workflow

1. Open **Theme Studio**; the main app remains usable.
2. Choose a preset or edit corners, palette, depth, typography or density. Valid live changes appear in the application after an 80 ms debounce. Invalid drafts do not replace the last valid preview.
3. Use **Inspect application** to hide the editor temporarily. Reopen the same editor with the main window's **Theme Studio** button.
4. Turn off **Live preview** to restore the saved/opening theme while editing a draft; **Apply preview now** is an explicit one-shot preview.
5. **Save theme** persists and applies immediately, then keeps the editor open with a confirmation. Closing later discards only changes made *after* that save.
6. **Revert changes** restores both controls and resources to the last saved/opening theme. **Close / discard unsaved** does the same and closes the editor.
7. Export writes a shareable theme but does not advance the application rollback point. Import loads a draft, not a saved preference.

The inspector and action bar keep stable, readable control metrics while the main app and component laboratory preview typography/density. Below 980 DIP studio width, the laboratory collapses to give editing controls enough room; use Inspect application for the full live view.

Save is disabled for invalid HEX/name/range/contrast data, with an explanation in the footer. If a disk write fails, the draft stays available and Save remains retryable. Check the displayed location/permissions or export to a writable location; do not run the entire app as administrator as a routine workaround. The normal store remains `%LOCALAPPDATA%\OceanGlass\theme.json`; existing version-1 theme JSON remains supported. Printing and sticker geometry do not depend on these theme settings.

## Stable Visual Studio setup

- Supported setup (updated in v0.6.1): a **stable .NET SDK 8 or newer**, .NET desktop development workload, and a Visual Studio release compatible with that SDK. Visual Studio 2022 17.11+ is suitable for SDK 8.0.4xx; newer SDKs may need a newer IDE.
- The original `8.0.400` / `latestPatch` pin was too restrictive and caused NETSDK1141 on SDK-10-only installations. **v0.6.1** requests `8.0.100` with `rollForward: major`: prefer a compatible SDK 8, allow newer stable majors if necessary. Projects remain net8, so Windows Desktop Runtime 8 is still required. See [SDK recovery](UPDATE_AND_BUILD.md#netsdk1141-sdk-resolution-error).
- `.vsconfig` declares `Microsoft.VisualStudio.Workload.ManagedDesktop`. Use Visual Studio Installer → Import configuration if the IDE does not prompt for missing components.
- Projects remain modern `Microsoft.NET.Sdk` projects with `UseWPF` for desktop projects, C# 12, and one AnyCPU Debug/Release configuration set. Library output intent is explicit. **Do not add legacy `ToolsVersion`, old MSBuild imports, or `ProjectTypeGuids` to these SDK-style csproj files.** Those do not install missing tools and can cause genuine compatibility problems.
- Open the committed **ExcelSticker.sln**, not a regenerated or converted `.slnx`. Project and solution identities are preserved. Git checks out the solution with CRLF; its BOM, tabs, and configuration order follow the conventional VS serializer format.
- `Build-and-Run.cmd` verifies the SDK and hashes solution/project/configuration metadata before and after building/testing. Unexpected modifications stop launch and are reported, not automatically discarded.

### What a repository cannot guarantee

Git clone itself copies tracked files; it does not upgrade a WPF project. Visual Studio, its extensions, solution conversion, or a save operation can write files afterward. A different **major IDE version** can legitimately update `VisualStudioVersion`. A repository cannot prohibit that while still allowing normal solution editing. Read-only files, `assume-unchanged`, global clean filters, and automatic `git restore` would conceal or destroy changes; none are used here.

If the solution still shows modified immediately after opening:

```powershell
git status --short
git diff --ignore-space-at-eol -- ExcelSticker.sln
git diff -- '*.csproj' Directory.Build.props Directory.Build.targets global.json
dotnet --info
```

Record Visual Studio's Help → About version and save these diffs before undoing anything. A version-line-only change is not a source-code update; new project GUIDs/paths/targets require investigation. Without that actual before/after diff, a universal “never changes on any Visual Studio installation” guarantee would be misleading.

## Required acceptance checks on Windows

Run `Build-and-Run.cmd` (or the explicit commands in UPDATE_AND_BUILD.md). Then:

- Open/close and save the solution in the supported IDE; compare metadata with Git before/after.
- Theme: Save/restart, invalid HEX and contrast, inaccessible destination, Revert, Close after Save, import/export, and reopening a hidden/minimized studio.
- Hover and hold mouse-down on both button styles; Tab/Space/Enter activation; slider arrows; combo keyboard/dropdown; disabled and mixed checkbox states.
- Inspect the **main application**, not only the studio, at 100/150/200% DPI, narrow work areas, maximum text/padding and High contrast/Reduced effects presets. Ensure Save remains reachable.
- Import, select entries, and compare a printed 100 × 70 mm label: theme changes must never alter output geometry/colors or batch data.

This sandbox has no .NET SDK or Windows runtime. Static C#/XML/metadata checks are not compilation, WPF interaction tests, visual sign-off, or hardware printing tests. The new regression cases are committed but must be executed on Windows.

## Engineering recommendations

1. Keep theme lifecycle/persistence in the independent engine/session, not in UI click handlers alone. Preserve the single-editor ownership rule.
2. Use semantic dynamic resources and template/style values; reserve local DP values for deliberate per-control overrides. Do not put shadows or opacity on text-bearing ancestors.
3. Require the Windows smoke suite and a short keyboard/DPI review before calling a UI release verified. Restore the existing workflow template only through an account with workflow-write permission.
4. Keep SDK/IDE migrations explicit and reviewed. Do not regenerate the solution to fix a missing workload. Plan a separately tested supported-framework upgrade rather than silently changing the target during a styling repair.
5. Keep user settings outside Git, and distinguish preview, saved preference, exported theme, and print-layout data. Do not solve update problems by deleting user settings.

### Checks performed in this repair

- Syntax-parsed all **30 C# files** with tree-sitter (not a C# compiler).
- XML-parsed all **10 XAML/project/build files**.
- Checked solution BOM/CRLF, all **5 SDK project paths**, and every Debug/Release ActiveCfg/Build mapping; parsed both toolchain JSON manifests.
- `git diff --check` passed.
- Windows compilation, execution of the regression suite, IDE round-trip, and visual/printer acceptance are **not run in this environment**.
