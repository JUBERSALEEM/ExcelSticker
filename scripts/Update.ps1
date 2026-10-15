[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$branch='arena/4ed12585-excelsticker'
$previous=Get-Location
$code=0
try {
    Set-Location (Split-Path -Parent $PSScriptRoot)
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is not on PATH. Use Visual Studio Git Fetch/Pull or install Git for Windows.' }
    $current=& git branch --show-current
    if ($LASTEXITCODE -ne 0) { throw 'Not a readable Git checkout.' }
    if ($current -ne $branch) { throw "Select branch '$branch' in Visual Studio first. This script does not create or switch branches." }
    $changes=& git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Git status.' }
    if ($changes) { $changes | Write-Host; throw 'Commit/stash wanted local changes or review and undo unwanted IDE solution edits first. Nothing was discarded.' }
    & git fetch origin $branch
    if ($LASTEXITCODE -ne 0) { throw 'Fetch failed. Check the remote, network and GitHub sign-in in Visual Studio. Never paste credentials into chat or logs.' }
    & git merge --ff-only "origin/$branch"
    if ($LASTEXITCODE -ne 0) { throw 'Branch diverged. Review local commits. No reset or force operation was used.' }
    & git log -1 --format='%h %s'
    Write-Host 'Updated. Open ExcelSticker.sln or run Build-and-Run.cmd. Never choose Launch old version after build errors.' -ForegroundColor Green
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; $code=1 }
finally { Set-Location $previous }
exit $code
