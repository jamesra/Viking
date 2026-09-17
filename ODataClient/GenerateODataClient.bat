@echo off
REM Generate Modern OData Client
REM Usage: GenerateODataClient.bat [metadata-uri] [output-dir] [namespace]

setlocal

if "%1"=="" (
    echo Generating OData client with default settings...
    powershell -ExecutionPolicy Bypass -File "%~dp0GenerateODataClient.ps1"
) else (
    echo Generating OData client with custom settings...
    powershell -ExecutionPolicy Bypass -File "%~dp0GenerateODataClient.ps1" -MetadataUri "%1" -OutputDirectory "%2" -Namespace "%3"
)

if %ERRORLEVEL% NEQ 0 (
    echo OData client generation failed!
    pause
    exit /b 1
) else (
    echo OData client generation completed successfully!
)

pause 