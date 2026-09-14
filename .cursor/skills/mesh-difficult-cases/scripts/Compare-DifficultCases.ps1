<#
.SYNOPSIS
Re-mesh every slice in difficult-cases.json with BajajTest, compare the Final-mesh PNGs against the stored baselines,
and write review.html for side-by-side judgement.

.DESCRIPTION
Steps: build a capture request from the case list, run MonogameTestbed --mode BajajTest --screenshots into
-OutputRoot, diff each PNG against MorphologyMeshTest/DifficultCases/baselines/<case>/, then write
<OutputRoot>/review.html (baseline | new | diff per shot, plus both manifold reports).

Exit code 0 when every case matches its baseline, 2 when any case changed or has no baseline, 1 on error.
A changed image is not a failure by itself: read the manifold reports and the images, decide whether the new mesh is
better, and run with -Accept to promote it.

.PARAMETER Case
Only cases whose key (RPC1-368195-368197) or ID list contains one of these strings.

.PARAMETER Accept
After comparing, copy the new PNGs and manifold.txt over the baselines for the selected cases.

.PARAMETER SkipCapture
Reuse the PNGs already in -OutputRoot instead of running BajajTest.

.PARAMETER NoCorrection
Capture the raw annotations (passes --correction none). Baselines are captured with default correction (`all`), as BajajMultiTest runs.

.PARAMETER Threshold
Percentage of changed pixels above which a shot is reported as changed (default 0.02).

.PARAMETER Tolerance
Per-channel difference (0-255) a pixel must exceed to count as changed (default 24), to ignore antialiasing jitter.

.PARAMETER BaselineWidth
Baselines are stored downscaled to this width (default 1280) to keep the repository small; new captures are
resized to the baseline size before comparison.
#>
[CmdletBinding()]
param(
    [string[]]$Case,
    [switch]$Accept,
    [switch]$SkipCapture,
    [switch]$NoCorrection,
    [string]$OutputRoot,
    [string]$CasesFile,
    [string]$BaselineRoot,
    [double]$Threshold = 0.02,
    [int]$Tolerance = 24,
    [int]$BaselineWidth = 1280,
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
if (-not $CasesFile) { $CasesFile = Join-Path $repoRoot 'MorphologyMeshTest\DifficultCases\difficult-cases.json' }
if (-not $BaselineRoot) { $BaselineRoot = Join-Path $repoRoot 'MorphologyMeshTest\DifficultCases\baselines' }
if (-not $OutputRoot) { $OutputRoot = Join-Path 'C:\Temp\DifficultCases' (Get-Date -Format 'yyyyMMdd-HHmmss') }
# dotnet build -p:Platform=x64 (the configuration BajajTest is normally built with) outputs under bin\x64; prefer it so
# the comparison does not silently run a stale AnyCPU build.
$testbedDir = Join-Path $repoRoot "Clients\MonogameTestbed\bin\x64\$Configuration\net9.0-windows"
if (-not (Test-Path (Join-Path $testbedDir 'MonogameTestbed.dll'))) { $testbedDir = Join-Path $repoRoot "Clients\MonogameTestbed\bin\$Configuration\net9.0-windows" }
$testbedDll = Join-Path $testbedDir 'MonogameTestbed.dll'
# Cameras used when a case lists none; a case's own list replaces these.
$defaultCameras = @(@{ preset = 'oblique' }, @{ preset = 'side' })

function Camera-Slug($cam) {
    if ($cam.name) { return $cam.name }
    if ($cam.preset) { return $cam.preset }
    return ('az{0:0}-el{1:0}' -f [double]($cam.azimuth), [double]($cam.elevation))
}

function Camera-Request($cam) {
    $r = [ordered]@{}
    foreach ($k in 'name', 'preset', 'azimuth', 'elevation', 'distance', 'lookAt', 'position') {
        if ($null -ne $cam.$k) { $r[$k] = $cam.$k }
    }
    return $r
}

function Read-Cases {
    param([string]$Path)
    $file = Get-Content $Path -Raw | ConvertFrom-Json
    $cases = @()
    foreach ($c in $file.cases) {
        if (-not $c.volume -or -not $c.locations) { continue }
        # Open = tracked BajajMultiTest failure not yet fixed; no accepted baseline to compare against.
        if ($c.open -eq $true) { continue }
        $ids = @($c.locations | ForEach-Object { [string]$_ })
        $cams = if ($c.cameras) { @($c.cameras) } else { $defaultCameras }
        $desc = if ($c.fix) { "$($c.problem) Fix: $($c.fix)" } else { [string]$c.problem }
        $cases += [pscustomobject]@{
            Volume      = ([string]$c.volume).ToUpperInvariant()
            Ids         = $ids
            Key         = (([string]$c.volume).ToUpperInvariant() + '-' + ($ids -join '-'))
            Description = $desc
            Cameras     = $cams
            Shots       = @('Final-mesh-2d') + @($cams | ForEach-Object { 'Final-mesh-3d-' + (Camera-Slug $_) })
        }
    }
    return $cases
}

function Select-Cases {
    param($All, [string[]]$Filter)
    if (-not $Filter) { return $All }
    return $All | Where-Object { $c = $_; ($Filter | Where-Object { $c.Key -like "*$_*" -or (($c.Ids -join ',') -like "*$_*") }).Count -gt 0 }
}

function Invoke-Capture {
    param($Cases)
    if (-not (Test-Path $testbedDll)) {
        Write-Host "Building MonogameTestbed ($Configuration)..."
        & dotnet build (Join-Path $repoRoot 'Clients\MonogameTestbed\MonogameTestbed.csproj') -c $Configuration -p:Platform=x64 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'MonogameTestbed build failed' }
    }

    $request = [ordered]@{
        reproLocations = @($Cases | ForEach-Object {
            [ordered]@{ locations = @($_.Ids | ForEach-Object { [uint64]$_ }); endpoint = $_.Volume; description = $_.Key }
        })
        # One capture process serves every case, so the request carries the union of the cases' cameras (one PNG per
        # camera per case); each case is then compared only on the shots it asked for.
        cameras3D = @($Cases | ForEach-Object { $_.Cameras } | Group-Object { Camera-Slug $_ } | ForEach-Object { Camera-Request $_.Group[0] })
        shots = @(
            [ordered]@{ stage = 'Final mesh'; view = '2d' },
            [ordered]@{ stage = 'Final mesh'; view = '3d' }
        )
    }
    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
    $requestPath = Join-Path $OutputRoot 'capture-request.json'
    $request | ConvertTo-Json -Depth 6 | Set-Content -Path $requestPath -Encoding UTF8

    $args = @('exec', $testbedDll, '--mode', 'BajajTest', '--screenshots', '--capture-request', $requestPath, '-o', $OutputRoot, '-q')
    if ($NoCorrection) { $args += '--correction'; $args += 'none' }
    Write-Host "Capturing $($Cases.Count) case(s) -> $OutputRoot"
    Push-Location $testbedDir
    try { & dotnet @args 2>&1 | Where-Object { $_ -match 'Exception|error' } | ForEach-Object { Write-Warning $_ } }
    finally { Pop-Location }
}

function Find-CaseFolder {
    param([string]$Key)
    $root = Join-Path $OutputRoot 'BajajTest'
    if (-not (Test-Path $root)) { return $null }
    return Get-ChildItem $root -Directory | Where-Object { $_.Name -like "case-*-$Key" } | Select-Object -First 1
}

function Find-Png {
    param([string]$Folder, [string]$Slug)
    if (-not $Folder -or -not (Test-Path $Folder)) { return $null }
    return Get-ChildItem $Folder -Filter "*$Slug.png" | Select-Object -First 1
}

function Get-FinalLine {
    param([string]$ManifoldPath)
    if (-not $ManifoldPath -or -not (Test-Path $ManifoldPath)) { return '(none)' }
    $line = Get-Content $ManifoldPath | Where-Object { $_ -match '^final' } | Select-Object -First 1
    if ($line) { return $line } else { return '(none)' }
}

function Html([string]$s) { return [System.Net.WebUtility]::HtmlEncode($s) }
function Rel([string]$path) { if (-not $path) { return '' }; $u = New-Object Uri($path); return $u.AbsoluteUri }

Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'ImageDiff.cs') -ReferencedAssemblies System.Drawing

$allCases = Read-Cases $CasesFile
$cases = @(Select-Cases $allCases $Case)
if ($cases.Count -eq 0) { throw "No cases matched: $Case" }

if (-not $SkipCapture) { Invoke-Capture $cases }

$diffRoot = Join-Path $OutputRoot 'diff'
New-Item -ItemType Directory -Force -Path $diffRoot | Out-Null

$results = @()
foreach ($c in $cases) {
    $folder = Find-CaseFolder $c.Key
    $baseDir = Join-Path $BaselineRoot $c.Key
    $hasBaseline = Test-Path $baseDir
    $caseResult = [pscustomobject]@{
        Case = $c; Folder = $folder; BaselineDir = $baseDir; HasBaseline = $hasBaseline
        Shots = @(); Verdict = 'unchanged'; MaxPercent = 0.0
        BaselineFinal = Get-FinalLine (Join-Path $baseDir 'manifold.txt')
        NewFinal = if ($folder) { Get-FinalLine (Join-Path $folder.FullName 'manifold.txt') } else { '(capture missing)' }
    }

    foreach ($slug in $c.Shots) {
        $newPng = Find-Png $(if ($folder) { $folder.FullName } else { $null }) $slug
        $basePng = Find-Png $baseDir $slug
        $shot = [pscustomobject]@{ Slug = $slug; New = $newPng; Baseline = $basePng; Diff = $null; Percent = $null; Resized = $false; State = 'unchanged' }

        if (-not $newPng) { $shot.State = 'missing' }
        elseif (-not $basePng) { $shot.State = 'new' }
        else {
            $diffPath = Join-Path $diffRoot "$($c.Key)-$slug.png"
            $r = [MeshDifficultCases.ImageDiff]::Compare($basePng.FullName, $newPng.FullName, $diffPath, $Tolerance)
            $shot.Diff = Get-Item $diffPath
            $shot.Percent = [math]::Round($r.Percent, 3)
            $shot.Resized = $r.Resized
            if ($r.Percent -gt $Threshold) { $shot.State = 'changed' }
            if ($r.Percent -gt $caseResult.MaxPercent) { $caseResult.MaxPercent = [math]::Round($r.Percent, 3) }
        }
        $caseResult.Shots += $shot
    }

    $states = $caseResult.Shots | ForEach-Object { $_.State }
    if ($states -contains 'missing') { $caseResult.Verdict = 'missing' }
    elseif ($states -contains 'new') { $caseResult.Verdict = 'new' }
    elseif ($states -contains 'changed') { $caseResult.Verdict = 'changed' }
    if ($caseResult.BaselineFinal -ne $caseResult.NewFinal -and $caseResult.Verdict -eq 'unchanged' -and $hasBaseline) { $caseResult.Verdict = 'changed' }
    $results += $caseResult
}

# review.html
$template = Get-Content (Join-Path $PSScriptRoot 'review-template.html') -Raw
$summaryRows = New-Object System.Text.StringBuilder
$caseBlocks = New-Object System.Text.StringBuilder
foreach ($r in $results) {
    $key = $r.Case.Key
    [void]$summaryRows.AppendLine("<tr><td><a href=""#$key"" style=""color:#ddd"">$(Html $key)</a></td><td><span class=""badge $($r.Verdict)"">$($r.Verdict)</span></td><td>$($r.MaxPercent)%</td><td><code>$(Html $r.BaselineFinal)</code></td><td><code>$(Html $r.NewFinal)</code></td></tr>")

    [void]$caseBlocks.AppendLine("<div class=""case"" id=""$key""><h2><a href=""#$key"">$(Html $key)</a><span class=""badge $($r.Verdict)"">$($r.Verdict)</span></h2>")
    [void]$caseBlocks.AppendLine("<div class=""desc"">$(Html $r.Case.Description)</div>")
    [void]$caseBlocks.AppendLine("<div class=""report"">baseline: $(Html $r.BaselineFinal)`nnew:      $(Html $r.NewFinal)</div>")
    foreach ($s in $r.Shots) {
        $pct = if ($null -ne $s.Percent) { "$($s.Percent)% changed" } else { '' }
        $resized = if ($s.Resized) { ' (new image resized to baseline size)' } else { '' }
        [void]$caseBlocks.AppendLine("<div class=""shot""><h3>$(Html $s.Slug)<span class=""badge $($s.State)"">$($s.State)</span> <span style=""color:#999;font-size:.8rem"">$pct$resized</span></h3><div class=""row"">")
        foreach ($col in @(@('Baseline', $s.Baseline), @('New', $s.New), @('Diff (red = changed)', $s.Diff))) {
            $src = if ($col[1]) { Rel $col[1].FullName } else { '' }
            $img = if ($src) { "<img src=""$src"" alt=""$($col[0])"">" } else { "<div style=""color:#666;font-size:.8rem"">none</div>" }
            [void]$caseBlocks.AppendLine("<figure><figcaption>$($col[0])</figcaption>$img</figure>")
        }
        [void]$caseBlocks.AppendLine('</div></div>')
    }
    [void]$caseBlocks.AppendLine("<div class=""judge"">Verdict: <label><input type=""radio"" name=""j-$key""> Improved / accept</label><label><input type=""radio"" name=""j-$key""> Regressed / fix</label><label><input type=""radio"" name=""j-$key""> Equivalent</label></div></div>")
}

$html = $template.Replace('{{TITLE}}', 'Difficult-case mesh review').
    Replace('{{GENERATED}}', (Get-Date -Format 'yyyy-MM-dd HH:mm')).
    Replace('{{OUTPUT}}', (Html $OutputRoot)).
    Replace('{{BASELINES}}', (Html $BaselineRoot)).
    Replace('{{THRESHOLD}}', "$Threshold").
    Replace('{{SUMMARY}}', $summaryRows.ToString()).
    Replace('{{CASES}}', $caseBlocks.ToString())
$reviewPath = Join-Path $OutputRoot 'review.html'
Set-Content -Path $reviewPath -Value $html -Encoding UTF8

# Console summary
Write-Host ''
Write-Host ('{0,-28} {1,-10} {2,8}  {3}' -f 'Case', 'Verdict', 'MaxDiff%', 'New final')
foreach ($r in $results) {
    Write-Host ('{0,-28} {1,-10} {2,8}  {3}' -f $r.Case.Key, $r.Verdict, $r.MaxPercent, $r.NewFinal)
}
Write-Host ''
Write-Host "Review: $reviewPath"

if ($Accept) {
    foreach ($r in $results) {
        if (-not $r.Folder) { Write-Warning "No capture for $($r.Case.Key); nothing to accept"; continue }
        $dest = $r.BaselineDir
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        Get-ChildItem $dest -Filter *.png | Remove-Item
        foreach ($s in $r.Shots) { if ($s.New) { [MeshDifficultCases.ImageDiff]::SaveScaled($s.New.FullName, (Join-Path $dest "$($s.Slug).png"), $BaselineWidth) } }
        $manifold = Join-Path $r.Folder.FullName 'manifold.txt'
        if (Test-Path $manifold) { Copy-Item $manifold (Join-Path $dest 'manifold.txt') }
        Write-Host "Accepted baseline: $dest"
    }
    exit 0
}

if (($results | Where-Object { $_.Verdict -ne 'unchanged' }).Count -gt 0) { exit 2 }
exit 0
