<#
.SYNOPSIS
    Configures IIS application pools and applications for the Viking combined services.

.DESCRIPTION
    Creates the ConnectomeODataV4 (.NET 4.8) and DataExport (ASP.NET Core) application pools and
    applications under one IIS site, sets MIME types and folder permissions, then restarts IIS.
    Used by the VolumeAnnotationServices Windows container and by deploy/Deploy-VikingIis.ps1
    -Action ConfigureIIS. The defaults reproduce the original hard-coded paths.

    The WCF AnnotationService is retired on dev and built and deployed from VikingLegacy; this
    script does not create, remove or change its pool or application.

.PARAMETER WebRoot
    Folder holding the odata and dataexport application folders. Default C:\inetpub\wwwroot.

.PARAMETER SiteName
    IIS site that receives the applications. Default "Default Web Site".

.EXAMPLE
    .\ConfigureIIS.ps1
    Configures C:\inetpub\wwwroot\{odata,dataexport} under Default Web Site.

.EXAMPLE
    .\ConfigureIIS.ps1 -WebRoot D:\inetpub\viking -SiteName "Viking"

.NOTES
    Run as Administrator on the IIS host. Removes and recreates the two pools and applications.
#>
param(
    [string]$WebRoot = "C:\inetpub\wwwroot",
    [string]$SiteName = "Default Web Site"
)
# Script to configure IIS for Viking Combined Services
# This script sets up the two services under different application paths

Import-Module WebAdministration

Write-Host "Configuring IIS for Viking Combined Services..." -ForegroundColor Green

# Ensure IIS is running
Start-Service W3SVC -ErrorAction SilentlyContinue

# Application paths
$odataPath = Join-Path $WebRoot "odata"
$dataexportPath = Join-Path $WebRoot "dataexport"

# Remove default website content
Write-Host "Cleaning up default website..." -ForegroundColor Yellow
Remove-Item (Join-Path $WebRoot "iisstart.*") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $WebRoot "index.html") -Force -ErrorAction SilentlyContinue

# Create Application Pools
Write-Host "Creating Application Pools..." -ForegroundColor Yellow

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
if (Test-Path "IIS:\Sites\$SiteName\odata") {
    Remove-WebApplication -Name "odata" -Site $SiteName
}
if (Test-Path "IIS:\Sites\$SiteName\dataexport") {
    Remove-WebApplication -Name "dataexport" -Site $SiteName
}

# Create new applications
New-WebApplication -Name "odata" -Site $SiteName -PhysicalPath $odataPath -ApplicationPool "ConnectomeODataPool" -Force
New-WebApplication -Name "dataexport" -Site $SiteName -PhysicalPath $dataexportPath -ApplicationPool "DataExportPool" -Force

# Configure MIME types for DataExport (ASP.NET Core)
Write-Host "Configuring MIME types..." -ForegroundColor Yellow
Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/staticContent" -name "." -value @{fileExtension='.json';mimeType='application/json'} -ErrorAction SilentlyContinue
Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/staticContent" -name "." -value @{fileExtension='.wasm';mimeType='application/wasm'} -ErrorAction SilentlyContinue

# Set proper permissions
Write-Host "Setting permissions..." -ForegroundColor Yellow
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")

$acl = Get-Acl $odataPath
$acl.SetAccessRule($accessRule)
Set-Acl $odataPath $acl

$acl = Get-Acl $dataexportPath
$acl.SetAccessRule($accessRule)
Set-Acl $dataexportPath $acl

# Enable detailed errors for debugging (optional - remove in production)
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST/$SiteName" -filter "system.webServer/httpErrors" -name "errorMode" -value "Detailed"

# Restart IIS
Write-Host "Restarting IIS..." -ForegroundColor Yellow
iisreset /restart

Write-Host "IIS Configuration Complete!" -ForegroundColor Green
Write-Host "Services are now available at:" -ForegroundColor Cyan
Write-Host "  - http://localhost/odata        (ConnectomeODataV4 - Web API)" -ForegroundColor White
Write-Host "  - http://localhost/dataexport   (DataExport - ASP.NET Core)" -ForegroundColor White









