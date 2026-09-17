<#
.SYNOPSIS
    Deploys the static Export Portal to the /Export application on a Viking web server.

.DESCRIPTION
    The Export Portal is a static HTML/CSS/JS front end that builds URLs against the existing
    per-volume DataExport services. It does not replace or modify those services.

    This script copies the portal into the physical path behind the site's /Export application
    and writes a web.config that serves it as plain static content.

    Two details of the target environment are handled explicitly:

    - The /Export path previously hosted an ASP.NET Core copy of DataExport. Its content root
      (appsettings.json, Content, Resources, Output, logs) is removed, because leaving the
      aspNetCore handler in place would keep IIS routing requests into a hosted app instead of
      serving files.

    - The site root web.config redirects to /Export via httpRedirect. That setting is inherited
      by child paths, so a static /Export would redirect to itself indefinitely. The web.config
      written here disables httpRedirect for this application.

.PARAMETER Source
    The ExportPortal folder in the repository. Defaults to the Servers\ExportPortal folder
    alongside this script.

.PARAMETER Destination
    Physical path behind the site's /Export application. Defaults to C:\inetpub\wwwroot\Export.

.PARAMETER BackupFolder
    Where the previous contents of Destination are copied before removal.
    Defaults to a timestamped folder under C:\Services.

.PARAMETER WhatIf
    Reports the actions that would be taken without changing anything.

.EXAMPLE
    .\Deploy-ExportPortal.ps1

    Deploys to C:\inetpub\wwwroot\Export on the local machine.

.EXAMPLE
    .\Deploy-ExportPortal.ps1 -Destination "\\192.168.0.80\c$\inetpub\wwwroot\Export"

    Deploys to a remote server over an administrative share.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Source,
    [string]$Destination = "C:\inetpub\wwwroot\Export",
    [string]$BackupFolder
)

$ErrorActionPreference = "Stop"

if (-not $Source) {
    $Source = Join-Path (Split-Path $PSScriptRoot -Parent) "Servers\ExportPortal"
}

if (-not (Test-Path $Source)) {
    throw "Portal source not found: $Source"
}

# index.html is the entry point; without it IIS would serve a directory listing or a 403.
$RequiredItems = @("index.html", "css", "js", "img")
foreach ($Item in $RequiredItems) {
    if (-not (Test-Path (Join-Path $Source $Item))) {
        throw "Portal source is incomplete, missing '$Item' in: $Source"
    }
}

# A JPEG that serves correctly but cannot be decoded produces a blank banner and no error
# anywhere in the logs, so the image headers are checked before anything is copied.
$Magic = @{
    ".jpg"  = @(0xFF, 0xD8, 0xFF)
    ".jpeg" = @(0xFF, 0xD8, 0xFF)
    ".png"  = @(0x89, 0x50, 0x4E, 0x47)
}
$BadImages = @()
foreach ($Image in Get-ChildItem (Join-Path $Source "img") -File) {
    $Expected = $Magic[$Image.Extension.ToLowerInvariant()]
    if (-not $Expected) { continue }

    $Stream = [System.IO.File]::OpenRead($Image.FullName)
    try {
        $Header = New-Object byte[] $Expected.Count
        $Read = $Stream.Read($Header, 0, $Expected.Count)
    } finally {
        $Stream.Close()
    }

    if ($Read -ne $Expected.Count) {
        $BadImages += $Image.Name
        continue
    }
    for ($i = 0; $i -lt $Expected.Count; $i++) {
        if ($Header[$i] -ne $Expected[$i]) { $BadImages += $Image.Name; break }
    }
}
if ($BadImages.Count -gt 0) {
    throw "These images are corrupt and would render blank: $($BadImages -join ', ')"
}

Write-Host "Source:      $Source"
Write-Host "Destination: $Destination"

if (Test-Path $Destination) {
    if (-not $BackupFolder) {
        $BackupFolder = "C:\Services\_export-portal-backup-{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss")
    }
    if ($PSCmdlet.ShouldProcess($Destination, "Back up existing contents to $BackupFolder")) {
        New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
        Copy-Item -Path (Join-Path $Destination "*") -Destination $BackupFolder -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Backed up previous contents to $BackupFolder"
    }

    # Remnants of the ASP.NET Core deployment that used to live here.
    $Stale = @("appsettings.json", "appsettings.Development.json", "appsettings.Docker.json",
               "Content", "Resources", "Output", "logs", "web.config")
    # The outgoing application may still be running and holding its stdout log open. That lock
    # disappears when IIS reloads, and a stranded log file does no harm once the aspNetCore
    # handler is gone, so a failure to delete must not abort the deployment.
    $Locked = @()
    foreach ($Name in $Stale) {
        $Path = Join-Path $Destination $Name
        if (Test-Path $Path) {
            if ($PSCmdlet.ShouldProcess($Path, "Remove stale DataExport content")) {
                Remove-Item $Path -Recurse -Force -ErrorAction SilentlyContinue -ErrorVariable RemoveErrors
                if (Test-Path $Path) { $Locked += $Name }
            }
        }
    }
    if ($Locked.Count -gt 0) {
        Write-Warning ("Still in use, left in place: {0}. Re-run after IIS reloads to clear them." -f ($Locked -join ', '))
    }
} else {
    if ($PSCmdlet.ShouldProcess($Destination, "Create destination folder")) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
}

foreach ($Item in $RequiredItems) {
    $From = Join-Path $Source $Item
    if ($PSCmdlet.ShouldProcess($From, "Copy to $Destination")) {
        Copy-Item -Path $From -Destination $Destination -Recurse -Force
    }
}

$WebConfig = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <!-- The site root redirects to /Export. Inheriting that here would redirect this
           application to itself, so it is switched off for this path. -->
      <httpRedirect enabled="false" />

      <!-- This application replaced a hosted ASP.NET Core copy of DataExport. Clearing the
           handler list inherited from that deployment is what makes IIS serve files. -->
      <handlers>
        <clear />
        <add name="StaticFile" path="*" verb="*" modules="StaticFileModule,DefaultDocumentModule,DirectoryListingModule" resourceType="Either" requireAccess="Read" />
      </handlers>

      <defaultDocument enabled="true">
        <files>
          <clear />
          <add value="index.html" />
        </files>
      </defaultDocument>

      <directoryBrowse enabled="false" />

      <staticContent>
        <remove fileExtension=".json" />
        <mimeMap fileExtension=".json" mimeType="application/json" />
        <!-- The portal is edited in place often enough that a long cache lifetime causes more
             confusion than the bandwidth is worth. -->
        <clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="0:10:00" />
      </staticContent>
    </system.webServer>
  </location>
</configuration>
'@

$WebConfigPath = Join-Path $Destination "web.config"
if ($PSCmdlet.ShouldProcess($WebConfigPath, "Write static-content web.config")) {
    Set-Content -Path $WebConfigPath -Value $WebConfig -Encoding UTF8
}

Write-Host ""
Write-Host "Deployed. Verify:"
Write-Host "  https://websvc.codepharm.net/Export/"
Write-Host "  https://websvc.codepharm.net/          (should redirect to /Export)"
