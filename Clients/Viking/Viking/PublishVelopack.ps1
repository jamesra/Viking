# PowerShell script to build, package, and sign Viking using Velopack
# Builds the application, packages it with Velopack, and signs with ECC HSM certificate

<#
.SYNOPSIS
    Builds, packages, and signs a Viking application using Velopack.

.DESCRIPTION
    This script:
    1. Checks for Velopack CLI (vpk) and installs if needed
    2. Builds the application using dotnet build
    3. Publishes the application
    4. Preserves or downloads previous release packages for delta generation
    5. Packages the application with Velopack (generates delta packages if previous release exists)
    6. Signs Setup.exe and all .nupkg files with ECC HSM certificate

.PARAMETER Configuration
    The build configuration to use (default: Release)

.PARAMETER CertificateThumbprint
    The thumbprint of the certificate to use for signing (default: 41403cbc59209b576efe575775abe8f4a42da6ba)

.PARAMETER TimestampRfc3161Url
    The RFC 3161 timestamp server URL (default: http://timestamp.sectigo.com)

.PARAMETER Version
    The version number to use for the package (default: reads from Viking.csproj)

.PARAMETER ReleaseUrl
    The base URL where releases are hosted for downloading previous versions (default: http://websvc.codepharm.net/Software/Viking)

.EXAMPLE
    .\PublishVelopack.cmd
    Builds Release (default) when no configuration is specified.
.EXAMPLE
    .\PublishVelopack.ps1 -Configuration Release
.EXAMPLE
    .\PublishVelopack.ps1 -Configuration Release -Version "1.2.1.0"
#>

param(
    [string]$Configuration = "Release",
    [string]$CertificateThumbprint = "41403cbc59209b576efe575775abe8f4a42da6ba",
    [string]$TimestampRfc3161Url = "http://timestamp.sectigo.com",
    [string]$Version = "",
    [string]$ReleaseUrl = "http://websvc.codepharm.net/Software/Viking"
)

$ErrorActionPreference = "Stop"

# Accept dotnet-style "--configuration Release" args (PowerShell 5.1 does not bind these to -Configuration).
for ($i = 0; $i -lt $args.Count; $i++) {
    $arg = $args[$i]
    if ($arg -match '^--?configuration$' -or $arg -eq '-c') {
        if ($i + 1 -lt $args.Count) {
            $Configuration = $args[$i + 1]
            $i++
        }
    }
}

# Default to Release when omitted or malformed (e.g. "--configuration" passed without a value).
$validConfigurations = @('Debug', 'Release')
if ([string]::IsNullOrWhiteSpace($Configuration) -or $Configuration.StartsWith('-') -or ($Configuration -notin $validConfigurations)) {
    if (-not [string]::IsNullOrWhiteSpace($Configuration) -and $Configuration -ne 'Release') {
        Write-Host "Warning: Unrecognized configuration '$Configuration'. Using Release." -ForegroundColor Yellow
    }
    $Configuration = 'Release'
}

$projectPath = Join-Path $PSScriptRoot "Viking.csproj"
$projectDir = $PSScriptRoot
$publishDir = Join-Path $projectDir "bin\$Configuration\net48"
$releaseDir = Join-Path $projectDir "releases"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Viking Velopack Build and Sign" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Certificate Thumbprint: $CertificateThumbprint" -ForegroundColor Yellow
Write-Host "Timestamp (RFC 3161): $TimestampRfc3161Url" -ForegroundColor Yellow
Write-Host ""

# Check if project file exists
if (-not (Test-Path $projectPath)) {
    Write-Host "Error: Project file not found at $projectPath" -ForegroundColor Red
    exit 1
}

# Step 1: Check for Velopack CLI
Write-Host "Step 1: Checking for Velopack CLI (vpk)..." -ForegroundColor Green
$vpkInstalled = dotnet tool list -g | Select-String "vpk"
if (-not $vpkInstalled) {
    Write-Host "Velopack CLI not found. Installing..." -ForegroundColor Yellow
    dotnet tool install -g vpk
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to install Velopack CLI" -ForegroundColor Red
        exit 1
    }
    Write-Host "Velopack CLI installed successfully!" -ForegroundColor Green
} else {
    Write-Host "Velopack CLI found" -ForegroundColor Gray
}

# Step 2: Find signtool.exe
Write-Host ""
Write-Host "Step 2: Finding signtool.exe..." -ForegroundColor Green

$signtoolPath = $null
$sdkVersions = @("10.0.26100.0", "10.0.22621.0", "10.0.22000.0", "10.0.19041.0", "10.0.18362.0")
$architectures = @("x64", "x86")

foreach ($sdkVer in $sdkVersions) {
    foreach ($arch in $architectures) {
        $path = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\$sdkVer\$arch\signtool.exe"
        if (Test-Path $path) {
            $signtoolPath = $path
            break
        }
    }
    if ($signtoolPath) { break }
}

if (-not $signtoolPath) {
    foreach ($arch in $architectures) {
        $path = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\$arch\signtool.exe"
        if (Test-Path $path) {
            $signtoolPath = $path
            break
        }
    }
}

if (-not $signtoolPath) {
    Write-Host "Error: signtool.exe not found." -ForegroundColor Red
    Write-Host "Please install the Windows SDK." -ForegroundColor Yellow
    exit 1
}

Write-Host "Found signtool: $signtoolPath" -ForegroundColor Gray

# Helper function to display progress in Velopack-style format
function Write-VelopackProgress {
    param(
        [string]$Activity,
        [int]$PercentComplete,
        [TimeSpan]$ElapsedTime
    )
    
    # Calculate number of dashes (40 total for the bar)
    $dashCount = [math]::Round(($PercentComplete / 100) * 40)
    $dashes = "-" * [math]::Min($dashCount, 40)
    $spaces = " " * [math]::Max(0, 40 - $dashCount)
    
    # Format elapsed time as hh:mm:ss (matching Velopack's format)
    $hours = [int]$ElapsedTime.TotalHours
    $minutes = $ElapsedTime.Minutes
    $seconds = $ElapsedTime.Seconds
    $elapsedStr = "{0:D2}:{1:D2}:{2:D2}" -f $hours, $minutes, $seconds
    
    # Write progress line
    $progressLine = "$Activity $dashes$spaces $PercentComplete% $elapsedStr"
    Write-Host $progressLine -NoNewline
    Write-Host "`r" -NoNewline
}

# Helper function to check if a file is signed
function Test-FileSigned {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    # Use signtool verify to check if file is signed
    # Suppress all output (stdout and stderr) and check exit code
    # signtool returns 0 if signed, non-zero if unsigned (which is expected)
    # Temporarily change error action to prevent any error handling issues
    $oldErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = "SilentlyContinue"
        $null = & $signtoolPath verify /pa "$FilePath" *>$null
        $isSigned = $LASTEXITCODE -eq 0
    } catch {
        # If verify fails for any reason, assume unsigned
        $isSigned = $false
    } finally {
        $ErrorActionPreference = $oldErrorAction
    }
    return $isSigned
}

# Helper function to get unsigned files from a list
function Get-UnsignedFiles {
    param([string[]]$FilePaths)
    
    $unsignedFiles = @()
    $totalFiles = $FilePaths.Count
    $currentFile = 0
    
    foreach ($filePath in $FilePaths) {
        $currentFile++
        $percentComplete = [math]::Round(($currentFile / $totalFiles) * 100)
        Write-Progress -Activity "Checking signed files" -Status "File $currentFile of $totalFiles ($percentComplete%)" -PercentComplete $percentComplete
        
        if (-not (Test-FileSigned -FilePath $filePath)) {
            $unsignedFiles += $filePath
        }
    }
    
    Write-Progress -Activity "Checking signed files" -Completed
    return $unsignedFiles
}

# Helper function to find the most recent previous version's full package
function Get-PreviousReleasePackage {
    param(
        [string]$ReleaseDir,
        [string]$CurrentVersion,
        [string]$PackId = "Viking"
    )
    
    if (-not (Test-Path $ReleaseDir)) {
        return $null
    }
    
    # Look for previous full packages (Viking-{version}-full.nupkg)
    $fullPackages = Get-ChildItem -Path $ReleaseDir -Filter "$PackId-*-full.nupkg" -File | 
        Where-Object { 
            # Extract version from filename and compare
            if ($_.Name -match "$PackId-([\d\.]+)-full\.nupkg$") {
                $packageVersion = $matches[1]
                # Compare versions (simple string comparison works for SemVer)
                return $packageVersion -lt $CurrentVersion
            }
            return $false
        } | 
        Sort-Object { 
            # Sort by version (extract and compare)
            if ($_.Name -match "$PackId-([\d\.]+)-full\.nupkg$") {
                return $matches[1]
            }
            return "0.0.0"
        } -Descending
    
    if ($fullPackages.Count -gt 0) {
        return $fullPackages[0]
    }
    
    return $null
}

# Helper function to download previous release using vpk download
function Download-PreviousRelease {
    param(
        [string]$ReleaseDir,
        [string]$ReleaseUrl,
        [string]$PackId = "Viking"
    )
    
    Write-Host "Attempting to download previous release from: $ReleaseUrl" -ForegroundColor Gray
    
    # Ensure release directory exists
    if (-not (Test-Path $ReleaseDir)) {
        New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
    }
    
    # Use vpk download to fetch the latest release
    # This will download the full package needed for delta generation
    vpk download `
        --url $ReleaseUrl `
        --outputDir $ReleaseDir `
        --packId $PackId
    
    if ($LASTEXITCODE -eq 0) {
        # Check if we got a full package
        $downloadedPackage = Get-ChildItem -Path $ReleaseDir -Filter "$PackId-*-full.nupkg" -File | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        
        if ($downloadedPackage) {
            Write-Host "Successfully downloaded previous release: $($downloadedPackage.Name)" -ForegroundColor Green
            return $downloadedPackage
        } else {
            Write-Host "Warning: vpk download completed but no full package found" -ForegroundColor Yellow
            return $null
        }
    } else {
        Write-Host "Warning: Failed to download previous release (exit code $LASTEXITCODE)" -ForegroundColor Yellow
        Write-Host "This is not an error - delta packages will not be generated for this release" -ForegroundColor Gray
        return $null
    }
}

# Helper function to sign files in one signtool invocation via cmd.exe.
# PowerShell's call operator / piping often breaks YubiKey PIN caching (prompt every
# file or batch) or suppresses the PIN dialog. cmd.exe keeps one native process and
# one PIN unlock for the whole file list.
function Sign-FilesBatch {
    param(
        [string[]]$FilePaths,
        [int]$AttemptNumber = 1,
        [int]$MaxAttempts = 10,
        [switch]$CheckUnsignedAfterFailure = $false
    )
    
    if ($FilePaths.Count -eq 0) {
        return @{ Success = $true; UnsignedFiles = @() }
    }
    
    $totalFiles = $FilePaths.Count
    
    if ($AttemptNumber -eq 1) {
        Write-Host "Signing $totalFiles file(s) in one signtool run (PIN once)..." -ForegroundColor Gray
    } else {
        Write-Host "Signing attempt $AttemptNumber of $MaxAttempts ($totalFiles file(s))..." -ForegroundColor Gray
    }
    Write-Host "Please enter your YubiKey PIN when the Windows dialog appears." -ForegroundColor Yellow
    Write-Host "If no dialog appears, check behind other windows, or run PublishVelopack.cmd from Explorer / an external console." -ForegroundColor DarkYellow
    
    $activityName = "Code-sign application"
    $startTime = Get-Date
    Write-Host ""
    Write-VelopackProgress -Activity $activityName -PercentComplete 0 -ElapsedTime (New-TimeSpan)
    Write-Host ""
    
    # Build a cmd.exe command line so signtool is not hosted by PowerShell.
    $quotedFiles = ($FilePaths | ForEach-Object { '"{0}"' -f $_ }) -join ' '
    $cmdLine = '"{0}" sign /sha1 {1} /tr "{2}" /td SHA256 /fd SHA256 {3}' -f `
        $signtoolPath, $CertificateThumbprint, $TimestampRfc3161Url, $quotedFiles
    
    $oldErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        cmd.exe /c $cmdLine
        $signtoolExitCode = $LASTEXITCODE
    }
    catch {
        $signtoolExitCode = $LASTEXITCODE
        if ($signtoolExitCode -eq 0 -or $null -eq $signtoolExitCode) {
            $signtoolExitCode = 1
        }
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
    finally {
        $ErrorActionPreference = $oldErrorAction
    }
    
    $global:LASTEXITCODE = $signtoolExitCode
    $elapsedTime = (Get-Date) - $startTime
    Write-VelopackProgress -Activity $activityName -PercentComplete 100 -ElapsedTime $elapsedTime
    Write-Host ""
    
    if ($signtoolExitCode -eq 0) {
        return @{ Success = $true; UnsignedFiles = @() }
    }
    
    if ($CheckUnsignedAfterFailure) {
        Write-Host "Signing failed. Checking which files are still unsigned..." -ForegroundColor Yellow
        $stillUnsigned = Get-UnsignedFiles -FilePaths $FilePaths
        if ($stillUnsigned.Count -gt 0) {
            Write-Host "Found $($stillUnsigned.Count) unsigned file(s). Failed file(s):" -ForegroundColor Yellow
            foreach ($failedFile in $stillUnsigned) {
                $fileName = [System.IO.Path]::GetFileName($failedFile)
                Write-Host "  - $fileName" -ForegroundColor Yellow
            }
        }
        return @{ Success = $false; UnsignedFiles = $stillUnsigned }
    }
    
    Write-Host "Signing failed. Retrying $($FilePaths.Count) file(s)..." -ForegroundColor Yellow
    return @{ Success = $false; UnsignedFiles = $FilePaths }
}

# Step 3: Get version from project if not provided
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host ""
    Write-Host "Step 3: Reading version from project file..." -ForegroundColor Green
    
    # Use XML parsing to get ApplicationVersion from the first PropertyGroup
    try {
        [xml]$projectXml = Get-Content $projectPath
        $Version = $projectXml.Project.PropertyGroup[0].ApplicationVersion
        
        # If first PropertyGroup doesn't have it, try finding it in any PropertyGroup
        if ([string]::IsNullOrWhiteSpace($Version)) {
            $Version = ($projectXml.Project.PropertyGroup | Where-Object { $_.ApplicationVersion } | Select-Object -First 1).ApplicationVersion
        }
    } catch {
        # Fallback to regex if XML parsing fails
        Write-Host "XML parsing failed, using regex fallback..." -ForegroundColor Yellow
        $projectContent = Get-Content $projectPath -Raw
        if ($projectContent -match '<ApplicationVersion>([^<]+)</ApplicationVersion>') {
            $Version = $matches[1].Trim()
        }
    }
    
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Host "Error: Could not determine version from project file" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Raw version from project: $Version" -ForegroundColor Gray
    
    # Convert 4-part version to 3-part SemVer if needed (e.g., 1.2.0.0 -> 1.2.0)
    if ($Version -match '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
        $Version = "$($matches[1]).$($matches[2]).$($matches[3])"
        Write-Host "Converted to SemVer: $Version" -ForegroundColor Gray
    } elseif ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Write-Host "Error: Version format '$Version' is not SemVer compliant" -ForegroundColor Red
        Write-Host "Expected format: X.Y.Z (e.g., 1.2.0)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host ""
    Write-Host "Step 3: Using provided version: $Version" -ForegroundColor Green
    
    # Ensure provided version is SemVer format
    if ($Version -match '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
        $Version = "$($matches[1]).$($matches[2]).$($matches[3])"
        Write-Host "Converted to SemVer: $Version" -ForegroundColor Gray
    } elseif ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Write-Host "Error: Version format '$Version' is not SemVer compliant" -ForegroundColor Red
        Write-Host "Expected format: X.Y.Z (e.g., 1.2.0)" -ForegroundColor Red
        exit 1
    }
}

# Step 4: Build the project
Write-Host ""
Write-Host "Step 4: Building project..." -ForegroundColor Green
Write-Host "This may take a few minutes..." -ForegroundColor Gray

dotnet build $projectPath -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Build completed successfully!" -ForegroundColor Green

# Verify build output directory exists
if (-not (Test-Path $publishDir)) {
    Write-Host "Error: Build output directory not found at $publishDir" -ForegroundColor Red
    Write-Host "The build output should be in the bin directory." -ForegroundColor Yellow
    exit 1
}

# Verify main executable exists
$mainExeCheck = Join-Path $publishDir "Viking.exe"
if (-not (Test-Path $mainExeCheck)) {
    Write-Host "Error: Viking.exe not found at $mainExeCheck" -ForegroundColor Red
    Write-Host "Build may not have completed successfully." -ForegroundColor Yellow
    exit 1
}

Write-Host "Build output verified: Viking.exe found" -ForegroundColor Gray

# Step 4b: Build VikingAU and copy to distribution
Write-Host ""
Write-Host "Step 4b: Building VikingAU and copying to distribution..." -ForegroundColor Green

$vikingAuProjectPath = Join-Path (Split-Path (Split-Path $projectDir -Parent) -Parent) "VikingAU\VikingAU.csproj"
if (Test-Path $vikingAuProjectPath) {
    dotnet build $vikingAuProjectPath -c $Configuration -f net48
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: VikingAU build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    $vikingAuOutputDir = Join-Path (Split-Path $vikingAuProjectPath -Parent) "bin\$Configuration\net48"
    $vikingAuExe = Join-Path $vikingAuOutputDir "VikingAU.exe"
    $vikingAuConfigExe = Join-Path $vikingAuOutputDir "VikingAU.exe.config"
    $vikingAuConfigDll = Join-Path $vikingAuOutputDir "VikingAU.dll.config"
    if (Test-Path $vikingAuExe) {
        Copy-Item $vikingAuExe $publishDir -Force
        if (Test-Path $vikingAuConfigExe) {
            Copy-Item $vikingAuConfigExe $publishDir -Force
        } elseif (Test-Path $vikingAuConfigDll) {
            Copy-Item $vikingAuConfigDll (Join-Path $publishDir "VikingAU.exe.config") -Force
        }
        Write-Host "VikingAU.exe copied to distribution" -ForegroundColor Green
    } else {
        Write-Host "Warning: VikingAU.exe not found at $vikingAuExe" -ForegroundColor Yellow
    }
} else {
    Write-Host "Warning: VikingAU project not found at $vikingAuProjectPath" -ForegroundColor Yellow
}

# Step 5: Pre-sign all files with HSM certificate
Write-Host ""
Write-Host "Step 5: Pre-signing all files with HSM certificate..." -ForegroundColor Green
Write-Host "This will require PIN entry, but only once for all files." -ForegroundColor Gray

# Find all signable files (.exe and .dll, excluding native DLLs that can't be signed)
$exeFiles = Get-ChildItem -Path $publishDir -Filter "*.exe" -Recurse -File
$dllFiles = Get-ChildItem -Path $publishDir -Filter "*.dll" -Recurse -File

# Filter out native DLLs that cannot be signed
$filesToSign = @()
$filesToSign += $exeFiles
$filesToSign += $dllFiles | Where-Object {
    $_.Name -notmatch "MathNet\.Numerics\.MKL\.dll$" -and
    $_.Name -notmatch "libMathNetNumericsMKL\.dll$" -and
    $_.Name -notmatch "libiomp5md\.dll$"
}

if ($filesToSign.Count -eq 0) {
    Write-Host "Warning: No files found to sign." -ForegroundColor Yellow
} else {
    Write-Host "Found $($filesToSign.Count) files to sign..." -ForegroundColor Gray
    
    # One signtool invocation for all files (via cmd.exe) so PIN is entered once.
    $filePaths = @($filesToSign | ForEach-Object { $_.FullName })
    $unsignedFiles = $filePaths
    $maxAttempts = 10
    $attempt = 1
    $allSigned = $false
    
    while ($attempt -le $maxAttempts -and $unsignedFiles.Count -gt 0) {
        $checkUnsigned = ($attempt -eq 1)
        $result = Sign-FilesBatch -FilePaths $unsignedFiles -AttemptNumber $attempt -MaxAttempts $maxAttempts -CheckUnsignedAfterFailure:$checkUnsigned
        
        if ($result.Success) {
            $allSigned = $true
            break
        }
        
        $unsignedFiles = @($result.UnsignedFiles)
        
        if ($unsignedFiles.Count -gt 0) {
            $attempt++
            if ($attempt -le $maxAttempts) {
                Write-Host ""
                Write-Host "Attempt $($attempt - 1) failed. $($unsignedFiles.Count) file(s) remain unsigned. Retrying..." -ForegroundColor Yellow
                Start-Sleep -Seconds 1
            }
        }
    }
    
    if ($allSigned) {
        Write-Host "Successfully pre-signed $($filesToSign.Count) files!" -ForegroundColor Green
        if ($attempt -gt 1) {
            Write-Host "(Completed after $attempt attempt(s))" -ForegroundColor Gray
        }
    } else {
        Write-Host ""
        Write-Host "Error: Failed to sign $($unsignedFiles.Count) file(s) after $maxAttempts attempts:" -ForegroundColor Red
        foreach ($failedFile in $unsignedFiles) {
            Write-Host "  - $failedFile" -ForegroundColor Red
        }
        Write-Host ""
        Write-Host "Check your YubiKey is connected and functioning properly." -ForegroundColor Yellow
        Write-Host "If no PIN dialog appeared, run PublishVelopack.cmd from Explorer or Windows Terminal (not Cursor)." -ForegroundColor Yellow
        exit 1
    }
}

# Step 6: Package with Velopack (skip signing since files are already signed)
Write-Host ""
Write-Host "Step 6: Packaging with Velopack (files already signed)..." -ForegroundColor Green

# NOTE: Files are already pre-signed in Step 5, so we intentionally do NOT provide
# --signParams here. Velopack will warn "No signing parameters provided, X file(s) will not be signed"
# but this is EXPECTED and not an error - the files are already signed from Step 5.
# We skip signing during packaging to avoid multiple PIN prompts.

# Ensure release directory exists
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Check for previous release package (needed for delta generation)
Write-Host "Checking for previous release package..." -ForegroundColor Gray
$previousPackage = Get-PreviousReleasePackage -ReleaseDir $releaseDir -CurrentVersion $Version -PackId "Viking"

if ($null -eq $previousPackage) {
    Write-Host "Previous release package not found locally." -ForegroundColor Yellow
    Write-Host "Attempting to download from server..." -ForegroundColor Gray
    $previousPackage = Download-PreviousRelease -ReleaseDir $releaseDir -ReleaseUrl $ReleaseUrl -PackId "Viking"
    
    if ($null -eq $previousPackage) {
        Write-Host "No previous release available. Delta packages will not be generated for this release." -ForegroundColor Yellow
        Write-Host "This is normal for the first release or if the server is not accessible." -ForegroundColor Gray
    }
} else {
    Write-Host "Found previous release package: $($previousPackage.Name)" -ForegroundColor Green
    Write-Host "Delta packages will be generated automatically." -ForegroundColor Gray
}

# Preserve existing .nupkg files and RELEASES file for delta generation
# Only clean up files that would conflict with the new build (Setup.exe, versioned directories)
Write-Host "Preserving previous release packages for delta generation..." -ForegroundColor Gray

# Remove only Setup.exe files (we'll create a new one)
$setupFiles = Get-ChildItem -Path $releaseDir -Filter "*Setup.exe" -File
if ($setupFiles.Count -gt 0) {
    Write-Host "Removing old Setup.exe files..." -ForegroundColor Gray
    $setupFiles | Remove-Item -Force
}

# Remove versioned subdirectories if they exist (Velopack may create these)
$versionedDirs = Get-ChildItem -Path $releaseDir -Directory | Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' }
if ($versionedDirs.Count -gt 0) {
    Write-Host "Removing old versioned directories..." -ForegroundColor Gray
    $versionedDirs | Remove-Item -Recurse -Force
}

# Keep all .nupkg files and RELEASES file - these are needed for delta generation
$preservedNupkg = Get-ChildItem -Path $releaseDir -Filter "*.nupkg" -File
$preservedReleases = Get-ChildItem -Path $releaseDir -Filter "RELEASES" -File

if ($preservedNupkg.Count -gt 0) {
    Write-Host "Preserving $($preservedNupkg.Count) existing .nupkg file(s) for delta generation" -ForegroundColor Green
}
if ($preservedReleases.Count -gt 0) {
    Write-Host "Preserving RELEASES file" -ForegroundColor Green
}

Write-Host "Packaging application..." -ForegroundColor Gray
Write-Host "Using version: $Version" -ForegroundColor Cyan
Write-Host "Pack directory: $publishDir" -ForegroundColor Gray
Write-Host "Output directory: $releaseDir" -ForegroundColor Gray

# Validate version format before calling vpk
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: Invalid version format '$Version'. Expected SemVer format (e.g., 1.2.0)" -ForegroundColor Red
    exit 1
}

# Check if main executable exists
$mainExePath = Join-Path $publishDir "Viking.exe"
if (-not (Test-Path $mainExePath)) {
    Write-Host "Error: Main executable not found at $mainExePath" -ForegroundColor Red
    Write-Host "Please ensure the build completed successfully." -ForegroundColor Yellow
    exit 1
}

# Don't provide any signing parameters - files are already pre-signed in Step 5
Write-Host "Skipping signing during packaging (files already pre-signed in Step 5)" -ForegroundColor Gray
Write-Host "Note: Velopack may warn about unsigned files - this is expected and safe to ignore." -ForegroundColor Gray

vpk pack `
    --packId "Viking" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "Viking.exe" `
    --outputDir $releaseDir `
    --verbose

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Velopack packaging failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Write-Host "Check the error messages above for details." -ForegroundColor Yellow
    exit $LASTEXITCODE
}

# Verify output was created
# Velopack creates Setup.exe with pattern: {PackId}-win-Setup.exe
$setupExe = Join-Path $releaseDir "Viking-win-Setup.exe"
$versionedDir = Join-Path $releaseDir $Version
$setupExeInVersionedDir = Join-Path $versionedDir "Viking-win-Setup.exe"

# Also check for generic Setup.exe (in case Velopack changes naming)
$setupExeGeneric = Join-Path $releaseDir "Setup.exe"
$setupExeGenericVersioned = Join-Path $versionedDir "Setup.exe"

if (Test-Path $setupExe) {
    Write-Host "Found Viking-win-Setup.exe in release directory" -ForegroundColor Gray
} elseif (Test-Path $setupExeInVersionedDir) {
    Write-Host "Found Viking-win-Setup.exe in versioned subdirectory: $versionedDir" -ForegroundColor Gray
    # Move to root for easier access
    Move-Item $setupExeInVersionedDir $setupExe -Force
} elseif (Test-Path $setupExeGeneric) {
    Write-Host "Found Setup.exe in release directory (using generic name)" -ForegroundColor Gray
    $setupExe = $setupExeGeneric
} elseif (Test-Path $setupExeGenericVersioned) {
    Write-Host "Found Setup.exe in versioned subdirectory (using generic name)" -ForegroundColor Gray
    Move-Item $setupExeGenericVersioned $setupExeGeneric -Force
    $setupExe = $setupExeGeneric
} else {
    Write-Host "Error: Setup.exe was not created" -ForegroundColor Red
    Write-Host "Searched for:" -ForegroundColor Yellow
    Write-Host "  - $setupExe" -ForegroundColor Yellow
    Write-Host "  - $setupExeInVersionedDir" -ForegroundColor Yellow
    Write-Host "  - $setupExeGeneric" -ForegroundColor Yellow
    Write-Host "  - $setupExeGenericVersioned" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Contents of releases directory:" -ForegroundColor Yellow
    Get-ChildItem $releaseDir -Recurse | Select-Object FullName, Length | Format-Table
    exit 1
}

Write-Host "Packaging completed successfully!" -ForegroundColor Green

# Step 7: Sign Velopack-generated Setup.exe
Write-Host ""
Write-Host "Step 7: Signing Velopack-generated Setup.exe with ECC certificate..." -ForegroundColor Green
Write-Host "Note: .nupkg files are ZIP archives and cannot be signed with signtool.exe" -ForegroundColor Gray

# Sign Setup.exe (the main installer that needs signing)
# Initialize success flag
$script:signingSuccess = $false
if ($setupExe -and (Test-Path $setupExe)) {
    Write-Host "Found Setup.exe: $setupExe" -ForegroundColor Gray
    Write-Host "Please enter your YubiKey PIN when prompted..." -ForegroundColor Yellow
    
    $setupCmd = '"{0}" sign /sha1 {1} /tr "{2}" /td SHA256 /fd SHA256 "{3}"' -f `
        $signtoolPath, $CertificateThumbprint, $TimestampRfc3161Url, $setupExe
    cmd.exe /c $setupCmd
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully signed Setup.exe!" -ForegroundColor Green
        $script:signingSuccess = $true
    } else {
        Write-Host "Error: Failed to sign Setup.exe (exit code $LASTEXITCODE)" -ForegroundColor Red
        Write-Host "Check your YubiKey is connected and PIN is correct." -ForegroundColor Yellow
        exit $LASTEXITCODE
    }
} else {
    Write-Host "Error: Setup.exe not found" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($script:signingSuccess) {
    Write-Host "Build and Sign Complete!" -ForegroundColor Green
} else {
    Write-Host "Build Complete (Signing Failed)" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan

if ($script:signingSuccess) {
    Write-Host "Release directory: $releaseDir" -ForegroundColor Cyan
    Write-Host "Version: $Version" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Review the files in: $releaseDir" -ForegroundColor White
    Write-Host "2. Run DeployVelopack.ps1 to deploy to the server" -ForegroundColor White
    Write-Host ""
}
