# PowerShell script to convert traditional .csproj files to SDK-style projects
# This script identifies traditional format projects and converts them to modern SDK-style format

param(
    [switch]$WhatIf = $false,
    [switch]$Force = $false
)

# Function to determine if a project is traditional format
function IsTraditionalProject {
    param([string]$ProjectPath)
    
    if (-not (Test-Path $ProjectPath)) {
        return $false
    }
    
    $content = Get-Content $ProjectPath -Raw
    return $content -match 'ToolsVersion=' -and $content -notmatch 'Sdk='
}

# Function to extract project information from traditional project file
function GetProjectInfo {
    param([string]$ProjectPath)
    
    $content = Get-Content $ProjectPath -Raw
    [xml]$xml = $content
    
    $projectInfo = @{
        Path = $ProjectPath
        AssemblyName = ""
        RootNamespace = ""
        TargetFramework = ""
        OutputType = ""
        PackageReferences = @()
        ProjectReferences = @()
        References = @()
        CompileItems = @()
        ContentItems = @()
        EmbeddedResources = @()
        IsWpf = $false
        IsWinForms = $false
        IsWebApp = $false
        ProjectTypeGuids = ""
    }
    
    # Extract basic properties
    $propertyGroups = $xml.Project.PropertyGroup
    foreach ($group in $propertyGroups) {
        if ($group.AssemblyName) { $projectInfo.AssemblyName = $group.AssemblyName }
        if ($group.RootNamespace) { $projectInfo.RootNamespace = $group.RootNamespace }
        if ($group.TargetFrameworkVersion) { 
            $version = $group.TargetFrameworkVersion
            # Convert .NET Framework versions to modern format
            switch ($version) {
                "v4.8" { $projectInfo.TargetFramework = "net48" }
                "v4.7.2" { $projectInfo.TargetFramework = "net472" }
                "v4.7.1" { $projectInfo.TargetFramework = "net471" }
                "v4.7" { $projectInfo.TargetFramework = "net47" }
                "v4.6.2" { $projectInfo.TargetFramework = "net462" }
                "v4.6.1" { $projectInfo.TargetFramework = "net461" }
                "v4.6" { $projectInfo.TargetFramework = "net46" }
                "v4.5.2" { $projectInfo.TargetFramework = "net452" }
                "v4.5.1" { $projectInfo.TargetFramework = "net451" }
                "v4.5" { $projectInfo.TargetFramework = "net45" }
                default { $projectInfo.TargetFramework = "net48" }
            }
        }
        if ($group.OutputType) { $projectInfo.OutputType = $group.OutputType }
        if ($group.ProjectTypeGuids) { $projectInfo.ProjectTypeGuids = $group.ProjectTypeGuids }
    }
    
    # Check for WPF project
    if ($projectInfo.ProjectTypeGuids -match "60dc8134-eba5-43b8-bcc9-bb4bc16c2548") {
        $projectInfo.IsWpf = $true
    }
    
    # Check for Web application
    if ($projectInfo.ProjectTypeGuids -match "349c5851-65df-11da-9384-00065b846f21") {
        $projectInfo.IsWebApp = $true
    }
    
    # Extract package references
    $itemGroups = $xml.Project.ItemGroup
    foreach ($group in $itemGroups) {
        if ($group.PackageReference) {
            foreach ($pkg in $group.PackageReference) {
                $projectInfo.PackageReferences += @{
                    Include = $pkg.Include
                    Version = $pkg.Version
                }
            }
        }
        
        if ($group.ProjectReference) {
            foreach ($proj in $group.ProjectReference) {
                $projectInfo.ProjectReferences += $proj.Include
            }
        }
        
        if ($group.Reference) {
            foreach ($ref in $group.Reference) {
                if ($ref.Include -notmatch "^(System|Microsoft|mscorlib)" -and 
                    $ref.Include -notmatch "^(WindowsBase|PresentationCore|PresentationFramework)") {
                    $projectInfo.References += $ref.Include
                }
            }
        }
    }
    
    return $projectInfo
}

# Function to generate SDK-style project content
function GenerateSdkStyleProject {
    param($ProjectInfo)
    
    $sdk = "Microsoft.NET.Sdk"
    if ($ProjectInfo.IsWpf) {
        $sdk = "Microsoft.NET.Sdk"
    } elseif ($ProjectInfo.IsWebApp) {
        $sdk = "Microsoft.NET.Sdk.Web"
    }
    
    $content = @"
<Project Sdk="$sdk">

  <PropertyGroup>
"@
    
    if ($ProjectInfo.OutputType -eq "Exe") {
        $content += "`n    <OutputType>Exe</OutputType>"
    } elseif ($ProjectInfo.OutputType -eq "Library") {
        # OutputType Library is default for SDK-style, so we can omit it
    } elseif ($ProjectInfo.OutputType -eq "WinExe") {
        $content += "`n    <OutputType>WinExe</OutputType>"
    }
    
    $content += "`n    <TargetFramework>$($ProjectInfo.TargetFramework)</TargetFramework>"
    
    if ($ProjectInfo.AssemblyName -and $ProjectInfo.AssemblyName -ne (Split-Path $ProjectInfo.Path -LeafBase)) {
        $content += "`n    <AssemblyName>$($ProjectInfo.AssemblyName)</AssemblyName>"
    }
    
    if ($ProjectInfo.RootNamespace -and $ProjectInfo.RootNamespace -ne (Split-Path $ProjectInfo.Path -LeafBase)) {
        $content += "`n    <RootNamespace>$($ProjectInfo.RootNamespace)</RootNamespace>"
    }
    
    if ($ProjectInfo.IsWpf) {
        $content += "`n    <UseWPF>true</UseWPF>"
    }
    
    if ($ProjectInfo.IsWinForms) {
        $content += "`n    <UseWindowsForms>true</UseWindowsForms>"
    }
    
    $content += "`n  </PropertyGroup>"
    
    # Add package references if any
    if ($ProjectInfo.PackageReferences.Count -gt 0) {
        $content += "`n`n  <ItemGroup>"
        foreach ($pkg in $ProjectInfo.PackageReferences) {
            $content += "`n    <PackageReference Include=`"$($pkg.Include)`" Version=`"$($pkg.Version)`" />"
        }
        $content += "`n  </ItemGroup>"
    }
    
    # Add project references if any
    if ($ProjectInfo.ProjectReferences.Count -gt 0) {
        $content += "`n`n  <ItemGroup>"
        foreach ($proj in $ProjectInfo.ProjectReferences) {
            $content += "`n    <ProjectReference Include=`"$proj`" />"
        }
        $content += "`n  </ItemGroup>"
    }
    
    $content += "`n`n</Project>`n"
    
    return $content
}

# Main script execution
Write-Host "Scanning for traditional format .csproj files..." -ForegroundColor Green

# Find all .csproj files
$allProjects = Get-ChildItem -Path "." -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notmatch "\\bin\\" -and 
    $_.FullName -notmatch "\\obj\\" -and
    $_.FullName -notmatch "\\MigrationBackup\\" 
}

$traditionalProjects = @()

foreach ($project in $allProjects) {
    if (IsTraditionalProject $project.FullName) {
        $traditionalProjects += $project.FullName
    }
}

Write-Host "Found $($traditionalProjects.Count) traditional format projects:" -ForegroundColor Yellow

foreach ($project in $traditionalProjects) {
    $relativePath = $project.Replace($PWD.Path + "\", "")
    Write-Host "  - $relativePath" -ForegroundColor Gray
}

if ($traditionalProjects.Count -eq 0) {
    Write-Host "No traditional format projects found to convert." -ForegroundColor Green
    exit 0
}

if ($WhatIf) {
    Write-Host "`nWhatIf mode: No files will be modified." -ForegroundColor Cyan
    exit 0
}

$response = "y"
if (-not $Force) {
    $response = Read-Host "`nDo you want to convert these projects to SDK-style? (y/n)"
}

if ($response -eq "y" -or $response -eq "Y" -or $response -eq "yes") {
    Write-Host "`nConverting projects..." -ForegroundColor Green
    
    $converted = 0
    $failed = 0
    
    foreach ($projectPath in $traditionalProjects) {
        try {
            Write-Host "Converting: $(Split-Path $projectPath -Leaf)" -ForegroundColor White
            
            # Backup original file
            $backupPath = $projectPath + ".backup"
            Copy-Item $projectPath $backupPath
            
            # Extract project information
            $projectInfo = GetProjectInfo $projectPath
            
            # Generate new SDK-style content
            $newContent = GenerateSdkStyleProject $projectInfo
            
            # Write the new project file
            Set-Content -Path $projectPath -Value $newContent -Encoding UTF8
            
            Write-Host "  ✓ Converted successfully (backup saved as .backup)" -ForegroundColor Green
            $converted++
        }
        catch {
            Write-Host "  ✗ Failed to convert: $($_.Exception.Message)" -ForegroundColor Red
            $failed++
            
            # Restore backup if conversion failed
            if (Test-Path "$projectPath.backup") {
                Copy-Item "$projectPath.backup" $projectPath
                Remove-Item "$projectPath.backup"
            }
        }
    }
    
    Write-Host "`nConversion completed:" -ForegroundColor Green
    Write-Host "  Successfully converted: $converted projects" -ForegroundColor Green
    if ($failed -gt 0) {
        Write-Host "  Failed to convert: $failed projects" -ForegroundColor Red
    }
    
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Test build the solution to ensure all projects compile correctly" -ForegroundColor Gray
    Write-Host "2. Remove .backup files if everything works correctly" -ForegroundColor Gray
    Write-Host "3. Commit the changes to version control" -ForegroundColor Gray
} else {
    Write-Host "Conversion cancelled by user." -ForegroundColor Yellow
}
