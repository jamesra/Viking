# IIS DataExport Permissions Fix Script
# Run this as Administrator to grant permissions to the app pool

# ===== UPDATE THESE VARIABLES =====
$appPath = "C:\inetpub\wwwroot\NeitzTemporalMonkey\Export"  # Web app path (web.config, appsettings)
$binariesPath = "C:\Services\Debug\Export"  # Binaries path (DLLs)
$appPoolName = "net9-TemporalMonkey"
# ==================================

$identity = "IIS AppPool\$appPoolName"

Write-Host "Setting up DataExport IIS permissions..." -ForegroundColor Cyan
Write-Host "Web App Path: $appPath" -ForegroundColor Yellow
Write-Host "Binaries Path: $binariesPath" -ForegroundColor Yellow
Write-Host "App Pool: $appPoolName" -ForegroundColor Yellow
Write-Host ""

# Verify paths exist
$pathsExist = $false
if (-not (Test-Path $appPath)) {
    Write-Host "WARNING: Web app path does not exist: $appPath" -ForegroundColor Yellow
} else {
    $pathsExist = $true
}

if (-not (Test-Path $binariesPath)) {
    Write-Host "WARNING: Binaries path does not exist: $binariesPath" -ForegroundColor Yellow
} else {
    $pathsExist = $true
}

if (-not $pathsExist) {
    Write-Host "ERROR: Neither path exists!" -ForegroundColor Red
    Write-Host "Please verify the paths at the top of this script." -ForegroundColor Yellow
    exit 1
}

# Create required folders in web app path
Write-Host "Creating required folders..." -ForegroundColor Cyan
if (Test-Path $appPath) {
    @("logs", "Output") | ForEach-Object {
        $folderPath = Join-Path $appPath $_
        if (-not (Test-Path $folderPath)) {
            New-Item -ItemType Directory -Path $folderPath -Force | Out-Null
            Write-Host "  ✓ Created folder: $_" -ForegroundColor Green
        } else {
            Write-Host "  ✓ Folder exists: $_" -ForegroundColor Green
        }
    }
}

# Grant permissions to app pool identity
Write-Host "`nGranting permissions to $identity..." -ForegroundColor Cyan

# Web app folder (web.config, appsettings) - Read & Execute on root
if (Test-Path $appPath) {
    icacls "$appPath" /grant "${identity}:(OI)(CI)RX" | Out-Null
    Write-Host "  ✓ Granted Read & Execute on web app folder" -ForegroundColor Green
    
    # Logs folder - MODIFY permissions (needs write access)
    $logsPath = Join-Path $appPath "logs"
    if (Test-Path $logsPath) {
        icacls "$logsPath" /grant "${identity}:(OI)(CI)M" /T | Out-Null
        Write-Host "  ✓ Granted MODIFY (write) permissions on logs folder" -ForegroundColor Green
    }
    
    # Output folder - MODIFY permissions (needs write access)
    $outputPath = Join-Path $appPath "Output"
    if (Test-Path $outputPath) {
        icacls "$outputPath" /grant "${identity}:(OI)(CI)M" /T | Out-Null
        Write-Host "  ✓ Granted MODIFY (write) permissions on Output folder" -ForegroundColor Green
    }
}

# Binaries folder - Read & Execute
if (Test-Path $binariesPath) {
    icacls "$binariesPath" /grant "${identity}:(OI)(CI)RX" /T | Out-Null
    Write-Host "  ✓ Granted Read & Execute on binaries folder" -ForegroundColor Green
}

# Also grant to IIS_IUSRS group
Write-Host "`nGranting permissions to IIS_IUSRS..." -ForegroundColor Cyan
if (Test-Path $appPath) {
    icacls "$appPath" /grant "IIS_IUSRS:(OI)(CI)RX" /T | Out-Null
    Write-Host "  ✓ Granted Read & Execute to IIS_IUSRS (web app)" -ForegroundColor Green
}
if (Test-Path $binariesPath) {
    icacls "$binariesPath" /grant "IIS_IUSRS:(OI)(CI)RX" /T | Out-Null
    Write-Host "  ✓ Granted Read & Execute to IIS_IUSRS (binaries)" -ForegroundColor Green
}

# Configure App Pool for .NET Core
Write-Host "`nConfiguring App Pool for .NET Core..." -ForegroundColor Cyan
Import-Module WebAdministration

if (Test-Path "IIS:\AppPools\$appPoolName") {
    Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name managedPipelineMode -Value "Integrated"
    Write-Host "  ✓ App Pool configured for .NET Core" -ForegroundColor Green
} else {
    Write-Host "  ✗ App Pool not found: $appPoolName" -ForegroundColor Red
    Write-Host "  Please verify the app pool name." -ForegroundColor Yellow
}

# Restart App Pool and IIS
Write-Host "`nRestarting IIS..." -ForegroundColor Cyan
try {
    Stop-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue
    Write-Host "  ✓ App Pool restarted" -ForegroundColor Green
} catch {
    Write-Host "  ! Could not restart app pool (may not exist yet)" -ForegroundColor Yellow
}

iisreset /noforce | Out-Null
Write-Host "  ✓ IIS reset complete" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Setup complete!" -ForegroundColor Green
Write-Host "Try accessing your DataExport site now." -ForegroundColor Green
Write-Host "`nIf issues persist:" -ForegroundColor Yellow
Write-Host "  1. Run DiagnoseIIS.ps1" -ForegroundColor Yellow
Write-Host "  2. Check logs at: $appPath\logs" -ForegroundColor Yellow
Write-Host "  3. Check Event Viewer (eventvwr.msc)" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Cyan

