<#
.SYNOPSIS
    Copies a Viking Velopack release folder to the update server and bumps the next build version.

.DESCRIPTION
    Run this after PublishVelopack.ps1 has written .\releases. It:

    1. Requires RELEASES and Viking-win-Setup.exe in ReleasesDir (plus any .nupkg files).
    2. Copies that folder to ServerPath when DeploymentMethod is Copy.
    3. Checks that Setup.exe and RELEASES exist at the destination.
    4. On a verified Copy, increments the patch of ApplicationVersion / FileVersion /
       AssemblyVersion in Viking.csproj (1.2.0.0 -> 1.2.1.0) so the next publish is newer.

    WebDAV, FTP, and SCP are accepted as DeploymentMethod values but are not implemented.

    Show this help:
        .\DeployVelopack.ps1 -?
        .\DeployVelopack.ps1 -Help
        Get-Help .\DeployVelopack.ps1 -Full

.PARAMETER Help
    Writes this help and exits without deploying or changing Viking.csproj.

.PARAMETER ReleasesDir
    Folder produced by PublishVelopack.ps1. Relative paths are resolved from this script's
    directory. Default: releases

.PARAMETER ReleaseUrl
    Public HTTP base printed in the summary (where clients download updates).
    Default: http://websvc.codepharm.net/Software/Viking

.PARAMETER DeploymentMethod
    How to place files on the server: Copy, WebDAV, FTP, or SCP. Only Copy is implemented.
    Default: Copy

.PARAMETER ServerPath
    Destination directory or UNC share for Copy (for example \\server\share\Software\Viking).
    Required when DeploymentMethod is Copy.

.EXAMPLE
    .\DeployVelopack.ps1 -Help

    Print usage and parameter descriptions.

.EXAMPLE
    .\DeployVelopack.ps1 -ServerPath "\\server\share\Software\Viking"

    Copy .\releases to that share and bump Viking.csproj if verification succeeds.

.EXAMPLE
    .\DeployVelopack.ps1 -ReleasesDir "D:\build\viking-releases" -ServerPath "\\server\share\Software\Viking"

    Deploy from an explicit releases folder instead of .\releases.

.NOTES
    Does not build or sign. Run PublishVelopack.ps1 first.
    Version increment happens only after a successful Copy verification.
    Clients then fetch Viking-win-Setup.exe and RELEASES from ReleaseUrl.

.LINK
    PublishVelopack.ps1
#>

[CmdletBinding(PositionalBinding = $false)]
param(
    [switch]$Help,
    [string]$ReleasesDir = "releases",
    [string]$ReleaseUrl = "http://websvc.codepharm.net/Software/Viking",
    [ValidateSet("WebDAV", "FTP", "SCP", "Copy")]
    [string]$DeploymentMethod = "Copy",
    [string]$ServerPath = ""
)

if ($Help) {
    Get-Help -Name $PSCommandPath -Full
    exit 0
}

$ErrorActionPreference = "Stop"

# Resolve releases directory relative to script location
if (-not [System.IO.Path]::IsPathRooted($ReleasesDir)) {
    $ReleasesDir = Join-Path $PSScriptRoot $ReleasesDir
}

# Function to increment patch version in Viking.csproj
function IncrementPatchVersion {
    param(
        [string]$ProjectFilePath
    )
    
    try {
        if (-not (Test-Path $ProjectFilePath)) {
            Write-Host "Warning: Project file not found at $ProjectFilePath" -ForegroundColor Yellow
            return $null
        }
        
        # Read and parse the project file
        [xml]$projectXml = Get-Content $ProjectFilePath
        
        # Find the ApplicationVersion in the first PropertyGroup
        $propertyGroup = $projectXml.Project.PropertyGroup[0]
        if (-not $propertyGroup.ApplicationVersion) {
            Write-Host "Warning: ApplicationVersion not found in project file" -ForegroundColor Yellow
            return $null
        }
        
        $currentVersion = $propertyGroup.ApplicationVersion
        Write-Host "Current version: $currentVersion" -ForegroundColor Gray
        
        # Parse version: X.Y.Z.W
        if ($currentVersion -match '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
            $major = [int]$matches[1]
            $minor = [int]$matches[2]
            $patch = [int]$matches[3]
            $revision = [int]$matches[4]
            
            # Increment patch version
            $newPatch = $patch + 1
            $newVersion = "$major.$minor.$newPatch.$revision"
            
            Write-Host "Incrementing version: $currentVersion -> $newVersion" -ForegroundColor Cyan
            
            # Update all three version properties
            $propertyGroup.ApplicationVersion = $newVersion
            $propertyGroup.FileVersion = $newVersion
            
            # Update AssemblyVersion (create if it doesn't exist)
            if ($propertyGroup.AssemblyVersion) {
                $propertyGroup.AssemblyVersion = $newVersion
            } else {
                # Create AssemblyVersion element if it doesn't exist
                $assemblyVersionElement = $projectXml.CreateElement("AssemblyVersion")
                $assemblyVersionElement.InnerText = $newVersion
                $propertyGroup.AppendChild($assemblyVersionElement) | Out-Null
            }
            
            # Save the updated project file
            $projectXml.Save($ProjectFilePath)
            
            Write-Host "Version incremented to $newVersion for next deployment" -ForegroundColor Green
            return $newVersion
        } else {
            Write-Host "Warning: Version format '$currentVersion' is not in expected format (X.Y.Z.W)" -ForegroundColor Yellow
            return $null
        }
    } catch {
        Write-Host "Warning: Failed to increment version: $($_.Exception.Message)" -ForegroundColor Yellow
        return $null
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Viking Velopack Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Releases Directory: $ReleasesDir" -ForegroundColor Yellow
Write-Host "Release URL: $ReleaseUrl" -ForegroundColor Yellow
Write-Host "Deployment Method: $DeploymentMethod" -ForegroundColor Yellow
Write-Host ""

# Step 1: Validate releases directory
Write-Host "Step 1: Validating releases directory..." -ForegroundColor Green

if (-not (Test-Path $ReleasesDir)) {
    Write-Host "Error: Releases directory not found at $ReleasesDir" -ForegroundColor Red
    Write-Host "Please run PublishVelopack.ps1 first to create the releases." -ForegroundColor Yellow
    exit 1
}

# Check for required files
$requiredFiles = @("RELEASES", "Viking-win-Setup.exe")
$missingFiles = @()

foreach ($file in $requiredFiles) {
    $filePath = Join-Path $ReleasesDir $file
    if (-not (Test-Path $filePath)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host "Error: Required files not found:" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "  - $file" -ForegroundColor Red
    }
    exit 1
}

# Check for .nupkg files
$nupkgFiles = Get-ChildItem -Path $ReleasesDir -Filter "*.nupkg" -File
if ($nupkgFiles.Count -eq 0) {
    Write-Host "Warning: No .nupkg files found in releases directory" -ForegroundColor Yellow
} else {
    Write-Host "Found $($nupkgFiles.Count) .nupkg file(s)" -ForegroundColor Gray
}

Write-Host "Releases directory validated successfully!" -ForegroundColor Green

# Step 2: Deploy to server
Write-Host ""
Write-Host "Step 2: Deploying to server..." -ForegroundColor Green

switch ($DeploymentMethod) {
    "Copy" {
        if ([string]::IsNullOrWhiteSpace($ServerPath)) {
            Write-Host "Error: ServerPath parameter is required when using Copy deployment method" -ForegroundColor Red
            Write-Host "Example: .\DeployVelopack.ps1 -ServerPath '\\server\share\Software\Viking'" -ForegroundColor Yellow
            exit 1
        }
        
        Write-Host "Copying files to: $ServerPath" -ForegroundColor Gray
        
        # Ensure destination directory exists
        if (-not (Test-Path $ServerPath)) {
            Write-Host "Creating destination directory..." -ForegroundColor Gray
            New-Item -ItemType Directory -Path $ServerPath -Force | Out-Null
        }
        
        # Copy all files from releases directory
        Copy-Item -Path "$ReleasesDir\*" -Destination $ServerPath -Recurse -Force
        
        Write-Host "Files copied successfully!" -ForegroundColor Green
    }
    "WebDAV" {
        Write-Host "WebDAV deployment not yet implemented" -ForegroundColor Yellow
        Write-Host "Please use Copy method with a network share, or implement WebDAV deployment manually" -ForegroundColor Yellow
        exit 1
    }
    "FTP" {
        Write-Host "FTP deployment not yet implemented" -ForegroundColor Yellow
        Write-Host "Please use Copy method with a network share, or implement FTP deployment manually" -ForegroundColor Yellow
        exit 1
    }
    "SCP" {
        Write-Host "SCP deployment not yet implemented" -ForegroundColor Yellow
        Write-Host "Please use Copy method with a network share, or implement SCP deployment manually" -ForegroundColor Yellow
        exit 1
    }
}

# Step 3: Verify deployment
Write-Host ""
Write-Host "Step 3: Verifying deployment..." -ForegroundColor Green

$deploymentSuccessful = $false
$newVersion = $null

if ($DeploymentMethod -eq "Copy" -and -not [string]::IsNullOrWhiteSpace($ServerPath)) {
    # Verify files exist at destination
    $setupExeDest = Join-Path $ServerPath "Viking-win-Setup.exe"
    $releasesDest = Join-Path $ServerPath "RELEASES"
    
    # Also check for generic Setup.exe (in case naming changes)
    $setupExeGeneric = Join-Path $ServerPath "Setup.exe"
    
    if (((Test-Path $setupExeDest) -or (Test-Path $setupExeGeneric)) -and (Test-Path $releasesDest)) {
        Write-Host "Deployment verified successfully!" -ForegroundColor Green
        Write-Host "  Setup.exe found" -ForegroundColor Gray
        Write-Host "  RELEASES file found" -ForegroundColor Gray
        
        $deployedNupkg = Get-ChildItem -Path $ServerPath -Filter "*.nupkg" -File
        if ($deployedNupkg.Count -gt 0) {
            Write-Host "  $($deployedNupkg.Count) .nupkg file(s) found" -ForegroundColor Gray
        }
        
        $deploymentSuccessful = $true
    } else {
        Write-Host "Warning: Could not verify all required files were deployed" -ForegroundColor Yellow
        $deploymentSuccessful = $false
    }
} else {
    Write-Host "Note: Automated verification not available for this deployment method" -ForegroundColor Yellow
    Write-Host "Please manually verify files are accessible at: $ReleaseUrl" -ForegroundColor Yellow
    # For non-Copy methods, assume success if we got this far without errors
    $deploymentSuccessful = $true
}

# Step 4: Increment version for next deployment (only if deployment was successful)
if ($deploymentSuccessful) {
    Write-Host ""
    Write-Host "Step 4: Incrementing version for next deployment..." -ForegroundColor Green
    
    $projectFilePath = Join-Path $PSScriptRoot "Viking.csproj"
    $newVersion = IncrementPatchVersion -ProjectFilePath $projectFilePath
    
    if ($newVersion) {
        Write-Host "Version successfully incremented to $newVersion" -ForegroundColor Green
        Write-Host "Next deployment will use version $newVersion" -ForegroundColor Gray
    } else {
        Write-Host "Warning: Version increment failed, but deployment was successful" -ForegroundColor Yellow
        Write-Host "You may need to manually update the version in Viking.csproj" -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "Step 4: Skipping version increment (deployment verification failed)" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($deploymentSuccessful) {
    Write-Host "Deployment Complete!" -ForegroundColor Green
} else {
    Write-Host "Deployment Completed with Warnings" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Release URL: $ReleaseUrl" -ForegroundColor Cyan
if ($deploymentSuccessful -and $newVersion) {
    Write-Host "Next deployment version: $newVersion" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Users can now:" -ForegroundColor Yellow
Write-Host "1. Download Setup.exe from: $ReleaseUrl/Viking-win-Setup.exe" -ForegroundColor White
Write-Host "2. Install Viking using the setup file" -ForegroundColor White
Write-Host "3. The application will automatically check for updates from this location" -ForegroundColor White
Write-Host ""
