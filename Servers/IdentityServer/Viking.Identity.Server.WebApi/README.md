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

**Important**: Connection strings with credentials should **never** be stored in `appsettings.json` files that are committed to version control. Use User Secrets for local development or environment variables for deployment.

The Viking.Identity.Server.WebApi application uses several configuration methods:

### A. User Secrets (Recommended for Local Development)

For local development, use .NET User Secrets to store sensitive configuration:

```bash
# Navigate to the project directory
cd C:\src\git\Viking\Servers\IdentityServer\Viking.Identity.Server.WebApi

# Set connection string with credentials
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=your-db-server;Database=IdentityViking;Trusted_Connection=False;User ID=your-user;Password=your-password;MultipleActiveResultSets=true;TrustServerCertificate=True"

# Set Identity Server secret
dotnet user-secrets set "VikingIdentityServerOptions:Secret" "your-identity-server-secret"
```

**User Secrets ID**: `aspnet-Viking.Identity.Server.WebApi-489B09E1-41D8-42F9-9F75-CD8530619CD5`

**User secrets location** (automatically ignored by git):
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\aspnet-Viking.Identity.Server.WebApi-489B09E1-41D8-42F9-9F75-CD8530619CD5\secrets.json`
- **Linux/Mac**: `~/.microsoft/usersecrets/aspnet-Viking.Identity.Server.WebApi-489B09E1-41D8-42F9-9F75-CD8530619CD5/secrets.json`

### B. Environment Variables
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
  -e JwtBearerOptions__TokenValidationParameters__ValidAudience="Viking.Annotation.API" \
  identity-webapi:latest
```

### C. Volume Mounting for Configuration Files

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

### D. Custom Configuration File
Create a custom `appsettings.json` file and mount it:

**Note**: Do not include credentials in `appsettings.json` files committed to version control. Use environment variable substitution or load credentials from user secrets/secrets.json.

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
      "ValidAudience": "Viking.Annotation.API",
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
  - **IdentityConnection**: Full connection string with server, database, and authentication
  - Should be stored in User Secrets or environment variables when credentials are included
- **VikingIdentityServerOptions**: Identity Server configuration
  - **Authority**: URL of the Identity Server
  - **Secret**: Client secret for authentication (should be in User Secrets or environment variables)
- **JwtBearerOptions**: JWT Bearer authentication configuration including:
  - **Authority**: URL of the Identity Server
  - **RequireHttpsMetadata**: Whether to require HTTPS for metadata
  - **TokenValidationParameters**: JWT token validation settings
- **SSL**: Certificate serial number for HTTPS (if not provided, uses developer signing credential)
- **Logging**: Log levels and output configuration

## Required User Secrets

For local development, the following secrets should be configured using `dotnet user-secrets`:

| Secret Key | Description | Example |
|------------|-------------|---------|
| `ConnectionStrings:IdentityConnection` | Database connection string with credentials | `Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True` |
| `VikingIdentityServerOptions:Secret` | Identity Server client secret | `your-identity-server-secret-here` |

### Managing User Secrets

```bash
# List all secrets
dotnet user-secrets list

# Get a specific secret
dotnet user-secrets get "ConnectionStrings:IdentityConnection"

# Remove a secret
dotnet user-secrets remove "ConnectionStrings:IdentityConnection"

# Clear all secrets
dotnet user-secrets clear
```

### Connection String Examples

**SQL Server with Integrated Security (Windows Authentication)**:
```
Server=YourServer;Database=IdentityViking;Trusted_Connection=True;Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

**SQL Server with SQL Authentication**:
```
Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True
```

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