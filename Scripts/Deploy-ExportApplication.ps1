<#
.SYNOPSIS
    Deploys the DataExport application to IIS with proper configuration.

.DESCRIPTION
    This script automates the deployment of the DataExport application to IIS. It:
    - Removes any existing Export application from IIS under the specified parent folder
    - Creates a new Export folder structure with required subfolders (logs, Output, Resources)
    - Copies and configures web.config and appsettings.json files
    - Updates configuration URLs based on the parent folder name and URL base
    - Creates an IIS application using the net9 application pool

.PARAMETER RelativePath
    The relative path under the root folder where the Export application will be deployed.
    The volume name is extracted from the last folder name in this path.
    Example: "RC1" or "Sites/RC1"

.PARAMETER RootFolder
    The root folder for IIS deployments. If not specified, attempts to query the IIS default 
    website root or defaults to "C:\inetpub\wwwroot".

.PARAMETER UrlBase
    The base URL for constructing VolumeURL and ODataURL. Defaults to "https://websvc.codepharm.net/".
    VolumeURL will be: {UrlBase}{VolumeName}
    ODataURL will be: {VolumeURL}/OData

    The Export application calls ODataURL server-to-server, so this must be a host the IIS
    machine can actually reach. It previously defaulted to vpn.codepharm.net, which resolves
    but refuses TLS, causing every export request to fail with an unhandled 500.

.PARAMETER SourceFolder
    The folder containing the source web.config and appsettings.json files. 
    Defaults to "C:\Services\Release\Export".

.EXAMPLE
    .\Deploy-ExportApplication.ps1 -RelativePath "RC1"
    
    Deploys Export application to {IISRoot}\RC1\Export with default URL base and IIS root.

.EXAMPLE
    .\Deploy-ExportApplication.ps1 -RelativePath "Sites/RC1" -RootFolder "C:\Services"
    
    Deploys Export application to C:\Services\Sites\RC1\Export with custom root folder.

.EXAMPLE
    .\Deploy-ExportApplication.ps1 -RelativePath "RC1" -UrlBase "https://websvc.codepharm.net/"
    
    Deploys Export application with custom URL base.

.NOTES
    - Requires Administrator privileges
    - Requires WebAdministration module for IIS management
    - Source files must exist at: C:\Services\Release\Export\web.config and C:\Services\Release\Export\appsettings.json
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$RelativePath,
    
    [Parameter(Mandatory=$false)]
    [string]$RootFolder = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$UrlBase = "https://websvc.codepharm.net/",
    
    [Parameter(Mandatory=$false)]
    [string]$SourceFolder = "C:\Services\Release\Export"
)

# Ensure the script is running with administrative privileges
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

# Import WebAdministration module for IIS management
# Use -SkipEditionCheck to avoid WinPSCompatSession issues in PowerShell 7
$ImportSuccess = $false
$IISDriveAvailable = $false

try {
    Import-Module WebAdministration -SkipEditionCheck -ErrorAction Stop
    $ImportSuccess = $true
    Write-Host "WebAdministration module loaded successfully with -SkipEditionCheck"
} catch {
    # Fallback for older PowerShell versions that don't support -SkipEditionCheck
    try {
        Import-Module WebAdministration -ErrorAction Stop
        $ImportSuccess = $true
        Write-Host "WebAdministration module loaded successfully (fallback mode)"
    } catch {
        Write-Error "Failed to load WebAdministration module: $_"
        exit 1
    }
}

# Check if IIS drive is available
if (Test-Path "IIS:\") {
    $IISDriveAvailable = $true
    Write-Host "IIS drive is available"
} else {
    Write-Warning "IIS drive not available (likely due to WinPSCompatSession). Will use alternative methods."
    $IISDriveAvailable = $false
}

# Determine the root folder if not specified
if ([string]::IsNullOrEmpty($RootFolder)) {
    # Try to query the IIS default website root
    try {
        $DefaultSite = Get-Website | Where-Object { $_.Name -eq "Default Web Site" } | Select-Object -First 1
        if ($DefaultSite) {
            $RootFolder = $DefaultSite.PhysicalPath
            Write-Host "Using IIS Default Web Site root: $RootFolder"
        } else {
            # Fall back to standard IIS root
            $RootFolder = "C:\inetpub\wwwroot"
            Write-Host "Using default IIS root: $RootFolder"
        }
    } catch {
        # Fall back to standard IIS root
        $RootFolder = "C:\inetpub\wwwroot"
        Write-Host "Using default IIS root: $RootFolder"
    }
}

# Normalize the relative path (remove leading/trailing slashes and backslashes)
$RelativePath = $RelativePath.Trim('\', '/')

# Construct the parent folder from root and relative path
$ParentFolder = Join-Path $RootFolder $RelativePath

# Verify that the parent folder exists (prevents typos)
if (-not (Test-Path $ParentFolder)) {
    Write-Error "Parent folder does not exist: $ParentFolder"
    Write-Error "Please verify the RelativePath is correct: '$RelativePath'"
    Write-Error "This check prevents accidental creation of folders with typos."
    Write-Host ""
    Write-Host "To create the parent folder manually, run:"
    Write-Host "  New-Item -ItemType Directory -Path '$ParentFolder' -Force"
    exit 1
}

Write-Host "Parent folder verified: $ParentFolder"

# Extract volume name from the last folder name in the relative path
$VolumeName = Split-Path $RelativePath -Leaf

# Ensure URL base ends with a slash
if (-not $UrlBase.EndsWith('/')) {
    $UrlBase = "$UrlBase/"
}

# Construct URLs for appsettings.json
$VolumeURL = "$UrlBase$VolumeName"
$ODataURL = "$VolumeURL/OData"

Write-Host "==================================================="
Write-Host "Deploying Export Application"
Write-Host "==================================================="
Write-Host "Root Folder: $RootFolder"
Write-Host "Relative Path: $RelativePath"
Write-Host "Parent Folder: $ParentFolder"
Write-Host "Volume Name: $VolumeName"
Write-Host "Application Pool: net9-$VolumeName"
Write-Host "URL Base: $UrlBase"
Write-Host "Volume URL: $VolumeURL"
Write-Host "OData URL: $ODataURL"
Write-Host "==================================================="

# Define paths
$ExportFolder = Join-Path $ParentFolder "Export"
$SourceWebConfig = Join-Path $SourceFolder "web.config"
$SourceAppSettings = Join-Path $SourceFolder "appsettings.json"

# Check if source files exist
if (-not (Test-Path $SourceWebConfig)) {
    Write-Error "Source web.config not found at: $SourceWebConfig"
    exit 1
}

if (-not (Test-Path $SourceAppSettings)) {
    Write-Error "Source appsettings.json not found at: $SourceAppSettings"
    exit 1
}

# Define the IIS site path - we need to find the site that contains this parent folder
$IISPath = $null
$SiteName = $null

# Get all IIS sites and find which one contains our parent folder
$Sites = Get-Website
foreach ($Site in $Sites) {
    $PhysicalPath = $Site.PhysicalPath.TrimEnd('\')
    if ($ParentFolder.StartsWith($PhysicalPath, [StringComparison]::OrdinalIgnoreCase)) {
        $SiteName = $Site.Name
        # Calculate the relative path from the site root to the Export application
        $RelativePath = $ParentFolder.Substring($PhysicalPath.Length).TrimStart('\').Replace('\', '/')
        $IISPath = "IIS:\Sites\$SiteName\$RelativePath\Export"
        break
    }
}

if (-not $IISPath) {
    Write-Error "Could not find IIS site containing parent folder: $ParentFolder"
    Write-Host "Available sites and their physical paths:"
    foreach ($Site in $Sites) {
        Write-Host "  $($Site.Name): $($Site.PhysicalPath)"
    }
    exit 1
}

Write-Host "IIS Site: $SiteName"
Write-Host "IIS Application Path: $IISPath"

# FIRST: Remove existing IIS application if it exists (before deleting folder)
$ExistingApp = Get-WebApplication -Name "Export" -Site $SiteName -ErrorAction SilentlyContinue
if ($null -ne $ExistingApp) {
    Write-Host "Removing existing IIS application (was using pool: $($ExistingApp.applicationPool))..."
    Remove-WebApplication -Name "Export" -Site $SiteName -ErrorAction SilentlyContinue
    Write-Host "Existing IIS application removed."
    
    # Wait longer for IIS to fully release the folder
    Write-Host "Waiting for IIS to release folder locks..."
    Start-Sleep -Seconds 2
    
    # Try to stop the application pool to force release
    try {
        $AppPoolName = "net9-$VolumeName"
        Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        Write-Host "Stopped application pool to release locks"
        Start-Sleep -Seconds 1
    } catch {
        # Ignore errors - pool might not exist or already stopped
    }
}

# SECOND: Delete existing Export folder if it exists
if (Test-Path $ExportFolder) {
    Write-Host "Removing existing Export folder at: $ExportFolder"
    
    # Try multiple times with increasing delays
    $MaxAttempts = 3
    $Attempt = 1
    do {
        try {
            Remove-Item -Path $ExportFolder -Recurse -Force -ErrorAction Stop
            Write-Host "Existing Export folder removed."
            break
        } catch {
            if ($Attempt -lt $MaxAttempts) {
                Write-Host "Folder still locked, waiting longer... (attempt $Attempt/$MaxAttempts)"
                Start-Sleep -Seconds 2
                $Attempt++
            } else {
                Write-Warning "Could not remove folder after $MaxAttempts attempts: $_"
                Write-Host "Trying to force remove individual files..."
                try {
                    Get-ChildItem -Path $ExportFolder -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
                    Remove-Item -Path $ExportFolder -Force -ErrorAction SilentlyContinue
                    Write-Host "Folder force-removed."
                } catch {
                    Write-Error "Failed to remove folder completely. Please manually delete: $ExportFolder"
                    exit 1
                }
            }
        }
    } while ($Attempt -le $MaxAttempts)
}

# Create new Export folder
Write-Host "Creating Export folder at: $ExportFolder"
New-Item -ItemType Directory -Path $ExportFolder -Force | Out-Null

# Create required subfolders
$SubFolders = @("logs", "Output", "Resources")
foreach ($SubFolder in $SubFolders) {
    $SubFolderPath = Join-Path $ExportFolder $SubFolder
    if (-not (Test-Path $SubFolderPath)) {
        Write-Host "Creating subfolder: $SubFolder"
        New-Item -ItemType Directory -Path $SubFolderPath -Force | Out-Null
    }
}

# Copy web.config
Write-Host "Copying web.config..."
$DestWebConfig = Join-Path $ExportFolder "web.config"
Copy-Item -Path $SourceWebConfig -Destination $DestWebConfig -Force

# Update web.config with correct DLL path (pointing to binaries in SourceFolder)
$WebConfigContent = Get-Content $DestWebConfig -Raw
$DllPath = Join-Path $SourceFolder "DataExport.dll"
$WebConfigContent = $WebConfigContent -replace 'arguments="[^"]*"', "arguments=`"$DllPath`""
Set-Content -Path $DestWebConfig -Value $WebConfigContent -Force
Write-Host "web.config updated with DLL path: $DllPath"

# Copy the content root payload. The binaries stay in SourceFolder and are launched through
# web.config, but ContentRootPath is this per-volume folder, so anything resolved relative to
# the content root has to live here. Omitting these leaves Resources empty, which makes every
# Morphology export throw while Network and Motif appear to work.
$ContentPayload = @("Content", "Resources")
foreach ($PayloadFolder in $ContentPayload) {
    $SourcePayload = Join-Path $SourceFolder $PayloadFolder
    if (Test-Path $SourcePayload) {
        Write-Host "Copying $PayloadFolder..."
        Copy-Item -Path $SourcePayload -Destination $ExportFolder -Recurse -Force
    } else {
        Write-Warning "$PayloadFolder not found in source folder: $SourcePayload"
        if ($PayloadFolder -eq "Resources") {
            Write-Warning "Morphology exports will fail without Resources\ColorMapping."
        }
    }
}

# Copy and update appsettings.json
Write-Host "Copying appsettings.json..."
$DestAppSettings = Join-Path $ExportFolder "appsettings.json"
Copy-Item -Path $SourceAppSettings -Destination $DestAppSettings -Force

# Update appsettings.json with correct URLs
$AppSettingsContent = Get-Content $DestAppSettings -Raw | ConvertFrom-Json
$AppSettingsContent.AppSettings.VolumeURL = $VolumeURL
$AppSettingsContent.AppSettings.ODataURL = $ODataURL
$AppSettingsContent | ConvertTo-Json -Depth 10 | Set-Content -Path $DestAppSettings -Force
Write-Host "appsettings.json updated:"
Write-Host "  VolumeURL: $VolumeURL"
Write-Host "  ODataURL: $ODataURL"

# Create/ensure the application pool exists (unique per volume)
$AppPoolName = "net9-$VolumeName"

# Create/ensure the application pool exists (unique per volume)
Write-Host "Creating application pool: $AppPoolName"
try {
    New-WebAppPool -Name $AppPoolName -Force -ErrorAction Stop | Out-Null
    Write-Host "Application pool created successfully"
    $PoolJustCreated = $true
} catch {
    # If creation fails, it might already exist - this is fine
    if ($_.Exception.Message -like "*duplicate*" -or $_.Exception.Message -like "*already exists*") {
        Write-Host "Application pool '$AppPoolName' already exists."
        $PoolJustCreated = $false
    } else {
        Write-Error "Failed to create application pool: $_"
        exit 1
    }
}

# Configure the application pool for .NET 9 (no managed runtime version)
Write-Host "Configuring application pool for .NET 9 (No Managed Code)..."
try {
    # Use Set-WebConfigurationProperty which works in compatibility mode
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter "system.applicationHost/applicationPools/add[@name='$AppPoolName']" `
        -Name "managedRuntimeVersion" `
        -Value "" `
        -ErrorAction Stop
    Write-Host "Application pool configured for .NET 9 (No Managed Code)"
} catch {
    Write-Warning "Could not configure application pool using Set-WebConfigurationProperty: $_"
    
    # Fallback to IIS drive method if available
    if ($IISDriveAvailable) {
        try {
            Write-Host "Attempting configuration via IIS drive..."
            $AppPool = Get-Item "IIS:\AppPools\$AppPoolName" -ErrorAction Stop
            $AppPool.managedRuntimeVersion = ""
            $AppPool | Set-Item
            Write-Host "Application pool configured for .NET 9 (No Managed Code)"
        } catch {
            Write-Warning "Could not configure application pool: $_"
            Write-Host "You MUST manually set the application pool '$AppPoolName' to 'No Managed Code' in IIS Manager"
        }
    } else {
        Write-Warning "You MUST manually set the application pool '$AppPoolName' to 'No Managed Code' in IIS Manager"
    }
}

# Create the IIS application
Write-Host "Creating IIS application..."
$ApplicationName = "$VolumeName/Export"

# Create the new application (old one was already removed earlier)
Write-Host "Creating new application with pool: $AppPoolName"
$NewApp = New-WebApplication -Name "Export" -Site $SiteName -PhysicalPath $ExportFolder -ApplicationPool $AppPoolName -Force

# Use appcmd.exe to set the application pool (more reliable than PowerShell cmdlets in compatibility mode)
Write-Host "Setting application pool using appcmd.exe..."
$AppcmdPath = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
if (Test-Path $AppcmdPath) {
    try {
        # Set the application pool using appcmd
        $AppVirtualPath = "/$VolumeName/Export"
        & $AppcmdPath set app "$SiteName$AppVirtualPath" /applicationPool:$AppPoolName | Out-Null
        Write-Host "Application pool set to: $AppPoolName using appcmd"
        
        # Verify using appcmd (more accurate than Get-WebApplication in compatibility mode)
        $AppConfigXml = & $AppcmdPath list app "$SiteName$AppVirtualPath" /text:applicationPool
        Write-Host "Verified application pool via appcmd: $AppConfigXml"
        
        if ($AppConfigXml -ne $AppPoolName) {
            Write-Warning "Application pool verification shows: $AppConfigXml (expected: $AppPoolName)"
        } else {
            Write-Host "Application pool correctly assigned!"
        }
    } catch {
        Write-Warning "Could not set application pool using appcmd: $_"
    }
} else {
    Write-Warning "appcmd.exe not found at: $AppcmdPath"
    
    # Fallback to PowerShell method
    $VerifyApp = Get-WebApplication -Name "Export" -Site $SiteName -ErrorAction SilentlyContinue
    if ($null -ne $VerifyApp) {
        $ActualAppPool = $VerifyApp.applicationPool
        Write-Host "IIS application created at: /$ApplicationName"
        Write-Host "Actual application pool (via Get-WebApplication): $ActualAppPool"
        
        if ($ActualAppPool -ne $AppPoolName) {
            Write-Warning "Application is using incorrect pool: $ActualAppPool (expected: $AppPoolName)"
        }
    }
}

# Restart the application pool to ensure clean state
Write-Host "Restarting application pool to ensure clean state..."
try {
    Restart-WebAppPool -Name $AppPoolName -ErrorAction Stop
    Write-Host "Application pool restarted successfully"
} catch {
    Write-Warning "Could not restart application pool: $_"
    Write-Host "You may need to manually restart the application pool in IIS Manager"
}

Write-Host "==================================================="
Write-Host "Export application deployment completed successfully!"
Write-Host "==================================================="
Write-Host "Physical Path: $ExportFolder"
Write-Host "Application URL: $VolumeURL/Export"
Write-Host "Application Pool: $AppPoolName"
Write-Host "==================================================="

