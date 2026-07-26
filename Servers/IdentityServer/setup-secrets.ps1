<#
.SYNOPSIS
    Sets up the secrets directory for Identity Server Docker containers.

.DESCRIPTION
    This script creates the secrets directory structure and provides instructions
    for populating secret files. Secrets are stored in C:\DockerContainerData\Identity\secrets\
    and are mounted into containers as read-only volumes.

.PARAMETER SQLPassword
    Optional: SQL Server password to write to the secret file.

.PARAMETER CloudflareAPIKey
    Optional: Cloudflare API key to write to the secret file.

.PARAMETER SSLCertPFXPath
    Optional: Path to existing SSL certificate PFX file to copy.

.EXAMPLE
    .\setup-secrets.ps1 -SQLPassword "MyPassword123" -CloudflareAPIKey "abc123..."
#>

param(
    [string]$SQLPassword,
    [string]$CloudflareAPIKey,
    [string]$SSLCertPFXPath
)

$SecretsDir = "C:\DockerContainerData\Identity\secrets"

# Create secrets directory if it doesn't exist
if (-not (Test-Path $SecretsDir)) {
    New-Item -ItemType Directory -Force -Path $SecretsDir | Out-Null
    Write-Host "Created directory: $SecretsDir" -ForegroundColor Green
} else {
    Write-Host "Directory already exists: $SecretsDir" -ForegroundColor Yellow
}

# Setup SQL Server password
$SQLPasswordFile = Join-Path $SecretsDir "sql_server_password.txt"
if ($SQLPassword) {
    $SQLPassword | Out-File -FilePath $SQLPasswordFile -Encoding utf8 -NoNewline
    Write-Host "Created SQL Server password file: $SQLPasswordFile" -ForegroundColor Green
} elseif (-not (Test-Path $SQLPasswordFile)) {
    Write-Host "SQL Server password file not found: $SQLPasswordFile" -ForegroundColor Yellow
    Write-Host "  Create this file with your SQL Server password (no quotes, no newlines)" -ForegroundColor Gray
    Write-Host "  Example: 'MyPassword123' | Out-File -FilePath '$SQLPasswordFile' -Encoding utf8 -NoNewline" -ForegroundColor Gray
}

# Setup Cloudflare API key
$CloudflareKeyFile = Join-Path $SecretsDir "cloudflare_api_key.txt"
if ($CloudflareAPIKey) {
    $CloudflareAPIKey | Out-File -FilePath $CloudflareKeyFile -Encoding utf8 -NoNewline
    Write-Host "Created Cloudflare API key file: $CloudflareKeyFile" -ForegroundColor Green
} elseif (-not (Test-Path $CloudflareKeyFile)) {
    Write-Host "Cloudflare API key file not found: $CloudflareKeyFile" -ForegroundColor Yellow
    Write-Host "  Create this file with your Cloudflare API key (no quotes, no newlines)" -ForegroundColor Gray
    Write-Host "  Get your API key from: https://dash.cloudflare.com/profile/api-tokens" -ForegroundColor Gray
    Write-Host "  Example: 'your-api-key' | Out-File -FilePath '$CloudflareKeyFile' -Encoding utf8 -NoNewline" -ForegroundColor Gray
}

# Setup SSL Certificate PFX
$SSLCertPFXFile = Join-Path $SecretsDir "ssl_cert_pfx.pfx"
if ($SSLCertPFXPath -and (Test-Path $SSLCertPFXPath)) {
    Copy-Item -Path $SSLCertPFXPath -Destination $SSLCertPFXFile -Force
    Write-Host "Copied SSL certificate PFX: $SSLCertPFXFile" -ForegroundColor Green
} elseif (-not (Test-Path $SSLCertPFXFile)) {
    Write-Host "SSL certificate PFX file not found: $SSLCertPFXFile" -ForegroundColor Yellow
    Write-Host "  Copy your SSL certificate PFX file to this location" -ForegroundColor Gray
    Write-Host "  Example: Copy-Item -Path 'C:\path\to\cert.pfx' -Destination '$SSLCertPFXFile'" -ForegroundColor Gray
}

Write-Host "`nSecrets directory setup complete!" -ForegroundColor Green
Write-Host "Location: $SecretsDir" -ForegroundColor Cyan
Write-Host "`nFiles in secrets directory:" -ForegroundColor Cyan
Get-ChildItem $SecretsDir -File | ForEach-Object {
    $size = if ($_.Length -lt 1024) { "$($_.Length) B" } else { "$([math]::Round($_.Length/1KB, 2)) KB" }
    Write-Host "  - $($_.Name) ($size)" -ForegroundColor Gray
}



