#!/usr/bin/env bash
# viking-certs: the one certificate renewer for every Viking TLS service on this host.
#
# It keeps one certificate lineage (TLS_CERT_NAME) covering TLS_HOSTNAMES and publishes it to
#   /certs/${TLS_CERT_NAME}/{fullchain.pem,privkey.pem,cert.pem,chain.pem,cert.pfx,.renewed-at}
# (/certs is the host's DOCKER_CERTS_DIR). When the published files change it restarts every
# container labelled viking.tls-reload=true through the Docker socket, because the services read
# their PEM files only at start.
#
# TLS_MODE
#   letsencrypt  HTTP-01 through the webroot /var/www/certbot, which viking-proxy serves on port 80.
#   acme-eab     Any ACME CA with External Account Binding (InCommon/Sectigo): TLS_ACME_SERVER,
#                TLS_EAB_KID, TLS_EAB_HMAC_KEY (or TLS_EAB_HMAC_KEY_FILE). Same webroot authenticator.
#   manual       No ACME. Someone else (stack.ps1 certs manual, or a copy from another host) writes
#                fullchain.pem and privkey.pem into /certs/${TLS_CERT_NAME}/. This loop then only
#                derives cert.pem/chain.pem/cert.pfx and restarts labelled containers when they change.
#   (dns-cloudflare is kept from the old per-service renewers and still works, but is not documented.)
#
# Usage
#   renew-loop.sh                  loop forever (the container's default)
#   renew-loop.sh --once           issue/renew once, publish, exit nonzero on failure (stack.ps1 certs ...)
#   renew-loop.sh --publish-only   publish the current lineage (or re-derive manual files) and exit
#
# Old variable names (LETSENCRYPT_*, CERTBOT_*, ACME_*, SSL_CERT_PATH/SSL_KEY_PATH) are still read
# when the TLS_* name is unset, with a deprecation warning.
set -uo pipefail

RUN_MODE="loop"
case "${1:-}" in
  --once) RUN_MODE="once" ;;
  --publish-only) RUN_MODE="publish" ;;
  "") ;;
  *) echo "[viking-certs] Unknown argument: $1 (expected --once or --publish-only)"; exit 2 ;;
esac

log() { echo "[viking-certs] $*"; }

# fallback NEW OLD: when NEW is empty and OLD is set, copy OLD into NEW and warn.
fallback() {
  local new="$1" old="$2"
  if [ -z "${!new:-}" ] && [ -n "${!old:-}" ]; then
    log "WARNING: ${old} is deprecated; set ${new} instead."
    printf -v "${new}" '%s' "${!old}"
  fi
}

fallback TLS_EMAIL LETSENCRYPT_EMAIL
fallback TLS_RENEW_INTERVAL_HOURS CERTBOT_RENEW_INTERVAL_HOURS
fallback TLS_ACME_SERVER ACME_SERVER
fallback TLS_EAB_KID ACME_EAB_KID
fallback TLS_EAB_HMAC_KEY ACME_EAB_HMAC_KEY
fallback TLS_EAB_HMAC_KEY_FILE ACME_EAB_HMAC_KEY_FILE
fallback TLS_STAGING CERTBOT_STAGING
fallback TLS_KEY_TYPE CERTBOT_KEY_TYPE
fallback TLS_CERT_NAME CERTBOT_CERT_NAME
fallback TLS_CERT_NAME LETSENCRYPT_PRIMARY_DOMAIN

# Old renewers named the mode by its certbot authenticator.
if [ -z "${TLS_MODE:-}" ] && [ -n "${CERTBOT_AUTH_METHOD:-}" ]; then
  log "WARNING: CERTBOT_AUTH_METHOD is deprecated; set TLS_MODE instead."
  case "${CERTBOT_AUTH_METHOD}" in
    webroot) TLS_MODE="letsencrypt" ;;
    *) TLS_MODE="${CERTBOT_AUTH_METHOD}" ;;
  esac
fi

# Old renewers listed names as primary + additional.
if [ -z "${TLS_HOSTNAMES:-}" ] && [ -n "${LETSENCRYPT_PRIMARY_DOMAIN:-}" ]; then
  log "WARNING: LETSENCRYPT_PRIMARY_DOMAIN/LETSENCRYPT_ADDITIONAL_DOMAINS are deprecated; set TLS_HOSTNAMES instead."
  TLS_HOSTNAMES="${LETSENCRYPT_PRIMARY_DOMAIN}${LETSENCRYPT_ADDITIONAL_DOMAINS:+,${LETSENCRYPT_ADDITIONAL_DOMAINS}}"
fi

# Default TLS_HOSTNAMES: every non-empty VIKING_HOSTNAME_* in the environment (compose passes them all).
if [ -z "${TLS_HOSTNAMES:-}" ]; then
  TLS_HOSTNAMES="$(for v in $(compgen -v VIKING_HOSTNAME_ | sort); do [ -n "${!v}" ] && echo "${!v}"; done | awk '!seen[$0]++' | paste -sd, -)"
fi

# Names other stacks ask for (the segmentation model servers), one per line in *.names files. Read on every
# cycle, so a new model's name is on the next certificate request without restarting this container.
TLS_HOSTNAMES_BASE="${TLS_HOSTNAMES:-}"
TLS_NAMES_DIR="${TLS_NAMES_DIR:-/etc/viking-certs/names.d}"
refresh_hostnames() {
  local extra=""
  if compgen -G "${TLS_NAMES_DIR}/*.names" >/dev/null; then
    extra="$(cat "${TLS_NAMES_DIR}"/*.names | tr -d '\r' | sed 's/#.*//' | tr -s ' \t\n' '\n' | awk 'NF && !seen[$0]++' | paste -sd, -)"
  fi
  # A name under a wildcard in the list (*.segmentation.<domain> covers v3.segmentation.<domain>) is left out.
  TLS_HOSTNAMES="$(printf '%s,%s' "${TLS_HOSTNAMES_BASE}" "${extra}" | tr ',' '\n' | awk '
    NF && !seen[$0]++ { names[++n] = $0 }
    END {
      for (i = 1; i <= n; i++) {
        covered = 0
        if (names[i] !~ /^\*\./) {
          parent = names[i]; sub(/^[^.]*/, "", parent)
          for (j = 1; j <= n; j++) if (names[j] == "*" parent) covered = 1
        }
        if (!covered) print names[i]
      }
    }' | paste -sd, -)"
}
refresh_hostnames

TLS_MODE="${TLS_MODE:-manual}"
TLS_CERT_NAME="${TLS_CERT_NAME:-viking}"
TLS_RENEW_INTERVAL_HOURS="${TLS_RENEW_INTERVAL_HOURS:-12}"
TLS_RETRY_MINUTES="${TLS_RETRY_MINUTES:-10}"
TLS_MANUAL_POLL_SECONDS="${TLS_MANUAL_POLL_SECONDS:-300}"
TLS_KEY_TYPE="${TLS_KEY_TYPE:-rsa}"
TLS_PFX_PASSWORD="${TLS_PFX_PASSWORD:-}"
TLS_RELOAD_LABEL="${TLS_RELOAD_LABEL:-viking.tls-reload}"
TLS_WEBROOT="${TLS_WEBROOT:-/var/www/certbot}"
CF_DNS_API_CREDENTIALS_FILE="${CF_DNS_API_CREDENTIALS_FILE:-/run/secrets/viking-certs/cloudflare.ini}"
CF_DNS_PROPAGATION_SECONDS="${CF_DNS_PROPAGATION_SECONDS:-60}"
case "${TLS_STAGING:-0}" in 1|true|TRUE|True|yes) TLS_STAGING=1 ;; *) TLS_STAGING=0 ;; esac

CERTS_ROOT="/certs"
OUT_DIR="${CERTS_ROOT}/${TLS_CERT_NAME}"
LIVE_DIR="/etc/letsencrypt/live/${TLS_CERT_NAME}"

if [[ ! "${TLS_CERT_NAME}" =~ ^[A-Za-z0-9._-]+$ ]]; then
  log "TLS_CERT_NAME '${TLS_CERT_NAME}' must be a plain directory name (letters, digits, . _ -)."
  exit 2
fi

# ---------------------------------------------------------------------------------------------------
# Publishing
# ---------------------------------------------------------------------------------------------------

# Writes cert.pfx from the PEMs in $1 (IIS imports it). 3DES/SHA1 PBE keeps it importable on older
# Windows Server builds, which cannot read OpenSSL 3's default AES PKCS#12.
write_pfx() {
  local dir="$1" out="$2"
  local chain_args=()
  # A self-signed or single-certificate fullchain has an empty chain.pem; -certfile rejects that.
  if grep -q "BEGIN CERTIFICATE" "${dir}/chain.pem" 2>/dev/null; then
    chain_args=(-certfile "${dir}/chain.pem")
  fi
  openssl pkcs12 -export -out "${out}" -name "${TLS_CERT_NAME}" \
    -inkey "${dir}/privkey.pem" -in "${dir}/cert.pem" "${chain_args[@]}" \
    -certpbe PBE-SHA1-3DES -keypbe PBE-SHA1-3DES -macalg sha1 \
    -passout "pass:${TLS_PFX_PASSWORD}"
}

# Splits a fullchain into its first certificate (cert.pem) and the rest (chain.pem).
split_fullchain() {
  local fullchain="$1" dir="$2"
  awk -v cert="${dir}/cert.pem" -v chain="${dir}/chain.pem" '
    BEGIN { printf "" > chain }
    /-----BEGIN CERTIFICATE-----/ { n++ }
    { if (n <= 1) print > cert; else print > chain }
  ' "${fullchain}"
}

content_hash() {
  cat "$1/fullchain.pem" "$1/privkey.pem" 2>/dev/null | sha256sum | awk '{print $1}'
}

# Restarts every running container labelled ${TLS_RELOAD_LABEL}=true (and, for old env files, the
# names in CERTBOT_RESTART_CONTAINER). Without the Docker socket it only logs.
restart_labelled() {
  if [ ! -S /var/run/docker.sock ]; then
    log "No Docker socket at /var/run/docker.sock; restart the TLS services yourself."
    return 0
  fi
  local self ids id name legacy
  self="$(hostname)"
  ids="$(docker ps -q --filter "label=${TLS_RELOAD_LABEL}=true" 2>/dev/null || true)"
  legacy="${CERTBOT_RESTART_CONTAINER:-${IDENTITY_CONTAINER_NAME:-}}"
  if [ -n "${legacy}" ]; then
    log "WARNING: CERTBOT_RESTART_CONTAINER/IDENTITY_CONTAINER_NAME are deprecated; label the service ${TLS_RELOAD_LABEL}: \"true\" instead."
    IFS=',' read -ra names <<< "${legacy}"
    for name in "${names[@]}"; do
      name="$(echo "${name}" | xargs)"
      [ -n "${name}" ] || continue
      ids="${ids} $(docker ps -q --filter "label=com.docker.compose.service=${name}" 2>/dev/null || true)"
      ids="${ids} $(docker ps -q --filter "name=^/?${name}$" 2>/dev/null || true)"
    done
  fi
  ids="$(echo "${ids}" | tr ' ' '\n' | awk 'NF && !seen[$0]++')"
  if [ -z "${ids}" ]; then
    log "No running container carries ${TLS_RELOAD_LABEL}=true; nothing to restart."
    return 0
  fi
  for id in ${ids}; do
    case "${id}" in "${self}"*) continue ;; esac
    name="$(docker inspect -f '{{.Name}}' "${id}" 2>/dev/null | sed 's#^/##')"
    log "Restarting ${name:-${id}} to load the new certificate."
    docker restart "${id}" >/dev/null 2>&1 || log "Could not restart ${name:-${id}}."
  done
}

# Copies the certbot lineage into /certs/<name>/. Returns 0 when files changed, 2 when unchanged,
# 1 when there is no lineage.
publish_lineage() {
  if [ ! -f "${LIVE_DIR}/fullchain.pem" ] || [ ! -f "${LIVE_DIR}/privkey.pem" ]; then
    log "No certbot lineage at ${LIVE_DIR}."
    return 1
  fi
  mkdir -p "${OUT_DIR}"
  local new_hash old_hash tmp f
  new_hash="$(content_hash "${LIVE_DIR}")"
  old_hash="$(cat "${OUT_DIR}/.published-sha256" 2>/dev/null || true)"
  if [ "${new_hash}" = "${old_hash}" ] && [ -f "${OUT_DIR}/cert.pfx" ] && [ "$(content_hash "${OUT_DIR}")" = "${new_hash}" ]; then
    return 2
  fi
  tmp="$(mktemp -d "${CERTS_ROOT}/.${TLS_CERT_NAME}.tmp.XXXXXX")" || return 1
  for f in fullchain.pem privkey.pem cert.pem chain.pem; do
    cp -L "${LIVE_DIR}/${f}" "${tmp}/${f}" || { rm -rf "${tmp}"; return 1; }
  done
  if ! write_pfx "${tmp}" "${tmp}/cert.pfx"; then
    log "openssl could not write cert.pfx; publishing the PEM files without it."
    rm -f "${tmp}/cert.pfx"
  fi
  # Services run as non-root users and mount /certs read-only, so the files must be world-readable.
  chmod 644 "${tmp}"/* 2>/dev/null || true
  for f in fullchain.pem privkey.pem cert.pem chain.pem cert.pfx; do
    if [ -f "${tmp}/${f}" ]; then mv -f "${tmp}/${f}" "${OUT_DIR}/${f}"; fi
  done
  rm -rf "${tmp}"
  echo "${new_hash}" > "${OUT_DIR}/.published-sha256"
  date -u +%Y-%m-%dT%H:%M:%SZ > "${OUT_DIR}/.renewed-at"
  chmod 755 "${CERTS_ROOT}" "${OUT_DIR}" 2>/dev/null || true
  chmod 644 "${OUT_DIR}/.published-sha256" "${OUT_DIR}/.renewed-at" 2>/dev/null || true
  log "Published ${TLS_CERT_NAME} to ${OUT_DIR} ($(openssl x509 -in "${OUT_DIR}/fullchain.pem" -noout -enddate 2>/dev/null))."
  return 0
}

# manual mode: derive cert.pem/chain.pem/cert.pfx from what was dropped into /certs/<name>/.
# Returns 0 when the PEMs changed since the last run, 2 when unchanged, 1 when they are missing.
publish_manual() {
  mkdir -p "${OUT_DIR}"
  # Old env files pointed SSL_CERT_PATH/SSL_KEY_PATH at PEMs elsewhere in this container.
  if [ -n "${SSL_CERT_PATH:-}" ] && [ -n "${SSL_KEY_PATH:-}" ] && [ -f "${SSL_CERT_PATH}" ] && [ -f "${SSL_KEY_PATH}" ] \
     && [ "${SSL_CERT_PATH}" != "${OUT_DIR}/fullchain.pem" ]; then
    log "WARNING: SSL_CERT_PATH/SSL_KEY_PATH are deprecated; put the PEMs in ${OUT_DIR}/ instead. Copying them there."
    cp -L "${SSL_CERT_PATH}" "${OUT_DIR}/fullchain.pem"
    cp -L "${SSL_KEY_PATH}" "${OUT_DIR}/privkey.pem"
  fi
  if [ ! -f "${OUT_DIR}/fullchain.pem" ] || [ ! -f "${OUT_DIR}/privkey.pem" ]; then
    log "manual mode: waiting for ${OUT_DIR}/fullchain.pem and privkey.pem (stack.ps1 certs manual)."
    return 1
  fi
  local new_hash old_hash
  new_hash="$(content_hash "${OUT_DIR}")"
  old_hash="$(cat "${OUT_DIR}/.published-sha256" 2>/dev/null || true)"
  if [ "${new_hash}" = "${old_hash}" ] && [ -f "${OUT_DIR}/cert.pfx" ]; then
    return 2
  fi
  split_fullchain "${OUT_DIR}/fullchain.pem" "${OUT_DIR}"
  if write_pfx "${OUT_DIR}" "${OUT_DIR}/cert.pfx.tmp"; then
    mv -f "${OUT_DIR}/cert.pfx.tmp" "${OUT_DIR}/cert.pfx"
  else
    rm -f "${OUT_DIR}/cert.pfx.tmp"
    log "openssl could not write cert.pfx (does privkey.pem match fullchain.pem?)."
  fi
  chmod 644 "${OUT_DIR}"/*.pem "${OUT_DIR}/cert.pfx" 2>/dev/null || true
  chmod 755 "${CERTS_ROOT}" "${OUT_DIR}" 2>/dev/null || true
  echo "${new_hash}" > "${OUT_DIR}/.published-sha256"
  date -u +%Y-%m-%dT%H:%M:%SZ > "${OUT_DIR}/.renewed-at"
  log "manual mode: new PEMs in ${OUT_DIR} ($(openssl x509 -in "${OUT_DIR}/fullchain.pem" -noout -enddate 2>/dev/null))."
  return 0
}

# ---------------------------------------------------------------------------------------------------
# ACME
# ---------------------------------------------------------------------------------------------------

AUTH_ARGS=()
COMMON_ARGS=()
build_acme_args() {
  local hmac="" name
  refresh_hostnames
  if [ -z "${TLS_HOSTNAMES}" ]; then
    log "TLS_HOSTNAMES is empty and no VIKING_HOSTNAME_* is set; nothing to request."
    return 1
  fi
  COMMON_ARGS=(--non-interactive --agree-tos --key-type "${TLS_KEY_TYPE}" --cert-name "${TLS_CERT_NAME}"
               --keep-until-expiring --expand)
  if [ -n "${TLS_EMAIL:-}" ]; then
    COMMON_ARGS+=(--email "${TLS_EMAIL}")
  else
    log "TLS_EMAIL is empty; registering without an e-mail address (no expiry notices)."
    COMMON_ARGS+=(--register-unsafely-without-email)
  fi
  IFS=',' read -ra NAMES <<< "${TLS_HOSTNAMES}"
  for name in "${NAMES[@]}"; do
    name="$(echo "${name}" | xargs)"
    if [ -n "${name}" ]; then COMMON_ARGS+=(-d "${name}"); fi
  done

  case "${TLS_MODE}" in
    letsencrypt)
      mkdir -p "${TLS_WEBROOT}"
      AUTH_ARGS=(--webroot --webroot-path "${TLS_WEBROOT}")
      if [ -n "${TLS_ACME_SERVER:-}" ]; then
        COMMON_ARGS+=(--server "${TLS_ACME_SERVER}")
      elif [ "${TLS_STAGING}" = "1" ]; then
        COMMON_ARGS+=(--staging --break-my-certs)
      fi
      ;;
    acme-eab)
      if [ -z "${TLS_EAB_HMAC_KEY:-}" ] && [ -n "${TLS_EAB_HMAC_KEY_FILE:-}" ] && [ -f "${TLS_EAB_HMAC_KEY_FILE}" ]; then
        hmac="$(tr -d '\r\n' < "${TLS_EAB_HMAC_KEY_FILE}")"
      else
        hmac="${TLS_EAB_HMAC_KEY:-}"
      fi
      if [ -z "${TLS_ACME_SERVER:-}" ] || [ -z "${TLS_EAB_KID:-}" ] || [ -z "${hmac}" ]; then
        log "acme-eab needs TLS_ACME_SERVER, TLS_EAB_KID and TLS_EAB_HMAC_KEY (or TLS_EAB_HMAC_KEY_FILE)."
        return 1
      fi
      mkdir -p "${TLS_WEBROOT}"
      AUTH_ARGS=(--webroot --webroot-path "${TLS_WEBROOT}")
      COMMON_ARGS+=(--server "${TLS_ACME_SERVER}" --eab-kid "${TLS_EAB_KID}" --eab-hmac-key "${hmac}")
      ;;
    dns-cloudflare)
      if [ ! -f "${CF_DNS_API_CREDENTIALS_FILE}" ]; then
        log "Cloudflare credentials file not found at ${CF_DNS_API_CREDENTIALS_FILE}."
        return 1
      fi
      cp "${CF_DNS_API_CREDENTIALS_FILE}" /tmp/cloudflare.ini && chmod 600 /tmp/cloudflare.ini
      AUTH_ARGS=(--dns-cloudflare --dns-cloudflare-credentials /tmp/cloudflare.ini
                 --dns-cloudflare-propagation-seconds "${CF_DNS_PROPAGATION_SECONDS}")
      if [ "${TLS_STAGING}" = "1" ]; then COMMON_ARGS+=(--staging --break-my-certs); fi
      ;;
    *)
      log "Unsupported TLS_MODE '${TLS_MODE}'. Use letsencrypt, acme-eab or manual."
      return 1
      ;;
  esac

  # A staging certificate is not near expiry, so --keep-until-expiring would keep it after
  # TLS_STAGING goes back to 0. Force one real issuance in that case.
  if [ "${TLS_STAGING}" = "0" ] && [ -f "${LIVE_DIR}/cert.pem" ] \
     && openssl x509 -in "${LIVE_DIR}/cert.pem" -noout -issuer 2>/dev/null | grep -qiE 'staging|fake'; then
    log "The current certificate is from a staging CA; forcing a production issuance."
    COMMON_ARGS+=(--force-renewal)
  fi
  return 0
}

# One ACME cycle: certonly (issues, renews when due, or changes the name list), then publish.
acme_cycle() {
  local rc
  build_acme_args || return 1
  log "certbot certonly (${TLS_MODE}) for ${TLS_HOSTNAMES} as lineage ${TLS_CERT_NAME}."
  if ! certbot certonly "${AUTH_ARGS[@]}" "${COMMON_ARGS[@]}"; then
    log "certbot failed; details in /var/log/letsencrypt/letsencrypt.log (STACK_LOGS_DIR on the host)."
    # Still publish an existing lineage so an empty /certs gets the last good certificate.
    publish_lineage
    rc=$?
    if [ ${rc} -eq 0 ]; then restart_labelled; fi
    return 1
  fi
  publish_lineage
  rc=$?
  case ${rc} in
    0) restart_labelled ;;
    2) log "Certificate unchanged." ;;
    *) return 1 ;;
  esac
  return 0
}

manual_cycle() {
  local rc
  publish_manual
  rc=$?
  case ${rc} in
    0) restart_labelled; return 0 ;;
    2) return 0 ;;
    *) return 1 ;;
  esac
}

cycle() {
  if [ "${TLS_MODE}" = "manual" ]; then manual_cycle; else acme_cycle; fi
}

log "mode=${TLS_MODE} cert=${TLS_CERT_NAME} names=${TLS_HOSTNAMES:-<none>} out=${OUT_DIR} staging=${TLS_STAGING}"

case "${RUN_MODE}" in
  once)
    cycle
    exit $?
    ;;
  publish)
    if [ "${TLS_MODE}" = "manual" ]; then manual_cycle; exit $?; fi
    publish_lineage
    rc=$?
    if [ ${rc} -eq 0 ]; then restart_labelled; fi
    if [ ${rc} -eq 2 ]; then rc=0; fi
    exit ${rc}
    ;;
esac

while true; do
  if cycle; then
    if [ "${TLS_MODE}" = "manual" ]; then
      sleep "${TLS_MANUAL_POLL_SECONDS}"
    else
      sleep $((TLS_RENEW_INTERVAL_HOURS * 3600))
    fi
  else
    log "Cycle failed; retrying in ${TLS_RETRY_MINUTES} minutes."
    sleep $((TLS_RETRY_MINUTES * 60))
  fi
done
