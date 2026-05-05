#!/usr/bin/env bash
# Upgrade Orkyo Community to a new version.
#
# Contract:
#   - NOT zero-downtime (single-server single-tenant)
#   - Steps: backup → update version → pull images → run migrator → restart services
#   - ABORTS if backup fails (never upgrade without a backup)
#
# Usage: upgrade.sh <new-version>
#   new-version  The target version (e.g. 1.3.0)

set -euo pipefail

NEW_VERSION="${1:?Usage: $0 <new-version>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUNDLE_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="${BUNDLE_DIR}/compose.yml"
ENV_FILE="${BUNDLE_DIR}/.env"

echo "=== Orkyo Community Upgrade → v${NEW_VERSION} ==="
echo ""

if [ ! -f "$ENV_FILE" ]; then
  echo "ERROR: .env not found at ${ENV_FILE}"
  exit 1
fi

# ── Step 1: Mandatory backup ──────────────────────────────────────────────────
echo "Step 1/4: Creating pre-upgrade backup (mandatory)..."
if ! bash "${SCRIPT_DIR}/backup.sh"; then
  echo "ERROR: Backup failed. Upgrade aborted — no changes made."
  exit 1
fi
echo "Backup succeeded."
echo ""

# ── Step 2: Stamp new version into .env ──────────────────────────────────────
echo "Step 2/4: Updating ORKYO_VERSION to ${NEW_VERSION}..."
sed -i "s/^ORKYO_VERSION=.*/ORKYO_VERSION=${NEW_VERSION}/" "$ENV_FILE"
echo ""

# ── Step 3: Pull new images ───────────────────────────────────────────────────
echo "Step 3/4: Pulling images for v${NEW_VERSION}..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" pull
echo ""

# ── Step 4: Restart with new images (migrator runs automatically via depends_on)
echo "Step 4/4: Restarting services (downtime begins)..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --force-recreate

echo ""
echo "Waiting for API to become healthy..."
if timeout 120 bash -c "until curl -sf http://localhost:8080/api/health >/dev/null 2>&1; do sleep 3; done"; then
  echo "Upgrade to v${NEW_VERSION} complete."
else
  echo "ERROR: API did not become healthy after upgrade."
  echo "Check logs: docker compose -f ${COMPOSE_FILE} logs"
  echo "If needed, restore from backup in ./backups/ and revert ORKYO_VERSION in .env."
  exit 1
fi
