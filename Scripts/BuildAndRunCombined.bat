@echo off
REM Wrapper script to run BuildAndRunCombined.ps1
REM This allows easy execution without changing PowerShell execution policy

PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildAndRunCombined.ps1" %*









