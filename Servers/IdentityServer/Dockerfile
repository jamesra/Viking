# Multi-service Dockerfile for Identity Server components
# This Dockerfile builds and runs IdentityServerStandalone, WebApi, and IdentityServer

# Build arguments for port configuration
ARG IDENTITY_STANDALONE_HTTP_PORT=5000
ARG IDENTITY_STANDALONE_HTTPS_PORT=5001
ARG IDENTITY_WEBAPI_HTTP_PORT=6000
ARG IDENTITY_WEBAPI_HTTPS_PORT=6001
ARG IDENTITY_MANAGEMENT_HTTP_PORT=4000
ARG IDENTITY_MANAGEMENT_HTTPS_PORT=4001

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Install supervisor for process management
RUN apt-get update && apt-get install -y supervisor curl && rm -rf /var/lib/apt/lists/*

# Create directories for each service
RUN mkdir -p /app/identity-standalone /app/identity-webapi /app/identity-server /app/DataProtectionKeys

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Docker

# Expose ports for all services
EXPOSE 5000 5001 6000 6001 4000 4001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

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
COPY IdentityServer/IdentityServer/ IdentityServer/IdentityServer/

# Build IdentityServerStandalone
WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN dotnet restore "IdentityServerStandalone.csproj"
RUN dotnet build "IdentityServerStandalone.csproj" -c $BUILD_CONFIGURATION -o /app/build/identity-standalone

# Build WebApi
WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebApi" 
RUN dotnet restore "Viking.Identity.Server.WebApi.csproj"
RUN dotnet build "Viking.Identity.Server.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build/identity-webapi

# Build IdentityServer (main management website)
WORKDIR "/src/IdentityServer/IdentityServer"
RUN dotnet restore "IdentityManagementWebsite.csproj"
RUN dotnet build "IdentityManagementWebsite.csproj" -c $BUILD_CONFIGURATION -o /app/build/identity-server

# Publish all services
FROM build AS publish

# Publish IdentityServerStandalone
WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN dotnet publish "IdentityServerStandalone.csproj" -c $BUILD_CONFIGURATION -o /app/publish/identity-standalone /p:UseAppHost=false --runtime linux-x64 --self-contained false

# Publish WebApi
WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebApi"
RUN dotnet publish "Viking.Identity.Server.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish/identity-webapi /p:UseAppHost=false --runtime linux-x64 --self-contained false

# Publish IdentityServer
WORKDIR "/src/IdentityServer/IdentityServer"
RUN dotnet publish "IdentityManagementWebsite.csproj" -c $BUILD_CONFIGURATION -o /app/publish/identity-server /p:UseAppHost=false --runtime linux-x64 --self-contained false

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
environment=ASPNETCORE_URLS="http://+:${IDENTITY_STANDALONE_HTTP_PORT};https://+:${IDENTITY_STANDALONE_HTTPS_PORT}",ASPNETCORE_ENVIRONMENT="Docker",IDENTITY_STANDALONE_HTTP_PORT="${IDENTITY_STANDALONE_HTTP_PORT}",IDENTITY_STANDALONE_HTTPS_PORT="${IDENTITY_STANDALONE_HTTPS_PORT}"

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
environment=ASPNETCORE_URLS="http://+:${IDENTITY_WEBAPI_HTTP_PORT};https://+:${IDENTITY_WEBAPI_HTTPS_PORT}",ASPNETCORE_ENVIRONMENT="Docker",IDENTITY_WEBAPI_HTTP_PORT="${IDENTITY_WEBAPI_HTTP_PORT}",IDENTITY_WEBAPI_HTTPS_PORT="${IDENTITY_WEBAPI_HTTPS_PORT}"

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
environment=ASPNETCORE_URLS="http://+:${IDENTITY_MANAGEMENT_HTTP_PORT};https://+:${IDENTITY_MANAGEMENT_HTTPS_PORT}",ASPNETCORE_ENVIRONMENT="Docker",IDENTITY_MANAGEMENT_HTTP_PORT="${IDENTITY_MANAGEMENT_HTTP_PORT}",IDENTITY_MANAGEMENT_HTTPS_PORT="${IDENTITY_MANAGEMENT_HTTPS_PORT}"
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
    echo 'if [ -f /app/.env.All ]; then echo "[IS] Found /app/.env.All - loading..."; set -a; source /app/.env.All; set +a; else echo "[IS] /app/.env.All not found"; fi' >> /app/run-identity-standalone.sh && \
    echo 'if [ -f /app/.env.All.${ASPNETCnORE_ENVIRONMENT} ]; then echo "[IS] Found /app/.env.All.${ASPNETCORE_ENVIRONMENT} - loading..."; set -a; source /app/.env.All.${ASPNETCORE_ENVIRONMENT}; set +a; else echo "[IS] /app/.env.All.${ASPNETCORE_ENVIRONMENT} not found"; fi' >> /app/run-identity-standalone.sh && \
    echo 'export $(cat /app/.env.All /app/.env.All.${ASPNETCORE_ENVIRONMENT} 2>/dev/null | grep -v "^#" | grep -v "^$" | cut -d= -f1 | tr "\n" " ")' >> /app/run-identity-standalone.sh && \
    echo 'echo "[IS] Loaded environment variables:"' >> /app/run-identity-standalone.sh && \
    echo 'env | grep -E "(IDENTITY_|SQL_|SSL_|AUTHORITY)" | sort' >> /app/run-identity-standalone.sh && \
    echo 'cd /app/identity-standalone' >> /app/run-identity-standalone.sh && \
    echo 'dotnet Viking.Identity.Server.Standalone.dll 2>&1 | while IFS= read -r line; do echo -e "\033[32m[IS]\033[0m $line"; done' >> /app/run-identity-standalone.sh && \
    chmod +x /app/run-identity-standalone.sh && \
    chown app:app /app/run-identity-standalone.sh

RUN echo '#!/bin/bash' > /app/run-identity-webapi.sh && \
    echo '# Load environment variables from .env files' >> /app/run-identity-webapi.sh && \
    echo 'echo "[API] Checking for .env files..."' >> /app/run-identity-webapi.sh && \
    echo 'if [ -f /app/.env.All ]; then echo "[API] Found /app/.env.All - loading..."; set -a; source /app/.env.All; set +a; else echo "[API] /app/.env.All not found"; fi' >> /app/run-identity-webapi.sh && \
    echo 'if [ -f /app/.env.All.${ASPNETCORE_ENVIRONMENT} ]; then echo "[API] Found /app/.env.All.${ASPNETCORE_ENVIRONMENT} - loading..."; set -a; source /app/.env.All.${ASPNETCORE_ENVIRONMENT}; set +a; else echo "[API] /app/.env.All.${ASPNETCORE_ENVIRONMENT} not found"; fi' >> /app/run-identity-webapi.sh && \
    echo 'export $(cat /app/.env.All /app/.env.All.${ASPNETCORE_ENVIRONMENT} 2>/dev/null | grep -v "^#" | grep -v "^$" | cut -d= -f1 | tr "\n" " ")' >> /app/run-identity-webapi.sh && \
    echo 'cd /app/identity-webapi' >> /app/run-identity-webapi.sh && \
    echo 'dotnet Viking.Identity.Server.WebApi.dll 2>&1 | while IFS= read -r line; do echo -e "\033[34m[API]\033[0m $line"; done' >> /app/run-identity-webapi.sh && \
    chmod +x /app/run-identity-webapi.sh && \
    chown app:app /app/run-identity-webapi.sh

RUN echo '#!/bin/bash' > /app/run-identity-server.sh && \
    echo '# Load environment variables from .env files' >> /app/run-identity-server.sh && \
    echo 'echo "[MGMT] Checking for .env files..."' >> /app/run-identity-server.sh && \
    echo 'if [ -f /app/.env.All ]; then echo "[MGMT] Found /app/.env.All - loading..."; set -a; source /app/.env.All; set +a; else echo "[MGMT] /app/.env.All not found"; fi' >> /app/run-identity-server.sh && \
    echo 'if [ -f /app/.env.All.${ASPNETCORE_ENVIRONMENT} ]; then echo "[MGMT] Found /app/.env.All.${ASPNETCORE_ENVIRONMENT} - loading..."; set -a; source /app/.env.All.${ASPNETCORE_ENVIRONMENT}; set +a; else echo "[MGMT] /app/.env.All.${ASPNETCORE_ENVIRONMENT} not found"; fi' >> /app/run-identity-server.sh && \
    echo 'export $(cat /app/.env.All /app/.env.All.${ASPNETCORE_ENVIRONMENT} 2>/dev/null | grep -v "^#" | grep -v "^$" | cut -d= -f1 | tr "\n" " ")' >> /app/run-identity-server.sh && \
    echo 'cd /app/identity-server' >> /app/run-identity-server.sh && \
    echo 'dotnet Viking.Identity.Server.WebManagement.dll 2>&1 | while IFS= read -r line; do echo -e "\033[33m[MGMT]\033[0m $line"; done' >> /app/run-identity-server.sh && \
    chmod +x /app/run-identity-server.sh && \
    chown app:app /app/run-identity-server.sh

# Health check script
RUN cat > /app/health-check.sh <<EOF
#!/bin/bash
# Check if all services are running
curl -f http://localhost:5001/.well-known/openid-configuration || exit 1
curl -f http://localhost:6001/health || exit 1
curl -f http://localhost:4001/ || exit 1
EOF

RUN chmod +x /app/health-check.sh

# Switch to non-root user
USER 1654

# Use supervisor to manage all services
ENTRYPOINT ["/app/start-services.sh"]

