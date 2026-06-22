# Script to configure IIS for Viking Combined Services
# This script sets up the three services under different application paths

Import-Module WebAdministration

Write-Host "Configuring IIS for Viking Combined Services..." -ForegroundColor Green

# Ensure IIS is running
Start-Service W3SVC -ErrorAction SilentlyContinue

# Application paths
$annotationPath = "C:\inetpub\wwwroot\annotation"
$odataPath = "C:\inetpub\wwwroot\odata"
$dataexportPath = "C:\inetpub\wwwroot\dataexport"

# Remove default website content
Write-Host "Cleaning up default website..." -ForegroundColor Yellow
Remove-Item "C:\inetpub\wwwroot\iisstart.*" -Force -ErrorAction SilentlyContinue
Remove-Item "C:\inetpub\wwwroot\index.html" -Force -ErrorAction SilentlyContinue

# Create Application Pools
Write-Host "Creating Application Pools..." -ForegroundColor Yellow

# AnnotationService Pool (WCF Service - .NET 4.8)
if (Test-Path "IIS:\AppPools\AnnotationServicePool") {
    Remove-WebAppPool -Name "AnnotationServicePool"
}
New-WebAppPool -Name "AnnotationServicePool" -Force
Set-ItemProperty "IIS:\AppPools\AnnotationServicePool" -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty "IIS:\AppPools\AnnotationServicePool" -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty "IIS:\AppPools\AnnotationServicePool" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\AnnotationServicePool" -Name "processModel.loadUserProfile" -Value $true

# ConnectomeOData Pool (Web API - .NET 4.8)
if (Test-Path "IIS:\AppPools\ConnectomeODataPool") {
    Remove-WebAppPool -Name "ConnectomeODataPool"
}
New-WebAppPool -Name "ConnectomeODataPool" -Force
Set-ItemProperty "IIS:\AppPools\ConnectomeODataPool" -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty "IIS:\AppPools\ConnectomeODataPool" -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty "IIS:\AppPools\ConnectomeODataPool" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# DataExport Pool (ASP.NET Core - .NET 9.0)
if (Test-Path "IIS:\AppPools\DataExportPool") {
    Remove-WebAppPool -Name "DataExportPool"
}
New-WebAppPool -Name "DataExportPool" -Force
Set-ItemProperty "IIS:\AppPools\DataExportPool" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\DataExportPool" -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty "IIS:\AppPools\DataExportPool" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# Create Web Applications
Write-Host "Creating Web Applications..." -ForegroundColor Yellow

# Remove existing applications if they exist
if (Test-Path "IIS:\Sites\Default Web Site\annotation") {
    Remove-WebApplication -Name "annotation" -Site "Default Web Site"
}
if (Test-Path "IIS:\Sites\Default Web Site\odata") {
    Remove-WebApplication -Name "odata" -Site "Default Web Site"
}
if (Test-Path "IIS:\Sites\Default Web Site\dataexport") {
    Remove-WebApplication -Name "dataexport" -Site "Default Web Site"
}

# Create new applications
New-WebApplication -Name "annotation" -Site "Default Web Site" -PhysicalPath $annotationPath -ApplicationPool "AnnotationServicePool" -Force
New-WebApplication -Name "odata" -Site "Default Web Site" -PhysicalPath $odataPath -ApplicationPool "ConnectomeODataPool" -Force
New-WebApplication -Name "dataexport" -Site "Default Web Site" -PhysicalPath $dataexportPath -ApplicationPool "DataExportPool" -Force

# Configure MIME types for DataExport (ASP.NET Core)
Write-Host "Configuring MIME types..." -ForegroundColor Yellow
Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/staticContent" -name "." -value @{fileExtension='.json';mimeType='application/json'} -ErrorAction SilentlyContinue
Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/staticContent" -name "." -value @{fileExtension='.wasm';mimeType='application/wasm'} -ErrorAction SilentlyContinue

# Set proper permissions
Write-Host "Setting permissions..." -ForegroundColor Yellow
$acl = Get-Acl $annotationPath
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($accessRule)
Set-Acl $annotationPath $acl

$acl = Get-Acl $odataPath
$acl.SetAccessRule($accessRule)
Set-Acl $odataPath $acl

$acl = Get-Acl $dataexportPath
$acl.SetAccessRule($accessRule)
Set-Acl $dataexportPath $acl

# Enable detailed errors for debugging (optional - remove in production)
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST/Default Web Site' -filter "system.webServer/httpErrors" -name "errorMode" -value "Detailed"

# Restart IIS
Write-Host "Restarting IIS..." -ForegroundColor Yellow
iisreset /restart

Write-Host "IIS Configuration Complete!" -ForegroundColor Green
Write-Host "Services are now available at:" -ForegroundColor Cyan
Write-Host "  - http://localhost/annotation   (AnnotationService - WCF)" -ForegroundColor White
Write-Host "  - http://localhost/odata        (ConnectomeODataV4 - Web API)" -ForegroundColor White
Write-Host "  - http://localhost/dataexport   (DataExport - ASP.NET Core)" -ForegroundColor White









