# Test the Viking Combined Services Docker image
# This script validates that the container is running correctly

param(
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "viking-services",
    
    [Parameter(Mandatory=$false)]
    [int]$HttpPort = 8080,
    
    [Parameter(Mandatory=$false)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Viking Docker Image Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if container exists
Write-Host "Checking if container '$ContainerName' exists..." -NoNewline
$container = docker ps -a -f name=$ContainerName --format "{{.Names}}"
if ($container -ne $ContainerName) {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "Container '$ContainerName' not found." -ForegroundColor Red
    Write-Host "Run the container first: .\Scripts\BuildAndRunCombined.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host " ✓" -ForegroundColor Green

# Check if container is running
Write-Host "Checking if container is running..." -NoNewline
$running = docker ps -f name=$ContainerName --format "{{.Names}}"
if ($running -ne $ContainerName) {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "Container exists but is not running." -ForegroundColor Red
    Write-Host "Start it with: docker start $ContainerName" -ForegroundColor Yellow
    exit 1
}
Write-Host " ✓" -ForegroundColor Green

# Wait for services to be ready
Write-Host ""
Write-Host "Waiting for services to start (timeout: ${TimeoutSeconds}s)..." -ForegroundColor Yellow
$elapsed = 0
$ready = $false

while ($elapsed -lt $TimeoutSeconds -and -not $ready) {
    Start-Sleep -Seconds 5
    $elapsed += 5
    
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$HttpPort" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 404) {
            $ready = $true
        }
    }
    catch {
        Write-Host "  Waiting... (${elapsed}s)" -ForegroundColor Gray
    }
}

if (-not $ready) {
    Write-Host "✗ Services did not start within ${TimeoutSeconds} seconds" -ForegroundColor Red
    Write-Host "Check container logs: docker logs $ContainerName" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Services are responding" -ForegroundColor Green
Write-Host ""

# Test each service
$tests = @(
    @{
        Name = "AnnotationService WSDL"
        Url = "http://localhost:$HttpPort/annotation/Annotate.svc?wsdl"
        ExpectedStatus = 200
        ExpectedContent = "wsdl:definitions"
    },
    @{
        Name = "ConnectomeOData Metadata"
        Url = "http://localhost:$HttpPort/odata/`$metadata"
        ExpectedStatus = 200
        ExpectedContent = "Edm"
    },
    @{
        Name = "DataExport Root"
        Url = "http://localhost:$HttpPort/dataexport"
        ExpectedStatus = @(200, 404)  # May return 404 if no root endpoint
        ExpectedContent = $null
    }
)

$passed = 0
$failed = 0

Write-Host "Running endpoint tests..." -ForegroundColor Yellow
Write-Host ""

foreach ($test in $tests) {
    Write-Host "Testing: $($test.Name)..." -NoNewline
    
    try {
        $response = Invoke-WebRequest -Uri $test.Url -UseBasicParsing -TimeoutSec 10
        
        $statusOk = if ($test.ExpectedStatus -is [array]) {
            $test.ExpectedStatus -contains $response.StatusCode
        } else {
            $response.StatusCode -eq $test.ExpectedStatus
        }
        
        if ($statusOk) {
            if ($test.ExpectedContent) {
                if ($response.Content -match $test.ExpectedContent) {
                    Write-Host " ✓ PASS" -ForegroundColor Green
                    $passed++
                }
                else {
                    Write-Host " ✗ FAIL" -ForegroundColor Red
                    Write-Host "  Expected content '$($test.ExpectedContent)' not found" -ForegroundColor Yellow
                    $failed++
                }
            }
            else {
                Write-Host " ✓ PASS" -ForegroundColor Green
                $passed++
            }
        }
        else {
            Write-Host " ✗ FAIL" -ForegroundColor Red
            Write-Host "  Expected status $($test.ExpectedStatus), got $($response.StatusCode)" -ForegroundColor Yellow
            $failed++
        }
    }
    catch {
        Write-Host " ✗ ERROR" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Yellow
        $failed++
    }
}

# Check IIS status inside container
Write-Host ""
Write-Host "Checking IIS status inside container..." -NoNewline
try {
    $iisStatus = docker exec $ContainerName powershell -c "Get-Service W3SVC | Select-Object -ExpandProperty Status"
    if ($iisStatus -eq "Running") {
        Write-Host " ✓" -ForegroundColor Green
    }
    else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  IIS status: $iisStatus" -ForegroundColor Yellow
        $failed++
    }
}
catch {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Could not check IIS status: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Check application pools
Write-Host "Checking application pools..." -ForegroundColor Yellow
$pools = @("AnnotationServicePool", "ConnectomeODataPool", "DataExportPool")

foreach ($pool in $pools) {
    Write-Host "  $pool..." -NoNewline
    try {
        $poolState = docker exec $ContainerName powershell -c "Import-Module WebAdministration; (Get-Item IIS:\AppPools\$pool).State"
        if ($poolState -eq "Started") {
            Write-Host " ✓ Running" -ForegroundColor Green
        }
        else {
            Write-Host " ✗ $poolState" -ForegroundColor Red
            $failed++
        }
    }
    catch {
        Write-Host " ✗ Error" -ForegroundColor Red
        $failed++
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "✓ All tests passed! Services are running correctly." -ForegroundColor Green
    Write-Host ""
    Write-Host "Service URLs:" -ForegroundColor Cyan
    Write-Host "  - AnnotationService: http://localhost:$HttpPort/annotation/Annotate.svc" -ForegroundColor White
    Write-Host "  - ConnectomeOData:   http://localhost:$HttpPort/odata" -ForegroundColor White
    Write-Host "  - DataExport:        http://localhost:$HttpPort/dataexport" -ForegroundColor White
    Write-Host ""
    exit 0
}
else {
    Write-Host "✗ Some tests failed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  - View logs: docker logs -f $ContainerName" -ForegroundColor White
    Write-Host "  - Check IIS: docker exec $ContainerName powershell Get-Service W3SVC" -ForegroundColor White
    Write-Host "  - Restart: docker restart $ContainerName" -ForegroundColor White
    Write-Host ""
    exit 1
}









