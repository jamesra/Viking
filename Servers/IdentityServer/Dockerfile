# Multi-service Dockerfile for Identity Server components
# This Dockerfile builds and runs IdentityServerStandalone, WebApi, and IdentityServer

# Build arguments for port configuration
ARG IDENTITY_STANDALONE_HTTP_PORT=5000
ARG IDENTITY_STANDALONE_HTTPS_PORT=5001
ARG IDENTITY_WEBAPI_HTTP_PORT=6000
ARG IDENTITY_WEBAPI_HTTPS_PORT=6001
ARG IDENTITY_MANAGEMENT_HTTP_PORT=4000
ARG IDENTITY_MANAGEMENT_HTTPS_PORT=4001
ARG IDENTITY_ENABLE_REMOTE_DEBUG=false
ARG REMOTE_DEBUG_PORT=4024
# Environment configuration - can be overridden by build args
ARG ASPNETCORE_ENVIRONMENT=Production
ARG HOSTING_ENVIRONMENT=Docker

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Install supervisor for process management and dos2unix for line ending conversion
RUN apt-get update && apt-get install -y supervisor curl dos2unix && rm -rf /var/lib/apt/lists/*

# Install Visual Studio remote debugger (vsdbg)
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

# Set proper permissions for vsdbg
RUN chmod +x /vsdbg/vsdbg

# Create directories for each service
RUN mkdir -p /app/identity-standalone /app/identity-webapi /app/identity-server /app/DataProtectionKeys

# Set environment variables from build arguments
ENV ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT
ENV HOSTING_ENVIRONMENT=$HOSTING_ENVIRONMENT
ENV IDENTITY_ENABLE_REMOTE_DEBUG=$IDENTITY_ENABLE_REMOTE_DEBUG
ENV REMOTE_DEBUG_PORT=$REMOTE_DEBUG_PORT

# Expose ports for all services (HTTP, HTTPS, and single Debug port)
EXPOSE 5000 5001 6000 6001 4000 4001 4024

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Pass build arguments to build stage
ARG ASPNETCORE_ENVIRONMENT
ARG BUILD_CONFIGURATION=Release

# BUILD_CONFIGURATION is passed from docker-compose

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /src

RUN mkdir IdentityServer

# Copy all project dependencies
COPY ../Identity.DataContext/ Identity.DataContext/
COPY ../Identity.Models/ Identity.Models/
COPY IdentityServer/Identity.Configuration/ IdentityServer/Identity.Configuration/
COPY IdentityServer/Viking.Identity.Server.Extensions/ IdentityServer/Viking.Identity.Server.Extensions/

COPY IdentityServer/IdentityServerStandalone/ IdentityServer/IdentityServerStandalone/
COPY IdentityServer/Viking.Identity.Server.WebApi/ IdentityServer/Viking.Identity.Server.WebApi/
COPY IdentityServer/Viking.Identity.Server.WebManagement/ IdentityServer/Viking.Identity.Server.WebManagement/

# Build IdentityServerStandalone
WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN dotnet restore "IdentityServerStandalone.csproj"
RUN dotnet build "IdentityServerStandalone.csproj" -c $BUILD_CONFIGURATION -o /app/build/identity-standalone

# Build WebApi
WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebApi" 
RUN dotnet restore "Viking.Identity.Server.WebApi.csproj"
RUN dotnet build "Viking.Identity.Server.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build/identity-webapi

# Build Viking.Identity.Server.WebManagement (main management website)
WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebManagement"
RUN dotnet restore "IdentityManagementWebsite.csproj"
RUN dotnet build "IdentityManagementWebsite.csproj" -c $BUILD_CONFIGURATION -o /app/build/identity-server

# Publish all services
FROM build AS publish

# Publish IdentityServerStandalone
WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN dotnet publish "IdentityServerStandalone.csproj" -c $BUILD_CONFIGURATION -o /app/publish/identity-standalone /p:UseAppHost=false --runtime linux-x64 --self-contained false /p:DebugType=portable

# Publish WebApi
WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebApi"
RUN dotnet publish "Viking.Identity.Server.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish/identity-webapi /p:UseAppHost=false --runtime linux-x64 --self-contained false /p:DebugType=portable

# Publish Viking.Identity.Server.WebManagement
WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebManagement"
RUN dotnet publish "IdentityManagementWebsite.csproj" -c $BUILD_CONFIGURATION -o /app/publish/identity-server /p:UseAppHost=false --runtime linux-x64 --self-contained false /p:DebugType=portable
# secrets.json is not copied into the image; mount it at runtime from
# IDENTITY_CONFIG_PATH (default: D:/Docker/mounted-configs/IdentityServer)

# Generate Data Protection keys
FROM build AS keygenerator

WORKDIR /src
COPY IdentityServer/KeyGenerator/ IdentityServer/KeyGenerator/
WORKDIR "/src/IdentityServer/KeyGenerator"
RUN dotnet restore "KeyGenerator.csproj"
RUN dotnet build "KeyGenerator.csproj" -c $BUILD_CONFIGURATION -o /app/build/KeyGenerator 
RUN dotnet run /app/DataProtectionKeys VikingIdentityServer 
  
WORKDIR "/app/IdentityServer/KeyGenerator"
  
# Final stage
FROM base AS final
# Pass build arguments to final stage
ARG ASPNETCORE_ENVIRONMENT
ARG HOSTING_ENVIRONMENT
ARG IDENTITY_ENABLE_REMOTE_DEBUG
ARG REMOTE_DEBUG_PORT

# Set environment variables from build arguments
ENV ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT
ENV HOSTING_ENVIRONMENT=$HOSTING_ENVIRONMENT
ENV IDENTITY_ENABLE_REMOTE_DEBUG=$IDENTITY_ENABLE_REMOTE_DEBUG
ENV REMOTE_DEBUG_PORT=$REMOTE_DEBUG_PORT

WORKDIR /app
 
RUN mkdir /app/wwwroot/

# Copy published applications
COPY --from=publish /app/publish/identity-standalone /app/identity-standalone/
COPY --from=publish /app/publish/identity-webapi /app/identity-webapi/
COPY --from=publish /app/publish/identity-server /app/identity-server/

# Copy Data Protection keys
COPY --from=keygenerator /app/DataProtectionKeys /app/DataProtectionKeys

# Environment files will be mounted from host

# Create supervisor configuration
RUN mkdir -p /etc/supervisor/conf.d

# Create supervisor configuration file
RUN cat > /etc/supervisor/conf.d/supervisord.conf <<EOF
[supervisord]
nodaemon=true
logfile=/var/log/supervisor/supervisord.log
pidfile=/app/supervisord.pid
loglevel=warn

[program:identity-standalone]
command=/app/run-identity-standalone.sh
directory=/app/identity-standalone
user=app
autostart=true
autorestart=true
startsecs=3
startretries=3
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
environment=ASPNETCORE_URLS="http://+:${IDENTITY_STANDALONE_HTTP_PORT};https://+:${IDENTITY_STANDALONE_HTTPS_PORT}",ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}",HOSTING_ENVIRONMENT="${HOSTING_ENVIRONMENT:-Docker}",IDENTITY_STANDALONE_HTTP_PORT="${IDENTITY_STANDALONE_HTTP_PORT}",IDENTITY_STANDALONE_HTTPS_PORT="${IDENTITY_STANDALONE_HTTPS_PORT}"

[program:identity-webapi]
command=/app/run-identity-webapi.sh
directory=/app/identity-webapi
user=app
autostart=true
autorestart=true
startsecs=3
startretries=3
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
environment=ASPNETCORE_URLS="http://+:${IDENTITY_WEBAPI_HTTP_PORT};https://+:${IDENTITY_WEBAPI_HTTPS_PORT}",ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}",HOSTING_ENVIRONMENT="${HOSTING_ENVIRONMENT:-Docker}",IDENTITY_WEBAPI_HTTP_PORT="${IDENTITY_WEBAPI_HTTP_PORT}",IDENTITY_WEBAPI_HTTPS_PORT="${IDENTITY_WEBAPI_HTTPS_PORT}"

[program:identity-server]
command=/app/run-identity-server.sh
directory=/app/identity-server
user=app
autostart=true
autorestart=true
startsecs=3
startretries=3
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
environment=ASPNETCORE_URLS="http://+:${IDENTITY_MANAGEMENT_HTTP_PORT};https://+:${IDENTITY_MANAGEMENT_HTTPS_PORT}",ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}",HOSTING_ENVIRONMENT="${HOSTING_ENVIRONMENT:-Docker}",IDENTITY_MANAGEMENT_HTTP_PORT="${IDENTITY_MANAGEMENT_HTTP_PORT}",IDENTITY_MANAGEMENT_HTTPS_PORT="${IDENTITY_MANAGEMENT_HTTPS_PORT}"
EOF
RUN chmod +x /etc/supervisor/conf.d/supervisord.conf

# Create log directories
RUN mkdir -p /var/log/supervisor /var/log/supervisor/identity-standalone /var/log/supervisor/identity-webapi /var/log/supervisor/identity-server

# Set proper ownership and permissions
RUN chown -R app:app /app
RUN chown -R app:app /var/log/supervisor
RUN chmod +x /app/identity-standalone/Viking.Identity.Server.Standalone.dll
RUN chmod +x /app/identity-webapi/Viking.Identity.Server.WebApi.dll
RUN chmod +x /app/identity-server/Viking.Identity.Server.WebManagement.dll

# Create startup script
RUN echo '#!/bin/bash' > /app/start-services.sh && \
    echo 'echo "Starting Identity Server Services..."' >> /app/start-services.sh && \
    echo 'echo "DEBUG: Container environment variables:"' >> /app/start-services.sh && \
    echo 'echo "ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"' >> /app/start-services.sh && \
    echo 'echo "HOSTING_ENVIRONMENT=${HOSTING_ENVIRONMENT}"' >> /app/start-services.sh && \
    echo '' >> /app/start-services.sh && \
    echo '# Start Visual Studio remote debugger if enabled' >> /app/start-services.sh && \
    echo 'if [ "$IDENTITY_ENABLE_REMOTE_DEBUG" = "true" ] || [ "$ASPNETCORE_ENVIRONMENT" = "Development" ] || [ "$ASPNETCORE_ENVIRONMENT" = "Debug" ]; then' >> /app/start-services.sh && \
    echo '  DEBUG_PORT=${REMOTE_DEBUG_PORT:-4024}' >> /app/start-services.sh && \
    echo '  echo "Starting Visual Studio remote debugger on port $DEBUG_PORT..."' >> /app/start-services.sh && \
    echo '  /vsdbg/vsdbg --interpreter=vscode --pauseEngineForDebugger &' >> /app/start-services.sh && \
    echo '  echo "Waiting for remote debugger to initialize..."' >> /app/start-services.sh && \
    echo '  sleep 5' >> /app/start-services.sh && \
    echo '  echo "Remote debugger ready on port $DEBUG_PORT"' >> /app/start-services.sh && \
    echo 'fi' >> /app/start-services.sh && \
    echo '' >> /app/start-services.sh && \
    echo '# Wait for database to be ready (if using external database)' >> /app/start-services.sh && \
    echo 'echo "Waiting for database connection..."' >> /app/start-services.sh && \
    echo 'sleep 1' >> /app/start-services.sh && \
    echo '' >> /app/start-services.sh && \
    echo '# Start supervisor' >> /app/start-services.sh && \
    echo 'exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf' >> /app/start-services.sh && \
    chmod +x /app/start-services.sh && \
    chown app:app /app/start-services.sh

# Create service wrapper scripts for logging with colors
RUN echo '#!/bin/bash' > /app/run-identity-standalone.sh && \
    echo '# Load environment variables from .env files' >> /app/run-identity-standalone.sh && \
    echo 'echo "[IS] Checking for .env files..."' >> /app/run-identity-standalone.sh && \
    echo 'echo "[IS] DEBUG: ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"' >> /app/run-identity-standalone.sh && \
    echo 'echo "[IS] DEBUG: HOSTING_ENVIRONMENT=${HOSTING_ENVIRONMENT}"' >> /app/run-identity-standalone.sh && \
    echo '# Convert line endings for .env files' >> /app/run-identity-standalone.sh && \
    echo 'if [ -f /app/.env ]; then dos2unix /app/.env 2>/dev/null || true; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then dos2unix /app/.env.${ASPNETCORE_ENVIRONMENT} 2>/dev/null || true; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then dos2unix /app/.env.${HOSTING_ENVIRONMENT} 2>/dev/null || true; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -f /app/.env ]; then echo "[IS] Found /app/.env - loading..."; set -a; source /app/.env; set +a; else echo "[IS] /app/.env not found"; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then echo "[IS] Found /app/.env.${ASPNETCORE_ENVIRONMENT} - loading..."; set -a; source /app/.env.${ASPNETCORE_ENVIRONMENT}; set +a; else echo "[IS] /app/.env.${ASPNETCORE_ENVIRONMENT} not found"; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then echo "[IS] Found /app/.env.${HOSTING_ENVIRONMENT} - loading..."; set -a; source /app/.env.${HOSTING_ENVIRONMENT}; set +a; else echo "[IS] /app/.env.${HOSTING_ENVIRONMENT} not found"; fi' >> /app/run-identity-standalone.sh && \
    echo 'ENV_FILES="/app/.env"' >> /app/run-identity-standalone.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then ENV_FILES="$ENV_FILES /app/.env.${ASPNETCORE_ENVIRONMENT}"; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then ENV_FILES="$ENV_FILES /app/.env.${HOSTING_ENVIRONMENT}"; fi' >> /app/run-identity-standalone.sh && \
    echo 'export $(cat $ENV_FILES 2>/dev/null | grep -v "^#" | grep -v "^$" | grep "=" | cut -d= -f1 | grep -E "^[A-Za-z_][A-Za-z0-9_]*$" | tr "\n" " ")' >> /app/run-identity-standalone.sh && \
    echo 'echo "[IS] Loaded environment variables:"' >> /app/run-identity-standalone.sh && \
    echo 'env | grep -E "(IDENTITY_|SQL_|SSL_|AUTHORITY)" | sort' >> /app/run-identity-standalone.sh && \
    echo 'cd /app/identity-standalone' >> /app/run-identity-standalone.sh && \
    echo 'dotnet Viking.Identity.Server.Standalone.dll 2>&1 | while IFS= read -r line; do echo -e "\033[32m[IS]\033[0m $line"; done' >> /app/run-identity-standalone.sh && \
    chmod +x /app/run-identity-standalone.sh && \
    chown app:app /app/run-identity-standalone.sh

RUN echo '#!/bin/bash' > /app/run-identity-webapi.sh && \
    echo '# Load environment variables from .env files' >> /app/run-identity-webapi.sh && \
    echo 'echo "[API] Checking for .env files..."' >> /app/run-identity-webapi.sh && \
    echo 'echo "[API] DEBUG: ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"' >> /app/run-identity-webapi.sh && \
    echo 'echo "[API] DEBUG: HOSTING_ENVIRONMENT=${HOSTING_ENVIRONMENT}"' >> /app/run-identity-webapi.sh && \
    echo '# Convert line endings for .env files' >> /app/run-identity-webapi.sh && \
    echo 'if [ -f /app/.env ]; then dos2unix /app/.env 2>/dev/null || true; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then dos2unix /app/.env.${ASPNETCORE_ENVIRONMENT} 2>/dev/null || true; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then dos2unix /app/.env.${HOSTING_ENVIRONMENT} 2>/dev/null || true; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -f /app/.env ]; then echo "[API] Found /app/.env - loading..."; set -a; source /app/.env; set +a; else echo "[API] /app/.env not found"; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then echo "[API] Found /app/.env.${ASPNETCORE_ENVIRONMENT} - loading..."; set -a; source /app/.env.${ASPNETCORE_ENVIRONMENT}; set +a; else echo "[API] /app/.env.${ASPNETCORE_ENVIRONMENT} not found"; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then echo "[API] Found /app/.env.${HOSTING_ENVIRONMENT} - loading..."; set -a; source /app/.env.${HOSTING_ENVIRONMENT}; set +a; else echo "[API] /app/.env.${HOSTING_ENVIRONMENT} not found"; fi' >> /app/run-identity-webapi.sh && \
    echo 'ENV_FILES="/app/.env"' >> /app/run-identity-webapi.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then ENV_FILES="$ENV_FILES /app/.env.${ASPNETCORE_ENVIRONMENT}"; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then ENV_FILES="$ENV_FILES /app/.env.${HOSTING_ENVIRONMENT}"; fi' >> /app/run-identity-webapi.sh && \
    echo 'export $(cat $ENV_FILES 2>/dev/null | grep -v "^#" | grep -v "^$" | grep "=" | cut -d= -f1 | grep -E "^[A-Za-z_][A-Za-z0-9_]*$" | tr "\n" " ")' >> /app/run-identity-webapi.sh && \
    echo 'cd /app/identity-webapi' >> /app/run-identity-webapi.sh && \
    echo 'dotnet Viking.Identity.Server.WebApi.dll 2>&1 | while IFS= read -r line; do echo -e "\033[34m[API]\033[0m $line"; done' >> /app/run-identity-webapi.sh && \
    chmod +x /app/run-identity-webapi.sh && \
    chown app:app /app/run-identity-webapi.sh

RUN echo '#!/bin/bash' > /app/run-identity-server.sh && \
    echo '# Load environment variables from .env files' >> /app/run-identity-server.sh && \
    echo 'echo "[MGMT] Checking for .env files..."' >> /app/run-identity-server.sh && \
    echo 'echo "[MGMT] DEBUG: ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"' >> /app/run-identity-server.sh && \
    echo 'echo "[MGMT] DEBUG: HOSTING_ENVIRONMENT=${HOSTING_ENVIRONMENT}"' >> /app/run-identity-server.sh && \
    echo '# Convert line endings for .env files' >> /app/run-identity-server.sh && \
    echo 'if [ -f /app/.env ]; then dos2unix /app/.env 2>/dev/null || true; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then dos2unix /app/.env.${ASPNETCORE_ENVIRONMENT} 2>/dev/null || true; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then dos2unix /app/.env.${HOSTING_ENVIRONMENT} 2>/dev/null || true; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -f /app/.env ]; then echo "[MGMT] Found /app/.env - loading..."; set -a; source /app/.env; set +a; else echo "[MGMT] /app/.env not found"; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then echo "[MGMT] Found /app/.env.${ASPNETCORE_ENVIRONMENT} - loading..."; set -a; source /app/.env.${ASPNETCORE_ENVIRONMENT}; set +a; else echo "[MGMT] /app/.env.${ASPNETCORE_ENVIRONMENT} not found"; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then echo "[MGMT] Found /app/.env.${HOSTING_ENVIRONMENT} - loading..."; set -a; source /app/.env.${HOSTING_ENVIRONMENT}; set +a; else echo "[MGMT] /app/.env.${HOSTING_ENVIRONMENT} not found"; fi' >> /app/run-identity-server.sh && \
    echo 'ENV_FILES="/app/.env"' >> /app/run-identity-server.sh && \
    echo 'if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then ENV_FILES="$ENV_FILES /app/.env.${ASPNETCORE_ENVIRONMENT}"; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then ENV_FILES="$ENV_FILES /app/.env.${HOSTING_ENVIRONMENT}"; fi' >> /app/run-identity-server.sh && \
    echo 'export $(cat $ENV_FILES 2>/dev/null | grep -v "^#" | grep -v "^$" | grep "=" | cut -d= -f1 | grep -E "^[A-Za-z_][A-Za-z0-9_]*$" | tr "\n" " ")' >> /app/run-identity-server.sh && \
    echo 'cd /app/identity-server' >> /app/run-identity-server.sh && \
    echo 'dotnet Viking.Identity.Server.WebManagement.dll 2>&1 | while IFS= read -r line; do echo -e "\033[33m[MGMT]\033[0m $line"; done' >> /app/run-identity-server.sh && \
    chmod +x /app/run-identity-server.sh && \
    chown app:app /app/run-identity-server.sh

# Health check script
RUN cat > /app/health-check.sh <<EOF
#!/bin/bash
# Check if all services are running
curl -f http://localhost:5000/.well-known/openid-configuration || exit 1
curl -f http://localhost:6000/health || exit 1
curl -f http://localhost:4000/ || exit 1
EOF

RUN chmod +x /app/health-check.sh
RUN sed -i 's/\r$//' /app/*.sh

# Switch to non-root user
USER 1654

# Use supervisor to manage all services
ENTRYPOINT ["/app/start-services.sh"]

