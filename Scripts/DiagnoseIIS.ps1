# IIS DataExport Diagnostic Script
# Run this as Administrator to diagnose IIS permissions issues

# ===== UPDATE THESE VARIABLES =====
$appPath = "C:\inetpub\wwwroot\NeitzTemporalMonkey\Export"  # Web app path (web.config, appsettings)
$binariesPath = "C:\Services\Debug\Export"  # Binaries path (DLLs)
$appPoolName = "net9-TemporalMonkey"
# ==================================

Write-Host "`n========== IIS DIAGNOSTIC ==========" -ForegroundColor Cyan

# 1. Check if paths exist
Write-Host "`n1. Checking application paths..." -ForegroundColor Yellow
if (Test-Path $appPath) {
    Write-Host "   ✓ Web app path exists: $appPath" -ForegroundColor Green
} else {
    Write-Host "   ✗ Web app path does NOT exist: $appPath" -ForegroundColor Red
}

if (Test-Path $binariesPath) {
    Write-Host "   ✓ Binaries path exists: $binariesPath" -ForegroundColor Green
} else {
    Write-Host "   ✗ Binaries path does NOT exist: $binariesPath" -ForegroundColor Red
}

if (-not (Test-Path $appPath) -and -not (Test-Path $binariesPath)) {
    Write-Host "   STOP: Both paths are missing!" -ForegroundColor Red
    exit
}

# 2. Check if web.config exists
Write-Host "`n2. Checking web.config..." -ForegroundColor Yellow
$webConfigPath = Join-Path $appPath "web.config"
if (Test-Path $webConfigPath) {
    Write-Host "   ✓ web.config exists" -ForegroundColor Green
} else {
    Write-Host "   ✗ web.config does NOT exist" -ForegroundColor Red
}

# 3. Check if DataExport.dll exists
Write-Host "`n3. Checking DataExport.dll..." -ForegroundColor Yellow
$dllPathWeb = Join-Path $appPath "DataExport.dll"
$dllPathBin = Join-Path $binariesPath "DataExport.dll"

if (Test-Path $dllPathWeb) {
    Write-Host "   ✓ DataExport.dll exists in web app path" -ForegroundColor Green
} elseif (Test-Path $dllPathBin) {
    Write-Host "   ✓ DataExport.dll exists in binaries path" -ForegroundColor Green
    Write-Host "   (Split deployment detected - binaries in separate location)" -ForegroundColor Cyan
} else {
    Write-Host "   ✗ DataExport.dll does NOT exist in either location" -ForegroundColor Red
    Write-Host "   You may need to publish/deploy the application first" -ForegroundColor Yellow
}

# 4. Check App Pool exists
Write-Host "`n4. Checking application pool..." -ForegroundColor Yellow
Import-Module WebAdministration
if (Test-Path "IIS:\AppPools\$appPoolName") {
    Write-Host "   ✓ App pool exists: $appPoolName" -ForegroundColor Green
    $appPool = Get-Item "IIS:\AppPools\$appPoolName"
    Write-Host "   Identity Type: $($appPool.processModel.identityType)" -ForegroundColor Cyan
    Write-Host "   .NET CLR Version: $($appPool.managedRuntimeVersion)" -ForegroundColor Cyan
    Write-Host "   State: $($appPool.state)" -ForegroundColor Cyan
} else {
    Write-Host "   ✗ App pool does NOT exist: $appPoolName" -ForegroundColor Red
}

# 5. Check permissions
Write-Host "`n5. Checking permissions..." -ForegroundColor Yellow
$identity = "IIS AppPool\$appPoolName"

# Check web.config permissions
if (Test-Path $webConfigPath) {
    $acl = Get-Acl $webConfigPath
    $hasPermission = $acl.Access | Where-Object { 
        $_.IdentityReference -like "*$appPoolName*" -or 
        $_.IdentityReference -eq "BUILTIN\IIS_IUSRS" 
    }

    if ($hasPermission) {
        Write-Host "   ✓ web.config has permissions:" -ForegroundColor Green
        $hasPermission | ForEach-Object {
            Write-Host "     $($_.IdentityReference): $($_.FileSystemRights)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "   ✗ web.config: No permissions for app pool identity" -ForegroundColor Red
    }
}

# Check binaries folder permissions
if (Test-Path $binariesPath) {
    $aclBin = Get-Acl $binariesPath
    $hasBinPermission = $aclBin.Access | Where-Object { 
        $_.IdentityReference -like "*$appPoolName*" -or 
        $_.IdentityReference -eq "BUILTIN\IIS_IUSRS" 
    }

    if ($hasBinPermission) {
        Write-Host "   ✓ Binaries folder has permissions:" -ForegroundColor Green
        $hasBinPermission | Select-Object -First 2 | ForEach-Object {
            Write-Host "     $($_.IdentityReference): $($_.FileSystemRights)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "   ✗ Binaries folder: No permissions for app pool identity" -ForegroundColor Red
    }
}

# 6. Recent errors
Write-Host "`n6. Recent IIS errors (last 30 minutes)..." -ForegroundColor Yellow
$errors = Get-EventLog -LogName Application -After (Get-Date).AddMinutes(-30) -EntryType Error -ErrorAction SilentlyContinue | 
    Where-Object { $_.Source -like "*IIS*" -or $_.Source -like "*ASP.NET*" } |
    Select-Object -First 3

if ($errors) {
    $errors | ForEach-Object {
        Write-Host "   [$($_.TimeGenerated)] $($_.Source)" -ForegroundColor Red
        Write-Host "   $($_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)))..." -ForegroundColor Red
        Write-Host ""
    }
} else {
    Write-Host "   (No recent IIS errors found)" -ForegroundColor Green
}

Write-Host "`n========== END DIAGNOSTIC ==========" -ForegroundColor Cyan

