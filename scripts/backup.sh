#!/usr/bin/env bash
# Back up the Orkyo Community database and persistent volumes.
# Output: ./backups/<timestamp>/

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUNDLE_DIR="$(dirname "$SCRIPT_DIR")"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP_DIR="${BUNDLE_DIR}/backups/${TIMESTAMP}"
ENV_FILE="${BUNDLE_DIR}/.env"

mkdir -p "$BACKUP_DIR"
echo "=== Backup: ${BACKUP_DIR} ==="

# ── PostgreSQL dump ───────────────────────────────────────────────────────────
echo "Dumping PostgreSQL database..."
docker compose -f "${BUNDLE_DIR}/docker-compose.yml" --env-file "$ENV_FILE" \
  exec -T db pg_dumpall -U "$(grep POSTGRES_USER "$ENV_FILE" | cut -d= -f2)" \
  > "${BACKUP_DIR}/pg_dumpall.sql"
echo "  Wrote pg_dumpall.sql ($(du -h "${BACKUP_DIR}/pg_dumpall.sql" | cut -f1))"

# ── Checksum ──────────────────────────────────────────────────────────────────
sha256sum "${BACKUP_DIR}/pg_dumpall.sql" > "${BACKUP_DIR}/checksums.sha256"

echo ""
echo "Backup complete: ${BACKUP_DIR}"
echo ""
echo "Restore instructions:"
echo "  1. Stop services: docker compose down"
echo "  2. Drop and recreate DB volume: docker volume rm <project>_postgres_data"
echo "  3. Start DB only: docker compose up -d db"
echo "  4. Restore: docker compose exec -T db psql -U <user> < ${BACKUP_DIR}/pg_dumpall.sql"
echo "  5. Start all: docker compose up -d"
