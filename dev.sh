#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

ROOT_DIR="$PWD"
LOCAL_COMPOSE_FILE="$ROOT_DIR/compose.local.yml"
FRONTEND_ROOT="$ROOT_DIR/frontend"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

COMPOSE_CMD=(docker compose -f "$LOCAL_COMPOSE_FILE" --env-file "$ROOT_DIR/.env")

log()     { echo -e "${BLUE}[dev]${NC} $*"; }
success() { echo -e "${GREEN}[dev]${NC} $*"; }
warn()    { echo -e "${YELLOW}[dev]${NC} $*"; }
error()   { echo -e "${RED}[dev]${NC} $*" >&2; }

show_help() {
  cat <<'EOF'
Usage: ./dev.sh <command>

  up        Start full stack in containers (everything needed for normal operation)
  down      Stop and remove all containers
  restart   Restart the full stack
  rebuild   Rebuild images and restart the full stack
  logs      Stream logs (optionally: ./dev.sh logs api)
  status    Show container status
  reset     Remove local Docker volumes (destroys all local data)

  infra     Start infrastructure only: db, redis, keycloak, mailhog, superset
            (use with host-process commands below for active development)

Host processes (fast hot-reload, run after ./dev.sh infra):
  migrator  Run the database migrator on the host
  api       Run the API on the host
  frontend  Run the Vite dev server on the host
  seed      Seed the database with realistic data
            e.g.: ./dev.sh seed --profile manufacturing --scale medium
                  ./dev.sh seed --profile camping --scale tiny --random

Other:
  doctor    Show startup sequences and runtime URLs
  help      Show this help
EOF
}

ensure_env() {
  if [[ ! -f .env ]]; then
    error ".env file not found"
    echo "Create it with: cp .env.template .env"
    exit 1
  fi
}

ensure_local_compose() {
  if [[ ! -f "$LOCAL_COMPOSE_FILE" ]]; then
    error "Local compose file not found: $LOCAL_COMPOSE_FILE"
    exit 1
  fi
}

sync_assets() {
  local sync_script="$ROOT_DIR/../orkyo-foundation/scripts/sync-assets.sh"
  if [[ -x "$sync_script" ]]; then
    log "Syncing brand assets from orkyo-foundation"
    "$sync_script"
  else
    warn "orkyo-foundation/scripts/sync-assets.sh not found — skipping asset sync"
  fi
}

load_env() {
  ensure_env

  while IFS='=' read -r key value; do
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
    export "$key=$value"
  done < <(grep -E '^[A-Za-z_][A-Za-z0-9_]*=' .env)

  export ASPNETCORE_ENVIRONMENT=Development
  export ASPNETCORE_URLS="http://localhost:${API_PORT}"
  # Single community DB — aliased to both names so foundation's validator is satisfied.
  local _cs="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export ConnectionStrings__DefaultConnection="$_cs"
  export ConnectionStrings__Postgres="$_cs"
  export VITE_API_BASE_URL="http://localhost:${API_PORT}"

  # Redis — used by API for BFF sessions and Data Protection keys
  export REDIS_CONNECTION="localhost:${REDIS_PORT},password=${REDIS_PASSWORD},abortConnect=false"

  # BFF auth constants for host-mode API
  export BFF_ENABLED=true
  export BFF_COOKIE_DOMAIN=""
  export BFF_COOKIE_SECURE=false
  export BFF_REDIRECT_URI="http://localhost:${API_PORT}/api/auth/bff/callback"
  export BFF_ALLOWED_HOSTS="localhost,*.localhost"

  # Reporting — Superset running in Docker (started by ./dev.sh infra)
  export Reporting__Enabled=true
  export Reporting__BaseUrl="http://localhost:${SUPERSET_PORT}"
  export Reporting__AdminUsername="${SUPERSET_ADMIN_USERNAME}"
  export Reporting__AdminPassword="${SUPERSET_ADMIN_PASSWORD}"
  export Reporting__GuestTokenJwtSecret="${SUPERSET_GUEST_TOKEN_JWT_SECRET}"
  export Reporting__ReaderCredentialMasterSecret="${SUPERSET_READER_CREDENTIAL_MASTER_SECRET}"
  # Host-mode: Postgres is exposed on localhost at the mapped port
  export Reporting__PostgresHost=localhost
  export Reporting__PostgresPort="${POSTGRES_PORT}"
  # Template dashboard UUIDs — populated from .env after first Superset bootstrap
  export "Reporting__TemplateDashboardIds__space_utilization=${Reporting__TemplateDashboardIds__space_utilization:-}"
  export "Reporting__TemplateDashboardIds__request_pipeline=${Reporting__TemplateDashboardIds__request_pipeline:-}"
  export "Reporting__TemplateDashboardIds__allocation_conflicts=${Reporting__TemplateDashboardIds__allocation_conflicts:-}"

  mkdir -p "$ROOT_DIR/.local/logs"
}

check_env_or_confirm() {
  if ! ./scripts/check-env.sh; then
    echo ""
    read -r -p "Continue anyway? (y/N) " reply
    if [[ ! "$reply" =~ ^[Yy]$ ]]; then
      error "Aborted"
      exit 1
    fi
  fi
}

wait_for_url() {
  local url="$1"
  local description="$2"
  local retries="${3:-45}"

  log "Waiting for ${description}: ${url}"
  until curl -sf "$url" >/dev/null 2>&1; do
    retries=$((retries - 1))
    if [[ $retries -le 0 ]]; then
      error "${description} did not become healthy in time"
      exit 1
    fi
    printf '.'
    sleep 2
  done
  echo ""
  success "${description} is healthy"
}

cmd_up() {
  ensure_local_compose
  load_env
  sync_assets
  check_env_or_confirm

  log "Starting full stack in containers (build may take a minute the first time)"
  "${COMPOSE_CMD[@]}" up -d --remove-orphans

  wait_for_url "http://localhost:9001/health/ready" "Keycloak"
  wait_for_url "http://localhost:${API_PORT}/health" "API"

  success "Full stack is up"
  echo "Frontend: http://localhost:${FRONTEND_PORT}  (Community)"
  echo "API:      http://localhost:${API_PORT}"
  echo "OpenAPI:  http://localhost:${API_PORT}/openapi/v1.json"
  echo "Keycloak: http://localhost:${KEYCLOAK_PORT}"
  echo "MailHog:  http://localhost:${MAILHOG_UI_PORT}"
}

cmd_down() {
  ensure_local_compose
  "${COMPOSE_CMD[@]}" down
  success "Stack stopped"
}

cmd_restart() { cmd_down; cmd_up; }

cmd_rebuild() {
  ensure_local_compose
  load_env
  log "Rebuilding containers (--no-cache to guarantee fresh binaries)..."
  "${COMPOSE_CMD[@]}" build --no-cache
  cmd_up
}

cmd_logs() {
  ensure_local_compose
  shift || true
  "${COMPOSE_CMD[@]}" logs -f "$@"
}

cmd_status() {
  ensure_local_compose
  "${COMPOSE_CMD[@]}" ps
}

cmd_reset() {
  ensure_local_compose
  warn "This removes local Docker volumes for the stack."
  read -r -p "Proceed? (y/N) " reply
  if [[ ! "$reply" =~ ^[Yy]$ ]]; then
    echo "Cancelled"; exit 0
  fi
  "${COMPOSE_CMD[@]}" down -v
  success "Volumes removed"
}

cmd_infra() {
  ensure_local_compose
  load_env
  sync_assets
  check_env_or_confirm

  log "Starting infrastructure (db, redis, keycloak, mailhog, superset)"
  "${COMPOSE_CMD[@]}" up -d --remove-orphans db redis keycloak mailhog superset-init superset

  wait_for_url "http://localhost:9001/health/ready" "Keycloak"
  wait_for_url "http://localhost:${SUPERSET_PORT}/health" "Superset" 90

  success "Infrastructure is up"
  echo "Postgres: localhost:${POSTGRES_PORT}"
  echo "Redis:    localhost:${REDIS_PORT}"
  echo "Keycloak: http://localhost:${KEYCLOAK_PORT}"
  echo "MailHog:  http://localhost:${MAILHOG_UI_PORT}"
  echo "Superset: http://localhost:${SUPERSET_PORT}"
  echo ""
  cmd_doctor
}

cmd_migrator() {
  load_env
  cd "$ROOT_DIR/backend/migrator"
  dotnet run -- migrate --target all
}

cmd_api() {
  load_env
  cd "$ROOT_DIR/backend/api"
  dotnet run
}

cmd_seed() {
  load_env
  cd "$ROOT_DIR/backend/cli/Orkyo.Community.Seed"
  dotnet run -- "$@"
}

cmd_frontend() {
  load_env
  if [[ ! -f "$FRONTEND_ROOT/package.json" ]]; then
    error "No frontend found in orkyo-community/frontend"
    exit 1
  fi
  cd "$FRONTEND_ROOT"
  npm run dev -- --host 0.0.0.0 --port "${FRONTEND_PORT}"
}

cmd_doctor() {
  cat <<EOF
── Standard workflow (fully containerised) ─────────────────────────────────
  1. ./dev.sh up           # Start everything in Docker

── Active development workflow (host processes) ─────────────────────────────
  1. ./dev.sh infra        # Start db, keycloak, mailhog in Docker
  2. ./dev.sh migrator     # Apply DB migrations on host
  3. ./dev.sh api          # Start API on host
  4. ./dev.sh frontend     # Start Vite dev server on host

── Runtime URLs ─────────────────────────────────────────────────────────────
  Frontend: http://localhost:${FRONTEND_PORT}
  API:      http://localhost:${API_PORT}
  OpenAPI:  http://localhost:${API_PORT}/openapi/v1.json
  Keycloak: http://localhost:${KEYCLOAK_PORT}  (admin: admin / changeme)
  MailHog:  http://localhost:${MAILHOG_UI_PORT}
  Superset: http://localhost:${SUPERSET_PORT}  (admin: ${SUPERSET_ADMIN_USERNAME} / ${SUPERSET_ADMIN_PASSWORD})
EOF
}

command="${1:-help}"

case "$command" in
  up) cmd_up ;;
  down) cmd_down ;;
  restart) cmd_restart ;;
  rebuild) cmd_rebuild ;;
  logs) cmd_logs "$@" ;;
  status) cmd_status ;;
  reset) cmd_reset ;;
  infra) cmd_infra ;;
  migrator) cmd_migrator ;;
  api) cmd_api ;;
  seed) shift; cmd_seed "$@" ;;
  frontend) cmd_frontend ;;
  doctor) cmd_doctor ;;
  help|-h|--help) show_help ;;
  *)
    error "Unknown command: $command"
    show_help
    exit 1
    ;;
esac
