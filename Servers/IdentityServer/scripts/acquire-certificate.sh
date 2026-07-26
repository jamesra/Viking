#!/bin/bash
# Don't use set -e here - we want to handle errors gracefully and continue
# set -e

# Note: This script runs before services start, so port 80 should be available
# Certbot's --standalone mode will start a temporary HTTP server on port 80
# The container has NET_BIND_SERVICE capability to allow binding to port 80

DOMAIN="${LETSENCRYPT_DOMAIN}"
EMAIL="${LETSENCRYPT_EMAIL:-}"
DNS_PLUGIN="${LETSENCRYPT_DNS_PLUGIN:-}"
CLOUDFLARE_EMAIL="${CLOUDFLARE_EMAIL:-}"
CLOUDFLARE_API_KEY_FILE="${CLOUDFLARE_API_KEY_FILE:-/app/secrets/cloudflare_api_key.txt}"
CERT_DIR="/etc/letsencrypt/live/${DOMAIN}"
CERT_ARCHIVE="/etc/letsencrypt/archive/${DOMAIN}"
# Read SSL certificate paths from environment (defaults set in start-services.sh)
SSL_CERT_PATH="${SSL_CERT_PATH:-/app/secrets/certs/fullchain.pem}"
SSL_KEY_PATH="${SSL_KEY_PATH:-/app/secrets/certs/privkey.pem}"
# Use writable directories for certbot logs and work
CERTBOT_CONFIG_DIR="/etc/letsencrypt"
CERTBOT_WORK_DIR="/var/lib/letsencrypt"
CERTBOT_LOGS_DIR="/var/log/letsencrypt"

# Check if domain is configured
if [ -z "$DOMAIN" ]; then
    echo "[CERT] LETSENCRYPT_DOMAIN not set, skipping certificate acquisition"
    exit 0
fi

# Create certificate directory if it doesn't exist
mkdir -p "$(dirname "${SSL_CERT_PATH}")"

# Check if certificate already exists and is valid at SSL_CERT_PATH
if [ -f "${SSL_CERT_PATH}" ] && [ -f "${SSL_KEY_PATH}" ]; then
    echo "[CERT] Certificate files exist at SSL_CERT_PATH, checking validity..."
    
    # Check certificate expiration using SSL_CERT_PATH
    if command -v openssl >/dev/null 2>&1; then
        EXPIRY_DATE=$(openssl x509 -enddate -noout -in "${SSL_CERT_PATH}" 2>/dev/null | cut -d= -f2)
        if [ -n "$EXPIRY_DATE" ]; then
            EXPIRY_EPOCH=$(date -d "$EXPIRY_DATE" +%s 2>/dev/null || date -j -f "%b %d %H:%M:%S %Y" "$EXPIRY_DATE" +%s 2>/dev/null || echo "")
            if [ -n "$EXPIRY_EPOCH" ]; then
                CURRENT_EPOCH=$(date +%s)
                DAYS_UNTIL_EXPIRY=$(( ($EXPIRY_EPOCH - $CURRENT_EPOCH) / 86400 ))
                
                # Calculate percentage expired (Let's Encrypt certs are valid for 90 days)
                # Renew when 90% expired = 9 days remaining
                PERCENT_EXPIRED=$(( (90 - DAYS_UNTIL_EXPIRY) * 100 / 90 ))
                
                if [ $DAYS_UNTIL_EXPIRY -gt 9 ]; then
                    echo "[CERT] Certificate exists and is valid for ${DAYS_UNTIL_EXPIRY} more days (${PERCENT_EXPIRED}% expired). Using existing certificate."
                    exit 0
                else
                    echo "[CERT] Certificate expires in ${DAYS_UNTIL_EXPIRY} days (${PERCENT_EXPIRED}% expired, >= 90%), will renew"
                fi
            fi
        fi
    fi
else
    echo "[CERT] Certificate files not found at SSL_CERT_PATH (${SSL_CERT_PATH}), will acquire new certificate"
fi

# Obtain or renew certificate
echo "[CERT] Requesting Let's Encrypt certificate for domain: ${DOMAIN}"

# Ensure certbot directories exist and have proper permissions
mkdir -p /etc/letsencrypt/live
mkdir -p /etc/letsencrypt/archive
mkdir -p /var/lib/letsencrypt

# Function to build base certbot command
build_base_certbot_cmd() {
    local cmd="certbot certonly --config-dir ${CERTBOT_CONFIG_DIR} --work-dir ${CERTBOT_WORK_DIR} --logs-dir ${CERTBOT_LOGS_DIR}"
    cmd="${cmd} --non-interactive"
    cmd="${cmd} --keep-until-expiring"
    cmd="${cmd} --agree-tos"
    
    if [ -n "$EMAIL" ]; then
        cmd="${cmd} --email ${EMAIL}"
    else
        cmd="${cmd} --register-unsafely-without-email"
    fi
    
    cmd="${cmd} -d ${DOMAIN}"
    echo "$cmd"
}

# Function to prepare DNS plugin credentials
prepare_dns_credentials() {
    local plugin="$1"
    local credentials_file="/tmp/cloudflare-credentials.ini"
    
    # Always remove any existing file first to ensure clean state
    rm -f "${credentials_file}"
    
    if [ "$plugin" = "cloudflare" ]; then
        # Read API key from secret file (trim all whitespace including newlines)
        if [ -f "$CLOUDFLARE_API_KEY_FILE" ]; then
            CLOUDFLARE_API_KEY=$(cat "$CLOUDFLARE_API_KEY_FILE" | tr -d '\n\r\t ' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
            CLOUDFLARE_API_KEY_LEN=${#CLOUDFLARE_API_KEY}
            echo "[CERT] Cloudflare API key loaded from secret file (length: ${CLOUDFLARE_API_KEY_LEN} characters)"
            
            # DO NOT export CLOUDFLARE_API_KEY as environment variable
            # The Cloudflare plugin should ONLY read from the credentials file
            # Exporting it might cause the plugin to see both file and env var
        else
            echo "[CERT] ERROR: Cloudflare API key secret file not found: ${CLOUDFLARE_API_KEY_FILE}"
            return 1
        fi
        
        # Validate API key is present
        if [ -z "$CLOUDFLARE_API_KEY" ]; then
            echo "[CERT] ERROR: Cloudflare API key is empty or not configured."
            return 1
        fi
        
        # Determine format based on key length (per Cloudflare plugin documentation):
        # - API Tokens are typically 40+ characters and use ONLY dns_cloudflare_api_token
        # - Global API Keys are typically 37 characters and require dns_cloudflare_email + dns_cloudflare_api_key
        if [ "$CLOUDFLARE_API_KEY_LEN" -ge 40 ]; then
            # API Token format - ONLY the token, no email, no api_key field
            echo "[CERT] Using Cloudflare API Token format (length: ${CLOUDFLARE_API_KEY_LEN} chars)"
            # Write ONLY the token line - ensure no trailing whitespace or newlines
            printf "dns_cloudflare_api_token = %s\n" "${CLOUDFLARE_API_KEY}" > "${credentials_file}"
        else
            # Global API Key format - email + api_key, NO token field
            echo "[CERT] Using Cloudflare Global API Key format (length: ${CLOUDFLARE_API_KEY_LEN} chars)"
            if [ -z "$CLOUDFLARE_EMAIL" ]; then
                echo "[CERT] ERROR: Global API Key requires CLOUDFLARE_EMAIL to be set"
                return 1
            fi
            # Write ONLY email and api_key lines, NO token - ensure clean formatting
            {
                printf "dns_cloudflare_email = %s\n" "${CLOUDFLARE_EMAIL}"
                printf "dns_cloudflare_api_key = %s\n" "${CLOUDFLARE_API_KEY}"
            } > "${credentials_file}"
        fi
        
        chmod 600 "${credentials_file}"
        
        # Verify file contains ONLY one format (critical check)
        # Count occurrences of each field
        TOKEN_COUNT=$(grep -c "^dns_cloudflare_api_token" "${credentials_file}" 2>/dev/null | tr -d '\n\r' || echo "0")
        KEY_COUNT=$(grep -c "^dns_cloudflare_api_key" "${credentials_file}" 2>/dev/null | tr -d '\n\r' || echo "0")
        EMAIL_COUNT=$(grep -c "^dns_cloudflare_email" "${credentials_file}" 2>/dev/null | tr -d '\n\r' || echo "0")
        
        TOKEN_COUNT=$((TOKEN_COUNT + 0))
        KEY_COUNT=$((KEY_COUNT + 0))
        EMAIL_COUNT=$((EMAIL_COUNT + 0))
        
        if [ "$TOKEN_COUNT" -gt 0 ] && [ "$KEY_COUNT" -gt 0 ]; then
            echo "[CERT] ERROR: Credentials file contains both API token and API key! This should never happen."
            echo "[CERT] File contents:"
            cat "${credentials_file}"
            rm -f "${credentials_file}"
            return 1
        fi
        
        if [ "$TOKEN_COUNT" -gt 0 ] && [ "$EMAIL_COUNT" -gt 0 ]; then
            echo "[CERT] ERROR: Credentials file contains both API token and email! This should never happen."
            echo "[CERT] File contents:"
            cat "${credentials_file}"
            rm -f "${credentials_file}"
            return 1
        fi
        
        # Debug: Verify file contents (show structure, not actual key)
        echo "[CERT] Cloudflare credentials file created with format:"
        if [ "$CLOUDFLARE_API_KEY_LEN" -ge 40 ]; then
            echo "[CERT]   - dns_cloudflare_api_token (API Token format)"
        else
            echo "[CERT]   - dns_cloudflare_email + dns_cloudflare_api_key (Global API Key format)"
        fi
        # Show file structure without revealing the key
        sed 's/=.*/=***HIDDEN***/' "${credentials_file}" | while read line; do
            echo "[CERT]   $line"
        done
        
        # Export the file path for use outside this function
        CLOUDFLARE_CREDENTIALS_FILE="${credentials_file}"
        
        return 0
    elif [ "$plugin" = "route53" ]; then
        if [ -z "$AWS_ACCESS_KEY_ID" ] || [ -z "$AWS_SECRET_ACCESS_KEY" ]; then
            echo "[CERT] ERROR: AWS Route53 credentials not configured."
            return 1
        fi
        return 0
    elif [ "$plugin" = "digitalocean" ]; then
        if [ -z "$DO_AUTH_TOKEN" ]; then
            echo "[CERT] ERROR: DigitalOcean credentials not configured."
            return 1
        fi
        return 0
    fi
    return 0
}

# Try HTTP-01 authentication first (standalone mode)
# Note: Certbot uses port 80 inside the container (mapped from external LETSENCRYPT_HTTP_PORT)
echo "[CERT] Attempting HTTP-01 authentication (standalone mode on port 80)..."

# Check if port 80 is already in use
if command -v netstat >/dev/null 2>&1 || command -v ss >/dev/null 2>&1; then
    if command -v ss >/dev/null 2>&1; then
        PORT_IN_USE=$(ss -tlnp | grep -c ":80 " || echo "0")
    else
        PORT_IN_USE=$(netstat -tlnp 2>/dev/null | grep -c ":80 " || echo "0")
    fi
    if [ "$PORT_IN_USE" -gt 0 ]; then
        echo "[CERT] WARNING: Port 80 appears to be in use. This may prevent HTTP-01 from working."
        echo "[CERT] Processes using port 80:"
        if command -v ss >/dev/null 2>&1; then
            ss -tlnp | grep ":80 " || true
        else
            netstat -tlnp 2>/dev/null | grep ":80 " || true
        fi
    else
        echo "[CERT] Port 80 is available for certbot standalone server"
    fi
fi

# Check if we can bind to port 80 (test with a simple check)
if ! command -v timeout >/dev/null 2>&1; then
    echo "[CERT] Note: 'timeout' command not available, skipping port binding test"
else
    # Try to see if we can create a socket on port 80 (quick test)
    if python3 -c "import socket; s = socket.socket(socket.AF_INET, socket.SOCK_STREAM); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); result = s.bind(('0.0.0.0', 80)); s.close()" 2>/dev/null; then
        echo "[CERT] Port 80 binding test: SUCCESS"
    else
        echo "[CERT] Port 80 binding test: FAILED (may need root or NET_BIND_SERVICE capability)"
    fi
fi

CERTBOT_CMD=$(build_base_certbot_cmd)
CERTBOT_CMD="${CERTBOT_CMD} --standalone"
CERTBOT_CMD="${CERTBOT_CMD} --preferred-challenges http"

echo "[CERT] Executing: ${CERTBOT_CMD}"
CERTBOT_EXIT_CODE=0
CERTBOT_OUTPUT=$(eval "${CERTBOT_CMD}" 2>&1) || CERTBOT_EXIT_CODE=$?

# Show certbot output
echo "$CERTBOT_OUTPUT"

if [ $CERTBOT_EXIT_CODE -ne 0 ]; then
    echo "[CERT] HTTP-01 failed with exit code: $CERTBOT_EXIT_CODE"
    echo "[CERT] Common causes:"
    echo "[CERT]   - Port 80 is already in use by another service"
    echo "[CERT]   - Cannot bind to port 80 (missing NET_BIND_SERVICE capability or root privileges)"
    echo "[CERT]   - Domain not accessible from the internet on port 80"
    echo "[CERT]   - Firewall blocking port 80"
    echo "[CERT]   - Reverse proxy not forwarding port 80 to container (502 Bad Gateway)"
    echo "[CERT]   - Reverse proxy needs to forward: ${LETSENCRYPT_HTTP_PORT:-80} -> container:80"
fi

# If HTTP-01 failed, try DNS-01 as fallback
if [ $CERTBOT_EXIT_CODE -ne 0 ]; then
    echo "[CERT] HTTP-01 authentication failed (exit code: $CERTBOT_EXIT_CODE), falling back to DNS-01..."
    
    # Check if DNS plugin is specified
    if [ -z "$DNS_PLUGIN" ]; then
        echo "[CERT] ERROR: HTTP-01 failed and LETSENCRYPT_DNS_PLUGIN not set."
        echo "[CERT] DNS-01 authentication requires a DNS plugin."
        echo "[CERT] Available plugins: cloudflare, route53, digitalocean, google, azure, etc."
        echo "[CERT] Set LETSENCRYPT_DNS_PLUGIN to the appropriate plugin name."
        exit 1
    fi
    
    # Prepare DNS credentials
    if ! prepare_dns_credentials "$DNS_PLUGIN"; then
        echo "[CERT] ERROR: Failed to prepare DNS plugin credentials for ${DNS_PLUGIN}"
        exit 1
    fi
    
    # The credentials file should already be created by prepare_dns_credentials
    # Just verify it exists and has the correct format
    if [ "$DNS_PLUGIN" = "cloudflare" ]; then
        if [ -z "${CLOUDFLARE_CREDENTIALS_FILE}" ]; then
            CLOUDFLARE_CREDENTIALS_FILE="/tmp/cloudflare-credentials.ini"
        fi
        
        if [ ! -f "${CLOUDFLARE_CREDENTIALS_FILE}" ]; then
            echo "[CERT] ERROR: Cloudflare credentials file not found: ${CLOUDFLARE_CREDENTIALS_FILE}"
            exit 1
        fi
        
        # Final verification: ensure file has ONLY one format
        # Use tr to remove newlines and ensure we get a clean number
        HAS_TOKEN=$(grep -c "^dns_cloudflare_api_token" "${CLOUDFLARE_CREDENTIALS_FILE}" 2>/dev/null | tr -d '\n\r' || echo "0")
        HAS_KEY=$(grep -c "^dns_cloudflare_api_key" "${CLOUDFLARE_CREDENTIALS_FILE}" 2>/dev/null | tr -d '\n\r' || echo "0")
        HAS_EMAIL=$(grep -c "^dns_cloudflare_email" "${CLOUDFLARE_CREDENTIALS_FILE}" 2>/dev/null | tr -d '\n\r' || echo "0")
        
        # Convert to integers to avoid comparison issues
        HAS_TOKEN=$((HAS_TOKEN + 0))
        HAS_KEY=$((HAS_KEY + 0))
        HAS_EMAIL=$((HAS_EMAIL + 0))
        
        if [ "$HAS_TOKEN" -gt 0 ] && [ "$HAS_KEY" -gt 0 ]; then
            echo "[CERT] ERROR: Credentials file contains BOTH API token and API key formats!"
            echo "[CERT] File contents:"
            cat "${CLOUDFLARE_CREDENTIALS_FILE}"
            echo "[CERT] This violates Cloudflare plugin requirements. Exiting."
            exit 1
        fi
        
        if [ "$HAS_TOKEN" -gt 0 ] && [ "$HAS_EMAIL" -gt 0 ]; then
            echo "[CERT] ERROR: Credentials file contains BOTH API token and email!"
            echo "[CERT] File contents:"
            cat "${CLOUDFLARE_CREDENTIALS_FILE}"
            echo "[CERT] This violates Cloudflare plugin requirements. Exiting."
            exit 1
        fi
        
        # Show what format we detected
        if [ "$HAS_TOKEN" -gt 0 ]; then
            echo "[CERT] Credentials file verified: contains API Token format only (token lines: $HAS_TOKEN)"
        elif [ "$HAS_KEY" -gt 0 ] && [ "$HAS_EMAIL" -gt 0 ]; then
            echo "[CERT] Credentials file verified: contains Global API Key format only (key lines: $HAS_KEY, email lines: $HAS_EMAIL)"
        else
            echo "[CERT] WARNING: Credentials file format unclear (token:$HAS_TOKEN, key:$HAS_KEY, email:$HAS_EMAIL)"
            echo "[CERT] File contents:"
            cat "${CLOUDFLARE_CREDENTIALS_FILE}"
        fi
        
        # Show what format we detected
        if [ "$HAS_TOKEN" -gt 0 ]; then
            echo "[CERT] Credentials file verified: contains API Token format only (token lines: $HAS_TOKEN)"
        elif [ "$HAS_KEY" -gt 0 ] && [ "$HAS_EMAIL" -gt 0 ]; then
            echo "[CERT] Credentials file verified: contains Global API Key format only (key lines: $HAS_KEY, email lines: $HAS_EMAIL)"
        else
            echo "[CERT] WARNING: Credentials file format unclear (token:$HAS_TOKEN, key:$HAS_KEY, email:$HAS_EMAIL)"
            echo "[CERT] File contents:"
            cat "${CLOUDFLARE_CREDENTIALS_FILE}"
        fi
    fi
    
    # Build certbot command with DNS plugin
    CERTBOT_CMD=$(build_base_certbot_cmd)
    CERTBOT_CMD="${CERTBOT_CMD} --dns-${DNS_PLUGIN}"
    CERTBOT_CMD="${CERTBOT_CMD} --preferred-challenges dns"
    
    if [ "$DNS_PLUGIN" = "cloudflare" ] && [ -f "${CLOUDFLARE_CREDENTIALS_FILE}" ]; then
        CERTBOT_CMD="${CERTBOT_CMD} --dns-cloudflare-credentials ${CLOUDFLARE_CREDENTIALS_FILE}"
        echo "[CERT] Cloudflare credentials configured (email: ${CLOUDFLARE_EMAIL:-not set for API Token})"
    fi
    
    echo "[CERT] Using DNS plugin: ${DNS_PLUGIN}"
    echo "[CERT] Executing: ${CERTBOT_CMD}"
    
    # Execute certbot command with DNS-01
    eval "${CERTBOT_CMD}" 2>&1 || CERTBOT_EXIT_CODE=$?
fi

# Clean up Cloudflare credentials file if it was created
if [ -n "$DNS_PLUGIN" ] && [ "$DNS_PLUGIN" = "cloudflare" ] && [ -f "${CLOUDFLARE_CREDENTIALS_FILE}" ]; then
    rm -f "${CLOUDFLARE_CREDENTIALS_FILE}"
    echo "[CERT] Cloudflare credentials file cleaned up"
fi

if [ $CERTBOT_EXIT_CODE -ne 0 ]; then
    echo "[CERT] ERROR: Certificate acquisition failed with both HTTP-01 and DNS-01 methods."
    if [ -n "$DNS_PLUGIN" ] && [ "$DNS_PLUGIN" = "cloudflare" ]; then
        echo "[CERT] For Cloudflare DNS-01, verify:"
        echo "[CERT]   - CLOUDFLARE_EMAIL is set correctly (if using Global API Key)"
        echo "[CERT]   - CLOUDFLARE_API_KEY is valid (Global API Key or API Token)"
        echo "[CERT]   - API key has DNS:Edit permissions for the domain"
    elif [ -n "$DNS_PLUGIN" ]; then
        echo "[CERT] For ${DNS_PLUGIN}, check the certbot-dns-${DNS_PLUGIN} documentation for required credentials."
    fi
    echo "[CERT] For HTTP-01, ensure port 80 is accessible and not blocked by a firewall or reverse proxy."
    exit 1
fi

# Verify certificate files exist in Let's Encrypt location
if [ -f "${CERT_DIR}/fullchain.pem" ] && [ -f "${CERT_DIR}/privkey.pem" ]; then
    echo "[CERT] Certificate successfully obtained"
    
    # Get file modification times before copying to detect changes
    OLD_CERT_MTIME=""
    OLD_KEY_MTIME=""
    if [ -f "${SSL_CERT_PATH}" ]; then
        OLD_CERT_MTIME=$(stat -c %Y "${SSL_CERT_PATH}" 2>/dev/null || stat -f %m "${SSL_CERT_PATH}" 2>/dev/null || echo "")
    fi
    if [ -f "${SSL_KEY_PATH}" ]; then
        OLD_KEY_MTIME=$(stat -c %Y "${SSL_KEY_PATH}" 2>/dev/null || stat -f %m "${SSL_KEY_PATH}" 2>/dev/null || echo "")
    fi
    
    # Copy certificates directly to SSL_CERT_PATH and SSL_KEY_PATH (not symlinks)
    # Remove any existing files/symlinks first to ensure clean copy
    echo "[CERT] Removing any existing certificate files at target location..."
    rm -f "${SSL_CERT_PATH}" "${SSL_KEY_PATH}" 2>/dev/null || true
    
    echo "[CERT] Copying certificates to ${SSL_CERT_PATH} and ${SSL_KEY_PATH}"
    # Use cp -L to follow symlinks and copy actual file content (not the symlink itself)
    cp -L "${CERT_DIR}/fullchain.pem" "${SSL_CERT_PATH}"
    cp -L "${CERT_DIR}/privkey.pem" "${SSL_KEY_PATH}"
    
    # Verify files are actual files, not symlinks
    if [ -L "${SSL_CERT_PATH}" ]; then
        echo "[CERT] ERROR: ${SSL_CERT_PATH} is still a symlink, removing and retrying..."
        rm -f "${SSL_CERT_PATH}"
        # Try copying from the archive directory directly
        ARCHIVE_FILE=$(readlink -f "${CERT_DIR}/fullchain.pem" 2>/dev/null || echo "")
        if [ -n "$ARCHIVE_FILE" ] && [ -f "$ARCHIVE_FILE" ]; then
            cp "$ARCHIVE_FILE" "${SSL_CERT_PATH}"
        else
            cp "${CERT_DIR}/fullchain.pem" "${SSL_CERT_PATH}"
        fi
    fi
    if [ -L "${SSL_KEY_PATH}" ]; then
        echo "[CERT] ERROR: ${SSL_KEY_PATH} is still a symlink, removing and retrying..."
        rm -f "${SSL_KEY_PATH}"
        # Try copying from the archive directory directly
        ARCHIVE_FILE=$(readlink -f "${CERT_DIR}/privkey.pem" 2>/dev/null || echo "")
        if [ -n "$ARCHIVE_FILE" ] && [ -f "$ARCHIVE_FILE" ]; then
            cp "$ARCHIVE_FILE" "${SSL_KEY_PATH}"
        else
            cp "${CERT_DIR}/privkey.pem" "${SSL_KEY_PATH}"
        fi
    fi
    
    # Set proper file permissions
    chmod 644 "${SSL_CERT_PATH}" 2>/dev/null || true
    chmod 600 "${SSL_KEY_PATH}" 2>/dev/null || true
    
    # Verify files are readable
    if [ ! -r "${SSL_CERT_PATH}" ]; then
        echo "[CERT] ERROR: ${SSL_CERT_PATH} is not readable after copy"
        exit 1
    fi
    if [ ! -r "${SSL_KEY_PATH}" ]; then
        echo "[CERT] ERROR: ${SSL_KEY_PATH} is not readable after copy"
        exit 1
    fi
    
    echo "[CERT] Certificates copied to ${SSL_CERT_PATH} and ${SSL_KEY_PATH}"
    
    # Check if certificates changed by comparing modification times
    NEW_CERT_MTIME=$(stat -c %Y "${SSL_CERT_PATH}" 2>/dev/null || stat -f %m "${SSL_CERT_PATH}" 2>/dev/null || echo "")
    NEW_KEY_MTIME=$(stat -c %Y "${SSL_KEY_PATH}" 2>/dev/null || stat -f %m "${SSL_KEY_PATH}" 2>/dev/null || echo "")
    
    CERT_CHANGED=0
    if [ -z "$OLD_CERT_MTIME" ] || [ "$OLD_CERT_MTIME" != "$NEW_CERT_MTIME" ]; then
        CERT_CHANGED=1
    fi
    if [ -z "$OLD_KEY_MTIME" ] || [ "$OLD_KEY_MTIME" != "$NEW_KEY_MTIME" ]; then
        CERT_CHANGED=1
    fi
    
    # Restart services if certificates changed
    if [ $CERT_CHANGED -eq 1 ]; then
        echo "[CERT] Certificates changed, restarting identity services..."
        # Wait a moment for files to be fully written
        sleep 1
        
        # Restart services via supervisorctl
        if command -v supervisorctl >/dev/null 2>&1; then
            supervisorctl restart identity-standalone 2>/dev/null || echo "[CERT] WARNING: Failed to restart identity-standalone"
            supervisorctl restart identity-webapi 2>/dev/null || echo "[CERT] WARNING: Failed to restart identity-webapi"
            supervisorctl restart identity-server 2>/dev/null || echo "[CERT] WARNING: Failed to restart identity-server"
            echo "[CERT] Identity services restart initiated"
        else
            echo "[CERT] WARNING: supervisorctl not found, services will need to be restarted manually"
        fi
    else
        echo "[CERT] Certificates unchanged, no service restart needed"
    fi
else
    echo "[CERT] ERROR: Certificate files not found after acquisition"
    echo "[CERT] Certificate files should be at: ${CERT_DIR}/fullchain.pem and ${CERT_DIR}/privkey.pem"
    echo "[CERT] This may indicate a problem with certificate acquisition"
    echo "[CERT] Services will attempt to use existing certificates or development certificates"
    # Ensure directory exists even if certificate acquisition failed
    mkdir -p "$(dirname "${SSL_CERT_PATH}")"
    # Don't exit with error - let services start and handle missing certificates gracefully
    exit 0
fi


