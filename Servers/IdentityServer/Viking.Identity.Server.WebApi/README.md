# Viking.Identity.Server.WebApi

## Docker Build Instructions

**Important**: This Dockerfile must be built from the **Server folder** (parent directory of IdentityServer), not from the Viking.Identity.Server.WebApi folder itself.

### Build Command

From the **Server folder** (parent directory of IdentityServer), use:

```bash
docker build -f IdentityServer\Viking.Identity.Server.WebApi\Dockerfile -t identity-webapi .
```

### Why This Build Context is Required

The Dockerfile uses COPY commands that reference projects relative to the solution root:

```dockerfile
COPY Identity.DataContext/ Identity.DataContext/
COPY Identity.Models/ Identity.Models/
COPY IdentityServer/Identity.Configuration/ IdentityServer/Identity.Configuration/
COPY IdentityServer/Viking.Identity.Server.Extensions/ IdentityServer/Viking.Identity.Server.Extensions/
COPY IdentityServer/Viking.Identity.Server.WebApi/ IdentityServer/Viking.Identity.Server.WebApi/
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
│   ├── Viking.Identity.Server.Extensions/
│   ├── Viking.Identity.Server.WebApi/
│   │   ├── Dockerfile
│   │   └── README.md
│   └── ...
└── ...
```

### Troubleshooting

- **"COPY failed" errors**: Ensure you're building from the correct directory
- **"Project not found" errors**: Verify the build context includes all required projects
- **Build context issues**: Check that the DockerfileContext property in the .csproj matches your build location

### Differences from IdentityServerStandalone

This WebApi project has additional dependencies:
- `Viking.Identity.Server.Extensions` - Contains custom extensions and services
- More complex project structure with additional project references

### Visual Studio Integration

When using Visual Studio's Docker tools, ensure the build context is properly configured in the project properties to match the Server folder location.

## Running with Network Access

The Viking.Identity.Server.WebApi container exposes ports **5000** (HTTP) and **5001** (HTTPS). Here are several ways to run it with network access:

### Option A: Using Docker Run Command
```bash
docker run -d \
  --name identity-webapi \
  -p 5000:5000 \
  -p 5001:5001 \
  identity-webapi:latest
```

### Option B: Using Docker Compose
```bash
# From the IdentityServer directory
docker-compose up -d identity-webapi
```

### Option C: With Custom Network
```bash
# Create a custom network
docker network create identity-network

# Run the container on the custom network
docker run -d \
  --name identity-webapi \
  --network identity-network \
  -p 5000:5000 \
  -p 5001:5001 \
  identity-webapi:latest
```

## Configuring Settings

The Viking.Identity.Server.WebApi application uses several configuration methods:

### A. Environment Variables
You can override configuration using environment variables:

```bash
docker run -d \
  --name identity-webapi \
  -p 5000:5000 \
  -p 5001:5001 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__IdentityConnection="Server=your-db-server;Database=IdentityViking;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True" \
  -e JwtBearerOptions__Authority="https://your-identity-server.com/identityserver/" \
  -e JwtBearerOptions__RequireHttpsMetadata="true" \
  -e JwtBearerOptions__TokenValidationParameters__ValidAudience="Viking.Annotation" \
  identity-webapi:latest
```

### B. Volume Mounting for Configuration Files

**Important**: Docker can only mount single files if they already exist on the host. If the file doesn't exist, Docker will create a directory instead.

#### Option 1: Mount existing files
```bash
# Ensure the files exist on your host first
docker run -d \
  --name identity-webapi \
  -p 5000:5000 \
  -p 5001:5001 \
  -v /path/to/your/appsettings.json:/app/appsettings.json \
  -v /path/to/your/appsettings.Production.json:/app/appsettings.Production.json \
  identity-webapi:latest
```

#### Option 2: Mount configuration directory
```bash
# Mount the entire config directory (recommended)
docker run -d \
  --name identity-webapi \
  -p 5000:5000 \
  -p 5001:5001 \
  -v /path/to/your/config:/app/config \
  identity-webapi:latest
```

### C. Custom Configuration File
Create a custom `appsettings.json` file and mount it:

```json
{
  "ConnectionStrings": {
    "IdentityConnection": "Server=your-db-server;Database=IdentityViking;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "JwtBearerOptions": {
    "Authority": "https://your-identity-server.com/identityserver/",
    "RequireHttpsMetadata": true,
    "SaveToken": true,
    "TokenValidationParameters": {
      "ValidateAudience": true,
      "ValidateLifetime": true,
      "ValidateIssuer": true,
      "ValidateTokenReplay": true,
      "ValidateIssuerSigningKey": true,
      "NameClaimType": "name",
      "ValidAudience": "Viking.Annotation",
      "ValidTypes": [ "at+jwt" ]
    }
  },
  "SSL": {
    "SerialNumber": "your-certificate-serial-number"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

## Key Configuration Sections

The application uses these main configuration sections:

- **ConnectionStrings**: Database connection for Identity data
- **JwtBearerOptions**: JWT Bearer authentication configuration including:
  - **Authority**: URL of the Identity Server
  - **RequireHttpsMetadata**: Whether to require HTTPS for metadata
  - **TokenValidationParameters**: JWT token validation settings
- **SSL**: Certificate serial number for HTTPS (if not provided, uses developer signing credential)
- **Logging**: Log levels and output configuration

## Accessing the Application

Once running, you can access:
- **HTTP**: `http://localhost:5000`
- **HTTPS**: `https://localhost:5001`
- **Swagger UI** (Development only): `https://localhost:5001/swagger`

## API Endpoints

The WebApi provides REST endpoints for:
- **Permissions Management**: User and group permission operations
- **Identity Management**: User and role management operations
- **Authentication**: JWT Bearer token validation

## Docker Compose Example

For a complete setup with custom configuration:

```yaml
version: '3.8'
services:
  identity-webapi:
    build:
      context: .
      dockerfile: Viking.Identity.Server.WebApi/Dockerfile
    ports:
      - "5000:5000"
      - "5001:5001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__IdentityConnection=Server=your-db;Database=IdentityViking;User ID=user;Password=pass;MultipleActiveResultSets=true;TrustServerCertificate=True
      - JwtBearerOptions__Authority=https://your-identity-server.com/identityserver/
    volumes:
      - ./custom-appsettings.json:/app/appsettings.Production.json
    networks:
      - identity-network

networks:
  identity-network:
    driver: bridge
```

## Integration with IdentityServerStandalone

This WebApi is designed to work with the IdentityServerStandalone project:

1. **IdentityServerStandalone** runs on ports 6000/6001 and provides authentication
2. **Viking.Identity.Server.WebApi** runs on ports 5000/5001 and consumes JWT tokens from the Identity Server
3. Configure the `JwtBearerOptions.Authority` to point to your IdentityServerStandalone instance
4. Ensure both containers can communicate (use the same Docker network)