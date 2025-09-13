# Viking.Identity.Server.WebApi Docker Build Script
# This script automatically rebuilds the Docker image when code changes

param(
    [string]$ImageTag = "latest",
    [switch]$ForceRebuild,
    [switch]$Watch
)

$ProjectName = "Viking.Identity.Server.WebApi"
$DockerfilePath = "IdentityServer\Viking.Identity.Server.WebApi\Dockerfile"
$ImageName = "identity-webapi"
$FullImageName = "$ImageName`:$ImageTag"

Write-Host "Building $ProjectName Docker Image..." -ForegroundColor Green
Write-Host "Image: $FullImageName" -ForegroundColor Cyan
Write-Host "Dockerfile: $DockerfilePath" -ForegroundColor Cyan

# Check if Docker is running
try {
    docker version | Out-Null
} catch {
    Write-Error "Docker is not running. Please start Docker Desktop and try again."
    exit 1
}

# Function to build Docker image
function Build-DockerImage {
    param([string]$Tag)
    
    $startTime = Get-Date
    Write-Host "Starting Docker build at $startTime..." -ForegroundColor Yellow
    
    try {
        # Build the Docker image
        docker build -f $DockerfilePath -t "$ImageName`:$Tag" .
        
        if ($LASTEXITCODE -eq 0) {
            $endTime = Get-Date
            $duration = $endTime - $startTime
            Write-Host "✅ Docker build completed successfully in $duration" -ForegroundColor Green
            
            # Show image info
            Write-Host "`nImage Details:" -ForegroundColor Cyan
            docker images $ImageName
            
            # Show build context
            Write-Host "`nBuild Context:" -ForegroundColor Cyan
            Get-ChildItem -Name | Where-Object { $_ -match "^(Identity\.|IdentityServer)" }
            
        } else {
            Write-Error "❌ Docker build failed with exit code $LASTEXITCODE"
            exit $LASTEXITCODE
        }
    } catch {
        Write-Error "❌ Docker build failed: $($_.Exception.Message)"
        exit 1
    }
}

# Function to watch for changes and rebuild
function Watch-AndRebuild {
    Write-Host "`n🔍 Watching for code changes..." -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop watching" -ForegroundColor Gray
    
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = "."
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
        
        Write-Host "🔄 Rebuilding Docker image due to code change..." -ForegroundColor Yellow
        Build-DockerImage -Tag $ImageTag
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

# Main execution
if ($ForceRebuild) {
    Write-Host "🔄 Force rebuilding Docker image..." -ForegroundColor Yellow
    docker rmi $FullImageName -f 2>$null
}

# Initial build
Build-DockerImage -Tag $ImageTag

# Watch mode
if ($Watch) {
    Watch-AndRebuild
} else {
    Write-Host "`n💡 Tip: Use -Watch parameter to automatically rebuild on code changes" -ForegroundColor Cyan
    Write-Host "Example: .\build-docker.ps1 -Watch" -ForegroundColor Gray
}

Write-Host "`n🎉 Build script completed!" -ForegroundColor Green




