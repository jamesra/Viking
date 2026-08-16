# Starts a standalone Data API Builder container against AnnotationTest
# (http://localhost:3002/mcp) for the Cursor "Annotation Test Database" MCP
# server. Uses docker run so it is not part of the VikingLegacy Compose group.
#
# Prerequisites:
#   - annotation-sql running and published on localhost,1433
#   - D:\Docker\Builds\AnnotationSql\.env with MSSQL_SA_PASSWORD
#
# Usage:
#   .\Servers\AnnotationDatabase\scripts\Start-AnnotationTestDab.ps1

[CmdletBinding()]
param(
    [string] $EnvFile = "D:\Docker\Builds\AnnotationSql\.env",
    [int] $Port = 3002,
    [string] $ConfigPath = "",
    [string] $RepoRoot = "",
    [string] $ContainerName = "dab-annotation-test"
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
}

if (-not $ConfigPath) {
    $ConfigPath = Join-Path $RepoRoot "Servers\AnnotationDatabase\dab-config.annotation-test.json"
}
$ConfigPath = (Resolve-Path $ConfigPath).Path

if (-not (Test-Path $EnvFile)) {
    throw "Missing env file: $EnvFile. Copy Servers/AnnotationDatabase/config-template/build/env-template.txt there first."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "docker is required to start $ContainerName."
}

$saPassword = $null
$database = "AnnotationTest"
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^\s*MSSQL_SA_PASSWORD=(.+)\s*$') {
        $saPassword = $Matches[1].Trim().Trim('"').Trim("'")
    }
    elseif ($_ -match '^\s*ANNOTATION_DATABASE=(.+)\s*$') {
        $database = $Matches[1].Trim().Trim('"').Trim("'")
    }
}
if ([string]::IsNullOrWhiteSpace($saPassword)) {
    throw "MSSQL_SA_PASSWORD not found in $EnvFile"
}

$connectionString = "Server=host.docker.internal,1433;Database=$database;User ID=sa;Password=$saPassword;TrustServerCertificate=True"

$existing = docker ps -aq --filter "name=^/${ContainerName}$"
if ($existing) {
    Write-Host "Removing existing $ContainerName so it is recreated outside Compose..."
    docker rm -f $ContainerName | Out-Null
}

Write-Host "Starting standalone $ContainerName on http://localhost:$Port/mcp ..."
docker run -d `
    --name $ContainerName `
    --restart unless-stopped `
    -p "${Port}:5000" `
    --add-host=host.docker.internal:host-gateway `
    -e "MSSQL_CONNECTION_STRING=$connectionString" `
    -e "ASPNETCORE_URLS=http://+:5000" `
    -v "${ConfigPath}:/App/dab-config.json:ro" `
    mcr.microsoft.com/azure-databases/data-api-builder:latest

if ($LASTEXITCODE -ne 0) {
    throw "docker run $ContainerName failed (exit $LASTEXITCODE)."
}

Write-Host "Started $ContainerName. Check: docker logs $ContainerName"
