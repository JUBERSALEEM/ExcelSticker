[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Debug', [switch]$Run)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$previous=Get-Location
$code=0
$previousVisualReview=$env:OCEANGLASS_VISUAL_REVIEW
try {
    Set-Location $root
    if ($env:OS -ne 'Windows_NT') { throw 'This WPF build/run script requires Windows.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install a stable .NET SDK 8 or newer and the Visual Studio .NET desktop development workload.' }
    $sdk=& dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'SDK resolution failed. Run dotnet --list-sdks and check global.json. Install a stable SDK 8 or newer; use a Visual Studio version compatible with that SDK.' }
    $sdkVersion=$null
    if (-not [Version]::TryParse(([string]$sdk).Trim(), [ref]$sdkVersion) -or $sdkVersion.Major -lt 8) { throw 'This checkout needs a stable .NET SDK 8 or newer. Preview SDKs are not selected by global.json.' }
    Write-Host "Selected SDK: $sdk (application target remains .NET 8)" -ForegroundColor Cyan
    $metadata = @('ExcelSticker.sln','Directory.Build.props','Directory.Build.targets','global.json','.vsconfig') + @(Get-ChildItem src,tests -Recurse -Filter *.csproj | ForEach-Object FullName)
    $before = @{}
    foreach ($file in $metadata) { $before[$file] = (Get-FileHash $file -Algorithm SHA256).Hash }
    $runtimes=& dotnet --list-runtimes
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match 'Microsoft.WindowsDesktop.App 8\.')) { throw 'Install .NET 8 SDK / Windows Desktop Runtime 8. A newer SDK alone does not include runtime 8.' }
    $running=Get-Process -Name 'OceanGlass.LabelMaker' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) }
    if ($running) { throw 'Close Ocean Glass from this checkout first. No app is killed automatically; the batch may contain unsaved data.' }
    New-Item -ItemType Directory -Force -Path artifacts | Out-Null
    $log=Join-Path $root 'artifacts/build.log'
    "Build started $(Get-Date -Format o)`nCheckout: $root`nConfiguration: $Configuration" | Set-Content $log
    function Invoke-Dotnet([string[]]$Arguments) {
        Write-Host ('dotnet '+($Arguments -join ' ')) -ForegroundColor Cyan
        & dotnet @Arguments 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw "Build/test failed. No application was launched. See $log" }
    }
    Invoke-Dotnet -Arguments @('clean','ExcelSticker.sln','-c',$Configuration,'--nologo')
    Invoke-Dotnet -Arguments @('build','ExcelSticker.sln','-c',$Configuration,'--no-incremental','--nologo','-bl:artifacts/build.binlog')
    Invoke-Dotnet -Arguments @('run','--project','tests/ExcelSticker.Core.Tests','-c',$Configuration,'--no-build')
    $env:OCEANGLASS_VISUAL_REVIEW=Join-Path $root 'artifacts/visual-review'
    Invoke-Dotnet -Arguments @('run','--project','tests/ExcelSticker.Wpf.Tests','-c',$Configuration,'--no-build')
    foreach ($file in $metadata) { if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $before[$file]) { throw "Project metadata changed during the build: $file. Review the diff; no files were restored automatically." } }
    $exe=Join-Path $root "src/ExcelSticker.Wpf/bin/$Configuration/net8.0-windows/OceanGlass.LabelMaker.exe"
    if (-not (Test-Path $exe)) { throw "Executable not produced: $exe" }
    Write-Host "Build and tests passed. Executable: $exe" -ForegroundColor Green
    Write-Host 'Native WPF review images: artifacts/visual-review (application, studio, and balanced material at 100/150/200%).'
    Write-Host 'Logs: artifacts/build.log and artifacts/build.binlog. Review/redact local paths before sharing.'
    if ($Run) { Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) }
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; $code=1 }
finally { $env:OCEANGLASS_VISUAL_REVIEW=$previousVisualReview; Set-Location $previous }
exit $code
