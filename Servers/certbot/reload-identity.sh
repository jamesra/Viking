#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="${IDENTITY_CONTAINER_NAME:-identity-all-services}"
LETSENCRYPT_PRIMARY_DOMAIN="${LETSENCRYPT_PRIMARY_DOMAIN:-}"

# Copy the live lineage with the longest remaining validity onto the canonical
# path Identity Server loads (live/${LETSENCRYPT_PRIMARY_DOMAIN}/). Certbot may
# have created identity.example.com-0001 because that canonical directory was
# seeded as plain files and blocked a new lineage under the original name.

remaining_days() {
  local pem="$1"
  local days=0
  local n
  for n in 0 7 14 30 45 60 75 90 120 150 180; do
    if openssl x509 -in "${pem}" -checkend $((n * 86400)) >/dev/null 2>&1; then
      days="${n}"
    else
      break
    fi
  done
  echo "${days}"
}

promote_newest_live_cert() {
  local domain="$1"
  local live_root="/etc/letsencrypt/live"
  local canonical_dir="${live_root}/${domain}"
  local candidate
  local best_fullchain=""
  local best_days=-1
  local best_lineage=-1
  local days
  local lineage

  shopt -s nullglob
  for candidate in "${live_root}/${domain}"*/fullchain.pem; do
    if [ ! -f "${candidate}" ]; then
      continue
    fi
    days="$(remaining_days "${candidate}")"
    lineage=0
    if [ -L "$(dirname "${candidate}")/cert.pem" ]; then
      lineage=1
    fi
    if [ "${days}" -gt "${best_days}" ] || { [ "${days}" -eq "${best_days}" ] && [ "${lineage}" -ge "${best_lineage}" ]; }; then
      best_days="${days}"
      best_lineage="${lineage}"
      best_fullchain="${candidate}"
    fi
  done
  shopt -u nullglob

  if [ -z "${best_fullchain}" ]; then
    echo "[certbot] No live fullchain.pem found for ${domain}."
    return 1
  fi

  local best_dir
  local best_key
  best_dir="$(dirname "${best_fullchain}")"
  best_key="${best_dir}/privkey.pem"

  if [ "${best_dir}" = "${canonical_dir}" ]; then
    echo "[certbot] Canonical ${canonical_dir} already holds the newest lineage (valid >= ${best_days}d)."
    return 2
  fi

  mkdir -p "${canonical_dir}"
  local before=""
  if [ -f "${canonical_dir}/fullchain.pem" ]; then
    before="$(md5sum "${canonical_dir}/fullchain.pem" | awk '{print $1}')"
  fi

  cp "${best_fullchain}" "${canonical_dir}/fullchain.pem"
  if [ -f "${best_key}" ]; then
    cp "${best_key}" "${canonical_dir}/privkey.pem"
  fi
  # Identity Server runs as a non-root app user and the letsencrypt volume is
  # mounted read-only there; world-readable copies are required.
  chmod 644 "${canonical_dir}/fullchain.pem" "${canonical_dir}/privkey.pem" || true

  local after
  after="$(md5sum "${canonical_dir}/fullchain.pem" | awk '{print $1}')"
  echo "[certbot] Promoted ${best_dir} (valid >= ${best_days}d) to ${canonical_dir}."
  if [ "${before}" = "${after}" ]; then
    return 2
  fi
  return 0
}

find_identity_container() {
  local id
  if docker container inspect "${CONTAINER_NAME}" >/dev/null 2>&1; then
    echo "${CONTAINER_NAME}"
    return 0
  fi

  id="$(docker ps -q --filter "label=com.docker.compose.service=${CONTAINER_NAME}" | head -n 1 || true)"
  if [ -n "${id}" ]; then
    echo "${id}"
    return 0
  fi

  id="$(docker ps -q --filter "name=${CONTAINER_NAME}" | head -n 1 || true)"
  if [ -n "${id}" ]; then
    echo "${id}"
    return 0
  fi

  return 1
}

needs_restart=1
if [ -n "${LETSENCRYPT_PRIMARY_DOMAIN}" ]; then
  set +e
  promote_newest_live_cert "${LETSENCRYPT_PRIMARY_DOMAIN}"
  promote_status=$?
  set -e
  if [ "${promote_status}" -eq 2 ]; then
    echo "[certbot] Canonical certificate already matches the newest lineage; skip restart."
    needs_restart=0
  elif [ "${promote_status}" -ne 0 ]; then
    needs_restart=0
  fi
fi

if [ "${needs_restart}" -eq 0 ]; then
  exit 0
fi

if target="$(find_identity_container)"; then
  echo "[certbot] Certificate changed; restarting ${target} to pick up new files..."
  docker restart "${target}" >/dev/null
else
  echo "[certbot] No running container matched IDENTITY_CONTAINER_NAME=${CONTAINER_NAME} (compose service or name)."
fi
