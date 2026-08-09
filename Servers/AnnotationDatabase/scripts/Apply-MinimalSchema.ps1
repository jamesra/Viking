# Applies Servers/AnnotationDatabase/scripts/minimal-schema.sql to the
# annotation-sql container (or a custom container name).
#
# Prerequisites:
#   - annotation-sql container running and healthy
#   - D:\Docker\Builds\AnnotationSql\.env with MSSQL_SA_PASSWORD

[CmdletBinding()]
param(
    [string] $ContainerName = "viking-annotation-sql",
    [string] $EnvFile = "D:\Docker\Builds\AnnotationSql\.env",
    [string] $ScriptPath = "",
    [int] $TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

if (-not $ScriptPath) {
    $ScriptPath = Join-Path $PSScriptRoot "minimal-schema.sql"
}

if (-not (Test-Path $EnvFile)) {
    throw "Missing env file: $EnvFile. Copy Servers/AnnotationDatabase/config-template/build/env-template.txt there first."
}

if (-not (Test-Path $ScriptPath)) {
    throw "Missing schema script: $ScriptPath"
}

$saPassword = $null
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^\s*MSSQL_SA_PASSWORD=(.+)\s*$') {
        $saPassword = $Matches[1].Trim().Trim('"').Trim("'")
    }
}
if ([string]::IsNullOrWhiteSpace($saPassword)) {
    throw "MSSQL_SA_PASSWORD not found in $EnvFile"
}

Write-Host "Waiting for container '$ContainerName' to accept SQL connections..."
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$ready = $false
while ((Get-Date) -lt $deadline) {
    $running = docker inspect -f '{{.State.Running}}' $ContainerName 2>$null
    if ($running -ne "true") {
        Start-Sleep -Seconds 2
        continue
    }

    docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $saPassword -C -I -Q "SELECT 1" 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 2
}

if (-not $ready) {
    throw "SQL Server in '$ContainerName' did not become ready within ${TimeoutSeconds}s."
}

Write-Host "Applying schema from $ScriptPath ..."
# -I enables QUOTED_IDENTIFIER (required for persisted computed geometry columns).
Get-Content -Raw $ScriptPath | docker exec -i $ContainerName /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P $saPassword -C -I -b
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed while applying minimal-schema.sql (exit $LASTEXITCODE)."
}

Write-Host "Schema applied."
