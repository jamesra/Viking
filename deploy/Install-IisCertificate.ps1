<#
.SYNOPSIS
    Imports a PFX into LocalMachine\My and points the IIS https binding(s) for a host name at it.

.DESCRIPTION
    Used on the legacy IIS host after viking-certs renews the shared certificate
    (DOCKER_CERTS_DIR/<TLS_CERT_NAME>/cert.pfx, copied to the IIS host). For each -HostHeader it
    finds the https binding on -Port with that host header and assigns the new certificate. When a
    host has no binding of its own it rebinds the catch-all https binding (empty host header) on that
    port, or creates an SNI binding when -CreateBinding is given.

    The WCF AnnotationService web.config finds its service certificate by serial number
    (serviceCertificate findValue, x509FindType="FindBySerialNumber"). The script prints the new
    serial number; update web.config when the AnnotationService uses message security with it.

.PARAMETER PfxPath
    Path to the PFX (cert.pfx from viking-certs).

.PARAMETER HostHeader
    One or more host names, for example websvc.connectomes.pitt.edu (VIKING_HOSTNAME_WEBSVC).

.PARAMETER PfxPassword
    PFX password (TLS_PFX_PASSWORD on the renewer). Omit for the default empty password.

.PARAMETER SiteName
    Site for new bindings (-CreateBinding). Default "Default Web Site".

.PARAMETER Port
    https port. Default 443.

.PARAMETER CreateBinding
    Create an SNI https binding for a host that has none, instead of rebinding the catch-all binding.

.PARAMETER RemoveOld
    Delete the previous certificate from LocalMachine\My after rebinding, when no binding uses it any more.

.EXAMPLE
    .\Install-IisCertificate.ps1 -PfxPath C:\certs\viking\cert.pfx -HostHeader websvc.connectomes.pitt.edu

.EXAMPLE
    .\Install-IisCertificate.ps1 -PfxPath .\cert.pfx -HostHeader websvc.connectomes.pitt.edu -CreateBinding -WhatIf
    Shows what would be imported and bound without changing anything.

.NOTES
    Run as Administrator on the IIS host. Needs the WebAdministration module.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$PfxPath,
    [Parameter(Mandatory = $true)][string[]]$HostHeader,
    [securestring]$PfxPassword,
    [string]$SiteName = 'Default Web Site',
    [int]$Port = 443,
    [switch]$CreateBinding,
    [switch]$RemoveOld
)

$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script as Administrator.'
}
if (-not (Test-Path -LiteralPath $PfxPath)) { throw "PFX not found: $PfxPath" }

try { Import-Module WebAdministration -SkipEditionCheck -ErrorAction Stop }
catch { Import-Module WebAdministration -ErrorAction Stop }

# Read the PFX first so -WhatIf can report it without touching the store.
$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
$plain = if ($PfxPassword) { [System.Net.NetworkCredential]::new('', $PfxPassword).Password } else { '' }
$probe = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path -LiteralPath $PfxPath).Path, $plain, $flags)
Write-Host "PFX: $($probe.Subject)  thumbprint $($probe.Thumbprint)  serial $($probe.SerialNumber)  expires $($probe.NotAfter)"
$dnsNames = @($probe.DnsNameList | ForEach-Object { $_.Unicode })
foreach ($h in $HostHeader) {
    $covered = $dnsNames | Where-Object { $_ -eq $h -or ($_ -like '`*.*' -and $h -like ('*' + $_.Substring(1)) -and ($h.Split('.').Count -eq $_.Split('.').Count)) }
    if (-not $covered) { Write-Warning "$h is not in the certificate's names ($($dnsNames -join ', '))." }
}

$thumb = $probe.Thumbprint
$existing = Get-Item -LiteralPath "Cert:\LocalMachine\My\$thumb" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Certificate already in LocalMachine\My.'
}
elseif ($PSCmdlet.ShouldProcess('Cert:\LocalMachine\My', "Import $PfxPath")) {
    $pw = if ($PfxPassword) { $PfxPassword } else { New-Object System.Security.SecureString }
    $null = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $pw
    Write-Host "Imported $thumb into LocalMachine\My."
}

$oldThumbs = New-Object System.Collections.Generic.List[string]
foreach ($h in $HostHeader) {
    $bindings = @(Get-WebBinding -Protocol https -Port $Port -HostHeader $h -ErrorAction SilentlyContinue)
    if ($bindings.Count -eq 0) {
        if ($CreateBinding) {
            if ($PSCmdlet.ShouldProcess("${SiteName}: https *:${Port}:$h", 'Create SNI binding')) {
                New-WebBinding -Name $SiteName -Protocol https -Port $Port -HostHeader $h -SslFlags 1
                $bindings = @(Get-WebBinding -Protocol https -Port $Port -HostHeader $h)
            }
        }
        else {
            $bindings = @(Get-WebBinding -Protocol https -Port $Port | Where-Object { ($_.bindingInformation -split ':')[2] -eq '' })
            if ($bindings.Count -gt 0) { Write-Host "No https binding for $h; rebinding the catch-all *:${Port}: binding." }
            else { Write-Warning "No https binding for $h or *:${Port}:. Re-run with -CreateBinding."; continue }
        }
    }
    foreach ($b in $bindings) {
        if ($b.certificateHash -and $b.certificateHash -ne $thumb) { $oldThumbs.Add($b.certificateHash) }
        if ($PSCmdlet.ShouldProcess("https $($b.bindingInformation)", "Assign certificate $thumb")) {
            $b.AddSslCertificate($thumb, 'My')
            Write-Host "Bound $thumb to https $($b.bindingInformation)."
        }
    }
}

if ($RemoveOld) {
    $inUse = @(Get-WebBinding -Protocol https | ForEach-Object { $_.certificateHash })
    foreach ($old in ($oldThumbs | Select-Object -Unique)) {
        if ($inUse -contains $old) { continue }
        if ($PSCmdlet.ShouldProcess("Cert:\LocalMachine\My\$old", 'Remove previous certificate')) {
            Remove-Item -LiteralPath "Cert:\LocalMachine\My\$old"
            Write-Host "Removed previous certificate $old."
        }
    }
}

Write-Host ''
Write-Host "If AnnotationService web.config uses <serviceCertificate x509FindType=""FindBySerialNumber"">, set findValue=""$($probe.SerialNumber.ToLowerInvariant())"" and recycle its pool."
