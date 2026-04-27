#!/usr/bin/env bash
# Upgrade Orkyo Community to a new version.
#
# Contract:
#   - NOT zero-downtime (single-server single-tenant)
#   - Steps: backup → pull images → run migrator → restart services
#   - ABORTS if backup fails (never upgrade without a backup)
#
# Usage: upgrade.sh <new-version>
#   new-version  The target version (e.g. 1.3.0)

set -euo pipefail

NEW_VERSION="${1:?Usage: $0 <new-version>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUNDLE_DIR="$(dirname "$SCRIPT_DIR")"
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

# ── Step 2: Pull new images ───────────────────────────────────────────────────
echo "Step 2/4: Pulling images for v${NEW_VERSION}..."
# Update image tags in a temporary env override
export COMMUNITY_VERSION="${NEW_VERSION}"
docker compose -f "${BUNDLE_DIR}/docker-compose.yml" --env-file "$ENV_FILE" \
  pull
echo ""

# ── Step 3: Stop services ─────────────────────────────────────────────────────
echo "Step 3/4: Stopping services (downtime begins)..."
docker compose -f "${BUNDLE_DIR}/docker-compose.yml" --env-file "$ENV_FILE" \
  stop api worker

# ── Step 4: Run migrator ──────────────────────────────────────────────────────
echo "Step 4/4: Running migrations..."
docker compose -f "${BUNDLE_DIR}/docker-compose.yml" --env-file "$ENV_FILE" \
  run --rm migrator migrate --target all

# ── Restart ───────────────────────────────────────────────────────────────────
echo "Restarting services..."
docker compose -f "${BUNDLE_DIR}/docker-compose.yml" --env-file "$ENV_FILE" \
  up -d --force-recreate

echo ""
echo "Waiting for API to become healthy..."
for i in $(seq 1 30); do
  if curl -sf http://localhost:8080/api/health >/dev/null 2>&1; then
    echo "Upgrade to v${NEW_VERSION} complete."
    exit 0
  fi
  sleep 2
done

echo "ERROR: API did not become healthy after upgrade."
echo "Check logs: docker compose logs api"
echo "If needed, restore from backup in ./backups/"
exit 1
