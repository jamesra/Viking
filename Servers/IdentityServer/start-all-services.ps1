# PowerShell script to start all Identity Server services
# This script builds and runs the combined Docker container with all three services

param(
    [string]$Environment = "Development",
    [switch]$Build = $true,
    [switch]$Detach = $false
)

Write-Host "Starting Identity Server All Services..." -ForegroundColor Green

# Check if Docker is running
try {
    docker version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not running"
    }
}
catch {
    Write-Error "Docker is not running or not installed. Please start Docker Desktop."
    exit 1
}

# Create necessary directories
Write-Host "Creating necessary directories..." -ForegroundColor Yellow
$directories = @(
    "logs/identity-standalone",
    "logs/identity-webapi", 
    "logs/identity-server",
    "DataProtectionKeys"
)

foreach ($dir in $directories) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "Created directory: $dir" -ForegroundColor Gray
    }
}

# Set environment variables
$env:ASPNETCORE_ENVIRONMENT = $Environment

# Build the Docker image if requested
if ($Build) {
    Write-Host "Building Docker image..." -ForegroundColor Yellow
    docker build -t identity-all-services .
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build Docker image"
        exit 1
    }
    Write-Host "Docker image built successfully" -ForegroundColor Green
}

# Start the services
Write-Host "Starting all Identity Server services..." -ForegroundColor Yellow

$detachFlag = if ($Detach) { "-d" } else { "" }

docker-compose -f docker-compose-all.yml up $detachFlag

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start services"
    exit 1
}

if ($Detach) {
    Write-Host "Services started in detached mode" -ForegroundColor Green
    Write-Host "Use 'docker-compose -f docker-compose-all.yml logs -f' to view logs" -ForegroundColor Cyan
    Write-Host "Use 'docker-compose -f docker-compose-all.yml down' to stop services" -ForegroundColor Cyan
} else {
    Write-Host "Services are running. Press Ctrl+C to stop." -ForegroundColor Green
}

