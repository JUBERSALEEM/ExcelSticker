@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Publish-SingleExe.ps1" %*
exit /b %ERRORLEVEL%
