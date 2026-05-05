#!/usr/bin/env bash
# Bootstrap a fresh Orkyo Community installation.
# Run once on a new server after extracting the release bundle.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUNDLE_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="${BUNDLE_DIR}/compose.yml"

echo "=== Orkyo Community Bootstrap ==="
echo ""

# ── Prerequisites ─────────────────────────────────────────────────────────────
for cmd in docker curl; do
  if ! command -v "$cmd" &>/dev/null; then
    echo "ERROR: $cmd is required but not installed."
    exit 1
  fi
done

if ! docker compose version &>/dev/null; then
  echo "ERROR: Docker Compose V2 is required (docker compose, not docker-compose)."
  exit 1
fi

# ── .env setup ────────────────────────────────────────────────────────────────
ENV_FILE="${BUNDLE_DIR}/.env"
if [ ! -f "$ENV_FILE" ]; then
  echo "Creating .env from .env.template..."
  cp "${BUNDLE_DIR}/.env.template" "$ENV_FILE"
  echo ""
  echo "IMPORTANT: Edit ${ENV_FILE} and replace all CHANGE_ME_* values before continuing."
  echo "Then re-run this script."
  exit 0
fi

if grep -q "CHANGE_ME_" "$ENV_FILE"; then
  echo "ERROR: .env still contains CHANGE_ME_ placeholders."
  echo "Edit ${ENV_FILE} and replace all placeholder values, then re-run."
  exit 1
fi

# ── Pull images and start ─────────────────────────────────────────────────────
echo "Pulling images..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" pull

echo "Starting services..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d

echo ""
echo "Waiting for API to become healthy..."
if timeout 120 bash -c "until curl -sf http://localhost:8080/api/health >/dev/null 2>&1; do sleep 3; done"; then
  echo ""
  echo "Orkyo Community is up."
  echo "  Frontend: http://localhost"
  echo "  Keycloak: http://localhost:9080"
  echo "  API:      http://localhost:8080"
else
  echo "ERROR: API did not become healthy within 120s."
  echo "Check logs: docker compose -f ${COMPOSE_FILE} logs"
  exit 1
fi
