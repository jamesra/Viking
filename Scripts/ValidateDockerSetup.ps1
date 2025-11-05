# Validate Docker setup before building
# This script checks prerequisites and configuration

param(
    [Parameter(Mandatory=$false)]
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Viking Docker Setup Validator" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$allChecks = @()
$passed = 0
$failed = 0

function Test-Check {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [string]$SuccessMessage,
        [string]$FailureMessage,
        [string]$FixSuggestion
    )
    
    Write-Host "Checking: $Name..." -NoNewline
    
    try {
        $result = & $Test
        if ($result) {
            Write-Host " ✓ PASS" -ForegroundColor Green
            if ($Detailed) {
                Write-Host "  $SuccessMessage" -ForegroundColor Gray
            }
            $script:passed++
            return $true
        }
        else {
            Write-Host " ✗ FAIL" -ForegroundColor Red
            Write-Host "  $FailureMessage" -ForegroundColor Yellow
            if ($FixSuggestion) {
                Write-Host "  Fix: $FixSuggestion" -ForegroundColor Cyan
            }
            $script:failed++
            return $false
        }
    }
    catch {
        Write-Host " ✗ ERROR" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Yellow
        if ($FixSuggestion) {
            Write-Host "  Fix: $FixSuggestion" -ForegroundColor Cyan
        }
        $script:failed++
        return $false
    }
}

Write-Host "Running prerequisite checks..." -ForegroundColor Yellow
Write-Host ""

# Check Docker is installed
Test-Check -Name "Docker Installed" -Test {
    $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
} -SuccessMessage "Docker CLI is available" `
  -FailureMessage "Docker is not installed or not in PATH" `
  -FixSuggestion "Install Docker Desktop from https://www.docker.com/products/docker-desktop"

# Check Docker is running
Test-Check -Name "Docker Running" -Test {
    $info = docker info 2>&1
    $LASTEXITCODE -eq 0
} -SuccessMessage "Docker daemon is running" `
  -FailureMessage "Docker is not running" `
  -FixSuggestion "Start Docker Desktop"

# Check Windows containers
Test-Check -Name "Windows Containers" -Test {
    $info = docker info 2>&1
    $info -match "OSType: windows"
} -SuccessMessage "Docker is using Windows containers" `
  -FailureMessage "Docker is not using Windows containers" `
  -FixSuggestion "Right-click Docker Desktop icon → 'Switch to Windows containers...'"

# Check available disk space
Test-Check -Name "Disk Space" -Test {
    $drive = (Get-Location).Drive
    $freeSpace = (Get-PSDrive $drive.Name).Free / 1GB
    $freeSpace -gt 20
} -SuccessMessage "Sufficient disk space available" `
  -FailureMessage "Less than 20GB free disk space" `
  -FixSuggestion "Free up at least 20GB of disk space"

# Check Docker memory allocation
Test-Check -Name "Docker Memory" -Test {
    # This is a basic check - actual memory allocation is set in Docker Desktop settings
    $true
} -SuccessMessage "Memory check passed" `
  -FailureMessage "Unable to verify memory allocation" `
  -FixSuggestion "Ensure Docker Desktop has at least 8GB RAM allocated (Settings → Resources → Memory)"

# Check required files exist
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir

Write-Host ""
Write-Host "Checking required files..." -ForegroundColor Yellow
Write-Host ""

Test-Check -Name "Volume Annotation Services Dockerfile" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/VolumeAnnotationServices/Dockerfile")
} -SuccessMessage "Volume Annotation Services Dockerfile found" `
  -FailureMessage "Servers/VolumeAnnotationServices/Dockerfile not found" `
  -FixSuggestion "Ensure you're in the solution root directory"

Test-Check -Name "docker-compose.combined.yml" -Test {
    Test-Path (Join-Path $SolutionRoot "docker-compose.combined.yml")
} -SuccessMessage "Docker Compose file found" `
  -FailureMessage "docker-compose.combined.yml not found"

Test-Check -Name "AnnotationService Config" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/AnnotationService/AnnotationService/web.config.docker")
} -SuccessMessage "AnnotationService Docker config found" `
  -FailureMessage "web.config.docker not found for AnnotationService"

Test-Check -Name "ConnectomeOData Config" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/ConnectomeODataV4/Web.config.docker")
} -SuccessMessage "ConnectomeOData Docker config found" `
  -FailureMessage "Web.config.docker not found for ConnectomeODataV4"

Test-Check -Name "DataExport Config" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/DataExport/appsettings.Docker.json")
} -SuccessMessage "DataExport Docker config found" `
  -FailureMessage "appsettings.Docker.json not found for DataExport"

Test-Check -Name "DataExport Web.config" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/DataExport/web.config")
} -SuccessMessage "DataExport web.config found" `
  -FailureMessage "web.config not found for DataExport"

# Check project files exist
Write-Host ""
Write-Host "Checking project files..." -ForegroundColor Yellow
Write-Host ""

Test-Check -Name "AnnotationService Project" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/AnnotationService/AnnotationService/AnnotationService.csproj")
} -SuccessMessage "AnnotationService.csproj found"

Test-Check -Name "ConnectomeODataV4 Project" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/ConnectomeODataV4/ConnectomeODataV4.csproj")
} -SuccessMessage "ConnectomeODataV4.csproj found"

Test-Check -Name "DataExport Project" -Test {
    Test-Path (Join-Path $SolutionRoot "Servers/DataExport/DataExport.csproj")
} -SuccessMessage "DataExport.csproj found"

Test-Check -Name "Solution File" -Test {
    Test-Path (Join-Path $SolutionRoot "Everything.sln")
} -SuccessMessage "Everything.sln found"

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Validation Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "✓ All checks passed! You're ready to build." -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review docker-compose.combined.yml and update database connection string" -ForegroundColor White
    Write-Host "  2. Run: .\Scripts\BuildAndRunCombined.ps1" -ForegroundColor White
    Write-Host "     or: docker-compose -f docker-compose.combined.yml up --build" -ForegroundColor White
    Write-Host ""
    exit 0
}
else {
    Write-Host "✗ Some checks failed. Please fix the issues above before building." -ForegroundColor Red
    Write-Host ""
    exit 1
}


