# Quick Start: Viking Volume Annotation Services Docker

This guide will help you quickly get all three Viking services running in a single Docker container.

## What You're Getting

A single Docker image containing three volume annotation services:
- **AnnotationService** at `/annotation` (WCF Service - volume annotation CRUD)
- **ConnectomeODataV4** at `/odata` (OData v4 API - connectome data queries)
- **DataExport** at `/dataexport` (ASP.NET Core API - morphology/network export)

## Prerequisites

1. **Docker Desktop** with Windows containers enabled
2. **SQL Server** database (local or remote)
3. At least **8GB RAM** for Docker

## Step 1: Switch to Windows Containers

Right-click Docker Desktop icon → "Switch to Windows containers..."

## Step 2: Validate Setup (Recommended)

Run the validation script to check prerequisites:

```powershell
.\Scripts\ValidateDockerSetup.ps1
```

This will verify:
- Docker is installed and running
- Windows containers are enabled
- Required files exist
- Sufficient disk space available

## Step 3: Configure Database (Important!)

Edit `docker-compose.combined.yml` and update the connection string:

```yaml
- ConnectionStrings__ConnectomeEntities=metadata=res://*/;provider=System.Data.SqlClient;provider connection string="data source=YOUR_SERVER;initial catalog=YOUR_DATABASE;User ID=YOUR_USER;Password=YOUR_PASSWORD;..."
```

## Step 4: Build and Run

### Option A: Using PowerShell Script (Easiest)

```powershell
# Build and run with all defaults
.\Scripts\BuildAndRunCombined.ps1

# Or specify your database
.\Scripts\BuildAndRunCombined.ps1 -DBServer myserver -DBName Connectome -DBUser sa -DBPassword mypass
```

### Option B: Using Docker Compose

```powershell
# Build and start
docker-compose -f docker-compose.combined.yml up -d

# View logs
docker-compose -f docker-compose.combined.yml logs -f
```

### Option C: Using Docker CLI

```powershell
# Build (from solution root)
docker build -f Servers/VolumeAnnotationServices/Dockerfile -t viking-annotation-services:latest .

# Run
docker run -d --name viking-annotation-services -p 8080:80 viking-annotation-services:latest
```

## Step 5: Test the Services

Run the test script to verify everything is working:

```powershell
.\Scripts\TestDockerImage.ps1
```

This will:
- Check the container is running
- Test each service endpoint
- Verify IIS and application pools
- Display service URLs

## Step 6: Access the Services

Once running (wait ~60 seconds for startup):

| Service | URL |
|---------|-----|
| **AnnotationService** | http://localhost:8080/annotation/Annotate.svc |
| **Service Metadata** | http://localhost:8080/annotation/Annotate.svc?wsdl |
| **OData Service** | http://localhost:8080/odata |
| **OData Metadata** | http://localhost:8080/odata/$metadata |
| **DataExport** | http://localhost:8080/dataexport |

## Common Commands

```powershell
# View logs
docker logs -f viking-annotation-services

# Stop services
docker stop viking-annotation-services

# Start again
docker start viking-annotation-services

# Remove container
docker rm -f viking-annotation-services

# Rebuild from scratch
.\Scripts\BuildAndRunCombined.ps1 -Action rebuild
```

## Troubleshooting

### Build Fails
- Ensure you're in the solution root directory
- Check you have enough disk space (at least 20GB free)
- Try: `docker system prune -a` then rebuild

### Services Won't Start
1. Check logs: `docker logs viking-services`
2. Verify database connection string is correct
3. Ensure SQL Server allows remote connections
4. Check ports 8080/8443 aren't already in use

### 503 Service Unavailable
- Wait 60 seconds after starting (IIS needs time to initialize)
- Check application pools are running:
  ```powershell
  docker exec viking-annotation-services powershell Get-WebAppPoolState -Name "AnnotationServicePool"
  ```

### Out of Memory
- Increase Docker Desktop memory: Settings → Resources → Memory (set to 8GB+)

## Next Steps

For detailed configuration, SSL setup, and production deployment, see:
- [Docker-Combined-Services-README.md](Docker-Combined-Services-README.md)

## Getting Help

If you encounter issues:
1. Check the logs: `docker logs viking-annotation-services`
2. Verify your database connection
3. Ensure Windows containers are enabled
4. Check that ports aren't already in use

## Summary of Files Created

- `Servers/VolumeAnnotationServices/Dockerfile` - Main Dockerfile for volume annotation services
- `Servers/VolumeAnnotationServices/README.md` - Service-specific documentation
- `docker-compose.combined.yml` - Docker Compose configuration
- `Scripts/BuildAndRunCombined.ps1` - Automated build/run script
- `Scripts/ConfigureIIS.ps1` - IIS configuration for local development
- `Docker-Combined-Services-README.md` - Comprehensive documentation
- Configuration templates in `Servers/*/` directories

All services will be accessible on **port 8080** (HTTP) and **port 8443** (HTTPS) under their respective paths.

