# Docker Configuration with appsettings.json Volume Mounts

This document explains how to run the Viking Legacy services in Docker containers with proper configuration file mapping.

## Overview

The Docker setup has been updated to map `appsettings.json` files from the project directories into the containers at runtime. This allows you to modify configuration without rebuilding the Docker images.

## Services

### 1. GrpcAnnotationService
- **Ports**: HTTP (5001), HTTPS (5002)
- **Configuration**: `Servers/GrpcAnnotationService/appsettings.json`
- **Development Config**: `Servers/GrpcAnnotationService/appsettings.Development.json`

### 2. GrpcAnnotation
- **Ports**: HTTP (5000), HTTPS (5003)
- **Configuration**: `Servers/GrpcAnnotation/appsettings.json`
- **Development Config**: `Servers/GrpcAnnotation/appsettings.Development.json`

### 3. SegmentationServer (SAM2)
- **Ports**: Web (8080), gRPC (50051)
- **Configuration**: Uses Python-based configuration

## Running Services

### Option 1: Using Docker Compose (Recommended)

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### Option 2: Using Individual Run Scripts

#### GrpcAnnotationService
```bash
cd Servers/GrpcAnnotationService
run-with-config.cmd
```

#### GrpcAnnotation
```bash
cd Servers/GrpcAnnotation
run-with-config.cmd
```

#### SegmentationServer
```bash
cd Servers/SegmentationServer
run-with-config.cmd
```

### Option 3: Manual Docker Commands

#### GrpcAnnotationService
```bash
docker build -t grpc-annotation-service -f Servers/GrpcAnnotationService/Dockerfile .
docker run -it -p 5001:80 -p 5002:443 \
  -v "$(pwd)/Servers/GrpcAnnotationService/appsettings.json:/app/appsettings.json:ro" \
  -v "$(pwd)/Servers/GrpcAnnotationService/appsettings.Development.json:/app/appsettings.Development.json:ro" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  --name grpc-annotation-service \
  grpc-annotation-service
```

#### GrpcAnnotation
```bash
docker build -t grpc-annotation -f Servers/GrpcAnnotation/Dockerfile .
docker run -it -p 5000:80 -p 5003:443 \
  -v "$(pwd)/Servers/GrpcAnnotation/appsettings.json:/app/appsettings.json:ro" \
  -v "$(pwd)/Servers/GrpcAnnotation/appsettings.Development.json:/app/appsettings.Development.json:ro" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  --name grpc-annotation \
  grpc-annotation
```

## Configuration Management

### Modifying Configuration

1. **Edit the appsettings.json files** in the project directories:
   - `Servers/GrpcAnnotationService/appsettings.json`
   - `Servers/GrpcAnnotation/appsettings.json`

2. **Restart the containers** to pick up changes:
   ```bash
   docker-compose restart
   # or
   docker restart <container-name>
   ```

### Environment-Specific Configuration

- **Development**: Uses `appsettings.Development.json` when `ASPNETCORE_ENVIRONMENT=Development`
- **Production**: Uses `appsettings.json` when `ASPNETCORE_ENVIRONMENT=Production`

### Volume Mount Details

- **Read-Only Mounts**: Configuration files are mounted as read-only (`:ro`) to prevent accidental modification from within the container
- **Host Path**: Absolute paths to the project's appsettings.json files
- **Container Path**: `/app/appsettings.json` (standard .NET Core location)

## Accessing Services

Once containers are running, access the services at:

- **GrpcAnnotationService**: 
  - HTTP: http://localhost:5001
  - HTTPS: https://localhost:5002
- **GrpcAnnotation**: 
  - HTTP: http://localhost:5000
  - HTTPS: https://localhost:5003
- **SegmentationServer**: 
  - Web: http://localhost:8080
  - gRPC: localhost:50051

## Troubleshooting

### Configuration Not Loading
1. Verify the volume mount paths are correct
2. Check that appsettings.json files exist in the project directories
3. Ensure the container has read permissions to the mounted files

### Port Conflicts
If you get port binding errors, modify the port mappings in:
- `docker-compose.yml` for compose-based deployment
- Run scripts for individual container deployment

### Container Not Starting
1. Check Docker logs: `docker logs <container-name>`
2. Verify the Dockerfile builds successfully
3. Ensure all required dependencies are available

## Network Configuration

All services are connected to a custom bridge network (`viking-network`) for inter-service communication while maintaining isolation from the host network.

## Security Notes

- Configuration files are mounted as read-only to prevent container modification
- Services run with appropriate environment variables
- Network isolation is maintained through Docker networks
- GPU access is properly configured for the SegmentationServer

















