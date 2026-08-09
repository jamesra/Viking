# Starts identity-devtest + annotation-sql + grpc-annotation-service for local
# gRPC annotation integration tests, waits for SQL healthy, optionally applies
# the minimal AnnotationTest schema.
#
# Usage (from anywhere):
#   .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1
#   .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -ApplySchema
#   .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -ApplySchema -Build

[CmdletBinding()]
param(
    [switch] $ApplySchema,
    [switch] $Build,
    [string] $AnnotationSqlEnv = "D:\Docker\Builds\AnnotationSql\.env",
    [string] $RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
}

$templateEnv = Join-Path $RepoRoot "Servers\AnnotationDatabase\config-template\build\env-template.txt"
$composeBase = Join-Path $RepoRoot "docker-compose.yml"
$composeOverride = Join-Path $RepoRoot "docker-compose.annotation-db.yml"
$applySchemaScript = Join-Path $RepoRoot "Servers\AnnotationDatabase\scripts\Apply-MinimalSchema.ps1"

if (-not (Test-Path $composeBase) -or -not (Test-Path $composeOverride)) {
    throw "Compose files not found under $RepoRoot"
}

if (-not (Test-Path $AnnotationSqlEnv)) {
    $dir = Split-Path $AnnotationSqlEnv -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    if (-not (Test-Path $templateEnv)) {
        throw "Missing template $templateEnv"
    }
    Copy-Item $templateEnv $AnnotationSqlEnv
    Write-Host "Created $AnnotationSqlEnv from template. Edit MSSQL_SA_PASSWORD if desired, then re-run."
}

$composeArgs = @(
    "--env-file", $AnnotationSqlEnv,
    "-f", $composeBase,
    "-f", $composeOverride,
    "up", "-d"
)
if ($Build) {
    $composeArgs += "--build"
}
$composeArgs += @("identity-devtest", "annotation-sql", "grpc-annotation-service")

Write-Host "Starting stack from $RepoRoot ..."
Push-Location $RepoRoot
try {
    docker compose @composeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed (exit $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

Write-Host "Waiting for annotation-sql health..."
$deadline = (Get-Date).AddSeconds(180)
$healthy = $false
while ((Get-Date) -lt $deadline) {
    $status = docker inspect -f '{{.State.Health.Status}}' viking-annotation-sql 2>$null
    if ($status -eq "healthy") {
        $healthy = $true
        break
    }
    if ($status -eq "unhealthy") {
        throw "annotation-sql reported unhealthy. Check: docker logs viking-annotation-sql"
    }
    Start-Sleep -Seconds 3
}
if (-not $healthy) {
    throw "Timed out waiting for annotation-sql to become healthy."
}
Write-Host "annotation-sql is healthy."

if ($ApplySchema) {
    & $applySchemaScript -EnvFile $AnnotationSqlEnv
}

Write-Host @"

Stack is up:
  Identity DevTest : http://localhost:5020
  gRPC (HTTP/h2c)  : http://localhost:5010
  Annotation SQL   : localhost,1433  (database AnnotationTest)

Run tests:
  dotnet test Clients/WebAnnotationModel.gRPC.Tests/WebAnnotationModel.gRPC.Tests.csproj

"@
