#!/bin/bash

echo "Starting Identity Server Services..."
echo "DEBUG: Container environment variables:"
echo "ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"
echo "HOSTING_ENVIRONMENT=${HOSTING_ENVIRONMENT}"

# Load environment variables from .env files
echo "Loading environment variables from .env files..."
if [ -f /app/.env ]; then
    echo "Found /app/.env - loading..."
    set -a
    source /app/.env
    set +a
fi

if [ -n "${ASPNETCORE_ENVIRONMENT}" ] && [ -f "/app/.env.${ASPNETCORE_ENVIRONMENT}" ]; then
    echo "Found /app/.env.${ASPNETCORE_ENVIRONMENT} - loading..."
    set -a
    source "/app/.env.${ASPNETCORE_ENVIRONMENT}"
    set +a
fi

if [ -n "${HOSTING_ENVIRONMENT}" ] && [ -f "/app/.env.${HOSTING_ENVIRONMENT}" ]; then
    echo "Found /app/.env.${HOSTING_ENVIRONMENT} - loading..."
    set -a
    source "/app/.env.${HOSTING_ENVIRONMENT}"
    set +a
fi

# Read SQL Server password from secret file and construct connection strings
if [ -f "${SQL_SERVER_PASSWORD_FILE:-/app/secrets/sql_server_password.txt}" ]; then
    SQL_SERVER_PASSWORD=$(cat "${SQL_SERVER_PASSWORD_FILE}" | tr -d '\n\r ')
    export SQL_SERVER_PASSWORD
    
    # Construct connection strings with password from secret
    export ConnectionStrings__IdentityConnection="Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=${SQL_SERVER_IDENTITY_DB};Trusted_Connection=False;User ID=${SQL_SERVER_USER};Password=${SQL_SERVER_PASSWORD};MultipleActiveResultSets=true;TrustServerCertificate=True"
    export ConnectionStrings__ConfigConnection="Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=${SQL_SERVER_CONFIG_DB};Trusted_Connection=False;User ID=${SQL_SERVER_USER};Password=${SQL_SERVER_PASSWORD};MultipleActiveResultSets=true;TrustServerCertificate=True"
    export ConnectionStrings__PersistedGrantConnection="Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=${SQL_SERVER_GRANTS_DB};Trusted_Connection=False;User ID=${SQL_SERVER_USER};Password=${SQL_SERVER_PASSWORD};MultipleActiveResultSets=true;TrustServerCertificate=True"
    echo "Database connection strings constructed from secret file"
else
    echo "WARNING: SQL Server password secret file not found. Connection strings may not be configured correctly."
fi

# Set SSL certificate path defaults before certificate acquisition
# Always use absolute paths that match appsettings.Docker.json
# If SSL_CERT_PATH is set to a relative path (like ./certs/fullchain.pem),
# convert it to the absolute path /app/secrets/certs/fullchain.pem
SSL_CERT_PATH=${SSL_CERT_PATH:-/app/secrets/certs/fullchain.pem}
SSL_KEY_PATH=${SSL_KEY_PATH:-/app/secrets/certs/privkey.pem}

# Convert relative paths or old paths to the correct absolute path
if [[ "$SSL_CERT_PATH" != /* ]]; then
    # Relative path - always use /app/secrets/certs/ regardless of what the relative path was
    SSL_CERT_PATH="/app/secrets/certs/fullchain.pem"
elif [[ "$SSL_CERT_PATH" == "/app/certs/"* ]]; then
    # Old path /app/certs/ - convert to /app/secrets/certs/
    SSL_CERT_PATH="/app/secrets/certs/$(basename "$SSL_CERT_PATH")"
fi

if [[ "$SSL_KEY_PATH" != /* ]]; then
    # Relative path - always use /app/secrets/certs/ regardless of what the relative path was
    SSL_KEY_PATH="/app/secrets/certs/privkey.pem"
elif [[ "$SSL_KEY_PATH" == "/app/certs/"* ]]; then
    # Old path /app/certs/ - convert to /app/secrets/certs/
    SSL_KEY_PATH="/app/secrets/certs/$(basename "$SSL_KEY_PATH")"
fi

export SSL_CERT_PATH
export SSL_KEY_PATH
echo "SSL_CERT_PATH set to: ${SSL_CERT_PATH}"
echo "SSL_KEY_PATH set to: ${SSL_KEY_PATH}"

# Ensure certificate directory exists (even if certificate acquisition fails)
mkdir -p "$(dirname "${SSL_CERT_PATH}")"

# Acquire Let's Encrypt certificate if configured
if [ -n "${LETSENCRYPT_DOMAIN}" ]; then
    echo "Acquiring Let's Encrypt certificate..."
    /app/acquire-certificate.sh || {
        echo "WARNING: Certificate acquisition failed, continuing with existing certificates..."
        echo "WARNING: If certificates don't exist, services will use development certificates or fail to start"
    }
else
    echo "LETSENCRYPT_DOMAIN not set, skipping certificate acquisition"
    echo "Services will use certificates from SSL_CERT_PATH if available, or development certificates"
fi

# Start Visual Studio remote debugger if enabled
if [ "$IDENTITY_ENABLE_REMOTE_DEBUG" = "true" ] || [ "$ASPNETCORE_ENVIRONMENT" = "Development" ] || [ "$ASPNETCORE_ENVIRONMENT" = "Debug" ]; then
    DEBUG_PORT=${REMOTE_DEBUG_PORT:-4024}
    echo "Starting Visual Studio remote debugger on port $DEBUG_PORT..."
    /vsdbg/vsdbg --interpreter=vscode --pauseEngineForDebugger &
    echo "Waiting for remote debugger to initialize..."
    sleep 5
    echo "Remote debugger ready on port $DEBUG_PORT"
fi

# Wait for database to be ready (if using external database)
echo "Waiting for database connection..."
sleep 1

# Start supervisor
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf

