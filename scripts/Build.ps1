[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Debug', [switch]$Run)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$previous=Get-Location
$code=0
try {
    Set-Location $root
    if ($env:OS -ne 'Windows_NT') { throw 'This WPF build/run script requires Windows.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 8 SDK and the Visual Studio .NET desktop development workload.' }
    $sdk=& dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^8\.0\.4[0-9]{2}$') { throw 'Install a stable .NET SDK 8.0.4xx (Visual Studio 2022 17.11 or newer). See global.json and .vsconfig. Newer major SDKs alone do not satisfy this checkout.' }
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
    Invoke-Dotnet -Arguments @('run','--project','tests/ExcelSticker.Wpf.Tests','-c',$Configuration,'--no-build')
    foreach ($file in $metadata) { if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $before[$file]) { throw "Project metadata changed during the build: $file. Review the diff; no files were restored automatically." } }
    $exe=Join-Path $root "src/ExcelSticker.Wpf/bin/$Configuration/net8.0-windows/OceanGlass.LabelMaker.exe"
    if (-not (Test-Path $exe)) { throw "Executable not produced: $exe" }
    Write-Host "Build and tests passed. Executable: $exe" -ForegroundColor Green
    Write-Host 'Logs: artifacts/build.log and artifacts/build.binlog. Review/redact local paths before sharing.'
    if ($Run) { Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) }
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; $code=1 }
finally { Set-Location $previous }
exit $code
