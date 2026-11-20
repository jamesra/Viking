<#
.SYNOPSIS
    Starts annotation-sql + grpc-annotation-service (viking-annotation stack, profile local-sql) for
    local gRPC annotation integration tests, on a freshly reset AnnotationTest database.

.DESCRIPTION
    The gRPC integration tests create, edit and delete annotations, so they get a database of their own: AnnotationTest
    in the local SQL Server container viking-annotation-sql (profile local-sql of
    Servers/GrpcAnnotationService/docker-compose.yml). The script starts that container, waits for it to report
    healthy, drops and recreates AnnotationTest with minimal-schema.sql and its seed rows
    (Servers/AnnotationDatabase/scripts/Reset-AnnotationTestDatabase.ps1), and only then starts the gRPC service, so
    every run starts from the same data and the service never holds a connection to the dropped database.

    The gRPC service's container env is written to %TEMP%\viking-annotation-test-stack.env: the local database, and the
    DevTest Identity server's introspection client. A -GrpcEnvFile of your own must also point at annotation-sql and
    AnnotationTest; any other server is refused, so a test run cannot reach a real annotation database.

    The service image is tagged viking-annotation:test-stack, never viking-annotation:latest, which the production
    volumes on this host run.

    Also starts identity-devtest (Servers/IdentityServer/DevTest/docker-compose.yml): an HTTPS-only
    in-memory Identity server on https://host.docker.internal:5020 with the tests' user and clients.
    Both containers make self-signed certificates; this script saves them to
    %TEMP%\viking-annotation-test-stack.pem and %TEMP%\viking-identity-test-stack.pem, which the gRPC tests
    pin, and the annotation container mounts the identity certificate to trust it
    (docker-compose.test-identity.yml).

    The stack variables stack.ps1 normally exports (NORNIR_DOCKER_USER_ROOT, STACK_*, DOCKER_CERTS_DIR) are filled with
    defaults under -DockerRoot when unset.

.PARAMETER Build
    Rebuild the gRPC image.

.PARAMETER AnnotationSqlEnv
    Env file with MSSQL_SA_PASSWORD (interpolation only). Created from the AnnotationDatabase template when missing.

.PARAMETER GrpcEnvFile
    Container env file for grpc-annotation-service. Default: written to %TEMP%\viking-annotation-test-stack.env.
    Its connection string must name Server=annotation-sql and Database=AnnotationTest.

.PARAMETER DockerRoot
    Root used for the default stack folders. Default $env:NORNIR_DOCKER_USER_ROOT, else D:\Docker.

.PARAMETER RepoRoot
    Viking repo root. Default: three folders above this script.

.EXAMPLE
    .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -Build
#>
[CmdletBinding()]
param(
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
$resetScript = Join-Path $RepoRoot "Servers\AnnotationDatabase\scripts\Reset-AnnotationTestDatabase.ps1"

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
# The test stack has its own port, never a volume's (Deploy-AnnotationGrpc.ps1 gives volumes 5011 and up and reserves
# 5019). The tests in WebAnnotationModel.gRPC.Tests and gRPC_Tests aim at it, and run only when the server answers
# the /test-stack marker that docker-compose.test-identity.yml turns on.
$env:GRPC_ANNOTATION_HTTPS_PORT = "5019"
# Its own image tag: a --build here must not replace viking-annotation:latest under the production volumes.
$env:VIKING_ANNOTATION_IMAGE = "viking-annotation:test-stack"
# The compose file's environment entry for the token issuer wins over the env file and has no host.env here.
$env:IdentityServer__Endpoint = "https://host.docker.internal:5020/"
foreach ($name in $stackDefaults.Keys) {
    if (-not [Environment]::GetEnvironmentVariable($name)) {
        [Environment]::SetEnvironmentVariable($name, $stackDefaults[$name], "Process")
    }
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
    Write-Host "Created $AnnotationSqlEnv from template."
}
$saPassword = $null
Get-Content $AnnotationSqlEnv | ForEach-Object {
    if ($_ -match '^\s*MSSQL_SA_PASSWORD=(.+)\s*$') {
        $saPassword = $Matches[1].Trim().Trim('"').Trim("'")
    }
}
if ([string]::IsNullOrWhiteSpace($saPassword)) {
    throw "MSSQL_SA_PASSWORD not found in $AnnotationSqlEnv"
}

if (-not $GrpcEnvFile) {
    $GrpcEnvFile = Join-Path ([System.IO.Path]::GetTempPath()) 'viking-annotation-test-stack.env'
    # annotation-sql is the compose service on the stack's network; the DevTest identity client is in
    # Servers/IdentityServer/DevTest/Config.cs.
    @(
        "ConnectionStrings__AnnotationConnection=Server=annotation-sql,1433;Database=AnnotationTest;User ID=sa;Password=$saPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
        "IdentityServer__Endpoint=https://host.docker.internal:5020/"
        "IdentityServer__ClientId=grpc-annotation"
        "IdentityServer__ClientSecret=DevTestIntrospectionSecret"
        "IdentityServer__AllowHttpMetadata=false"
        "Corrections__RootDirectory=/corrections"
    ) | Set-Content -Path $GrpcEnvFile -Encoding ascii
}
if (-not (Test-Path $GrpcEnvFile)) {
    throw "gRPC env file not found: $GrpcEnvFile"
}
$connection = Get-Content $GrpcEnvFile | Where-Object { $_ -match '^\s*ConnectionStrings__AnnotationConnection=' } | Select-Object -Last 1
if ($connection -notmatch '(?i)(^|;|=)\s*(Server|Data Source)\s*=\s*(tcp:)?annotation-sql\s*(,\s*1433)?\s*;' -or
    $connection -notmatch '(?i);\s*(Database|Initial Catalog)\s*=\s*AnnotationTest\s*(;|$)') {
    throw "Refusing to start: $GrpcEnvFile must point ConnectionStrings__AnnotationConnection at Server=annotation-sql and Database=AnnotationTest, the local test container."
}
$env:GRPC_ANNOTATION_ENV_FILE = $GrpcEnvFile.Replace('\', '/')

# identity-devtest first: it creates the certificate volume the annotation container mounts.
$identityCompose = Join-Path $RepoRoot "Servers\IdentityServer\DevTest\docker-compose.yml"
$testIdentityOverride = Join-Path $RepoRoot "Servers\GrpcAnnotationService\docker-compose.test-identity.yml"
$identityArgs = @("-f", $identityCompose, "up", "-d")
if ($Build) { $identityArgs += "--build" }
Write-Host "Starting identity-devtest ..."
docker compose @identityArgs
if ($LASTEXITCODE -ne 0) {
    throw "docker compose up of identity-devtest failed (exit $LASTEXITCODE)."
}

$composeBase = @(
    "-p", "viking-annotation",
    "--env-file", $AnnotationSqlEnv,
    "-f", $composeFile,
    "-f", $testIdentityOverride,
    "--profile", "local-sql"
)

Push-Location $RepoRoot
try {
    Write-Host "Starting annotation-sql ..."
    docker compose @composeBase up -d annotation-sql
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up of annotation-sql failed (exit $LASTEXITCODE)."
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

    # Before the service starts, so it never caches a connection to the database being dropped.
    & $resetScript -EnvFile $AnnotationSqlEnv

    # --force-recreate: a service still running from the previous run would hold pooled connections to the old database.
    $grpcArgs = @("up", "-d", "--force-recreate", "--no-deps")
    if ($Build) { $grpcArgs += "--build" }
    Write-Host "Starting grpc-annotation-service ..."
    docker compose @composeBase @grpcArgs grpc-annotation-service
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up of grpc-annotation-service failed (exit $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

# The service is HTTPS only. Save the container's self-signed certificate where the tests pin it
# (WebAnnotationModel.gRPC.Tests TestServerTls); the container makes it at start, so retry briefly.
$testCertPath = Join-Path ([System.IO.Path]::GetTempPath()) 'viking-annotation-test-stack.pem'
$certSaved = $false
for ($i = 0; $i -lt 30 -and -not $certSaved; $i++) {
    docker compose -p viking-annotation -f $composeFile cp 'grpc-annotation-service:/tmp/https/cert.pem' $testCertPath 2>$null
    $certSaved = ($LASTEXITCODE -eq 0) -and (Test-Path $testCertPath)
    if (-not $certSaved) { Start-Sleep -Seconds 2 }
}
if ($certSaved) { Write-Host "Saved the service's self-signed certificate to $testCertPath" }
else { Write-Warning "Could not copy the service certificate to $testCertPath; the tests will fail the TLS handshake unless a real certificate is mounted." }

# The identity server's certificate, for the tests (TestServerTls.LoadPinnedIdentityCertificate).
$identityCertPath = Join-Path ([System.IO.Path]::GetTempPath()) 'viking-identity-test-stack.pem'
$identityCertSaved = $false
for ($i = 0; $i -lt 30 -and -not $identityCertSaved; $i++) {
    docker cp 'identity-devtest:/https/cert.pem' $identityCertPath 2>$null
    $identityCertSaved = ($LASTEXITCODE -eq 0) -and (Test-Path $identityCertPath)
    if (-not $identityCertSaved) { Start-Sleep -Seconds 2 }
}
if ($identityCertSaved) { Write-Host "Saved the identity server's self-signed certificate to $identityCertPath" }
else { Write-Warning "Could not copy the identity certificate to $identityCertPath; the sign-in tests will fail the TLS handshake." }

Write-Host @"

Stack is up:
  Identity (DevTest): https://host.docker.internal:5020  (self-signed certificate, saved to $identityCertPath for the tests)
  gRPC (HTTPS only): https://localhost:5019  (self-signed certificate, saved to $testCertPath for the tests;
                     answers https://localhost:5019/test-stack, which the tests require)
  Annotation SQL   : localhost,1433  (database AnnotationTest, reset with the seed rows)

Run tests:
  dotnet test Clients/WebAnnotationModel.gRPC.Tests/WebAnnotationModel.gRPC.Tests.csproj

Re-run this script before each test run to reset AnnotationTest.

"@
