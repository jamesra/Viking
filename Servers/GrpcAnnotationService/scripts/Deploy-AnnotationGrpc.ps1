<#
.SYNOPSIS
    Deploys, lists, or removes a gRPC annotation service deployment for one annotation database.

.DESCRIPTION
    The per-database deploy script (see the "One deployment per annotation database"
    section of Documentation/source/deployment/annotation_grpc.rst). Each annotation
    database gets its own Docker Compose project, viking-annotation-<database>, so several
    databases run side by side without name clashes.

    What it does:
      - Validates the inputs and derives the compose project name and the host HTTPS port. A project
        that is already running keeps the port it publishes, so a re-run does not move it.
      - First run: creates the per-database env file from
        config-template/build/docker-env-template.txt (outside the repo, by default under
        D:\Docker\Builds\GrpcAnnotationService\), fills in the database name and, when given,
        the SQL server, then stops and lists the keys that still need real values.
      - Later runs: checks that no placeholder is left, loads the host-wide values compose needs
        (Run\_host\host.env under the Docker root, as stack.ps1 does; a value already set in the
        process wins), stops with one message naming anything compose still lacks, prints the deployment plan and the
        GRPC_ANNOTATION_* compose variables for the project (named in the "One deployment
        per annotation database" section of Documentation/source/deployment/annotation_grpc.rst), then checks the configuration with the read-only
        `docker compose config --quiet`, then starts the project with
        `docker compose up -d --build grpc-annotation-service` (skipped by -WhatIf), with those
        variables set only for those calls. It then waits up to 120 seconds for
        the HTTPS port to complete a TLS handshake (Docker accepts TCP on a published port before
        the service listens) and prints the endpoint a Viking client should use.
      - -Status lists deployed viking-annotation-* projects with their containers and ports, then
        each project's Viking endpoint (https://localhost:<HTTPS port>/) and health: ready when
        the HTTPS port completes a TLS handshake within 2 seconds, not answering when the service
        runs but does not, stopped when it is not running. Read-only.
      - -Remove stops and removes one project's containers and network (volumes are kept).

    Safety contract:
      - -Environment Production is refused unless -ConfirmProduction is given. It is a plain switch,
        not -Confirm: -Confirm would also make every ShouldProcess step prompt, which throws under
        pwsh -NonInteractive.
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
    Host HTTPS port of the service (the endpoint Viking uses). The service is HTTPS only: no plain
    HTTP or h2c port is published (.cursor/rules/https-only-servers.mdc).
    Default: the port the running project already publishes, else the first free port at or
    above 5011, skipping 5020 (Identity DevTest).

.PARAMETER Environment
    Test (default) or Production. Production also requires -ConfirmProduction.

.PARAMETER ConfirmProduction
    Acknowledges a Production deployment or removal. Separate from -Confirm so it works
    non-interactively: -Confirm would also make every ShouldProcess step prompt.

.PARAMETER EnvFile
    Per-database env file. Default: D:\Docker\Builds\GrpcAnnotationService\<Database>.env.
    A relative path is resolved against the current PowerShell location.
    Must be outside the repo; it holds credentials.

.PARAMETER Remove
    Stops and removes the viking-annotation-<database> compose project (docker compose down,
    without -v, so volumes are kept).

.PARAMETER Status
    Lists deployed viking-annotation-* projects with container status, published ports, endpoint
    and health (ready, not answering or stopped).

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
    [Parameter(ParameterSetName = 'Remove')]
    [switch] $ConfirmProduction,

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
$firstPort = 5011
# 5020 was the DevTest Identity port (identity-devtest, since removed); still skipped so old test setups keep working.
$reservedPorts = @(5020)
# Seconds to wait after `up` for the HTTPS port to answer.
$startTimeoutSeconds = 120
# Milliseconds -Status gives each running project's TLS probe.
$statusProbeMilliseconds = 2000
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
    Returns the first HTTPS port at or above $firstPort that is free and not reserved.
.DESCRIPTION
    The default is 5011, the HTTPS port of Servers/GrpcAnnotationService/docker-compose.yml. There is no plain
    HTTP/h2c companion port, so ports are taken one at a time.
#>
function Find-FreePort([int[]] $InUse) {
    for ($p = $firstPort; $p -lt 65535; $p++) {
        if ($InUse -contains $p -or $reservedPorts -contains $p) { continue }
        return $p
    }
    throw 'No free port found.'
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
    Pure: maps the host HTTPS port and env file to GRPC_ANNOTATION_HTTPS_PORT and GRPC_ANNOTATION_ENV_FILE (forward slashes,
    as compose files write paths). GRPC_ANNOTATION_CORRECTIONS_PATH is left unset so every
    project mounts the shared corrections folder. The ANNOTATION_SQL_* variables are not
    set: they belong to the local-sql profile (annotation-sql), the local test SQL, which
    a per-database deployment does not use; its database is named in the env file's
    connection string.
#>
function Get-ComposeVariables([int] $HttpsPort, [string] $EnvFilePath) {
    return [ordered]@{
        GRPC_ANNOTATION_HTTPS_PORT = [string]$HttpsPort
        GRPC_ANNOTATION_ENV_FILE   = $EnvFilePath.Replace('\', '/')
    }
}

<#
.SYNOPSIS
    Returns the uncommented KEY=VALUE pairs of an env file as an ordered dictionary (empty when the file is missing).
.DESCRIPTION
    Values lose one pair of surrounding quotes; a value still holding REPLACE_WITH_ is skipped.
    Used for host.env and for the per-database env file; values are never printed by callers.
#>
function Read-EnvFileValues([string] $Path) {
    $result = [ordered]@{}
    if (-not (Test-Path -LiteralPath $Path)) { return $result }
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*#' -or $line -notmatch '^\s*(?:export\s+)?([^=\s]+)\s*=(.*)$') { continue }
        $key = $Matches[1]
        $value = $Matches[2].Trim()
        if ($value -match '^"(.*)"$' -or $value -match "^'(.*)'$") { $value = $Matches[1] }
        if ($value -and $value -notmatch 'REPLACE_WITH_') { $result[$key] = $value }
    }
    return $result
}

<#
.SYNOPSIS
    Returns the host-wide compose variables to set for the call: host.env values, then the env file's Identity issuer.
.DESCRIPTION
    Pure apart from reading the two files. A name already set in the process environment is
    left alone (it wins, as with stack.ps1). IdentityServer__Endpoint falls back to the
    per-database env file's own value when neither the process nor host.env supplies an issuer.
#>
function Get-HostComposeVariables([string] $HostEnvPath, [string] $EnvFilePath) {
    $vars = [ordered]@{}
    foreach ($pair in (Read-EnvFileValues $HostEnvPath).GetEnumerator()) {
        if (-not [Environment]::GetEnvironmentVariable($pair.Key)) { $vars[$pair.Key] = $pair.Value }
    }
    $issuerNames = 'IdentityServer__Endpoint', 'VIKING_IDENTITY_URL', 'VIKING_HOSTNAME_IDENTITY'
    $haveIssuer = $issuerNames | Where-Object { $vars.Contains($_) -or [Environment]::GetEnvironmentVariable($_) }
    if (-not $haveIssuer) {
        $fromEnvFile = (Read-EnvFileValues $EnvFilePath)['IdentityServer__Endpoint']
        if ($fromEnvFile) { $vars['IdentityServer__Endpoint'] = $fromEnvFile }
    }
    return $vars
}

<#
.SYNOPSIS
    Returns a description of each compose input that is still missing, so one message can list them all.
.DESCRIPTION
    Compose needs a token issuer (IdentityServer__Endpoint, VIKING_IDENTITY_URL or
    VIKING_HOSTNAME_IDENTITY). The Docker root and certificate folder are defaulted by
    Invoke-ComposeService, so they are never missing. Variables are those about to be set.
#>
function Get-MissingComposeInputs([System.Collections.IDictionary] $Variables) {
    $missing = @()
    $issuerNames = 'IdentityServer__Endpoint', 'VIKING_IDENTITY_URL', 'VIKING_HOSTNAME_IDENTITY'
    if (-not ($issuerNames | Where-Object { $Variables.Contains($_) -or [Environment]::GetEnvironmentVariable($_) })) {
        $missing += 'Identity issuer: set one of IdentityServer__Endpoint, VIKING_IDENTITY_URL or VIKING_HOSTNAME_IDENTITY (host.env, the environment, or IdentityServer__Endpoint in the env file)'
    }
    return $missing
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
    Returns the host port a `docker ps` Ports column maps to the given container TCP port, or $null.
.DESCRIPTION
    Pure. Reads text such as "0.0.0.0:5010->80/tcp, [::]:5010->80/tcp, 0.0.0.0:5011->443/tcp".
    The IPv4 and IPv6 rows of one mapping name the same host port, so the first match is taken.
    Used with 443 for the HTTPS port a running project keeps, which is also its Viking endpoint.
#>
function Get-PublishedPort([string] $Ports, [int] $ContainerPort) {
    if ($Ports -match ":(\d+)->$ContainerPort/tcp") { return [int]$Matches[1] }
    return $null
}

<#
.SYNOPSIS
    Returns the `docker ps` Ports text of each running grpc-annotation-service container of the project.
.DESCRIPTION
    Read-only. Returns nothing when the service is not running (stopped or never started).
#>
function Get-ServicePorts([string] $ProjectName) {
    return (docker ps --filter "label=com.docker.compose.project=$ProjectName" `
            --filter "label=com.docker.compose.service=$serviceName" --format '{{.Ports}}')
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
    Bounded by a clock deadline: each probe gets at most 3 s and never more than the time left
    (at least 1 ms), and the wait stops instead of sleeping when the next probe would start after
    the deadline. A port that accepts TCP but never handshakes therefore ends the wait near
    TimeoutSeconds, not after attempts x (probe timeout + interval). The attempt cap
    (TimeoutSeconds / IntervalSeconds, at least one) also ends the wait when Start-Sleep is mocked
    and the clock barely moves. At least one probe always runs.
#>
function Wait-ServiceReady([int] $Port, [int] $TimeoutSeconds, [int] $IntervalSeconds = 2) {
    $attempts = [Math]::Max(1, [int][Math]::Ceiling($TimeoutSeconds / $IntervalSeconds))
    $timeoutMs = $TimeoutSeconds * 1000
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    for ($i = 1; $i -le $attempts; $i++) {
        # Floor of 1 ms: a negative timeout would make the connect wait forever (-1) or throw.
        $probeMs = [Math]::Max(1, [Math]::Min(3000, $timeoutMs - $clock.ElapsedMilliseconds))
        if (Test-TlsHandshake $Port $probeMs) { return $true }
        if ($i -eq $attempts -or $clock.ElapsedMilliseconds + $IntervalSeconds * 1000 -ge $timeoutMs) { break }
        Start-Sleep -Seconds $IntervalSeconds
    }
    return $false
}

<#
.SYNOPSIS
    Builds and starts grpc-annotation-service for the project, waits for its HTTPS port and prints the endpoint.
.DESCRIPTION
    Runs `docker compose up -d --build grpc-annotation-service` through Invoke-ComposeService,
    then Wait-ServiceReady. Throws when compose fails or the port does not answer within
    TimeoutSeconds; the containers are left running for `docker compose logs`.
#>
function Start-ComposeProject([string] $ProjectName, [System.Collections.IDictionary] $Variables, [int] $HttpsPort, [int] $TimeoutSeconds) {
    $httpsPort = $HttpsPort
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
    Returns the -Status line for one project: its Viking endpoint and whether it is ready, not answering, or stopped.
.DESCRIPTION
    RunningPorts is the Ports text of the project's running grpc-annotation-service containers
    (Get-ServicePorts); none means stopped, and nothing is probed. A running service is probed
    once with Test-TlsHandshake on the host port mapped to 443/tcp: ready when the handshake
    completes within ProbeMilliseconds, otherwise not answering. A running service that publishes
    no HTTPS port has no endpoint and is not answering. Read-only; never throws.
#>
function Get-StatusLine([string[]] $RunningPorts, [int] $ProbeMilliseconds) {
    if (@($RunningPorts | Where-Object { $null -ne $_ }).Count -eq 0) { return '  Endpoint: none  Health: stopped' }
    $httpsPort = Get-PublishedPort ($RunningPorts -join ', ') 443
    if (-not $httpsPort) { return '  Endpoint: none  Health: not answering' }
    $health = if (Test-TlsHandshake $httpsPort $ProbeMilliseconds) { 'ready' } else { 'not answering' }
    return "  Endpoint: https://localhost:$httpsPort/  Health: $health"
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

if ($Environment -eq 'Production' -and -not $ConfirmProduction) {
    throw 'Refusing a Production deployment without -ConfirmProduction. Re-run with -Environment Production -ConfirmProduction.'
}

if ($Status) {
    Assert-Docker
    # Assigned before filtering: Windows PowerShell 5.1's ConvertFrom-Json emits a JSON array as one
    # pipeline object, which Where-Object would pass through whole.
    $listed = docker compose ls --all --format json | ConvertFrom-Json
    $projects = @($listed | Where-Object { $_.Name -like "$projectPrefix*" })
    if ($projects.Count -eq 0) {
        Write-Host 'No viking-annotation-* deployments found.'
        return
    }
    foreach ($proj in $projects) {
        Write-Host ("{0}  [{1}]" -f $proj.Name, $proj.Status)
        docker ps --all --filter "label=com.docker.compose.project=$($proj.Name)" --format '  {{.Names}}  {{.Status}}  {{.Ports}}'
        Write-Host (Get-StatusLine (Get-ServicePorts $proj.Name) $statusProbeMilliseconds)
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

# A running project keeps its port: its own listener would otherwise make a re-run fail or move it.
$published = $null
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $published = Get-PublishedPort ((Get-ServicePorts $project) -join ', ') 443
}
$inUse = Get-ListeningPorts
if ($PSBoundParameters.ContainsKey('Port')) {
    if ($Port -ne $published -and $inUse -contains $Port) {
        throw "Port $Port is already in use on this host."
    }
}
elseif ($published) {
    $Port = $published
}
else {
    $Port = Find-FreePort $inUse
}

Write-Host "Database     : $Database"
Write-Host "Environment  : $Environment"
Write-Host "Project      : $project"
Write-Host "Host port    : $Port (HTTPS only, Viking endpoint https://localhost:$Port)"
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

$dockerRoot = if ($env:NORNIR_DOCKER_USER_ROOT) { $env:NORNIR_DOCKER_USER_ROOT } else { 'D:\Docker' }
$hostEnvPath = Join-Path $dockerRoot 'Run\_host\host.env'
$hostVars = Get-HostComposeVariables $hostEnvPath $envFull
foreach ($pair in $hostVars.GetEnumerator()) { $composeVars[$pair.Key] = $pair.Value }
if ($hostVars.Count -gt 0) { Write-Host "From host settings ($hostEnvPath or the env file): $($hostVars.Keys -join ', ')" }
$missingInputs = Get-MissingComposeInputs $composeVars
if ($missingInputs.Count -gt 0) {
    throw ("Compose is missing:`n  " + ($missingInputs -join "`n  "))
}

Assert-Docker
Invoke-ComposeService $project $composeVars @('config', '--quiet', $serviceName)
Write-Host "Compose configuration for $project is valid."

if ($PSCmdlet.ShouldProcess($project, "docker compose up -d --build $serviceName")) {
    Start-ComposeProject $project $composeVars $Port $startTimeoutSeconds
}
