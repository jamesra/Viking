#!/usr/bin/env bash
set -euo pipefail

LETSENCRYPT_EMAIL="${LETSENCRYPT_EMAIL:-}"
LETSENCRYPT_PRIMARY_DOMAIN="${LETSENCRYPT_PRIMARY_DOMAIN:-}"
LETSENCRYPT_ADDITIONAL_DOMAINS="${LETSENCRYPT_ADDITIONAL_DOMAINS:-}"
CERTBOT_RENEW_INTERVAL_HOURS="${CERTBOT_RENEW_INTERVAL_HOURS:-12}"
CERTBOT_WEBROOT_PATH="${CERTBOT_WEBROOT_PATH:-/var/www/certbot}"
CERTBOT_KEY_TYPE="${CERTBOT_KEY_TYPE:-rsa}"
CERTBOT_STAGING="${CERTBOT_STAGING:-false}"
CERTBOT_AUTH_METHOD="${CERTBOT_AUTH_METHOD:-dns-cloudflare}"
CF_DNS_API_CREDENTIALS_FILE="${CF_DNS_API_CREDENTIALS_FILE:-/run/secrets/cloudflare.ini}"
CF_DNS_PROPAGATION_SECONDS="${CF_DNS_PROPAGATION_SECONDS:-60}"
CERTBOT_CERT_NAME="${CERTBOT_CERT_NAME:-${LETSENCRYPT_PRIMARY_DOMAIN}}"
STAGED_CF_CREDENTIALS="/tmp/cloudflare.ini"

if [ -n "${LETSENCRYPT_PRIMARY_DOMAIN}" ]; then
  seed_cert="/seed-certs/fullchain.pem"
  seed_key="/seed-certs/privkey.pem"
  target_dir="/etc/letsencrypt/live/${LETSENCRYPT_PRIMARY_DOMAIN}"
  if [ ! -f "${target_dir}/fullchain.pem" ] && [ -f "${seed_cert}" ] && [ -f "${seed_key}" ]; then
    echo "[certbot] Seeding certificate files from /seed-certs into ${target_dir}."
    mkdir -p "${target_dir}"
    cp "${seed_cert}" "${target_dir}/fullchain.pem"
    cp "${seed_key}" "${target_dir}/privkey.pem"
    chmod 644 "${target_dir}/privkey.pem"
  fi
fi

if [ -z "${LETSENCRYPT_EMAIL}" ] || [ -z "${LETSENCRYPT_PRIMARY_DOMAIN}" ]; then
  echo "[certbot] LETSENCRYPT_EMAIL and LETSENCRYPT_PRIMARY_DOMAIN are required."
  echo "[certbot] Waiting 10 minutes before retrying startup checks."
  sleep 600
  exec /opt/identity-certbot/renew-loop.sh
fi

broken_renewal="/etc/letsencrypt/renewal/${LETSENCRYPT_PRIMARY_DOMAIN}.conf"
if [ -f "${broken_renewal}" ] && [ ! -s "${broken_renewal}" ]; then
  echo "[certbot] Removing empty/broken renewal config: ${broken_renewal}"
  rm -f "${broken_renewal}"
fi

DOMAIN_ARGS=(-d "${LETSENCRYPT_PRIMARY_DOMAIN}")
if [ -n "${LETSENCRYPT_ADDITIONAL_DOMAINS}" ]; then
  IFS=',' read -ra EXTRA_DOMAINS <<< "${LETSENCRYPT_ADDITIONAL_DOMAINS}"
  for domain in "${EXTRA_DOMAINS[@]}"; do
    trimmed="$(echo "${domain}" | xargs)"
    if [ -n "${trimmed}" ]; then
      DOMAIN_ARGS+=(-d "${trimmed}")
    fi
  done
fi

COMMON_ARGS=(
  --non-interactive
  --agree-tos
  --email "${LETSENCRYPT_EMAIL}"
  --key-type "${CERTBOT_KEY_TYPE}"
)

if [ "${CERTBOT_STAGING}" = "true" ]; then
  COMMON_ARGS+=(--staging)
fi

CERT_NAME_ARGS=(--cert-name "${CERTBOT_CERT_NAME}")

case "${CERTBOT_AUTH_METHOD}" in
  dns-cloudflare)
    if [ ! -f "${CF_DNS_API_CREDENTIALS_FILE}" ]; then
      echo "[certbot] Cloudflare credentials file not found at ${CF_DNS_API_CREDENTIALS_FILE}."
      echo "[certbot] Waiting 10 minutes before retrying startup checks."
      sleep 600
      exec /opt/identity-certbot/renew-loop.sh
    fi
    cp "${CF_DNS_API_CREDENTIALS_FILE}" "${STAGED_CF_CREDENTIALS}"
    chmod 600 "${STAGED_CF_CREDENTIALS}"
    AUTH_ARGS=(
      --dns-cloudflare
      --dns-cloudflare-credentials "${STAGED_CF_CREDENTIALS}"
      --dns-cloudflare-propagation-seconds "${CF_DNS_PROPAGATION_SECONDS}"
    )
    ;;
  webroot)
    mkdir -p "${CERTBOT_WEBROOT_PATH}"
    AUTH_ARGS=(--webroot --webroot-path "${CERTBOT_WEBROOT_PATH}")
    ;;
  *)
    echo "[certbot] Unsupported CERTBOT_AUTH_METHOD: ${CERTBOT_AUTH_METHOD}"
    echo "[certbot] Valid options: dns-cloudflare, webroot"
    sleep 600
    exec /opt/identity-certbot/renew-loop.sh
    ;;
esac

cert_path="/etc/letsencrypt/live/${LETSENCRYPT_PRIMARY_DOMAIN}/fullchain.pem"

if [ ! -f "${cert_path}" ]; then
  echo "[certbot] No certificate found for ${LETSENCRYPT_PRIMARY_DOMAIN}; requesting initial certificate..."
  if ! certbot certonly "${AUTH_ARGS[@]}" "${COMMON_ARGS[@]}" "${CERT_NAME_ARGS[@]}" "${DOMAIN_ARGS[@]}"; then
    echo "[certbot] Initial certificate request failed; retrying in 10 minutes."
    sleep 600
    exec /opt/identity-certbot/renew-loop.sh
  fi
  /opt/identity-certbot/reload-identity.sh
elif ! openssl x509 -in "${cert_path}" -checkend 0 >/dev/null 2>&1; then
  echo "[certbot] Existing certificate for ${LETSENCRYPT_PRIMARY_DOMAIN} is expired; forcing renewal now."
  if ! certbot certonly "${AUTH_ARGS[@]}" "${COMMON_ARGS[@]}" "${CERT_NAME_ARGS[@]}" "${DOMAIN_ARGS[@]}" --force-renewal; then
    echo "[certbot] Forced renewal failed; retrying in 10 minutes."
    sleep 600
    exec /opt/identity-certbot/renew-loop.sh
  fi
  /opt/identity-certbot/reload-identity.sh
else
  echo "[certbot] Existing certificate is currently valid; running startup renew check."
fi

echo "[certbot] Starting renewal loop every ${CERTBOT_RENEW_INTERVAL_HOURS} hours."

# Always perform a renewal check on container startup.
if ! certbot renew \
  "${AUTH_ARGS[@]}" \
  "${COMMON_ARGS[@]}" \
  --deploy-hook "/opt/identity-certbot/reload-identity.sh"; then
  echo "[certbot] Startup renewal check failed; retrying loop in 10 minutes."
  sleep 600
fi

while true; do
  if ! certbot renew \
    "${AUTH_ARGS[@]}" \
    "${COMMON_ARGS[@]}" \
    --deploy-hook "/opt/identity-certbot/reload-identity.sh"; then
    echo "[certbot] Periodic renewal check failed; retrying in 10 minutes."
    sleep 600
    continue
  fi

  sleep_seconds=$((CERTBOT_RENEW_INTERVAL_HOURS * 3600))
  sleep "${sleep_seconds}"
done
