#!/usr/bin/env bash
# Back up an Orkyo Community installation: the database dump and the .env file.
#
# The dump alone is not a backup. Floorplan assets and the assistant credential are
# encrypted at rest with ORKYO_MASTER_ENCRYPTION_KEY from .env, and a dump restored
# without that key holds data nobody can read. The two files travel together.
#
# Usage: scripts/backup.sh            (run from the bundle directory or anywhere)
# Output: <bundle>/backups/<timestamp>/{pg_dumpall.sql,env,checksums.sha256}

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUNDLE_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="${BUNDLE_DIR}/compose.yml"
ENV_FILE="${BUNDLE_DIR}/.env"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP_DIR="${BUNDLE_DIR}/backups/${TIMESTAMP}"

[ -f "$ENV_FILE" ] || { echo "ERROR: .env not found at ${ENV_FILE}" >&2; exit 1; }
[ -f "$COMPOSE_FILE" ] || { echo "ERROR: compose.yml not found at ${COMPOSE_FILE}" >&2; exit 1; }

# compose.yml defaults POSTGRES_USER to orkyo when .env leaves it unset.
PG_USER="$(sed -n 's/^POSTGRES_USER=//p' "$ENV_FILE" | tail -n1)"
PG_USER="${PG_USER:-orkyo}"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
echo "=== Backup: ${BACKUP_DIR} ==="

echo "Dumping PostgreSQL (application and Keycloak databases)..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" \
  exec -T db pg_dumpall -U "$PG_USER" --clean --if-exists \
  > "${BACKUP_DIR}/pg_dumpall.sql"

# A dump that is only the cluster preamble is not a backup: fail instead of archiving it.
SIZE="$(stat -c%s "${BACKUP_DIR}/pg_dumpall.sql")"
if [ "$SIZE" -lt 4096 ]; then
  echo "ERROR: dump is only ${SIZE} bytes; refusing to record it as a backup" >&2
  exit 1
fi
echo "  Wrote pg_dumpall.sql ($(du -h "${BACKUP_DIR}/pg_dumpall.sql" | cut -f1))"

echo "Copying .env (holds ORKYO_MASTER_ENCRYPTION_KEY and the service passwords)..."
install -m 600 "$ENV_FILE" "${BACKUP_DIR}/env"

(cd "$BACKUP_DIR" && sha256sum pg_dumpall.sql env > checksums.sha256)

echo ""
echo "Backup complete: ${BACKUP_DIR}"
echo "Keep the directory as one unit. Restore steps: release/docs/OPERATIONS.md, section Restore."
