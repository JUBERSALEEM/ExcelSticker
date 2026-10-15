@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build.ps1" -Run
set "result=%ERRORLEVEL%"
if not "%result%"=="0" pause
exit /b %result%
