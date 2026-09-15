<#
.SYNOPSIS
Append BajajMultiTest failed-slice lines into MorphologyMeshTest/DifficultCases/difficult-cases.json as open cases.

.DESCRIPTION
Parses bajajmultitest_failed_slices*.txt (FailedSliceReport format: a "# structure=… — [Kind] reason"
comment followed by a space-separated LocationID line). Skips keys already present in the JSON.
Open cases are tracked but skipped by DifficultCaseRegressionTests and Compare-DifficultCases.ps1.

.PARAMETER FailedSlicesPath
Path to bajajmultitest_failed_slices.txt or a stamped per-run copy.

.PARAMETER Volume
Viking.Common.Endpoint name (RC1, RPC1, …).

.PARAMETER Kind
Optional filter matching the [Kind] header (Topology, FaceGenerationException, InvalidSurface, UntiledLinkedPair).
When omitted, every record is imported.

.PARAMETER CasesFile
Defaults to MorphologyMeshTest/DifficultCases/difficult-cases.json under the repo root.

.PARAMETER WhatIf
Print what would be appended without writing the file.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $FailedSlicesPath,

    [Parameter(Mandatory = $true)]
    [string] $Volume,

    [string] $Kind,

    [string] $CasesFile,

    [switch] $WhatIf
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
if (-not $CasesFile) {
    $CasesFile = Join-Path $repoRoot 'MorphologyMeshTest\DifficultCases\difficult-cases.json'
}

if (-not (Test-Path -LiteralPath $FailedSlicesPath)) {
    throw "Failed slices file not found: $FailedSlicesPath"
}
if (-not (Test-Path -LiteralPath $CasesFile)) {
    throw "Cases file not found: $CasesFile"
}

$failedAbs = (Resolve-Path -LiteralPath $FailedSlicesPath).Path
$json = Get-Content -LiteralPath $CasesFile -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $json.cases) {
    $json | Add-Member -NotePropertyName cases -NotePropertyValue @() -Force
}

function Get-CaseKey([string] $vol, [uint64[]] $ids) {
    $sorted = $ids | Sort-Object
    return ($vol.ToUpperInvariant() + '-' + ($sorted -join '-'))
}

$existing = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($c in $json.cases) {
    $loc = @($c.locations | ForEach-Object { [uint64]$_ })
    [void]$existing.Add((Get-CaseKey $c.volume $loc))
}

$pendingHeader = $null
$pendingKind = $null
$added = 0
$skipped = 0
$filtered = 0

Get-Content -LiteralPath $FailedSlicesPath -Encoding UTF8 | ForEach-Object {
    $line = $_
    if ($line -match '^\s*#\s*structure=\d+\s+—\s+\[(?<kind>[^\]]+)\]\s*(?<reason>.*)$' `
        -or $line -match '^\s*#\s*structure=\d+\s+-\s+\[(?<kind>[^\]]+)\]\s*(?<reason>.*)$' `
        -or $line -match '^\s*#\s*structure=\d+\s+.*\[(?<kind>[^\]]+)\]\s*(?<reason>.*)$') {
        $pendingKind = $Matches['kind'].Trim()
        $reason = $Matches['reason'].Trim()
        $pendingHeader = $line.TrimStart('#').Trim()
        if ([string]::IsNullOrWhiteSpace($reason) -eq $false -and $pendingHeader -notmatch [regex]::Escape($reason)) {
            # keep full heading as problem text
        }
        return
    }

    if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
        return
    }

    if ($null -eq $pendingHeader) {
        Write-Warning "Location line without a preceding [Kind] header: $line"
        return
    }

    if ($Kind -and ($pendingKind -ne $Kind)) {
        $filtered++
        $pendingHeader = $null
        $pendingKind = $null
        return
    }

    $ids = @($line.Trim() -split '\s+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [uint64]$_ })
    if ($ids.Count -eq 0) {
        $pendingHeader = $null
        $pendingKind = $null
        return
    }

    $key = Get-CaseKey $Volume $ids
    if ($existing.Contains($key)) {
        $skipped++
        $pendingHeader = $null
        $pendingKind = $null
        return
    }

    $problem = "$pendingHeader Tracked from $failedAbs."
    $caseObj = [ordered]@{
        volume    = $Volume
        locations = @($ids | ForEach-Object { [long]$_ })
        open      = $true
        problem   = $problem
        fix       = 'Open — not yet fixed. Clear open, rewrite problem/fix, and accept baselines when the mesh is correct.'
        cameras   = @(
            @{ preset = 'side' },
            @{ preset = 'oblique' }
        )
    }
    if ($pendingKind) {
        $caseObj['failureKind'] = $pendingKind
    }

    Write-Host "ADD $key [$pendingKind]"
    if (-not $WhatIf) {
        $json.cases += [pscustomobject]$caseObj
        [void]$existing.Add($key)
    }
    $added++
    $pendingHeader = $null
    $pendingKind = $null
}

Write-Host "Added: $added  Skipped (already present): $skipped  Filtered by -Kind: $filtered"

if ($WhatIf) {
    Write-Host 'WhatIf: no file written.'
    return
}

if ($added -eq 0) {
    Write-Host 'Nothing to write.'
    return
}

$outJson = $json | ConvertTo-Json -Depth 8
# PowerShell ConvertTo-Json can emit UTF16; write UTF8 without BOM for the repo.
[System.IO.File]::WriteAllText((Resolve-Path $CasesFile), $outJson + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Updated $CasesFile"
