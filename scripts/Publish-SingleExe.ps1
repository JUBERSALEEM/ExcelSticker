[CmdletBinding()]
param([ValidateSet('win-x64','win-arm64')][string]$Runtime='win-x64')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$previous=Get-Location
$code=0
try {
    Set-Location $root
    if ($env:OS -ne 'Windows_NT') { throw 'Package and smoke-test the WPF executable on Windows.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install a stable .NET SDK 8 or newer.' }
    # A fresh staging directory prevents old DLLs/JSON/PDBs from masquerading as single-file output.
    $output=Join-Path $root ('artifacts/single-exe-'+$Runtime+'-'+[guid]::NewGuid().ToString('N'))
    & dotnet publish src/ExcelSticker.Wpf/ExcelSticker.Wpf.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. No package has been marked ready.' }
    $files=@(Get-ChildItem -LiteralPath $output -Recurse -File)
    $exe=Join-Path $output 'LableStudio.exe'
    if ($files.Count -ne 1 -or -not (Test-Path -LiteralPath $exe)) {
        throw "Expected only LableStudio.exe. Inspect the output before distribution: $output"
    }
    Write-Host "Single-EXE package created: $exe" -ForegroundColor Green
    Write-Host ('SHA256: '+(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash)
    Write-Host 'Packaging is not a hardware test. Launch this EXE from an empty folder; verify version, company/logo and one physical sticker.'
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; $code=1 }
finally { Set-Location $previous }
exit $code
