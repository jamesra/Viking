#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="${IDENTITY_CONTAINER_NAME:-identity-all-services}"
LETSENCRYPT_PRIMARY_DOMAIN="${LETSENCRYPT_PRIMARY_DOMAIN:-}"

if [ -n "${LETSENCRYPT_PRIMARY_DOMAIN}" ]; then
  canonical_dir="/etc/letsencrypt/live/${LETSENCRYPT_PRIMARY_DOMAIN}"
  latest_fullchain=""
  for candidate in /etc/letsencrypt/live/${LETSENCRYPT_PRIMARY_DOMAIN}*/fullchain.pem; do
    if [ -f "${candidate}" ]; then
      latest_fullchain="${candidate}"
    fi
  done

  if [ -n "${latest_fullchain}" ]; then
    latest_dir="$(dirname "${latest_fullchain}")"
    latest_key="${latest_dir}/privkey.pem"
    mkdir -p "${canonical_dir}"
    cp "${latest_fullchain}" "${canonical_dir}/fullchain.pem"
    if [ -f "${latest_key}" ]; then
      cp "${latest_key}" "${canonical_dir}/privkey.pem"
    fi
    chmod 644 "${canonical_dir}/fullchain.pem" "${canonical_dir}/privkey.pem" || true
  fi
fi

if docker container inspect "${CONTAINER_NAME}" >/dev/null 2>&1; then
  echo "[certbot] Certificate changed; restarting ${CONTAINER_NAME} to pick up new files..."
  docker restart "${CONTAINER_NAME}" >/dev/null
else
  echo "[certbot] ${CONTAINER_NAME} container was not found."
fi
