# Master Docker Build Script for IdentityServer Solution
# This script builds all Docker images in the solution

param(
    [ValidateSet("all", "standalone", "webapi")]
    [string]$Project = "all",
    [string]$ImageTag = "latest",
    [switch]$ForceRebuild,
    [switch]$Watch,
    [switch]$Push
)

$SolutionRoot = Get-Location
$Projects = @{
    "standalone" = @{
        Name = "IdentityServerStandalone"
        DockerfilePath = "IdentityServer\IdentityServerStandalone\Dockerfile"
        ImageName = "identityserver-standalone"
        Ports = @(6000, 6001)
    }
    "webapi" = @{
        Name = "Viking.Identity.Server.WebApi"
        DockerfilePath = "IdentityServer\Viking.Identity.Server.WebApi\Dockerfile"
        ImageName = "identity-webapi"
        Ports = @(5000, 5001)
    }
}

# Check if Docker is running
try {
    docker version | Out-Null
} catch {
    Write-Error "Docker is not running. Please start Docker Desktop and try again."
    exit 1
}

# Function to build a single Docker image
function Build-DockerImage {
    param(
        [string]$ProjectKey,
        [string]$Tag
    )
    
    $project = $Projects[$ProjectKey]
    $FullImageName = "$($project.ImageName):$Tag"
    
    Write-Host "`n🔨 Building $($project.Name)..." -ForegroundColor Green
    Write-Host "Image: $FullImageName" -ForegroundColor Cyan
    Write-Host "Dockerfile: $($project.DockerfilePath)" -ForegroundColor Cyan
    
    $startTime = Get-Date
    
    try {
        # Force rebuild if requested
        if ($ForceRebuild) {
            Write-Host "🔄 Force rebuilding $($project.Name)..." -ForegroundColor Yellow
            docker rmi $FullImageName -f 2>$null
        }
        
        # Build the Docker image
        docker build -f $project.DockerfilePath -t $FullImageName .
        
        if ($LASTEXITCODE -eq 0) {
            $endTime = Get-Date
            $duration = $endTime - $startTime
            Write-Host "✅ $($project.Name) built successfully in $duration" -ForegroundColor Green
            
            # Show image info
            docker images $project.ImageName
            
            # Push if requested
            if ($Push) {
                Write-Host "📤 Pushing $FullImageName..." -ForegroundColor Yellow
                docker push $FullImageName
            }
            
            return $true
        } else {
            Write-Error "❌ $($project.Name) build failed with exit code $LASTEXITCODE"
            return $false
        }
    } catch {
        Write-Error "❌ $($project.Name) build failed: $($_.Exception.Message)"
        return $false
    }
}

# Function to watch for changes and rebuild
function Watch-AndRebuild {
    param([string]$ProjectKey)
    
    $project = $Projects[$ProjectKey]
    Write-Host "`n🔍 Watching for code changes in $($project.Name)..." -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop watching" -ForegroundColor Gray
    
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $SolutionRoot
    $watcher.Filter = "*.cs"
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents = $true
    
    $action = {
        $path = $Event.SourceEventArgs.FullPath
        $changeType = $Event.SourceEventArgs.ChangeType
        $timestamp = Get-Date -Format "HH:mm:ss"
        
        Write-Host "[$timestamp] $changeType detected: $path" -ForegroundColor Magenta
        
        # Debounce rebuilds - wait 2 seconds after last change
        Start-Sleep -Seconds 2
        
        Write-Host "🔄 Rebuilding $($project.Name) due to code change..." -ForegroundColor Yellow
        Build-DockerImage -ProjectKey $ProjectKey -Tag $ImageTag
    }
    
    Register-ObjectEvent $watcher "Changed" -Action $action | Out-Null
    Register-ObjectEvent $watcher "Created" -Action $action | Out-Null
    Register-ObjectEvent $watcher "Deleted" -Action $action | Out-Null
    
    try {
        while ($true) { Start-Sleep -Seconds 1 }
    } finally {
        $watcher.EnableRaisingEvents = $false
        $watcher.Dispose()
        Get-EventSubscriber | Unregister-Event
    }
}

# Function to build all projects
function Build-AllProjects {
    param([string]$Tag)
    
    Write-Host "🚀 Building all Docker images..." -ForegroundColor Green
    Write-Host "Tag: $Tag" -ForegroundColor Cyan
    
    $successCount = 0
    $totalCount = $Projects.Count
    
    foreach ($projectKey in $Projects.Keys) {
        if (Build-DockerImage -ProjectKey $projectKey -Tag $Tag) {
            $successCount++
        }
    }
    
    Write-Host "`n📊 Build Summary:" -ForegroundColor Cyan
    Write-Host "✅ Successful: $successCount" -ForegroundColor Green
    Write-Host "❌ Failed: $($totalCount - $successCount)" -ForegroundColor Red
    Write-Host "📦 Total: $totalCount" -ForegroundColor Cyan
    
    if ($successCount -eq $totalCount) {
        Write-Host "`n🎉 All projects built successfully!" -ForegroundColor Green
        return $true
    } else {
        Write-Host "`n⚠️  Some projects failed to build" -ForegroundColor Yellow
        return $false
    }
}

# Function to show project status
function Show-ProjectStatus {
    Write-Host "`n📋 Project Status:" -ForegroundColor Cyan
    foreach ($projectKey in $Projects.Keys) {
        $project = $Projects[$projectKey]
        $imageExists = docker images $project.ImageName --format "table {{.Repository}}:{{.Tag}}" 2>$null | Select-String -Pattern $project.ImageName
        
        if ($imageExists) {
            Write-Host "✅ $($project.Name): Image exists" -ForegroundColor Green
        } else {
            Write-Host "❌ $($project.Name): No image found" -ForegroundColor Red
        }
    }
}

# Main execution
Write-Host "🐳 IdentityServer Docker Build Script" -ForegroundColor Blue
Write-Host "=====================================" -ForegroundColor Blue

# Show current status
Show-ProjectStatus

# Build based on project selection
switch ($Project) {
    "all" {
        $success = Build-AllProjects -Tag $ImageTag
        if ($success -and $Watch) {
            Write-Host "`n⚠️  Watch mode not available for 'all' projects. Use individual project names." -ForegroundColor Yellow
        }
    }
    "standalone" {
        if ($Watch) {
            Watch-AndRebuild -ProjectKey "standalone"
        } else {
            Build-DockerImage -ProjectKey "standalone" -Tag $ImageTag
        }
    }
    "webapi" {
        if ($Watch) {
            Watch-AndRebuild -ProjectKey "webapi"
        } else {
            Build-DockerImage -ProjectKey "webapi" -Tag $ImageTag
        }
    }
}

Write-Host "`n🎯 Build script completed!" -ForegroundColor Green
Write-Host "`n💡 Usage Examples:" -ForegroundColor Cyan
Write-Host "  .\build-all-docker.ps1 -Project standalone -Watch" -ForegroundColor Gray
Write-Host "  .\build-all-docker.ps1 -Project webapi -ForceRebuild" -ForegroundColor Gray
Write-Host "  .\build-all-docker.ps1 -Project all -ImageTag v1.0.0" -ForegroundColor Gray




