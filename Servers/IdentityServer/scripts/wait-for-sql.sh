#!/bin/bash
# Block until SQL Server accepts TCP connections (or timeout).
# Used after host reboot when the Identity container can start before the DB VM.

set -euo pipefail

HOST="${SQL_SERVER_HOST:-}"
PORT="${SQL_SERVER_PORT:-1433}"
TIMEOUT_SECONDS="${SQL_WAIT_TIMEOUT_SECONDS:-900}"
INTERVAL_SECONDS="${SQL_WAIT_INTERVAL_SECONDS:-5}"
SETTLE_SECONDS="${SQL_WAIT_SETTLE_SECONDS:-3}"
REQUIRED_SUCCESSES="${SQL_WAIT_REQUIRED_SUCCESSES:-3}"

if [ -z "${HOST}" ]; then
  echo "SQL_SERVER_HOST is not set; skipping database wait"
  exit 0
fi

echo "Waiting for SQL Server at ${HOST}:${PORT} (timeout ${TIMEOUT_SECONDS}s, interval ${INTERVAL_SECONDS}s)..."

elapsed=0
successes=0

tcp_ready() {
  # Bash /dev/tcp avoids needing nc/sqlcmd in the image.
  timeout 2 bash -c "echo >/dev/tcp/${HOST}/${PORT}" >/dev/null 2>&1
}

while [ "${elapsed}" -lt "${TIMEOUT_SECONDS}" ]; do
  if tcp_ready; then
    successes=$((successes + 1))
    echo "  SQL TCP probe succeeded (${successes}/${REQUIRED_SUCCESSES}) after ${elapsed}s"
    if [ "${successes}" -ge "${REQUIRED_SUCCESSES}" ]; then
      echo "SQL Server is ready at ${HOST}:${PORT}; settling ${SETTLE_SECONDS}s before starting apps"
      sleep "${SETTLE_SECONDS}"
      exit 0
    fi
    sleep "${SETTLE_SECONDS}"
    elapsed=$((elapsed + SETTLE_SECONDS))
    continue
  fi

  successes=0
  echo "  ... not ready yet (${elapsed}s/${TIMEOUT_SECONDS}s)"
  sleep "${INTERVAL_SECONDS}"
  elapsed=$((elapsed + INTERVAL_SECONDS))
done

echo "ERROR: Timed out after ${TIMEOUT_SECONDS}s waiting for SQL Server at ${HOST}:${PORT}"
exit 1
