<#
.SYNOPSIS
    Deploys the legacy IIS services (WCF annotation, DataExport, IIS configuration, TLS certificate)
    for the viking-iis stack.

.DESCRIPTION
    Thin wrapper over the existing scripts, with the paths taken from parameters or from the
    viking-iis run env (deploy/examples/example.viking-iis.run.env) instead of the hard-coded
    \\OPR-MARC-WEBSV2 and C:\ paths:

      Annotation    VikingLegacy's Servers/AnnotationService/DeployProduction.cmd copies the WCF
                    AnnotationService bin folder to <DeployRoot>\Release\AnnotationService\bin (web.config kept).
                    Runs on the build machine; DeployRoot may be a UNC path to the IIS host.
      Export        Scripts/Deploy-ExportApplication.ps1 once per volume (RC1, RC2, ...).
                    Runs on the IIS host as Administrator.
      ConfigureIIS  Scripts/ConfigureIIS.ps1 (pools + applications). Runs on the IIS host as
                    Administrator. Destructive: it recreates the odata and dataexport pools and
                    applications (the WCF annotation application is left alone).
      Certificate   deploy/Install-IisCertificate.ps1 with the cert.pfx viking-certs publishes.
                    Runs on the IIS host as Administrator.
      Status        Prints the resolved settings and exits.

    nornir-docker's stack.ps1 runs this script for the viking-iis stack (registry Deploy field)
    with the run env exported, so every parameter can come from VIKING_IIS_* variables.

.PARAMETER Action
    One or more of Annotation, Export, ConfigureIIS, Certificate, Status. Default Annotation, the
    usual production update.

.PARAMETER DeployRoot
    Deployment root on the IIS host that holds Release\AnnotationService and Release\Export, as a
    local path on the IIS host (C:\Services) or a UNC path from the build machine
    (\\iis-host\C$\Services). Default $env:VIKING_IIS_DEPLOY_ROOT.

.PARAMETER RepoRoot
    Viking checkout with the built binaries. Default $env:VIKING_REPO_ROOT_HOST, else this script's repo.

.PARAMETER LegacyRepoRoot
    VikingLegacy checkout, where the WCF AnnotationService is built. Default $env:VIKING_LEGACY_REPO_ROOT, else VikingLegacy beside RepoRoot.

.PARAMETER AnnotationSourceBin
    Built WCF AnnotationService bin folder. Default <LegacyRepoRoot>\Servers\AnnotationService\AnnotationService\bin.

.PARAMETER Volume
    Volume folder names (relative to SiteRoot) for -Action Export. Default $env:VIKING_IIS_VOLUMES
    (comma separated).

.PARAMETER SiteRoot
    IIS root that holds the volume folders (Deploy-ExportApplication.ps1 -RootFolder). Default
    $env:VIKING_IIS_SITE_ROOT; empty lets that script use the Default Web Site root.

.PARAMETER UrlBase
    Public base URL of the IIS services. Default $env:VIKING_IIS_URL_BASE, else https://$env:VIKING_HOSTNAME_WEBSVC/.

.PARAMETER ExportSourceFolder
    Published DataExport folder on the IIS host. Default $env:VIKING_IIS_EXPORT_SOURCE, else <DeployRoot>\Release\Export.

.PARAMETER WebRoot
    ConfigureIIS.ps1 -WebRoot. Default $env:VIKING_IIS_WEBROOT, else C:\inetpub\wwwroot.

.PARAMETER SiteName
    IIS site for ConfigureIIS and Certificate. Default $env:VIKING_IIS_SITE_NAME, else "Default Web Site".

.PARAMETER PfxPath
    cert.pfx for -Action Certificate. Default $env:VIKING_IIS_PFX_PATH.

.PARAMETER PfxPassword
    Password of the PFX (TLS_PFX_PASSWORD on the renewer). Empty by default.

.EXAMPLE
    pwsh deploy/Deploy-VikingIis.ps1 -Action Status
    Shows the resolved paths.

.EXAMPLE
    pwsh deploy/Deploy-VikingIis.ps1 -Action Annotation -DeployRoot \\iis-host\C$\Services
    Copies the WCF binaries from the build machine (same as the old DeployProduction.cmd).

.EXAMPLE
    pwsh deploy/Deploy-VikingIis.ps1 -Action Export,Certificate -DeployRoot C:\Services -Volume RC1,RC2 -PfxPath C:\certs\cert.pfx
    On the IIS host: redeploys DataExport for two volumes and installs the renewed certificate.

.NOTES
    The wrapped scripts are unchanged except that their paths are parameters now (defaults keep the
    old values). See Documentation/source/deployment/iis_legacy_services.rst.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Annotation', 'Export', 'ConfigureIIS', 'Certificate', 'Status')]
    [string[]]$Action = @('Annotation'),
    [string]$DeployRoot = $env:VIKING_IIS_DEPLOY_ROOT,
    [string]$RepoRoot = $env:VIKING_REPO_ROOT_HOST,
    [string]$LegacyRepoRoot = $env:VIKING_LEGACY_REPO_ROOT,
    [string]$AnnotationSourceBin,
    [string[]]$Volume,
    [string]$SiteRoot = $env:VIKING_IIS_SITE_ROOT,
    [string]$UrlBase = $env:VIKING_IIS_URL_BASE,
    [string]$ExportSourceFolder = $env:VIKING_IIS_EXPORT_SOURCE,
    [string]$WebRoot = $env:VIKING_IIS_WEBROOT,
    [string]$SiteName = $env:VIKING_IIS_SITE_NAME,
    [string]$PfxPath = $env:VIKING_IIS_PFX_PATH,
    [securestring]$PfxPassword
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
if (-not $LegacyRepoRoot) { $LegacyRepoRoot = Join-Path (Split-Path $RepoRoot -Parent) 'VikingLegacy' }
if (-not $AnnotationSourceBin) { $AnnotationSourceBin = Join-Path $LegacyRepoRoot 'Servers\AnnotationService\AnnotationService\bin' }
if (-not $Volume -and $env:VIKING_IIS_VOLUMES) {
    $Volume = @($env:VIKING_IIS_VOLUMES -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
if (-not $UrlBase -and $env:VIKING_HOSTNAME_WEBSVC) { $UrlBase = "https://$($env:VIKING_HOSTNAME_WEBSVC)/" }
if (-not $ExportSourceFolder -and $DeployRoot) { $ExportSourceFolder = Join-Path $DeployRoot 'Release\Export' }
if (-not $WebRoot) { $WebRoot = 'C:\inetpub\wwwroot' }
if (-not $SiteName) { $SiteName = 'Default Web Site' }
if (-not $PfxPassword -and $env:TLS_PFX_PASSWORD) {
    $PfxPassword = ConvertTo-SecureString $env:TLS_PFX_PASSWORD -AsPlainText -Force
}

<#
.SYNOPSIS
    Throws with a clear message when a required setting is empty.
#>
function Assert-Setting([string]$Name, [string]$Value, [string]$EnvName) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is not set. Pass -$Name or set $EnvName in the viking-iis run env."
    }
}

$repo = $RepoRoot
foreach ($a in $Action) {
    switch ($a) {
        'Status' {
            [pscustomobject]@{
                RepoRoot            = $repo
                LegacyRepoRoot      = $LegacyRepoRoot
                DeployRoot          = $DeployRoot
                AnnotationSourceBin = $AnnotationSourceBin
                AnnotationTarget    = if ($DeployRoot) { Join-Path $DeployRoot 'Release\AnnotationService\bin' } else { '' }
                Volumes             = ($Volume -join ',')
                SiteRoot            = $SiteRoot
                UrlBase             = $UrlBase
                ExportSourceFolder  = $ExportSourceFolder
                WebRoot             = $WebRoot
                SiteName            = $SiteName
                PfxPath             = $PfxPath
            } | Format-List
        }

        'Annotation' {
            Assert-Setting 'DeployRoot' $DeployRoot 'VIKING_IIS_DEPLOY_ROOT'
            $target = Join-Path $DeployRoot 'Release\AnnotationService\bin'
            $cmd = Join-Path $LegacyRepoRoot 'Servers\AnnotationService\DeployProduction.cmd'
            if (-not (Test-Path -LiteralPath $AnnotationSourceBin)) { throw "Build output not found: $AnnotationSourceBin" }
            if ($PSCmdlet.ShouldProcess($target, "robocopy AnnotationService bin from $AnnotationSourceBin")) {
                & cmd.exe /d /c "`"$cmd`" `"$AnnotationSourceBin`" `"$target`""
                # robocopy: 0-7 are success codes, 8+ failure.
                if ($LASTEXITCODE -ge 8) { throw "DeployProduction.cmd failed (robocopy exit $LASTEXITCODE)." }
                Write-Host "AnnotationService binaries copied to $target (robocopy exit $LASTEXITCODE)."
                Write-Host 'Recycle the AnnotationService application pool on the IIS host if it does not pick them up.'
            }
        }

        'Export' {
            Assert-Setting 'DeployRoot' $DeployRoot 'VIKING_IIS_DEPLOY_ROOT'
            Assert-Setting 'UrlBase' $UrlBase 'VIKING_IIS_URL_BASE or VIKING_HOSTNAME_WEBSVC'
            if (-not $Volume) { throw 'No volumes. Pass -Volume RC1,RC2 or set VIKING_IIS_VOLUMES.' }
            $script = Join-Path $repo 'Scripts\Deploy-ExportApplication.ps1'
            foreach ($v in $Volume) {
                if ($PSCmdlet.ShouldProcess($v, 'Deploy DataExport application')) {
                    $exportArgs = @{ RelativePath = $v; UrlBase = $UrlBase; SourceFolder = $ExportSourceFolder }
                    if ($SiteRoot) { $exportArgs.RootFolder = $SiteRoot }
                    & $script @exportArgs
                    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Deploy-ExportApplication.ps1 failed for $v (exit $LASTEXITCODE)." }
                }
            }
        }

        'ConfigureIIS' {
            $script = Join-Path $repo 'Scripts\ConfigureIIS.ps1'
            if ($PSCmdlet.ShouldProcess("$SiteName ($WebRoot)", 'Recreate odata/dataexport pools and applications, then iisreset')) {
                & $script -WebRoot $WebRoot -SiteName $SiteName
            }
        }

        'Certificate' {
            Assert-Setting 'PfxPath' $PfxPath 'VIKING_IIS_PFX_PATH'
            Assert-Setting 'VIKING_HOSTNAME_WEBSVC' $env:VIKING_HOSTNAME_WEBSVC 'VIKING_HOSTNAME_WEBSVC'
            $script = Join-Path $PSScriptRoot 'Install-IisCertificate.ps1'
            $certArgs = @{ PfxPath = $PfxPath; HostHeader = @($env:VIKING_HOSTNAME_WEBSVC); SiteName = $SiteName; RemoveOld = $true }
            if ($PfxPassword) { $certArgs.PfxPassword = $PfxPassword }
            if ($PSCmdlet.ShouldProcess($PfxPath, 'Import certificate and rebind https')) {
                & $script @certArgs
            }
        }
    }
}
