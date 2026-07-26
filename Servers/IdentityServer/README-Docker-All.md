# Identity Server - All Services Docker Setup

This setup allows you to run all three Identity Server components in a single Docker container:
- **IdentityServerStandalone** (Ports 5000/5001) - Core Identity Server
- **WebApi** (Ports 6000/6001) - API endpoints  
- **IdentityServer** (Ports 4000/4001) - Management website

## Quick Start

### Option 1: Using PowerShell Script (Recommended)
```powershell
# Start all services in foreground
.\start-all-services.ps1

# Start all services in background
.\start-all-services.ps1 -Detach

# Start without rebuilding
.\start-all-services.ps1 -Build:$false
```

### Option 2: Using Docker Compose Directly
```bash
# Build and start all services
docker-compose -f docker-compose-all.yml up --build

# Start in background
docker-compose -f docker-compose-all.yml up -d

# View logs
docker-compose -f docker-compose-all.yml logs -f

# Stop services
docker-compose -f docker-compose-all.yml down
```

### Option 3: Using Docker Directly
```bash
# Build the image
docker build -t identity-all-services .

# Run the container
docker run -p 4000:4000 -p 4001:4001 -p 5000:5000 -p 5001:5001 -p 6000:6000 -p 6001:6001 identity-all-services
```

## Service Endpoints

Once running, the services will be available at:

- **IdentityServerStandalone**: 
  - HTTP: http://localhost:5000
  - HTTPS: https://localhost:5001
  - Discovery: https://localhost:5001/.well-known/openid-configuration

- **WebApi**: 
  - HTTP: http://localhost:6000
  - HTTPS: https://localhost:6001
  - Health: https://localhost:6001/health

- **IdentityServer (Management)**: 
  - HTTP: http://localhost:4000
  - HTTPS: https://localhost:4001

## Configuration

### Environment Variables
Set these environment variables before starting:

```bash
# Database Configuration
SQL_SERVER_HOST=localhost
SQL_SERVER_PORT=1433
SQL_SERVER_USER=sa
SQL_SERVER_PASSWORD=YourPassword123!
SQL_SERVER_IDENTITY_DB=IdentityViking
SQL_SERVER_CONFIG_DB=IdentityConfig
SQL_SERVER_GRANTS_DB=IdentityPersistedGrants

# SSL Certificate Paths (optional, PEM)
SSL_CERT_PATH=/path/to/certificate.crt
SSL_KEY_PATH=/path/to/private.key

# Duende License (optional)
DUENDE_KEY_PATH=/path/to/Duende_License.key
```

### Port Configuration
You can customize the ports by setting these environment variables:

```bash
IDENTITY_STANDALONE_HTTP_PORT=5000
IDENTITY_STANDALONE_HTTPS_PORT=5001
IDENTITY_WEBAPI_HTTP_PORT=6000
IDENTITY_WEBAPI_HTTPS_PORT=6001
IDENTITY_SERVER_HTTP_PORT=4000
IDENTITY_SERVER_HTTPS_PORT=4001
```

### Remote Debugging Configuration
Enable Visual Studio remote debugging for development:

```bash
# Enable remote debugging (default: false)
IDENTITY_ENABLE_REMOTE_DEBUG=true

# Custom debug port (default: 4024)
REMOTE_DEBUG_PORT=4024
```

**Note**: Remote debugging is automatically enabled when `ASPNETCORE_ENVIRONMENT` is set to `Development` or `Debug`.

### Docker Image Versioning
Version your Docker images for deployment to registries:

```bash
# Set image version (default: latest)
IMAGE_VERSION=1.2.3

# Set registry prefix (optional, for pushing to registries)
# Examples:
#   Docker Hub: IMAGE_REGISTRY=username/
#   Azure ACR: IMAGE_REGISTRY=myregistry.azurecr.io/
#   AWS ECR: IMAGE_REGISTRY=123456789.dkr.ecr.us-east-1.amazonaws.com/
IMAGE_REGISTRY=myregistry.azurecr.io/

# Build with version
docker-compose -f docker-compose-all.yml build

# Tag with multiple versions (best practice)
docker tag ${IMAGE_REGISTRY:-}identity-all-services:${IMAGE_VERSION:-latest} ${IMAGE_REGISTRY:-}identity-all-services:1.2
docker tag ${IMAGE_REGISTRY:-}identity-all-services:${IMAGE_VERSION:-latest} ${IMAGE_REGISTRY:-}identity-all-services:1
docker tag ${IMAGE_REGISTRY:-}identity-all-services:${IMAGE_VERSION:-latest} ${IMAGE_REGISTRY:-}identity-all-services:latest

# Push to registry
docker push ${IMAGE_REGISTRY:-}identity-all-services:${IMAGE_VERSION:-latest}
docker push ${IMAGE_REGISTRY:-}identity-all-services:latest
```

**Versioning Strategy:**
- Use semantic versioning (e.g., `1.2.3`) for releases
- Tag with major (`1`), minor (`1.2`), and patch (`1.2.3`) versions
- Always tag `latest` to the most recent stable version
- Use git commit SHA for development builds: `IMAGE_VERSION=$(git rev-parse --short HEAD)`

## Remote Debugging

### Enabling Remote Debugging

Remote debugging allows you to attach Visual Studio or VS Code to the running Docker container for debugging .NET applications.

#### Method 1: Environment Variable
```bash
# Enable remote debugging explicitly
IDENTITY_ENABLE_REMOTE_DEBUG=true docker-compose -f docker-compose-all.yml up --build
```

#### Method 2: Development Environment
```bash
# Automatically enables remote debugging
ASPNETCORE_ENVIRONMENT=Development docker-compose -f docker-compose-all.yml up --build
```

#### Method 3: Custom Debug Port
```bash
# Use custom debug port
IDENTITY_ENABLE_REMOTE_DEBUG=true REMOTE_DEBUG_PORT=5000 docker-compose -f docker-compose-all.yml up --build
```

### Connecting to Remote Debugger

1. **Start the container** with remote debugging enabled
2. **Open Visual Studio** or VS Code
3. **Attach to Process**:
   - **Visual Studio**: Debug → Attach to Process → Remote → `localhost:4024`
   - **VS Code**: Use the "Remote Debugging" configuration in `.vscode/launch.json`

### Debug Port Mapping

The debugger runs on a single port (default: 4024) and can debug all three services:
- IdentityServerStandalone
- WebApi  
- IdentityServer (Management)

### Troubleshooting Remote Debugging

1. **Debugger not starting**: Check that `IDENTITY_ENABLE_REMOTE_DEBUG=true` is set
2. **Connection refused**: Verify the debug port (4024) is not blocked by firewall
3. **Build required**: Use `--build` flag when changing debug settings
4. **Port conflicts**: Change `REMOTE_DEBUG_PORT` if 4024 is in use

## Logs and Monitoring

### View Logs
```bash
# View all service logs
docker-compose -f docker-compose-all.yml logs -f

# View specific service logs
docker-compose -f docker-compose-all.yml logs -f identity-all-services
```

### Health Checks
The container includes health checks for all services:
- IdentityServerStandalone: Checks OpenID Connect discovery endpoint
- WebApi: Checks health endpoint
- IdentityServer: Checks management website

### Log Files
Logs are stored in the following locations:
- Container: `/var/log/supervisor/`
- Host: `./logs/` directory

## Troubleshooting

### Common Issues

1. **Port Conflicts**: Make sure the ports (4000, 4001, 5000, 5001, 6000, 6001) are not in use by other services.

2. **Database Connection**: Ensure your SQL Server is running and accessible with the provided connection string.

3. **SSL Certificates**: If you're using custom SSL certificates, make sure the paths are correct and the certificates are accessible.

4. **Environment Variables**: Verify all required environment variables are set correctly.

### Debug Mode
To run in debug mode with more verbose logging:

```bash
# Set environment to Development
export ASPNETCORE_ENVIRONMENT=Development

# Start with debug logging
docker-compose -f docker-compose-all.yml up
```

### Container Shell Access
To access the container shell for debugging:

```bash
# Get container ID
docker ps

# Access shell
docker exec -it <container-id> /bin/bash
```

## Architecture

The Docker setup uses:
- **Supervisor**: Process manager to run all three services
- **Multi-stage build**: Optimized build process
- **Shared dependencies**: Common libraries and Data Protection keys
- **Health checks**: Automatic service monitoring
- **Log aggregation**: Centralized logging for all services

## Development vs Production

### Development
- Uses development certificates
- Verbose logging enabled
- Hot reload capabilities
- Debug symbols included

### Production
- Requires production SSL certificates
- Optimized logging
- Security hardening
- Performance optimizations

## Security Considerations

1. **SSL Certificates**: Always use proper SSL certificates in production
2. **Database Security**: Use strong passwords and secure database connections
3. **Network Security**: Consider using Docker networks for service isolation
4. **Secrets Management**: Use Docker secrets for sensitive configuration
5. **Regular Updates**: Keep base images and dependencies updated

