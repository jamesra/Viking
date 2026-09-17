<#
.SYNOPSIS
    Diagnoses every deployed Export application on this IIS host and reports the faults that
    make exports fail, optionally repairing the ones that are safe to repair automatically.

.DESCRIPTION
    DataExport has no volume segment in its routes. The volume is bound to a deployment through
    the AppSettings:ODataURL found in the content root, which means the IIS physical path of each
    application decides which volume it actually serves. That makes the deployment easy to get
    wrong in ways that are invisible from the outside: the help page keeps serving normally while
    every export fails, or worse, every volume silently serves the same volume's data.

    This script checks for the four faults observed on this host:

    1. Shared content root. Several applications pointed at C:\Services\Release\Export, the
       binaries folder, rather than at C:\inetpub\wwwroot\{Volume}\Export. All of them therefore
       read one appsettings.json and served one volume regardless of the URL.

    2. Application pool collision. ASP.NET Core in-process hosting allows exactly one application
       per pool. A second application in the same pool fails with HTTP 500.30 at startup. This is
       why /Export and /RPC1/Export both failed while /RC1/Export worked.

    3. Managed runtime set on the pool. An ASP.NET Core pool must be "No Managed Code".

    4. Unreachable or wrong ODataURL, and an incomplete content root. Resources\ColorMapping must
       be present or every Morphology export throws while Network and Motif appear to work.

.PARAMETER SiteName
    IIS site to inspect. Defaults to "Default Web Site".

.PARAMETER UrlBase
    Base URL used to rebuild ODataURL during repair. Defaults to "https://websvc.codepharm.net/".

.PARAMETER BinariesFolder
    Folder holding the published DataExport binaries and the canonical Content and Resources
    payload. Defaults to "C:\Services\Release\Export".

.PARAMETER Repair
    Apply the repairs that can be made safely: point each application at its own per-volume
    content root, give each its own "No Managed Code" pool, populate Content and Resources,
    and write a correct per-volume appsettings.json.

.PARAMETER LogLines
    Number of lines to show from the end of each stdout log. Defaults to 20.

.EXAMPLE
    .\Diagnose-ExportDeployments.ps1

    Report only. Makes no changes.

.EXAMPLE
    .\Diagnose-ExportDeployments.ps1 -Repair

    Report, then restructure each Export application so it serves its own volume.

.NOTES
    Requires Administrator privileges and the WebAdministration module. Run this on the IIS host.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$SiteName = "Default Web Site",

    [Parameter(Mandatory = $false)]
    [string]$UrlBase = "https://websvc.codepharm.net/",

    [Parameter(Mandatory = $false)]
    [string]$BinariesFolder = "C:\Services\Release\Export",

    [Parameter(Mandatory = $false)]
    [switch]$Repair,

    [Parameter(Mandatory = $false)]
    [int]$LogLines = 20
)

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

try {
    Import-Module WebAdministration -SkipEditionCheck -ErrorAction Stop
} catch {
    try {
        Import-Module WebAdministration -ErrorAction Stop
    } catch {
        Write-Error "Failed to load WebAdministration module: $_"
        exit 1
    }
}

if (-not $UrlBase.EndsWith('/')) { $UrlBase = "$UrlBase/" }

$appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"

function Write-Status {
    param([bool]$Ok, [string]$Message)
    if ($Ok) { Write-Host "    [ OK ] $Message" -ForegroundColor Green }
    else     { Write-Host "    [FAIL] $Message" -ForegroundColor Red }
}

function Test-ODataEndpoint {
    param([string]$Url)

    $result = [pscustomobject]@{ Reachable = $false; StatusCode = $null; Detail = $null }

    if ([string]::IsNullOrWhiteSpace($Url)) { $result.Detail = "no URL configured"; return $result }
    if (-not [Uri]::IsWellFormedUriString($Url, [UriKind]::Absolute)) {
        $result.Detail = "not an absolute URL"
        return $result
    }

    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 20 -UseBasicParsing -ErrorAction Stop
        $result.StatusCode = [int]$response.StatusCode
        $result.Reachable = $true
        $result.Detail = "HTTP $($result.StatusCode)"
    } catch {
        $webResponse = $_.Exception.Response
        if ($null -ne $webResponse) {
            $result.StatusCode = [int]$webResponse.StatusCode
            $result.Detail = "HTTP $($result.StatusCode)"
        } else {
            $result.Detail = $_.Exception.Message
        }
    }

    return $result
}

Write-Host ""
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " Export deployment diagnostic" -ForegroundColor Cyan
Write-Host " Site: $SiteName   Host: $env:COMPUTERNAME" -ForegroundColor Cyan
Write-Host " Mode: $(if ($Repair) { 'REPAIR' } else { 'report only' })" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$allApps = @(Get-WebApplication -Site $SiteName)
$exportApps = @($allApps | Where-Object { $_.Path -match '(?i)/Export/?$' })

if (-not $exportApps) {
    Write-Warning "No applications ending in /Export were found under site '$SiteName'."
    exit 0
}

# Pool collisions are a property of the whole site, so they must be computed up front.
$poolUsage = @{}
foreach ($a in $allApps) {
    $pool = $a.applicationPool
    if (-not $poolUsage.ContainsKey($pool)) { $poolUsage[$pool] = @() }
    $poolUsage[$pool] += $a.Path
}

$siteRoot = (Get-Website | Where-Object { $_.Name -eq $SiteName } | Select-Object -First 1).PhysicalPath
if ([string]::IsNullOrWhiteSpace($siteRoot)) { $siteRoot = "C:\inetpub\wwwroot" }

$summary = @()

foreach ($app in $exportApps) {
    $virtualPath = $app.Path
    $physicalPath = $app.PhysicalPath
    $poolName = $app.applicationPool

    # The volume name is the segment before /Export. The host-level /Export has none.
    $volumeName = ($virtualPath.Trim('/') -split '/')[0]
    $isHostRoot = $virtualPath.Trim('/') -eq 'Export'

    Write-Host ""
    Write-Host "--- $virtualPath ---" -ForegroundColor Yellow
    Write-Host "    Physical path : $physicalPath"
    Write-Host "    App pool      : $poolName"

    $faults = @()

    # Fault 2: pool collision. This is the HTTP 500.30 cause.
    $sharing = @($poolUsage[$poolName] | Where-Object { $_ -ne $virtualPath })
    if ($sharing.Count -gt 0) {
        Write-Status $false "Pool '$poolName' also hosts: $($sharing -join ', ')"
        Write-Host "           In-process ASP.NET Core allows one application per pool; this yields HTTP 500.30." -ForegroundColor Red
        $faults += "pool-collision"
    } else {
        Write-Status $true "Application pool is not shared"
    }

    # Fault 3: managed runtime must be empty for ASP.NET Core.
    if (Test-Path "IIS:\AppPools\$poolName") {
        $pool = Get-Item "IIS:\AppPools\$poolName"
        $runtimeOk = [string]::IsNullOrEmpty($pool.managedRuntimeVersion)
        Write-Status $runtimeOk "Pool runtime is '$(if ($runtimeOk) { 'No Managed Code' } else { $pool.managedRuntimeVersion })'"
        if (-not $runtimeOk) { $faults += "pool-runtime" }
    } else {
        Write-Status $false "Application pool '$poolName' does not exist"
        $faults += "pool-missing"
    }

    # Fault 1: content root must be the per-volume folder, not the shared binaries folder.
    $expectedRoot = if ($isHostRoot) { $null } else { Join-Path (Join-Path $siteRoot $volumeName) "Export" }
    $sharesBinaries = $physicalPath.TrimEnd('\') -ieq $BinariesFolder.TrimEnd('\')
    if ($sharesBinaries) {
        Write-Status $false "Content root is the shared binaries folder, so this app serves whatever volume that folder is configured for"
        $faults += "shared-content-root"
    } elseif ($expectedRoot -and ($physicalPath.TrimEnd('\') -ine $expectedRoot.TrimEnd('\'))) {
        Write-Status $false "Content root is '$physicalPath', expected '$expectedRoot'"
        $faults += "wrong-content-root"
    } else {
        Write-Status $true "Content root is dedicated to this application"
    }

    # Fault 4a: content root payload.
    $hasResources = (Get-ChildItem (Join-Path $physicalPath "Resources\ColorMapping") -File -ErrorAction SilentlyContinue).Count -gt 0
    Write-Status $hasResources "Resources\ColorMapping is populated (required by Morphology exports)"
    if (-not $hasResources) { $faults += "missing-resources" }

    $hasContent = Test-Path (Join-Path $physicalPath "Content\index.html")
    Write-Status $hasContent "Content\index.html is present (the help page and web root)"
    if (-not $hasContent) { $faults += "missing-content" }

    # Fault 4b: the OData target.
    $odataUrl = $null
    $appSettingsPath = Join-Path $physicalPath "appsettings.json"
    if (Test-Path $appSettingsPath) {
        try {
            $settings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
            $odataUrl = $settings.AppSettings.ODataURL
            Write-Host "    ODataURL      : $odataUrl"
        } catch {
            Write-Status $false "appsettings.json could not be parsed: $_"
            $faults += "bad-appsettings"
        }
    } else {
        Write-Status $false "appsettings.json is missing"
        $faults += "missing-appsettings"
    }

    $probe = Test-ODataEndpoint -Url $odataUrl
    Write-Status $probe.Reachable "OData endpoint reachable from this host ($($probe.Detail))"
    if (-not $probe.Reachable) { $faults += "odata-unreachable" }

    # The configured volume should match the URL the caller uses to reach this app.
    if (-not $isHostRoot -and -not [string]::IsNullOrWhiteSpace($odataUrl)) {
        $expectedOData = "$UrlBase$volumeName/OData"
        if ($odataUrl.TrimEnd('/') -ine $expectedOData.TrimEnd('/')) {
            Write-Status $false "Configured volume does not match the URL: expected '$expectedOData'"
            $faults += "volume-mismatch"
        } else {
            Write-Status $true "Configured volume matches the URL"
        }
    }

    $logFolder = Join-Path $physicalPath "logs"
    if (Test-Path $logFolder) {
        $latestLog = Get-ChildItem -Path $logFolder -Filter "stdout*.log" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($null -ne $latestLog) {
            Write-Host "    --- $($latestLog.Name), last $LogLines lines ---" -ForegroundColor DarkGray
            Get-Content $latestLog.FullName -Tail $LogLines | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
        }
    }

    if ($Repair -and -not $isHostRoot) {
        Write-Host "    Repairing..." -ForegroundColor Cyan

        $targetRoot = $expectedRoot
        $dedicatedPool = "net9-$volumeName"

        # Give the application its own content root, populated from the binaries folder.
        if (-not (Test-Path $targetRoot)) { New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null }
        foreach ($sub in @("logs", "Output")) {
            $subPath = Join-Path $targetRoot $sub
            if (-not (Test-Path $subPath)) { New-Item -ItemType Directory -Path $subPath -Force | Out-Null }
        }
        foreach ($payload in @("Content", "Resources")) {
            $src = Join-Path $BinariesFolder $payload
            if (Test-Path $src) { Copy-Item -Path $src -Destination $targetRoot -Recurse -Force }
        }

        # web.config must launch the binaries from the shared folder while the content root stays local.
        $srcWebConfig = Join-Path $BinariesFolder "web.config"
        $dstWebConfig = Join-Path $targetRoot "web.config"
        if ((Test-Path $srcWebConfig) -and -not (Test-Path $dstWebConfig)) {
            Copy-Item -Path $srcWebConfig -Destination $dstWebConfig -Force
        }
        if (Test-Path $dstWebConfig) {
            $dll = Join-Path $BinariesFolder "DataExport.dll"
            $wc = Get-Content $dstWebConfig -Raw
            $wc = $wc -replace 'arguments="[^"]*"', "arguments=`"$dll`""
            # Startup failures are undiagnosable without this.
            $wc = $wc -replace 'stdoutLogEnabled="false"', 'stdoutLogEnabled="true"'
            Set-Content -Path $dstWebConfig -Value $wc -Force
        }

        # Write a correct per-volume appsettings.json.
        $srcSettings = Join-Path $BinariesFolder "appsettings.json"
        $dstSettings = Join-Path $targetRoot "appsettings.json"
        if (-not (Test-Path $dstSettings) -and (Test-Path $srcSettings)) {
            Copy-Item -Path $srcSettings -Destination $dstSettings -Force
        }
        if (Test-Path $dstSettings) {
            $s = Get-Content $dstSettings -Raw | ConvertFrom-Json
            $s.AppSettings.VolumeURL = "$UrlBase$volumeName"
            $s.AppSettings.ODataURL = "$UrlBase$volumeName/OData"
            $s | ConvertTo-Json -Depth 10 | Set-Content -Path $dstSettings -Force
        }

        # One application per pool, with no managed runtime.
        if (-not (Test-Path "IIS:\AppPools\$dedicatedPool")) {
            New-WebAppPool -Name $dedicatedPool -Force | Out-Null
        }
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
            -Filter "system.applicationHost/applicationPools/add[@name='$dedicatedPool']" `
            -Name "managedRuntimeVersion" -Value "" -ErrorAction SilentlyContinue

        if (Test-Path $appcmd) {
            & $appcmd set app "$SiteName$virtualPath" /applicationPool:$dedicatedPool | Out-Null
            & $appcmd set app "$SiteName$virtualPath/" "/[path='/'].physicalPath:$targetRoot" | Out-Null
        }

        try { Restart-WebAppPool -Name $dedicatedPool -ErrorAction Stop } catch { }

        Write-Status $true "Repointed to '$targetRoot' in dedicated pool '$dedicatedPool'"
    }

    $summary += [pscustomobject]@{
        Application = $virtualPath
        Pool        = $poolName
        ODataOk     = $probe.Reachable
        Faults      = if ($faults.Count) { $faults -join ',' } else { 'none' }
    }
}

Write-Host ""
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " Summary" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
$summary | Format-Table -AutoSize

if (-not $Repair) {
    $broken = @($summary | Where-Object { $_.Faults -ne 'none' })
    if ($broken.Count) {
        Write-Host "Re-run with -Repair to restructure these applications." -ForegroundColor Yellow
        Write-Host "The host-level /Export application is skipped by repair; it is replaced by the" -ForegroundColor Yellow
        Write-Host "static portal via Deploy-ExportPortal.ps1." -ForegroundColor Yellow
    }
}
