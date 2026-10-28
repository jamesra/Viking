<#
.SYNOPSIS
    Deploys, lists, or removes a gRPC annotation service deployment for one annotation database.

.DESCRIPTION
    The per-database deploy script (see the "One deployment per annotation database"
    section of Documentation/source/deployment/annotation_grpc.rst). Each annotation
    database gets its own Docker Compose project, viking-annotation-<database>, so several
    databases run side by side without name clashes.

    What it does:
      - Validates the inputs and derives the compose project name and host ports. A project
        that is already running keeps the port pair it publishes, so a re-run does not move it.
      - First run: creates the per-database env file from
        config-template/build/docker-env-template.txt (outside the repo, by default under
        D:\Docker\Builds\GrpcAnnotationService\), fills in the database name and, when given,
        the SQL server, then stops and lists the keys that still need real values.
      - Later runs: checks that no placeholder is left, prints the deployment plan and the
        GRPC_ANNOTATION_* compose variables for the project (see the README's "Compose
        variables"), then checks the configuration with the read-only
        `docker compose config --quiet`, then starts the project with
        `docker compose up -d --build grpc-annotation-service` (skipped by -WhatIf), with those
        variables set only for those calls. It then waits up to 120 seconds for
        the HTTPS port to complete a TLS handshake (Docker accepts TCP on a published port before
        the service listens) and prints the endpoint a Viking client should use.
      - -Status lists deployed viking-annotation-* projects with their containers and ports.
      - -Remove stops and removes one project's containers and network (volumes are kept).

    Safety contract:
      - -Environment Production is refused unless -Confirm is given explicitly.
      - The env file must live outside the repo, and its values are never printed: only
        key names are shown.
      - Identity settings are left exactly as the template has them; this script does not
        choose an Identity Server.
      - The script never runs schema changes or connects to the database.

.PARAMETER Database
    Annotation database name (letters, digits, '_' and '-'). Written into the
    connection string and used to derive the compose project viking-annotation-<database>.

.PARAMETER SqlServer
    SQL Server the service connects to, as written in a connection string
    (for example "192.168.0.110,1433"). Used only when the env file is created;
    when omitted, the template's server (the local annotation-sql container) is kept.

.PARAMETER Port
    Host port for HTTP/2 cleartext. HTTPS (used by Viking on .NET 4.8) is Port + 1.
    Default: the first free pair at or above 5010, skipping 5020 (Identity DevTest).

.PARAMETER Environment
    Test (default) or Production. Production also requires -Confirm.

.PARAMETER EnvFile
    Per-database env file. Default: D:\Docker\Builds\GrpcAnnotationService\<Database>.env.
    A relative path is resolved against the current PowerShell location.
    Must be outside the repo; it holds credentials.

.PARAMETER Remove
    Stops and removes the viking-annotation-<database> compose project (docker compose down,
    without -v, so volumes are kept).

.PARAMETER Status
    Lists deployed viking-annotation-* projects with container status and published ports.

.EXAMPLE
    .\Servers\GrpcAnnotationService\scripts\Deploy-AnnotationGrpc.ps1 -Database RC3 -SqlServer "192.168.0.110,1433" -WhatIf

    Shows the project name, ports and env file it would create, without writing anything.

.EXAMPLE
    .\Servers\GrpcAnnotationService\scripts\Deploy-AnnotationGrpc.ps1 -Database RC3 -SqlServer "192.168.0.110,1433"

    First run creates D:\Docker\Builds\GrpcAnnotationService\RC3.env and lists the keys to fill in.
    Once they are filled in, the same command builds and starts the project and prints its endpoint.

.EXAMPLE
    .\Servers\GrpcAnnotationService\scripts\Deploy-AnnotationGrpc.ps1 -Status

.EXAMPLE
    .\Servers\GrpcAnnotationService\scripts\Deploy-AnnotationGrpc.ps1 -Database RC3 -Remove -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'Deploy')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Deploy')]
    [Parameter(Mandatory = $true, ParameterSetName = 'Remove')]
    [ValidatePattern('^[A-Za-z0-9_-]{1,100}$')]
    [string] $Database,

    [Parameter(ParameterSetName = 'Deploy')]
    [ValidatePattern('^[A-Za-z0-9._\\-]+(,[0-9]{1,5})?$')]
    [string] $SqlServer,

    [Parameter(ParameterSetName = 'Deploy')]
    [ValidateRange(1024, 65534)]
    [int] $Port,

    [Parameter(ParameterSetName = 'Deploy')]
    [Parameter(ParameterSetName = 'Remove')]
    [ValidateSet('Test', 'Production')]
    [string] $Environment = 'Test',

    [Parameter(ParameterSetName = 'Deploy')]
    [string] $EnvFile,

    [Parameter(Mandatory = $true, ParameterSetName = 'Remove')]
    [switch] $Remove,

    [Parameter(Mandatory = $true, ParameterSetName = 'Status')]
    [switch] $Status
)

$ErrorActionPreference = 'Stop'

$projectPrefix = 'viking-annotation-'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$templatePath = Join-Path $repoRoot 'Servers\GrpcAnnotationService\config-template\build\docker-env-template.txt'
$defaultEnvDir = 'D:\Docker\Builds\GrpcAnnotationService'
$firstPort = 5010
# 5020 was the DevTest Identity port (identity-devtest, since removed); still skipped so old test setups keep working.
$reservedPorts = @(5020)
# Seconds to wait after `up` for the HTTPS port to answer.
$startTimeoutSeconds = 120
$serviceName = 'grpc-annotation-service'

<#
.SYNOPSIS
    Returns the compose project name for a database: viking-annotation-<lowercase database>.
.DESCRIPTION
    Compose project names must be lowercase; '_' is kept because compose accepts it.
#>
function Get-ProjectName([string] $Name) {
    return $projectPrefix + $Name.ToLowerInvariant()
}

<#
.SYNOPSIS
    Returns the TCP ports that have a listener on this host.
.DESCRIPTION
    Read-only: asks the OS for active listeners and does not open sockets, so it cannot
    disturb running containers. Docker Desktop's published ports show up here.
#>
function Get-ListeningPorts {
    $props = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    return @($props.GetActiveTcpListeners() | ForEach-Object { $_.Port } | Sort-Object -Unique)
}

<#
.SYNOPSIS
    Returns the first HTTP port at or above $firstPort where Port and Port + 1 are both free.
.DESCRIPTION
    Ports are taken in pairs (h2c, HTTPS) to match the 5010/5011 convention of Servers/GrpcAnnotationService/docker-compose.yml.
#>
function Find-FreePortPair([int[]] $InUse) {
    for ($p = $firstPort; $p -lt 65534; $p += 2) {
        if ($InUse -contains $p -or $InUse -contains ($p + 1)) { continue }
        if ($reservedPorts -contains $p -or $reservedPorts -contains ($p + 1)) { continue }
        return $p
    }
    throw 'No free port pair found.'
}

<#
.SYNOPSIS
    Returns the names of uncommented keys whose values still hold a REPLACE_WITH_ placeholder.
.DESCRIPTION
    Only key names are returned so callers can report them without exposing values.
#>
function Get-UnfilledKeys([string[]] $Lines) {
    return @($Lines |
        Where-Object { $_ -notmatch '^\s*#' -and $_ -match '^\s*([^=\s]+)\s*=.*REPLACE_WITH_' } |
        ForEach-Object { ($_ -split '=', 2)[0].Trim() })
}

<#
.SYNOPSIS
    Returns the compose variables Servers/GrpcAnnotationService/docker-compose.yml reads for one per-database project.
.DESCRIPTION
    Pure: maps the host port pair and env file to GRPC_ANNOTATION_HTTP_PORT (Port),
    GRPC_ANNOTATION_HTTPS_PORT (Port + 1) and GRPC_ANNOTATION_ENV_FILE (forward slashes,
    as compose files write paths). GRPC_ANNOTATION_CORRECTIONS_PATH is left unset so every
    project mounts the shared corrections folder. The ANNOTATION_SQL_* variables are not
    set: they belong to the local-sql profile (annotation-sql), the local test SQL, which
    a per-database deployment does not use; its database is named in the env file's
    connection string.
#>
function Get-ComposeVariables([int] $HttpPort, [string] $EnvFilePath) {
    return [ordered]@{
        GRPC_ANNOTATION_HTTP_PORT  = [string]$HttpPort
        GRPC_ANNOTATION_HTTPS_PORT = [string]($HttpPort + 1)
        GRPC_ANNOTATION_ENV_FILE   = $EnvFilePath.Replace('\', '/')
    }
}

<#
.SYNOPSIS
    Runs `docker compose <Arguments>` for one project against the viking-annotation stack file.
.DESCRIPTION
    Sets the given variables (plus the stack folders stack.ps1 would export, when unset) in
    this process only for the duration of the call and restores their previous values
    afterwards. Used for the read-only `config --quiet` check, which validates the compose
    file without printing it (so env file values are not shown), and for `up`. Throws when
    compose exits non-zero.
#>
function Invoke-ComposeService([string] $ProjectName, [System.Collections.IDictionary] $Variables, [string[]] $Arguments) {
    # Stack folders stack.ps1 would export; defaulted under D:\Docker only when unset.
    $root = if ($env:NORNIR_DOCKER_USER_ROOT) { $env:NORNIR_DOCKER_USER_ROOT } else { 'D:\Docker' }
    $Variables = [ordered]@{} + $Variables
    foreach ($pair in @(@('NORNIR_DOCKER_USER_ROOT', $root), @('DOCKER_CERTS_DIR', (Join-Path $root 'Certs')), @('STACK_RUN_DIR', (Join-Path $root 'Run\viking-annotation')))) {
        if (-not [Environment]::GetEnvironmentVariable($pair[0]) -and -not $Variables.Contains($pair[0])) { $Variables[$pair[0]] = $pair[1].Replace('\', '/') }
    }
    $saved = @{}
    foreach ($name in $Variables.Keys) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $Variables[$name], 'Process')
    }
    try {
        # The viking-annotation stack file; its relative paths resolve from its own folder.
        docker compose --project-name $ProjectName `
            -f (Join-Path $repoRoot 'Servers\GrpcAnnotationService\docker-compose.yml') @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose $($Arguments[0]) failed for $ProjectName (exit $LASTEXITCODE)."
        }
    }
    finally {
        foreach ($name in $saved.Keys) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
        }
    }
}

<#
.SYNOPSIS
    Returns the host port a `docker ps` Ports column maps to container port 80/tcp, or $null.
.DESCRIPTION
    Pure. Reads text such as "0.0.0.0:5010->80/tcp, [::]:5010->80/tcp, 0.0.0.0:5011->443/tcp".
#>
function Get-PublishedHttpPort([string] $Ports) {
    if ($Ports -match ':(\d+)->80/tcp') { return [int]$Matches[1] }
    return $null
}

<#
.SYNOPSIS
    Returns $true when a TLS handshake with 127.0.0.1:<Port> completes within the timeout.
.DESCRIPTION
    A TCP connect alone is not enough: Docker accepts connections on a published port before the
    container listens. Any certificate is accepted (the container may use its self-signed
    localhost certificate) because nothing is sent after the handshake. Never throws.
#>
function Test-TlsHandshake([int] $Port, [int] $TimeoutMilliseconds = 3000) {
    $tcp = [System.Net.Sockets.TcpClient]::new([System.Net.Sockets.AddressFamily]::InterNetwork)
    try {
        if (-not $tcp.ConnectAsync('127.0.0.1', $Port).Wait($TimeoutMilliseconds)) { return $false }
        $stream = $tcp.GetStream()
        $stream.ReadTimeout = $TimeoutMilliseconds
        $stream.WriteTimeout = $TimeoutMilliseconds
        $ssl = [System.Net.Security.SslStream]::new($stream, $false, { $true })
        try {
            $ssl.AuthenticateAsClient('localhost')
            return $true
        }
        finally { $ssl.Dispose() }
    }
    catch { return $false }
    finally { $tcp.Dispose() }
}

<#
.SYNOPSIS
    Probes the port with Test-TlsHandshake every IntervalSeconds until it answers; $false after about TimeoutSeconds.
.DESCRIPTION
    Counts attempts (TimeoutSeconds / IntervalSeconds, at least one) rather than reading a clock,
    so the wait also ends when Start-Sleep is mocked; each failed probe adds up to its own timeout.
#>
function Wait-ServiceReady([int] $Port, [int] $TimeoutSeconds, [int] $IntervalSeconds = 2) {
    $attempts = [Math]::Max(1, [int][Math]::Ceiling($TimeoutSeconds / $IntervalSeconds))
    for ($i = 1; $i -le $attempts; $i++) {
        if (Test-TlsHandshake $Port) { return $true }
        if ($i -lt $attempts) { Start-Sleep -Seconds $IntervalSeconds }
    }
    return $false
}

<#
.SYNOPSIS
    Builds and starts grpc-annotation-service for the project, waits for its HTTPS port (HttpPort + 1) and prints the endpoint.
.DESCRIPTION
    Runs `docker compose up -d --build grpc-annotation-service` through Invoke-ComposeService,
    then Wait-ServiceReady. Throws when compose fails or the port does not answer within
    TimeoutSeconds; the containers are left running for `docker compose logs`.
#>
function Start-ComposeProject([string] $ProjectName, [System.Collections.IDictionary] $Variables, [int] $HttpPort, [int] $TimeoutSeconds) {
    $httpsPort = $HttpPort + 1
    $endpoint = "https://localhost:$httpsPort/"
    Invoke-ComposeService $ProjectName $Variables @('up', '-d', '--build', $serviceName)
    Write-Host "Waiting up to $TimeoutSeconds s for $endpoint ..."
    if (-not (Wait-ServiceReady $httpsPort $TimeoutSeconds)) {
        throw "$ProjectName did not answer on $endpoint within $TimeoutSeconds s. Check: docker compose --project-name $ProjectName logs $serviceName"
    }
    Write-Host "$ProjectName is running. Viking endpoint: $endpoint"
}

<#
.SYNOPSIS
    Throws unless docker is on PATH.
#>
function Assert-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'docker was not found on PATH.'
    }
}

if ($Environment -eq 'Production') {
    $confirmGiven = $PSBoundParameters.ContainsKey('Confirm') -and [bool]$PSBoundParameters['Confirm']
    if (-not $confirmGiven) {
        throw 'Refusing a Production deployment without -Confirm. Re-run with -Environment Production -Confirm.'
    }
}

if ($Status) {
    Assert-Docker
    $projects = @(docker compose ls --all --format json | ConvertFrom-Json |
        Where-Object { $_.Name -like "$projectPrefix*" })
    if ($projects.Count -eq 0) {
        Write-Host 'No viking-annotation-* deployments found.'
        return
    }
    foreach ($proj in $projects) {
        Write-Host ("{0}  [{1}]" -f $proj.Name, $proj.Status)
        docker ps --all --filter "label=com.docker.compose.project=$($proj.Name)" --format '  {{.Names}}  {{.Status}}  {{.Ports}}'
    }
    return
}

$project = Get-ProjectName $Database

if ($Remove) {
    Assert-Docker
    if ($PSCmdlet.ShouldProcess($project, 'docker compose down (volumes kept)')) {
        docker compose --project-name $project down
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose down failed for $project (exit $LASTEXITCODE)."
        }
        Write-Host "Removed $project."
    }
    return
}

if (-not $EnvFile) {
    $EnvFile = Join-Path $defaultEnvDir "$Database.env"
}
# Resolve against $PWD, not the process working directory that [IO.Path]::GetFullPath uses;
# the two differ in an interactive session, which would let a relative path slip past the
# repo check below and land the file somewhere else.
$envFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($EnvFile)
if ($envFull.StartsWith($repoRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "EnvFile must be outside the repo ($repoRoot): it holds credentials."
}

# A running project keeps its ports: its own listeners would otherwise make a re-run fail or move it.
$published = $null
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $published = Get-PublishedHttpPort ((docker ps --filter "label=com.docker.compose.project=$project" `
                --filter "label=com.docker.compose.service=$serviceName" --format '{{.Ports}}') -join ', ')
}
$inUse = Get-ListeningPorts
if ($PSBoundParameters.ContainsKey('Port')) {
    if ($Port -ne $published -and ($inUse -contains $Port -or $inUse -contains ($Port + 1))) {
        throw "Port $Port or $($Port + 1) is already in use on this host."
    }
}
elseif ($published) {
    $Port = $published
}
else {
    $Port = Find-FreePortPair $inUse
}

Write-Host "Database     : $Database"
Write-Host "Environment  : $Environment"
Write-Host "Project      : $project"
Write-Host "Host ports   : $Port (h2c), $($Port + 1) (HTTPS, Viking endpoint https://localhost:$($Port + 1))"
Write-Host "Env file     : $envFull"

if (-not (Test-Path -LiteralPath $envFull)) {
    if (-not (Test-Path -LiteralPath $templatePath)) {
        throw "Missing template $templatePath"
    }
    $lines = Get-Content -LiteralPath $templatePath
    $lines = $lines | ForEach-Object {
        if ($_ -match '^\s*ConnectionStrings__AnnotationConnection=') {
            $line = $_ -replace '(?i)(Database=)[^;]*', "`${1}$Database"
            if ($SqlServer) { $line = $line -replace '(?i)(Server=)[^;]*', "`${1}$SqlServer" }
            $line
        }
        else { $_ }
    }
    if ($PSCmdlet.ShouldProcess($envFull, 'Create env file from docker-env-template.txt')) {
        $dir = Split-Path $envFull -Parent
        if (-not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir | Out-Null
        }
        # UTF-8 without BOM from both PowerShell editions (Set-Content -Encoding UTF8 adds a
        # BOM on Windows PowerShell 5.1), so the file matches the template byte for byte.
        [System.IO.File]::WriteAllLines($envFull, [string[]]$lines, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Created $envFull."
    }
    Write-Host 'Fill in these keys in the env file, then re-run:'
    Get-UnfilledKeys $lines | ForEach-Object { Write-Host "  $_" }
    Write-Host '  IdentityServer__* : point at the real Identity issuer (template defaults are the retired DevTest instance).'
    return
}

$unfilled = Get-UnfilledKeys (Get-Content -LiteralPath $envFull)
if ($unfilled.Count -gt 0) {
    Write-Host 'The env file still has placeholders for:'
    $unfilled | ForEach-Object { Write-Host "  $_" }
    throw "Fill in $envFull, then re-run."
}

$composeVars = Get-ComposeVariables $Port $envFull
Write-Host 'Compose variables:'
$composeVars.GetEnumerator() | ForEach-Object { Write-Host "  $($_.Key)=$($_.Value)" }

Assert-Docker
Invoke-ComposeService $project $composeVars @('config', '--quiet', $serviceName)
Write-Host "Compose configuration for $project is valid."

if ($PSCmdlet.ShouldProcess($project, "docker compose up -d --build $serviceName")) {
    Start-ComposeProject $project $composeVars $Port $startTimeoutSeconds
}
