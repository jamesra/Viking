# Build and Run Viking Combined Services
# This script builds and runs all three Viking services in a single Docker container

param(
    [Parameter(Mandatory=$false)]
    [string]$Action = "build-and-run",
    
    [Parameter(Mandatory=$false)]
    [string]$DBServer = "localhost",
    
    [Parameter(Mandatory=$false)]
    [string]$DBName = "Connectome",
    
    [Parameter(Mandatory=$false)]
    [string]$DBUser = "",
    
    [Parameter(Mandatory=$false)]
    [string]$DBPassword = "",
    
    [Parameter(Mandatory=$false)]
    [int]$HttpPort = 8080,
    
    [Parameter(Mandatory=$false)]
    [int]$HttpsPort = 8443
)

$ErrorActionPreference = "Stop"

# Get the solution root directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Viking Combined Services Docker Manager" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running and using Windows containers
function Test-DockerWindows {
    try {
        $dockerInfo = docker info 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Docker is not running or not accessible" -ForegroundColor Red
            Write-Host "Please start Docker Desktop" -ForegroundColor Yellow
            return $false
        }
        
        if ($dockerInfo -notmatch "OSType: windows") {
            Write-Host "Error: Docker is not using Windows containers" -ForegroundColor Red
            Write-Host "Please switch to Windows containers:" -ForegroundColor Yellow
            Write-Host "  1. Right-click Docker Desktop icon in system tray" -ForegroundColor White
            Write-Host "  2. Select 'Switch to Windows containers...'" -ForegroundColor White
            return $false
        }
        
        return $true
    }
    catch {
        Write-Host "Error checking Docker: $_" -ForegroundColor Red
        return $false
    }
}

# Build the Docker image
function Build-CombinedImage {
    Write-Host "Building Viking Volume Annotation Services image..." -ForegroundColor Green
    Write-Host "This may take 15-30 minutes on first build..." -ForegroundColor Yellow
    Write-Host ""
    
    Push-Location $SolutionRoot
    try {
        docker build -f Servers/VolumeAnnotationServices/Dockerfile -t viking-annotation-services:latest .
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "Build completed successfully!" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host ""
            Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
            return $false
        }
    }
    finally {
        Pop-Location
    }
}

# Run the Docker container
function Start-CombinedServices {
    Write-Host "Starting Viking Volume Annotation Services..." -ForegroundColor Green
    
    # Build connection string
    $connString = "metadata=res://*/;provider=System.Data.SqlClient;provider connection string=`"data source=$DBServer;initial catalog=$DBName;"
    
    if ($DBUser -and $DBPassword) {
        $connString += "User ID=$DBUser;Password=$DBPassword;"
    }
    else {
        $connString += "Integrated Security=True;"
    }
    
    $connString += "Connection Timeout=60;multipleactiveresultsets=True;Type System Version=SQL Server 2012;application name=VikingServices`""
    
    # Stop existing container if running
    $existing = docker ps -a -q -f name=viking-services
    if ($existing) {
        Write-Host "Stopping existing container..." -ForegroundColor Yellow
        docker stop viking-services 2>&1 | Out-Null
        docker rm viking-services 2>&1 | Out-Null
    }
    
    # Run the container
    Write-Host "Starting container on ports $HttpPort (HTTP) and $HttpsPort (HTTPS)..." -ForegroundColor Yellow
    
    docker run -d `
        --name viking-services `
        -p "${HttpPort}:80" `
        -p "${HttpsPort}:443" `
        -e "ConnectionStrings__ConnectomeEntities=$connString" `
        -e "ASPNETCORE_ENVIRONMENT=Development" `
        viking-annotation-services:latest
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "Container started successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Services are available at:" -ForegroundColor Cyan
        Write-Host "  - AnnotationService: http://localhost:$HttpPort/annotation/Annotate.svc" -ForegroundColor White
        Write-Host "  - ConnectomeOData:   http://localhost:$HttpPort/odata" -ForegroundColor White
        Write-Host "  - DataExport:        http://localhost:$HttpPort/dataexport" -ForegroundColor White
        Write-Host ""
        Write-Host "To view logs: docker logs -f viking-services" -ForegroundColor Yellow
        Write-Host "To stop:      docker stop viking-services" -ForegroundColor Yellow
        return $true
    }
    else {
        Write-Host ""
        Write-Host "Failed to start container" -ForegroundColor Red
        return $false
    }
}

# Stop the container
function Stop-CombinedServices {
    Write-Host "Stopping Viking Volume Annotation Services..." -ForegroundColor Yellow
    docker stop viking-services
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Container stopped successfully" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "Failed to stop container" -ForegroundColor Red
        return $false
    }
}

# Remove the container
function Remove-CombinedServices {
    Write-Host "Removing Viking Volume Annotation Services container..." -ForegroundColor Yellow
    docker stop viking-services 2>&1 | Out-Null
    docker rm viking-services
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Container removed successfully" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "Failed to remove container" -ForegroundColor Red
        return $false
    }
}

# Show container logs
function Show-Logs {
    Write-Host "Showing logs (Ctrl+C to exit)..." -ForegroundColor Yellow
    docker logs -f viking-services
}

# Run validation
function Invoke-Validation {
    Write-Host "Running setup validation..." -ForegroundColor Yellow
    & "$ScriptDir\ValidateDockerSetup.ps1" -Detailed:$false
    return $LASTEXITCODE -eq 0
}

# Main execution
if (-not (Test-DockerWindows)) {
    exit 1
}

switch ($Action.ToLower()) {
    "build" {
        if (Build-CombinedImage) {
            exit 0
        }
        else {
            exit 1
        }
    }
    
    "run" {
        if (Start-CombinedServices) {
            exit 0
        }
        else {
            exit 1
        }
    }
    
    "build-and-run" {
        # Run validation first
        if (-not (Invoke-Validation)) {
            Write-Host "Validation failed. Please fix issues before building." -ForegroundColor Red
            exit 1
        }
        
        if (Build-CombinedImage) {
            if (Start-CombinedServices) {
                Write-Host ""
                Write-Host "Testing services..." -ForegroundColor Yellow
                Start-Sleep -Seconds 10
                & "$ScriptDir\TestDockerImage.ps1"
                exit 0
            }
        }
        exit 1
    }
    
    "stop" {
        if (Stop-CombinedServices) {
            exit 0
        }
        else {
            exit 1
        }
    }
    
    "remove" {
        if (Remove-CombinedServices) {
            exit 0
        }
        else {
            exit 1
        }
    }
    
    "logs" {
        Show-Logs
    }
    
    "rebuild" {
        # Run validation first
        if (-not (Invoke-Validation)) {
            Write-Host "Validation failed. Please fix issues before rebuilding." -ForegroundColor Red
            exit 1
        }
        
        Remove-CombinedServices | Out-Null
        docker rmi viking-annotation-services:latest 2>&1 | Out-Null
        if (Build-CombinedImage) {
            if (Start-CombinedServices) {
                Write-Host ""
                Write-Host "Testing services..." -ForegroundColor Yellow
                Start-Sleep -Seconds 10
                & "$ScriptDir\TestDockerImage.ps1"
                exit 0
            }
        }
        exit 1
    }
    
    "test" {
        & "$ScriptDir\TestDockerImage.ps1"
    }
    
    "validate" {
        & "$ScriptDir\ValidateDockerSetup.ps1" -Detailed
    }
    
    default {
        Write-Host "Usage: .\BuildAndRunCombined.ps1 [-Action <action>] [options]" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Actions:" -ForegroundColor Cyan
        Write-Host "  build          - Build the Docker image only" -ForegroundColor White
        Write-Host "  run            - Run the container (image must exist)" -ForegroundColor White
        Write-Host "  build-and-run  - Build and run (default)" -ForegroundColor White
        Write-Host "  stop           - Stop the running container" -ForegroundColor White
        Write-Host "  remove         - Remove the container" -ForegroundColor White
        Write-Host "  logs           - Show container logs" -ForegroundColor White
        Write-Host "  rebuild        - Rebuild from scratch and run" -ForegroundColor White
        Write-Host "  test           - Test running container" -ForegroundColor White
        Write-Host "  validate       - Validate setup prerequisites" -ForegroundColor White
        Write-Host ""
        Write-Host "Options:" -ForegroundColor Cyan
        Write-Host "  -DBServer      Database server (default: localhost)" -ForegroundColor White
        Write-Host "  -DBName        Database name (default: Connectome)" -ForegroundColor White
        Write-Host "  -DBUser        Database user (optional, uses Integrated Security if not provided)" -ForegroundColor White
        Write-Host "  -DBPassword    Database password (optional)" -ForegroundColor White
        Write-Host "  -HttpPort      HTTP port (default: 8080)" -ForegroundColor White
        Write-Host "  -HttpsPort     HTTPS port (default: 8443)" -ForegroundColor White
        Write-Host ""
        Write-Host "Examples:" -ForegroundColor Cyan
        Write-Host "  .\BuildAndRunCombined.ps1" -ForegroundColor White
        Write-Host "  .\BuildAndRunCombined.ps1 -Action build" -ForegroundColor White
        Write-Host "  .\BuildAndRunCombined.ps1 -Action run -DBServer myserver -DBName Connectome -DBUser sa -DBPassword mypass" -ForegroundColor White
        Write-Host "  .\BuildAndRunCombined.ps1 -Action logs" -ForegroundColor White
        exit 0
    }
}

