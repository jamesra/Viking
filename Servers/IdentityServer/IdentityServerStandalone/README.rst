# IdentityServerStandalone

## Authentication

### Clients

    mvc - The Viking.Identity.Server.WebApi client
    web - Believed deprecated
    viking - The Viking Application 
    ro.viking - A read-only client of Annotation data, possibly anonymous users

### Scopes

       Viking.Annotations - Access to the API in Viking.Identity.Server.WebApi
       Volume

           - Read     - Read-only access to the volume
           - Annotate - Annotate/modify access to the volume
           - Review   - Full review access to the volume, includes more dangerous operations such as merge/split
 

### Apis

    



## Docker Build Instructions

**Important**: This Dockerfile must be built from the **Server folder** (parent directory of IdentityServer), not from the IdentityServerStandalone folder itself.

### Build Command

From the **Server folder** (parent directory of IdentityServer), use:

```bash
docker build -f IdentityServer\IdentityServerStandalone\Dockerfile -t identityserver .
```

### Why This Build Context is Required

The Dockerfile uses COPY commands that reference projects relative to the solution root:

```dockerfile
COPY Identity.DataContext/ Identity.DataContext/
COPY Identity.Models/ Identity.Models/
COPY IdentityServer/Identity.Configuration/ IdentityServer/Identity.Configuration/
COPY IdentityServer/IdentityServerStandalone/ IdentityServer/IdentityServerStandalone/
```

These paths assume the build context is at the **Server folder** level where:
- `Identity.DataContext/` exists
- `Identity.Models/` exists  
- `IdentityServer/` folder contains the IdentityServer projects

### Alternative Build Contexts

If you need to build from a different location, you must adjust the COPY paths in the Dockerfile accordingly.

### Project Structure

```
Server/
├── Identity.DataContext/
├── Identity.Models/
├── IdentityServer/
│   ├── Identity.Configuration/
│   ├── IdentityServerStandalone/
│   │   ├── Dockerfile
│   │   └── README.md
│   └── ...
└── ...
```

### Troubleshooting

- **"COPY failed" errors**: Ensure you're building from the correct directory
- **"Project not found" errors**: Verify the build context includes all required projects
- **Build context issues**: Check that the DockerfileContext property in the .csproj matches your build location

## Running with Network Access

The IdentityServerStandalone container exposes ports **6000** (HTTP) and **6001** (HTTPS). Here are several ways to run it with network access:

### Option A: Using Docker Run Command
```bash
docker run -d \
  --name identityserver-standalone \
  -p 6000:6000 \
  -p 6001:6001 \
  identityserver-standalone:latest
```

### Option B: Using Docker Compose
```bash
# From the IdentityServer directory
docker-compose up -d identity-standalone
```

### Option C: With Custom Network
```bash
# Create a custom network
docker network create identity-network

# Run the container on the custom network
docker run -d \
  --name identityserver-standalone \
  --network identity-network \
  -p 6000:6000 \
  -p 6001:6001 \
  identityserver-standalone:latest
```

## Configuring Settings

The IdentityServerStandalone application uses several configuration methods:

### A. Environment Variables
You can override configuration using environment variables:

```bash
docker run -d \
  --name identityserver-standalone \
  -p 6000:6000 \
  -p 6001:6001 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__IdentityConnection="Server=your-db-server;Database=IdentityViking;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True" \
  -e ConnectionStrings__ConfigConnection="Server=your-db-server;Database=IdentityConfig;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True" \
  -e ConnectionStrings__PersistedGrantConnection="Server=your-db-server;Database=IdentityPersistedGrants;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True" \
  identityserver-standalone:latest
```

### B. Volume Mounting for Configuration Files

**Important**: Docker can only mount single files if they already exist on the host. If the file doesn't exist, Docker will create a directory instead.

#### Option 1: Mount existing files
```bash
# Ensure the files exist on your host first
docker run -d \
  --name identityserver-standalone \
  -p 6000:6000 \
  -p 6001:6001 \
  -v /path/to/your/appsettings.json:/app/appsettings.json \
  -v /path/to/your/appsettings.Production.json:/app/appsettings.Production.json \
  identityserver-standalone:latest
```

#### Option 2: Mount configuration directory
```bash
# Mount the entire config directory (recommended)
docker run -d \
  --name identityserver-standalone \
  -p 6000:6000 \
  -p 6001:6001 \
  -v /path/to/your/config:/app/config \
  identityserver-standalone:latest
```

### C. Custom Configuration File
Create a custom `appsettings.json` file and mount it:

```json
{
  "ConnectionStrings": {
    "PersistedGrantConnection": "Server=your-db-server;Database=IdentityPersistedGrants;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True",
    "ConfigConnection": "Server=your-db-server;Database=IdentityConfig;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True",
    "IdentityConnection": "Server=your-db-server;Database=IdentityViking;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "VikingIdentityServerOptions": {
    "Authority": "https://your-domain.com/identityserver/",
    "Secret": "YourSecretKey",
    "ApiScopes": [
      {
        "Name": "Viking.Annotation",
        "Description": "Access to Annotate a volume"
      }
    ]
  },
  "SSL": {
    "SerialNumber": "your-certificate-serial-number"
  },
  "AllowedHosts": "*"
}
```

## Key Configuration Sections

The application uses these main configuration sections:

- **ConnectionStrings**: Database connections for Identity, Config, and PersistedGrants
- **VikingIdentityServerOptions**: IdentityServer authority URL and API scopes
- **SSL**: Certificate serial number for HTTPS (if not provided, uses developer signing credential)
- **Logging**: Log levels and output configuration

## Accessing the Application

Once running, you can access:
- **HTTP**: `http://localhost:6000`
- **HTTPS**: `https://localhost:6001`

## Docker Compose Example

For a complete setup with custom configuration:

```yaml
version: '3.8'
services:
  identity-standalone:
    build:
      context: .
      dockerfile: IdentityServerStandalone/Dockerfile
    ports:
      - "6000:6000"
      - "6001:6001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__IdentityConnection=Server=your-db;Database=IdentityViking;User ID=user;Password=pass;MultipleActiveResultSets=true;TrustServerCertificate=True
    volumes:
      - ./custom-appsettings.json:/app/appsettings.Production.json
    networks:
      - identity-network

networks:
  identity-network:
    driver: bridge
```