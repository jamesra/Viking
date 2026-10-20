<#
.SYNOPSIS
    Starts annotation-sql + grpc-annotation-service (viking-annotation stack, profile local-sql) for
    local gRPC annotation integration tests.

.DESCRIPTION
    Runs Servers/GrpcAnnotationService/docker-compose.yml as compose project viking-annotation with
    profile local-sql, waits for SQL to report healthy and optionally applies the minimal
    AnnotationTest schema. The stack variables stack.ps1 normally exports (NORNIR_DOCKER_USER_ROOT,
    STACK_*, DOCKER_CERTS_DIR) are filled with defaults under -DockerRoot when unset.

    The DevTest Identity server (identity-devtest, :5020) is no longer started: its compose service
    referenced a Dockerfile that does not exist. Point IdentityServer__* in the gRPC env file at a
    real Identity instance, or start a DevTest Identity yourself.

.PARAMETER ApplySchema
    Apply Servers/AnnotationDatabase/scripts/Apply-MinimalSchema.ps1 after SQL is healthy.

.PARAMETER Build
    Rebuild the gRPC image.

.PARAMETER AnnotationSqlEnv
    Env file with MSSQL_SA_PASSWORD (interpolation only). Created from the AnnotationDatabase template when missing.

.PARAMETER GrpcEnvFile
    Container env file for grpc-annotation-service (connection string, IdentityServer__*). Default
    D:\Docker\Builds\GrpcAnnotationService\.env.Docker when it exists, else the stack run env.

.PARAMETER DockerRoot
    Root used for the default stack folders. Default $env:NORNIR_DOCKER_USER_ROOT, else D:\Docker.

.PARAMETER RepoRoot
    Viking repo root. Default: three folders above this script.

.EXAMPLE
    .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -ApplySchema -Build
#>
[CmdletBinding()]
param(
    [switch] $ApplySchema,
    [switch] $Build,
    [string] $AnnotationSqlEnv = "D:\Docker\Builds\AnnotationSql\.env",
    [string] $GrpcEnvFile = "",
    [string] $DockerRoot = "",
    [string] $RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
}
if (-not $DockerRoot) {
    $DockerRoot = if ($env:NORNIR_DOCKER_USER_ROOT) { $env:NORNIR_DOCKER_USER_ROOT } else { "D:\Docker" }
}

$templateEnv = Join-Path $RepoRoot "Servers\AnnotationDatabase\config-template\build\env-template.txt"
$composeFile = Join-Path $RepoRoot "Servers\GrpcAnnotationService\docker-compose.yml"
$applySchemaScript = Join-Path $RepoRoot "Servers\AnnotationDatabase\scripts\Apply-MinimalSchema.ps1"

if (-not (Test-Path $composeFile)) {
    throw "Compose file not found: $composeFile"
}

# Same variables stack.ps1 exports; only filled when the caller has not set them.
$stackDefaults = [ordered]@{
    NORNIR_DOCKER_USER_ROOT = $DockerRoot
    STACK_NAME              = "viking-annotation"
    STACK_RUN_DIR           = Join-Path $DockerRoot "Run\viking-annotation"
    STACK_SECRETS_DIR       = Join-Path $DockerRoot "Run\viking-annotation\secrets"
    STACK_DATA_DIR          = Join-Path $DockerRoot "Data\viking-annotation"
    STACK_LOGS_DIR          = Join-Path $DockerRoot "Logs\viking-annotation"
    DOCKER_CERTS_DIR        = Join-Path $DockerRoot "Certs"
}
foreach ($name in $stackDefaults.Keys) {
    if (-not [Environment]::GetEnvironmentVariable($name)) {
        [Environment]::SetEnvironmentVariable($name, $stackDefaults[$name], "Process")
    }
}
if (-not $GrpcEnvFile) {
    $legacy = "D:\Docker\Builds\GrpcAnnotationService\.env.Docker"
    $GrpcEnvFile = if (Test-Path $legacy) { $legacy } else { Join-Path $env:STACK_RUN_DIR "$($env:STACK_NAME).run.env" }
}
if (-not (Test-Path $GrpcEnvFile)) {
    throw "gRPC env file not found: $GrpcEnvFile (template: deploy/examples/example.viking-annotation.run.env)"
}
$env:GRPC_ANNOTATION_ENV_FILE = $GrpcEnvFile.Replace('\', '/')

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
    "-p", "viking-annotation",
    "--env-file", $AnnotationSqlEnv,
    "-f", $composeFile,
    "--profile", "local-sql",
    "up", "-d"
)
if ($Build) {
    $composeArgs += "--build"
}
$composeArgs += @("annotation-sql", "grpc-annotation-service")

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
  Identity         : IdentityServer__Endpoint in the gRPC env file (DevTest :5020 is not started)
  gRPC (HTTP/h2c)  : http://localhost:5010
  gRPC (HTTPS)     : https://localhost:5011  (Viking .NET 4.8)
  Annotation SQL   : localhost,1433  (database AnnotationTest)

Run tests:
  dotnet test Clients/WebAnnotationModel.gRPC.Tests/WebAnnotationModel.gRPC.Tests.csproj

"@
