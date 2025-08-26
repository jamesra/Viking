Write-Host "Testing Command Line Error Handling" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green

Write-Host ""
Write-Host "Testing MonogameTestbed with invalid arguments:" -ForegroundColor Yellow
Write-Host "----------------------------------------------" -ForegroundColor Yellow
try {
    & "Clients\MonogameTestbed\bin\Debug\net48\MonogameTestbed.exe" --invalid-option
} catch {
    Write-Host "Expected error occurred: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing MeasureDistance with missing required argument:" -ForegroundColor Yellow
Write-Host "------------------------------------------------------" -ForegroundColor Yellow
try {
    & "Clients\MeasureDistance\bin\Debug\net48\MeasureDistance.exe"
} catch {
    Write-Host "Expected error occurred: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing Neo4JGenerator with missing required argument:" -ForegroundColor Yellow
Write-Host "-----------------------------------------------------" -ForegroundColor Yellow
try {
    & "Servers\Neo4JGenerator\bin\Debug\net48\Neo4JGenerator.exe"
} catch {
    Write-Host "Expected error occurred: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing VikingAU with invalid arguments:" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow
try {
    & "Clients\VikingAU\bin\Debug\net48\VikingAU.exe" --invalid-option
} catch {
    Write-Host "Expected error occurred: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "All tests completed." -ForegroundColor Green
Read-Host "Press Enter to continue" 