#!/usr/bin/env bash
# Upgrade Orkyo Community to a new version.
#
# Contract:
#   - Not zero-downtime (single server, single tenant)
#   - Steps: backup → set ORKYO_VERSION in .env → pull → up
#   - Aborts if the backup fails: never upgrade without a backup
#   - Migrations run inside `docker compose up`: the migrator service is in the
#     api service's depends_on chain with service_completed_successfully
#
# Usage: scripts/upgrade.sh <new-version>       e.g. scripts/upgrade.sh 1.4.0

set -euo pipefail

NEW_VERSION="${1:?Usage: $0 <new-version>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUNDLE_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="${BUNDLE_DIR}/compose.yml"
ENV_FILE="${BUNDLE_DIR}/.env"

echo "=== Orkyo Community upgrade → v${NEW_VERSION} ==="
echo ""

[ -f "$ENV_FILE" ] || { echo "ERROR: .env not found at ${ENV_FILE}" >&2; exit 1; }

echo "Step 1/3: Pre-upgrade backup (mandatory)..."
if ! bash "${SCRIPT_DIR}/backup.sh"; then
  echo "ERROR: Backup failed. Upgrade aborted, nothing changed." >&2
  exit 1
fi
echo ""

echo "Step 2/3: Setting ORKYO_VERSION=${NEW_VERSION} in .env..."
if grep -qE '^ORKYO_VERSION=' "$ENV_FILE"; then
  sed -i -E "s|^ORKYO_VERSION=.*|ORKYO_VERSION=${NEW_VERSION}|" "$ENV_FILE"
else
  printf 'ORKYO_VERSION=%s\n' "$NEW_VERSION" >> "$ENV_FILE"
fi

echo "Step 3/3: Pulling images and restarting (downtime begins)..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" pull
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d

echo ""
echo "Waiting for the API to become healthy..."
for _ in $(seq 1 60); do
  if curl -sf http://localhost:8080/health >/dev/null 2>&1; then
    echo "Upgrade to v${NEW_VERSION} complete."
    exit 0
  fi
  sleep 2
done

echo "ERROR: the API did not become healthy within 120s." >&2
echo "Check: docker compose -f ${COMPOSE_FILE} logs api migrator" >&2
echo "Roll back with the backup in ${BUNDLE_DIR}/backups/ (OPERATIONS.md, section Rollback)." >&2
exit 1
