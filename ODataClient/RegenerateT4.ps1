#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Regenerates the ODataClient T4 template for the specified target framework.

.DESCRIPTION
    This script regenerates the ODataClient.cs file from the T4 template.
    It can target specific frameworks or regenerate for all configured targets.

.PARAMETER TargetFramework
    The target framework to regenerate for (net48, net9.0, or all).

.EXAMPLE
    .\RegenerateT4.ps1 -TargetFramework net9.0
    Regenerates the template for .NET 9.0

.EXAMPLE
    .\RegenerateT4.ps1 -TargetFramework all
    Regenerates the template for all target frameworks (net48 and net9.0)
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("net48", "net9.0", "all")]
    [string]$TargetFramework = "all"
)

$ErrorActionPreference = "Stop"

# Find TextTransform.exe
$textTransformPath = $null

# Try to find it in common locations
$possiblePaths = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\Common7\IDE\TextTransform.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\*\Common7\IDE\TextTransform.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2019\*\Common7\IDE\TextTransform.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\*\Common7\IDE\TextTransform.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\TextTransform.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\TextTransform.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\TextTransform.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\TextTransform.exe"
)

foreach ($path in $possiblePaths) {
    $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        $textTransformPath = $found.FullName
        break
    }
}

if (-not $textTransformPath) {
    Write-Error "TextTransform.exe not found. Please ensure Visual Studio or Build Tools are installed."
    exit 1
}

Write-Host "Using TextTransform.exe: $textTransformPath" -ForegroundColor Green

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$templateFile = Join-Path $scriptDir "ODataClient.tt"
$outputFile = Join-Path $scriptDir "ODataClient.cs"

if (-not (Test-Path $templateFile)) {
    Write-Error "Template file not found: $templateFile"
    exit 1
}

function Regenerate-ForFramework {
    param([string]$framework)
    
    Write-Host "Regenerating T4 template for $framework..." -ForegroundColor Yellow
    
    # Set environment variable for the T4 template
    $env:T4TargetFramework = $framework
    
    try {
        # Run TextTransform
        $arguments = @(
            $templateFile,
            "-out",
            $outputFile
        )
        
        $process = Start-Process -FilePath $textTransformPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
        
        if ($process.ExitCode -eq 0) {
            Write-Host "Successfully regenerated T4 template for $framework" -ForegroundColor Green
        } else {
            Write-Error "Failed to regenerate T4 template for $framework (Exit code: $($process.ExitCode))"
            return $false
        }
    }
    catch {
        Write-Error "Error regenerating T4 template for $framework`: $_"
        return $false
    }
    finally {
        # Clean up environment variable
        Remove-Item Env:T4TargetFramework -ErrorAction SilentlyContinue
    }
    
    return $true
}

# Regenerate based on target framework
$success = $true

switch ($TargetFramework) {
    "net48" {
        $success = Regenerate-ForFramework "net48"
    }
    "net9.0" {
        $success = Regenerate-ForFramework "net9.0"
    }
    "all" {
        # Regenerate for both frameworks
        $success = Regenerate-ForFramework "net48" -and Regenerate-ForFramework "net9.0"
    }
}

if ($success) {
    Write-Host "T4 template regeneration completed successfully!" -ForegroundColor Green
} else {
    Write-Error "T4 template regeneration failed!"
    exit 1
} 