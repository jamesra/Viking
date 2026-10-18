@echo off
rem Runs each net9.0 Debug build with bad arguments to show its parse-error output.
rem Run from anywhere; paths are relative to this file. MeasureDistance is not run because
rem it does not currently build (see CommandLineErrorHandling.md).
set ROOT=%~dp0

echo Testing Command Line Error Handling
echo ===================================

echo.
echo Testing Neo4JGenerator with missing required argument:
echo ------------------------------------------------------
"%ROOT%Servers\Neo4JGenerator\bin\Debug\net9.0\Neo4JGenerator.exe"
echo Exit code: %ERRORLEVEL% (expected 1)

echo.
echo Testing Neo4JGenerator with an unknown option:
echo ----------------------------------------------
"%ROOT%Servers\Neo4JGenerator\bin\Debug\net9.0\Neo4JGenerator.exe" --invalid-option
echo Exit code: %ERRORLEVEL% (expected 1)

echo.
echo Testing VikingAU with an unknown option:
echo ----------------------------------------
"%ROOT%Clients\VikingAU\bin\Debug\net9.0\VikingAU.exe" --invalid-option
echo Exit code: %ERRORLEVEL% (expected 1)

echo.
echo Testing MonogameTestbed with an unknown option (start /wait: it is a WinExe):
echo ---------------------------------------------------------------------------
if exist "%ROOT%Clients\MonogameTestbed\bin\Debug\net9.0-windows\MonogameTestbed.exe" (
    start /wait "" "%ROOT%Clients\MonogameTestbed\bin\Debug\net9.0-windows\MonogameTestbed.exe" --invalid-option
    echo Exit code: %ERRORLEVEL% ^(expected 1^)
) else (
    echo Not built, skipping.
)

echo.
echo All tests completed.
pause
