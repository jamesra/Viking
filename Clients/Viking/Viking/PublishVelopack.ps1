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
    4. Packages the application with Velopack
    5. Signs Setup.exe and all .nupkg files with ECC HSM certificate

.PARAMETER Configuration
    The build configuration to use (default: Release)

.PARAMETER CertificateThumbprint
    The thumbprint of the certificate to use for signing (default: 41403cbc59209b576efe575775abe8f4a42da6ba)

.PARAMETER TimestampUrl
    The timestamp server URL (default: http://timestamp.digicert.com)

.PARAMETER Version
    The version number to use for the package (default: reads from Viking.csproj)

.EXAMPLE
    .\PublishVelopack.ps1 -Configuration Release
.EXAMPLE
    .\PublishVelopack.ps1 -Configuration Release -Version "1.2.1.0"
#>

param(
    [string]$Configuration = "Release",
    [string]$CertificateThumbprint = "41403cbc59209b576efe575775abe8f4a42da6ba",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "Viking.csproj"
$projectDir = $PSScriptRoot
$publishDir = Join-Path $projectDir "bin\$Configuration\net48"
$releaseDir = Join-Path $projectDir "releases"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Viking Velopack Build and Sign" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Certificate Thumbprint: $CertificateThumbprint" -ForegroundColor Yellow
Write-Host "Timestamp URL: $TimestampUrl" -ForegroundColor Yellow
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
    
    # Build file list for signtool
    $filePaths = $filesToSign | ForEach-Object { $_.FullName }
    
    # Sign all files in a single signtool invocation to minimize PIN prompts
    Write-Host "Signing all files in one batch (you will be prompted for PIN once)..." -ForegroundColor Gray
    Write-Host "Please enter your YubiKey PIN when prompted..." -ForegroundColor Yellow
    
    & $signtoolPath sign `
        /sha1 $CertificateThumbprint `
        /t $TimestampUrl `
        /fd SHA256 `
        $filePaths
    
    $exitCode = $LASTEXITCODE
    
    # If signing failed, retry failed files individually
    if ($exitCode -ne 0) {
        Write-Host "" -ForegroundColor Yellow
        Write-Host "Initial batch signing failed. Attempting to retry failed files individually..." -ForegroundColor Yellow
        Write-Host "This may require additional PIN entries." -ForegroundColor Yellow
        Write-Host "" -ForegroundColor Yellow
        
        $retrySuccess = 0
        $retryFailed = 0
        $failedFiles = @()
        
        # Retry each file individually with up to 3 attempts
        foreach ($filePath in $filePaths) {
            $retryAttempt = 0
            $fileSigned = $false
            
            while ($retryAttempt -lt 3 -and -not $fileSigned) {
                $retryAttempt++
                
                if ($retryAttempt -gt 1) {
                    Write-Host "Retry attempt $retryAttempt of 3 for: $([System.IO.Path]::GetFileName($filePath))" -ForegroundColor Gray
                    Start-Sleep -Milliseconds 500  # Brief pause between retries
                }
                
                & $signtoolPath sign `
                    /sha1 $CertificateThumbprint `
                    /t $TimestampUrl `
                    /fd SHA256 `
                    "$filePath" `
                    | Out-Null
                
                if ($LASTEXITCODE -eq 0) {
                    $retrySuccess++
                    $fileSigned = $true
                } else {
                    if ($retryAttempt -eq 3) {
                        $retryFailed++
                        $failedFiles += $filePath
                        Write-Host "  Failed after 3 attempts: $([System.IO.Path]::GetFileName($filePath))" -ForegroundColor Red
                    }
                }
            }
        }
        
        if ($retryFailed -gt 0) {
            Write-Host "" -ForegroundColor Red
            Write-Host "Error: Failed to sign $retryFailed file(s) after retries:" -ForegroundColor Red
            foreach ($failedFile in $failedFiles) {
                Write-Host "  - $failedFile" -ForegroundColor Red
            }
            Write-Host "" -ForegroundColor Red
            Write-Host "Check your YubiKey is connected and functioning properly." -ForegroundColor Yellow
            exit 1
        } else {
            Write-Host "" -ForegroundColor Green
            Write-Host "Successfully signed all files after retries ($retrySuccess files retried)!" -ForegroundColor Green
        }
    } else {
        Write-Host "Successfully pre-signed $($filesToSign.Count) files!" -ForegroundColor Green
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
if (Test-Path $releaseDir) {
    Write-Host "Cleaning existing release directory..." -ForegroundColor Gray
    Remove-Item -Path $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

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
    
    & $signtoolPath sign `
        /sha1 $CertificateThumbprint `
        /t $TimestampUrl `
        /fd SHA256 `
        "$setupExe"
    
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
