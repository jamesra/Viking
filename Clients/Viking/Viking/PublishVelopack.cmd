@echo off
REM Open a real console window so the YubiKey smart-card PIN dialog can appear.
REM Cursor/VS Code integrated terminals often hide or block that UI.
REM Use: PublishVelopack.cmd --inplace   to run in the current terminal instead.
cd /d "%~dp0"

if /I "%~1"=="--inplace" (
  PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0PublishVelopack.ps1" %2 %3 %4 %5 %6 %7 %8 %9
  exit /b %ERRORLEVEL%
)

start "Viking PublishVelopack" /D "%~dp0" cmd /c "PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File PublishVelopack.ps1 %* & echo. & pause"
