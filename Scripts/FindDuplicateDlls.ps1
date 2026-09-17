# FindDuplicateDlls.ps1
# Script to identify duplicate DLLs in Viking application and modules

param(
    [string]$AppPath = "Clients\Viking\Viking\bin\Debug\net48",
    [string]$ModulesPath = "Clients\Viking\Viking\bin\Debug\net48\Modules"
)

Write-Host "Analyzing DLLs in Viking application..." -ForegroundColor Green
Write-Host "Application Path: $AppPath" -ForegroundColor Yellow
Write-Host "Modules Path: $ModulesPath" -ForegroundColor Yellow
Write-Host ""

# Get all DLLs from main application directory
$appDlls = Get-ChildItem -Path $AppPath -Filter "*.dll" -Recurse | ForEach-Object {
    [PSCustomObject]@{
        Name = $_.Name
        FullPath = $_.FullName
        Directory = $_.DirectoryName
        Size = $_.Length
        Version = (Get-Item $_.FullName).VersionInfo.FileVersion
        AssemblyName = try { [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName).Name } catch { "Unknown" }
    }
}

# Get all DLLs from modules directory
$moduleDlls = Get-ChildItem -Path $ModulesPath -Filter "*.dll" -Recurse | ForEach-Object {
    [PSCustomObject]@{
        Name = $_.Name
        FullPath = $_.FullName
        Directory = $_.DirectoryName
        Size = $_.Length
        Version = (Get-Item $_.FullName).VersionInfo.FileVersion
        AssemblyName = try { [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName).Name } catch { "Unknown" }
    }
}

# Find duplicates
$duplicates = @()
foreach ($moduleDll in $moduleDlls) {
    $appDll = $appDlls | Where-Object { $_.Name -eq $moduleDll.Name }
    if ($appDll) {
        $duplicates += [PSCustomObject]@{
            Name = $moduleDll.Name
            AppPath = $appDll.FullPath
            ModulePath = $moduleDll.FullPath
            AppVersion = $appDll.Version
            ModuleVersion = $moduleDll.Version
            AppSize = $appDll.Size
            ModuleSize = $moduleDll.Size
            AssemblyName = $moduleDll.AssemblyName
        }
    }
}

# Display results
Write-Host "Found $($duplicates.Count) duplicate DLLs:" -ForegroundColor Cyan
Write-Host ""

if ($duplicates.Count -gt 0) {
    $duplicates | Format-Table -Property Name, AppVersion, ModuleVersion, AppSize, ModuleSize, AssemblyName -AutoSize
    
    Write-Host ""
    Write-Host "Duplicate DLLs by module:" -ForegroundColor Yellow
    $duplicates | Group-Object { Split-Path (Split-Path $_.ModulePath -Parent) -Leaf } | ForEach-Object {
        Write-Host "  $($_.Name): $($_.Count) duplicates" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "Recommended actions:" -ForegroundColor Green
    Write-Host "1. Remove duplicate DLLs from modules using the RemoveDuplicateDlls target in Viking.csproj" -ForegroundColor White
    Write-Host "2. Ensure assembly binding redirects are configured in app.config" -ForegroundColor White
    Write-Host "3. Use Private=false for shared assemblies in module projects" -ForegroundColor White
} else {
    Write-Host "No duplicate DLLs found!" -ForegroundColor Green
}

# Show total sizes
$totalAppSize = ($appDlls | Measure-Object -Property Size -Sum).Sum
$totalModuleSize = ($moduleDlls | Measure-Object -Property Size -Sum).Sum
$duplicateSize = ($duplicates | Measure-Object -Property ModuleSize -Sum).Sum

Write-Host ""
Write-Host "Size Analysis:" -ForegroundColor Yellow
Write-Host "  Application DLLs: $([math]::Round($totalAppSize / 1MB, 2)) MB" -ForegroundColor White
Write-Host "  Module DLLs: $([math]::Round($totalModuleSize / 1MB, 2)) MB" -ForegroundColor White
Write-Host "  Duplicate DLLs: $([math]::Round($duplicateSize / 1MB, 2)) MB" -ForegroundColor White
Write-Host "  Potential savings: $([math]::Round($duplicateSize / 1MB, 2)) MB" -ForegroundColor Green 