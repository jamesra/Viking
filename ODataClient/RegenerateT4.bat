@echo off
REM Regenerate T4 Template for ODataClient
REM Usage: RegenerateT4.bat [net48|net9.0|all]

setlocal

if "%1"=="" (
    echo Regenerating T4 template for all target frameworks...
    powershell -ExecutionPolicy Bypass -File "%~dp0RegenerateT4.ps1" -TargetFramework all
) else (
    echo Regenerating T4 template for %1...
    powershell -ExecutionPolicy Bypass -File "%~dp0RegenerateT4.ps1" -TargetFramework %1
)

if %ERRORLEVEL% NEQ 0 (
    echo T4 template regeneration failed!
    pause
    exit /b 1
) else (
    echo T4 template regeneration completed successfully!
)

pause 