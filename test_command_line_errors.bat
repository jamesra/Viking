@echo off
echo Testing Command Line Error Handling
echo ===================================

echo.
echo Testing MonogameTestbed with invalid arguments:
echo ----------------------------------------------
cd Clients\MonogameTestbed\bin\Debug\net48
MonogameTestbed.exe --invalid-option

echo.
echo Testing MeasureDistance with missing required argument:
echo ------------------------------------------------------
cd ..\..\..\..\MeasureDistance\bin\Debug\net48
MeasureDistance.exe

echo.
echo Testing Neo4JGenerator with missing required argument:
echo ------------------------------------------------------
cd ..\..\..\..\..\Servers\Neo4JGenerator\bin\Debug\net48
Neo4JGenerator.exe

echo.
echo Testing VikingAU with invalid arguments:
echo ----------------------------------------
cd ..\..\..\..\..\..\Clients\VikingAU\bin\Debug\net48
VikingAU.exe --invalid-option

echo.
echo All tests completed.
pause 