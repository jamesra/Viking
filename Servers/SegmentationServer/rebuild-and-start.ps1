<#
.SYNOPSIS
  Add, change or remove a segmentation server in models.json, and write what the stack needs to run it.

.DESCRIPTION
  Run it through stack.ps1, which supplies the stack's settings and starts the servers afterwards:

      stack.ps1 viking-segmentation model v3 sam2_em_v3_epoch_9.pt
      stack.ps1 viking-segmentation model v5 v5/serve_epoch4.pt
      stack.ps1 viking-segmentation model            (every row)
      stack.ps1 viking-segmentation model -Remove v3

  models.json (<STACK_RUN_DIR>/models.json) is the one place that says which servers exist. Each row maps
  a DNS prefix to a weights file:

      v3  ->  v3.segmentation.<VIKING_DOMAIN>  ->  <models folder>/sam2_em_v3_epoch_9.pt

  The bare segmentation.<VIKING_DOMAIN> is the stack's own segmentation-server (SAM2_CHECKPOINT in the run
  env), not a row. A row has:

      prefix                 DNS label; the service is segmentation-<prefix>
      model                  weights file in the models folder, or a path below it (v5/serve_epoch4.pt);
                             <stem>.model.json beside it is served with it
      port                   host port when the server is published (default: next free from 40445)
      hostname               full host name (default <prefix>.segmentation.<VIKING_DOMAIN>: two labels, so
                             the certificate needs *.segmentation.<VIKING_DOMAIN> or each name listed)
      network                how the proxy reaches it: backend | published | macvlan
      mac, ip, dhcpHostname  macvlan only: the DHCP-reserved MAC and IP (and the name sent with the request)
      serviceName            Identity SegmentationService resource, to read local tilesets (optional)
      cacheMaxMemoryBytes    cap on the GPU cache of encoded tiles (optional; the server default is 16 GiB).
                             Set it for a server that shares the GPU with training.

  Without "network", a row uses backend when VIKING_BACKEND_NETWORK is set (viking-proxy is on this host
  and shares that Docker network, so nothing is published) and published otherwise (the proxy reaches the host
  port). macvlan gives the server its own MAC and IP on the LAN (docs/segmentation-host.md in connectomes-deploy).

  Writes, then reports the services to start:
    <STACK_RUN_DIR>/models.json              the table
    <STACK_RUN_DIR>/compose.generated.yaml   one service per row, merged by stack.ps1 after docker-compose.yml
    <STACK_DATA_DIR>/proxy-models.map        host name -> upstream lines for viking-proxy's SNI map; written
                                             as segmentation.map to SEGMENTATION_PROXY_MAP_DIR when that is set
                                             (viking-proxy's models.d folder on this host)
    <STACK_DATA_DIR>/cert-names.names        the rows' host names, for the certificate manager; written as
                                             segmentation.names to SEGMENTATION_CERT_NAMES_DIR when that is set
                                             (viking-certs' names.d folder on this host)
  A certificate that does not cover a row's host name is reported, never fatal: the server waits for valid
  files. Creating the DNS record for a new name is up to you.

.PARAMETER Prefix
  DNS prefix of the row. Omit to regenerate and select every row.

.PARAMETER Model
  Weights file for the row; saved to models.json. Omit to keep the stored one.

.PARAMETER Remove
  Delete the row. stack.ps1 then removes its container.

.PARAMETER DryRun
  Validate and print what would be written; change nothing.

.PARAMETER Help
  Show this text. Also -h.

.EXAMPLE
  rebuild-and-start.ps1 v5 v5/serve_epoch4.pt
  Add or update v5, serving the weights in the v5 subfolder.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Prefix,
    [Parameter(Position = 1)][string]$Model,
    [switch]$Remove,
    [switch]$DryRun,
    [Alias('h')][switch]$Help
)

if ($Help -or $Prefix -in @('-h', '--help', '/?', 'help')) {
    Get-Help -Full $PSCommandPath
    exit 0
}

$ErrorActionPreference = 'Stop'
$FirstFreePort = 40445
$Warnings = [System.Collections.Generic.List[string]]::new()

function Get-Setting([string]$Name, [string]$Default = '') {
    $v = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($v)) { return $Default }
    return $v.Trim()
}

function Write-Utf8([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Get-RowValue($Row, [string]$Name, $Default = $null) {
    $p = $Row.PSObject.Properties[$Name]
    if ($p -and -not [string]::IsNullOrWhiteSpace([string]$p.Value)) { return $p.Value }
    return $Default
}

$RunDir = Get-Setting 'STACK_RUN_DIR'
$DataDir = Get-Setting 'STACK_DATA_DIR'
if (-not $RunDir -or -not $DataDir) {
    throw 'STACK_RUN_DIR and STACK_DATA_DIR are not set. Run this through stack.ps1: stack.ps1 viking-segmentation model <prefix> [file.pt]'
}
$Domain = Get-Setting 'VIKING_DOMAIN'
if (-not $Domain) { throw 'VIKING_DOMAIN is not set (host.env).' }
$TablePath = Join-Path $RunDir 'models.json'
$GeneratedPath = Join-Path $RunDir 'compose.generated.yaml'
$MapPath = Join-Path $DataDir 'proxy-models.map'
$NamesPath = Join-Path $DataDir 'cert-names.names'
$ModelDir = Get-Setting 'SEGMENTATION_MODEL_HOST' (Join-Path $DataDir 'models')
$BackendNetwork = Get-Setting 'VIKING_BACKEND_NETWORK'
$UpstreamHost = Get-Setting 'SEGMENTATION_UPSTREAM_HOST' (Get-Setting 'VIKING_COMPUTE_HOST')
# The bare segmentation host name (VIKING_HOSTNAME_SEGMENTATION); each model is <prefix>.<that name>.
$SegmentationHost = Get-Setting 'VIKING_HOSTNAME_SEGMENTATION' "segmentation.$Domain"
$BaseCompose = (Join-Path $PSScriptRoot 'docker-compose.yml') -replace '\\', '/'

# ---- the table ---------------------------------------------------------------------------------------------
$rows = [System.Collections.Generic.List[object]]::new()
if (Test-Path -LiteralPath $TablePath) {
    $table = Get-Content -LiteralPath $TablePath -Raw | ConvertFrom-Json
    foreach ($r in @($table.models)) { if ($r) { $rows.Add($r) } }
}

$selected = @()
$removed = @()
if ($Prefix) {
    $Prefix = $Prefix.ToLowerInvariant()
    if ($Prefix -notmatch '^[a-z0-9][a-z0-9-]*$') { throw "Prefix '$Prefix' must be a DNS label (letters, digits, '-')." }
    $row = $rows | Where-Object { $_.prefix -eq $Prefix } | Select-Object -First 1
    if ($Remove) {
        if (-not $row) { throw "'$Prefix' is not in $TablePath." }
        [void]$rows.Remove($row)
        $removed = @("segmentation-$Prefix")
    }
    else {
        if (-not $row) {
            if (-not $Model) { throw "'$Prefix' is not in $TablePath. Give a weights file: model $Prefix <file.pt>" }
            $used = @($rows | ForEach-Object { [int](Get-RowValue $_ 'port' 0) })
            $port = $FirstFreePort
            while ($used -contains $port) { $port++ }
            $row = [pscustomobject]@{ prefix = $Prefix; model = $Model; port = $port }
            $rows.Add($row)
        }
        elseif ($Model) { $row.model = $Model }
        $selected = @($row)
    }
}
else {
    if ($Model -or $Remove) { throw 'A weights file or -Remove needs a prefix.' }
    $selected = @($rows)
}

# ---- validate ----------------------------------------------------------------------------------------------
function Get-HostName($Row) {
    $custom = Get-RowValue $Row 'hostname'
    if ($custom) { return [string]$custom }
    return "$($Row.prefix).$SegmentationHost"
}

$seenHosts = @{}
foreach ($r in $rows) {
    $name = Get-HostName $r
    if ($seenHosts.ContainsKey($name)) { throw "Rows '$($seenHosts[$name])' and '$($r.prefix)' both use the host name $name." }
    $seenHosts[$name] = $r.prefix
}
$seenPorts = @{}
foreach ($r in $rows) {
    $port = [int](Get-RowValue $r 'port' 0)
    if ($port -and $seenPorts.ContainsKey($port)) { throw "Rows '$($seenPorts[$port])' and '$($r.prefix)' both use port $port." }
    if ($port) { $seenPorts[$port] = $r.prefix }
}

function Get-RowNetwork($Row) {
    $n = [string](Get-RowValue $Row 'network' $(if ($BackendNetwork) { 'backend' } else { 'published' }))
    if ($n -notin 'backend', 'published', 'macvlan') { throw "Row '$($Row.prefix)': network must be backend, published or macvlan (got '$n')." }
    if ($n -eq 'backend' -and -not $BackendNetwork) { throw "Row '$($Row.prefix)': network backend needs VIKING_BACKEND_NETWORK in host.env (the Docker network viking-proxy shares)." }
    if ($n -eq 'published' -and -not $UpstreamHost) { $script:Warnings.Add("Row '$($Row.prefix)' is published, but neither SEGMENTATION_UPSTREAM_HOST nor VIKING_COMPUTE_HOST says where the proxy reaches this host; the proxy map line uses localhost.") }
    if ($n -eq 'macvlan') {
        foreach ($f in 'mac', 'ip') {
            if (-not (Get-RowValue $Row $f)) { throw "Row '$($Row.prefix)': network macvlan needs '$f' (the DHCP reservation)." }
        }
        foreach ($v in 'SEGMENTATION_MACVLAN_PARENT', 'SEGMENTATION_LAN_SUBNET', 'SEGMENTATION_LAN_GATEWAY') {
            if (-not (Get-Setting $v)) { throw "Row '$($Row.prefix)': network macvlan needs $v in the run env." }
        }
    }
    return $n
}

foreach ($r in $rows) {
    $relative = ([string]$r.model) -replace '\\', '/'
    if ($relative -match ':' -or $relative.StartsWith('/') -or $relative -match '//' -or $relative -match '(^|/)\.{1,2}(/|$)') {
        throw "Model '$($r.model)' for '$($r.prefix)' must be a file name or a path relative to $ModelDir (for example v5/serve_epoch4.pt)."
    }
    $r.model = $relative
    [void](Get-RowNetwork $r)
}
foreach ($r in $selected) {
    $weights = Join-Path $ModelDir $r.model
    if (-not (Test-Path -LiteralPath $weights -PathType Leaf)) { throw "Model file not found: $weights" }
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($weights)
    $dir = Split-Path -Parent $weights
    if (-not (Test-Path -LiteralPath (Join-Path $dir "$stem.model.json")) -and
        -not (Test-Path -LiteralPath (Join-Path $dir "$stem.prompt_encoding.json"))) {
        $Warnings.Add("$($r.model) has no $stem.model.json beside it; the server will serve it as an unnamed model with the legacy encoding.")
    }
}

# ---- compose overlay ---------------------------------------------------------------------------------------
$usesBackend = $false
$usesLan = $false
$y = [System.Text.StringBuilder]::new()
[void]$y.AppendLine('# Written by rebuild-and-start.ps1 from models.json. Do not edit; edit the table.')
$names = @('default') + @($rows | ForEach-Object { $_.prefix })
$initCommand = 'for d in ' + ($names -join ' ') + '; do mkdir -p /cache/segmentation-$$d-inductor /cache/segmentation-$$d-embeddings; done; chown -R user:user /cache'
[void]$y.AppendLine('services:')
[void]$y.AppendLine('  segmentation-init:')
[void]$y.AppendLine('    command:')
[void]$y.AppendLine("      - '$initCommand'")

foreach ($r in $rows) {
    $svc = "segmentation-$($r.prefix)"
    $network = Get-RowNetwork $r
    $cacheRoot = '${SEGMENTATION_CACHE_DIR:-${STACK_DATA_DIR}/cache}'
    [void]$y.AppendLine('')
    [void]$y.AppendLine("  ${svc}:")
    [void]$y.AppendLine('    extends:')
    [void]$y.AppendLine("      file: $BaseCompose")
    [void]$y.AppendLine('      service: segmentation-model')
    [void]$y.AppendLine('    profiles: !reset []')
    [void]$y.AppendLine('    depends_on:')
    [void]$y.AppendLine('      segmentation-init:')
    [void]$y.AppendLine('        condition: service_completed_successfully')
    if ($network -eq 'macvlan') {
        [void]$y.AppendLine("      segmentation-net-$($r.prefix):")
        [void]$y.AppendLine('        condition: service_started')
    }
    $cap = Get-RowValue $r 'cacheMaxMemoryBytes'
    if ($cap) {
        [void]$y.AppendLine("    command: [`"--cache-max-memory-bytes`", `"$([int64]$cap)`"]")
    }
    [void]$y.AppendLine('    environment:')
    [void]$y.AppendLine("      SAM2_CHECKPOINT: /models/$($r.model)")
    [void]$y.AppendLine("      SEGMENTATION_MODEL_NAME: `"$($r.prefix)`"")
    $service = Get-RowValue $r 'serviceName'
    if ($service) { [void]$y.AppendLine("      SEGMENTATION_SERVICE_NAME: `"$service`"") }
    [void]$y.AppendLine('    volumes:')
    [void]$y.AppendLine("      - ${cacheRoot}/segmentation-$($r.prefix)-inductor:/home/user/.cache/torch_inductor")
    [void]$y.AppendLine("      - ${cacheRoot}/segmentation-$($r.prefix)-embeddings:/var/cache/segmentation-embeddings")
    switch ($network) {
        'published' {
            [void]$y.AppendLine('    ports:')
            [void]$y.AppendLine("      - `"$([int](Get-RowValue $r 'port' 0)):443`"")
            [void]$y.AppendLine("      - `"`${SEGMENTATION_MODEL_DEMO_HOST_BIND:-127.0.0.1}:$([int](Get-RowValue $r 'port' 0) + 100):80`"")
        }
        'backend' {
            $usesBackend = $true
            [void]$y.AppendLine('    networks:')
            [void]$y.AppendLine('      - backend')
        }
        'macvlan' {
            $usesLan = $true
            [void]$y.AppendLine("    network_mode: service:segmentation-net-$($r.prefix)")
        }
    }
}

foreach ($r in $rows | Where-Object { (Get-RowNetwork $_) -eq 'macvlan' }) {
    $dhcpName = Get-RowValue $r 'dhcpHostname' "segmentation-$($r.prefix)"
    [void]$y.AppendLine('')
    [void]$y.AppendLine("  segmentation-net-$($r.prefix):")
    [void]$y.AppendLine('    image: ${SEGMENTATION_NET_IMAGE:-alpine:3.20}')
    [void]$y.AppendLine('    restart: unless-stopped')
    [void]$y.AppendLine('    entrypoint: ["/bin/sh", "/usr/local/bin/segmentation-net.sh"]')
    [void]$y.AppendLine('    networks:')
    [void]$y.AppendLine('      lan:')
    [void]$y.AppendLine("        ipv4_address: $($r.ip)")
    [void]$y.AppendLine("        mac_address: $($r.mac)")
    [void]$y.AppendLine('    environment:')
    [void]$y.AppendLine("      SEGMENTATION_LAN_IP: $($r.ip)")
    [void]$y.AppendLine('      SEGMENTATION_DHCP: ${SEGMENTATION_DHCP:-1}')
    [void]$y.AppendLine("      SEGMENTATION_DHCP_HOSTNAME: $dhcpName")
    [void]$y.AppendLine('    volumes:')
    [void]$y.AppendLine('      - type: bind')
    [void]$y.AppendLine('        source: ${DEPLOY_REPO_ROOT_HOST:?DEPLOY_REPO_ROOT_HOST must be set (stack.ps1 exports it)}/stacks/viking-segmentation/segmentation-net.sh')
    [void]$y.AppendLine('        target: /usr/local/bin/segmentation-net.sh')
    [void]$y.AppendLine('        read_only: true')
    [void]$y.AppendLine('    healthcheck:')
    [void]$y.AppendLine('      test: ["CMD-SHELL", "[ \"$${SEGMENTATION_DHCP}\" != 1 ] || [ -f /tmp/segmentation-net.bound ]"]')
    [void]$y.AppendLine('      interval: 30s')
    [void]$y.AppendLine('      timeout: 5s')
    [void]$y.AppendLine('      start_period: 60s')
    [void]$y.AppendLine('      retries: 3')
}

if ($usesBackend -or $usesLan) {
    [void]$y.AppendLine('')
    [void]$y.AppendLine('networks:')
    if ($usesBackend) {
        [void]$y.AppendLine('  backend:')
        [void]$y.AppendLine("    name: $BackendNetwork")
        [void]$y.AppendLine('    external: true')
    }
    if ($usesLan) {
        [void]$y.AppendLine('  lan:')
        [void]$y.AppendLine('    driver: macvlan')
        [void]$y.AppendLine('    driver_opts:')
        [void]$y.AppendLine('      parent: ${SEGMENTATION_MACVLAN_PARENT}')
        [void]$y.AppendLine('    ipam:')
        [void]$y.AppendLine('      config:')
        [void]$y.AppendLine('        - subnet: ${SEGMENTATION_LAN_SUBNET}')
        [void]$y.AppendLine('          gateway: ${SEGMENTATION_LAN_GATEWAY}')
    }
}

# ---- proxy map ---------------------------------------------------------------------------------------------
$map = [System.Text.StringBuilder]::new()
[void]$map.AppendLine('# Written by rebuild-and-start.ps1 from models.json. Included inside the SNI map of viking-proxy.')
foreach ($r in $rows) {
    $upstream = switch (Get-RowNetwork $r) {
        'backend' { "segmentation-$($r.prefix):443" }
        'macvlan' { "$($r.ip):443" }
        default { "$(if ($UpstreamHost) { $UpstreamHost } else { 'localhost' }):$([int](Get-RowValue $r 'port' 0))" }
    }
    [void]$map.AppendLine(('{0,-60} {1};' -f (Get-HostName $r), $upstream))
}

$names = [System.Text.StringBuilder]::new()
[void]$names.AppendLine('# Written by rebuild-and-start.ps1 from models.json: host names the certificate must cover.')
foreach ($r in $rows) { [void]$names.AppendLine((Get-HostName $r)) }

# ---- certificate coverage (report only) --------------------------------------------------------------------
$certDir = Get-Setting 'SEGMENTATION_CERT_DIR'
if (-not $certDir) {
    $certName = Get-Setting 'TLS_CERT_NAME' $Domain
    $certRoot = Get-Setting 'DOCKER_CERTS_DIR'
    if ($certRoot) { $certDir = Join-Path $certRoot $certName }
}
$fullchain = if ($certDir) { Join-Path $certDir 'fullchain.pem' } else { $null }
if ($fullchain -and (Test-Path -LiteralPath $fullchain) -and (Get-Command openssl -ErrorAction SilentlyContinue)) {
    $san = (& openssl x509 -noout -ext subjectAltName -in $fullchain 2>&1) -join ' '
    foreach ($r in $selected) {
        $name = Get-HostName $r
        $dot = $name.IndexOf('.')
        $covered = ($san -match ([regex]::Escape("DNS:$name") + '(,|\s|$)')) -or
            ($dot -gt 0 -and $san -match ([regex]::Escape('DNS:*' + $name.Substring($dot)) + '(,|\s|$)'))
        if (-not $covered) { $Warnings.Add("The certificate in $certDir does not cover $name. Request *.$SegmentationHost (or list the name in TLS_HOSTNAMES) and renew, or point SEGMENTATION_CERT_DIR at a certificate that does.") }
    }
}
elseif ($selected.Count -gt 0) {
    $Warnings.Add("Could not check the certificate for the host names ($(if ($fullchain) { "no openssl or no $fullchain" } else { 'no certificate folder known' })). The servers wait for valid PEM files.")
}

# ---- write -------------------------------------------------------------------------------------------------
if ($DryRun) {
    Write-Host "Dry run: would write $TablePath, $GeneratedPath and $MapPath."
    Write-Host $y.ToString()
}
else {
    $null = New-Item -ItemType Directory -Force -Path $RunDir, $DataDir
    Write-Utf8 $TablePath ((@{ models = @($rows) } | ConvertTo-Json -Depth 5) + "`n")
    Write-Utf8 $GeneratedPath $y.ToString()
    Write-Utf8 $MapPath $map.ToString()
    Write-Utf8 $NamesPath $names.ToString()
    $mapDir = Get-Setting 'SEGMENTATION_PROXY_MAP_DIR'
    if ($mapDir) {
        $null = New-Item -ItemType Directory -Force -Path $mapDir
        Write-Utf8 (Join-Path $mapDir 'segmentation.map') $map.ToString()
        Write-Host "Wrote the proxy map to $mapDir. Restart viking-proxy to load it."
    }
    else {
        Write-Host "Proxy map: $MapPath. On the proxy host, put it in viking-proxy's models.d folder as segmentation.map and restart viking-proxy."
    }
    $namesDir = Get-Setting 'SEGMENTATION_CERT_NAMES_DIR'
    if ($namesDir) {
        $null = New-Item -ItemType Directory -Force -Path $namesDir
        Write-Utf8 (Join-Path $namesDir 'segmentation.names') $names.ToString()
        Write-Host "Wrote the certificate names to $namesDir. viking-certs adds them on its next cycle."
    }
    else {
        Write-Host "Certificate names: $NamesPath. For a certificate that lists its names (letsencrypt, acme-eab), put it in viking-certs' names.d folder as segmentation.names."
    }
}

# ---- report ------------------------------------------------------------------------------------------------
Write-Host ''
Write-Host 'Segmentation servers (models.json):'
foreach ($r in $rows) {
    $mark = if ($selected | Where-Object { $_.prefix -eq $r.prefix }) { '*' } else { ' ' }
    Write-Host ('  {0} {1,-52} {2,-34} {3}' -f $mark, (Get-HostName $r), $r.model, (Get-RowNetwork $r))
}
if ($removed) { Write-Host "  removed: $($removed -join ', ')" }
Write-Host '  (* started by this run. A new host name needs a DNS record; viking-proxy routes it by SNI.)'
foreach ($w in ($Warnings | Select-Object -Unique)) { Write-Warning $w }

$resultPath = Get-Setting 'STACK_ACTION_RESULT'
if ($resultPath -and -not $DryRun) {
    $result = @{ services = @($selected | ForEach-Object { "segmentation-$($_.prefix)" }); removed = $removed }
    Write-Utf8 $resultPath ($result | ConvertTo-Json -Compress)
}
exit 0
