# Viking Volume Annotation Services - Docker Container

This directory contains the Dockerfile and configuration for building a unified Docker container that hosts **three interconnected services for volume annotation and connectome data management**.

## Services Included

### 1. **AnnotationService** (`/annotation`)
- **Type**: WCF Service (.NET Framework 4.8)
- **Purpose**: Handles CRUD operations for volume annotations
- **Endpoints**: Multiple WCF service endpoints for:
  - Structure management
  - Location management  
  - Structure type management
  - Permitted structure links
  - Volume metadata
- **Authentication**: JWT via IdentityServer or ASP.NET Membership
- **Protocol**: Binary over HTTPS with protobuf serialization

### 2. **ConnectomeODataV4** (`/odata`)
- **Type**: ASP.NET Web API with OData v4 (.NET Framework 4.8)
- **Purpose**: Provides RESTful OData access to connectome data
- **Endpoints**: OData v4 compliant API for:
  - Structures
  - Locations
  - Structure links
  - Location links
  - Structure types
  - Spatial caches
- **Features**: Full OData query capabilities ($filter, $select, $expand, etc.)
- **CORS**: Enabled for cross-origin access

### 3. **DataExport** (`/dataexport`)
- **Type**: ASP.NET Core Web API (.NET 9.0)
- **Purpose**: Exports morphology and network data
- **Endpoints**: RESTful API for:
  - Morphology export (swc, collada, obj formats)
  - Network/circuit export (graph, motif, connectivity)
  - Neuron data retrieval
- **Performance**: Optimized for large data exports

## Architecture

All three services are hosted in a single Windows Server Core container using IIS:

```
┌─────────────────────────────────────────┐
│   Windows Server Core + IIS             │
├─────────────────────────────────────────┤
│  ┌───────────────────────────────────┐  │
│  │  Default Web Site (Port 80/443)   │  │
│  ├───────────────────────────────────┤  │
│  │  /annotation                      │  │
│  │  ├─ AnnotationServicePool         │  │
│  │  │  (.NET 4.8, WCF)               │  │
│  │  └─ AnnotationService.dll          │  │
│  ├───────────────────────────────────┤  │
│  │  /odata                           │  │
│  │  ├─ ConnectomeODataPool           │  │
│  │  │  (.NET 4.8, Web API)           │  │
│  │  └─ ConnectomeODataV4.dll          │  │
│  ├───────────────────────────────────┤  │
│  │  /dataexport                      │  │
│  │  ├─ DataExportPool                │  │
│  │  │  (ASP.NET Core 9.0)            │  │
│  │  └─ DataExport.dll                 │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

## Building the Image

From the **solution root directory**:

```powershell
# Using the automated script (recommended)
.\Scripts\BuildAndRunCombined.ps1

# Or manually with Docker CLI
docker build -f Servers/VolumeAnnotationServices/Dockerfile -t viking-annotation-services:latest .

# Or with Docker Compose
docker-compose -f docker-compose.combined.yml build
```

## Running the Container

```powershell
# Using the automated script
.\Scripts\BuildAndRunCombined.ps1 -Action run

# Or manually
docker run -d `
  --name viking-annotation-services `
  -p 8080:80 `
  -p 8443:443 `
  -e "ConnectionStrings__ConnectomeEntities=YOUR_CONNECTION_STRING" `
  viking-annotation-services:latest
```

## Accessing the Services

Once running, services are available at:

| Service | URL | Description |
|---------|-----|-------------|
| **AnnotationService WSDL** | http://localhost:8080/annotation/Annotate.svc?wsdl | Service metadata |
| **AnnotationService Endpoint** | http://localhost:8080/annotation/Annotate.svc | WCF service endpoint |
| **OData Root** | http://localhost:8080/odata | OData service document |
| **OData Metadata** | http://localhost:8080/odata/$metadata | Entity model |
| **OData Structures** | http://localhost:8080/odata/Structures | Example query |
| **DataExport** | http://localhost:8080/dataexport | API root |
| **DataExport Morphology** | http://localhost:8080/dataexport/Morphology/{id} | Morphology export |
| **DataExport Network** | http://localhost:8080/dataexport/Network | Network export |

## Configuration

### Database Connection (Required)

All three services require a SQL Server database connection. Configure via:

**Environment Variable** (recommended):
```yaml
environment:
  - ConnectionStrings__ConnectomeEntities=metadata=res://*/;provider=System.Data.SqlClient;provider connection string="data source=YOUR_SERVER;initial catalog=YOUR_DB;User ID=YOUR_USER;Password=YOUR_PASSWORD;..."
```

**Configuration Files**:
- `/inetpub/wwwroot/annotation/web.config`
- `/inetpub/wwwroot/odata/Web.config`
- `/inetpub/wwwroot/dataexport/appsettings.json`

### Identity Server (Optional)

For JWT authentication:
```yaml
environment:
  - IdentityServer__Authority=https://your-identity-server/
  - IdentityServer__Audience=Viking.Annotation.API
```

### Volume URLs

For cross-service references:
```yaml
environment:
  - AppSettings__VolumeURL=http://localhost
  - AppSettings__ODataURL=http://localhost/odata
```

## Testing

Validate the deployment:

```powershell
# Run automated tests
.\Scripts\TestDockerImage.ps1

# Manual tests
Invoke-WebRequest http://localhost:8080/annotation/Annotate.svc?wsdl
Invoke-WebRequest http://localhost:8080/odata/$metadata
Invoke-WebRequest http://localhost:8080/dataexport
```

## Troubleshooting

### View Logs
```powershell
docker logs -f viking-annotation-services
```

### Check IIS Status
```powershell
docker exec viking-annotation-services powershell Get-Service W3SVC
```

### Check Application Pools
```powershell
docker exec viking-annotation-services powershell "Import-Module WebAdministration; Get-ChildItem IIS:\AppPools"
```

### Restart Container
```powershell
docker restart viking-annotation-services
```

### Access Container Shell
```powershell
docker exec -it viking-annotation-services powershell
```

## Build Stages

The Dockerfile uses a multi-stage build:

1. **build-framework**: Builds .NET Framework 4.8 services (AnnotationService, ConnectomeODataV4)
2. **build-core**: Builds .NET 9.0 service (DataExport)
3. **runtime**: Creates final image with IIS hosting all services

This approach:
- Keeps final image size smaller
- Separates build and runtime dependencies
- Enables parallel builds

## Requirements

- **Docker Desktop** with Windows containers enabled
- **8GB+ RAM** allocated to Docker
- **20GB+ free disk space**
- **SQL Server** database (local or remote)
- **Windows Server Core** compatible host

## Performance Considerations

- **Startup Time**: 60-90 seconds for IIS and app pools to initialize
- **Memory Usage**: 4-6 GB typical
- **Image Size**: ~3-4 GB
- **Build Time**: 20-30 minutes (first build), 5-10 minutes (cached)

## Related Documentation

- `../../DOCKER-COMBINED-QUICKSTART.md` - Quick start guide
- `../../Docker-Combined-Services-README.md` - Comprehensive documentation
- `../../DOCKER-BUILD-FIXES.md` - Build fixes and troubleshooting
- `../../Scripts/BuildAndRunCombined.ps1` - Build automation script
- `../../Scripts/ValidateDockerSetup.ps1` - Prerequisites validation
- `../../Scripts/TestDockerImage.ps1` - Deployment testing

## Support

For issues, check:
1. Container logs: `docker logs viking-annotation-services`
2. Windows Event Log inside container
3. IIS logs at `C:\inetpub\logs\LogFiles`
4. Application-specific logs

## Version

- **Container Version**: 1.0
- **.NET Framework**: 4.8
- **.NET**: 9.0
- **IIS**: Windows Server Core LTSC 2022
- **OData**: v4 (7.21.6)


