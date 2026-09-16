#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

ROOT_DIR="$PWD"
PRODUCT_NAME="Community"
PRODUCT_REPO="orkyo-community"
SEED_PROJECT_DIR="$ROOT_DIR/backend/cli/Orkyo.Community.Seed"
# Host mapping of Keycloak's management port — must track the keycloak `ports:`
# entry in compose.local.yml (community maps 9001:9000; SaaS uses 9000 so both
# stacks can run side-by-side).
KEYCLOAK_MGMT_PORT=9001

# The part of this script that is the same in every product (helpers, compose plumbing,
# host-process runners, the dispatcher) lives in orkyo-foundation; what follows is Community-only.
DEV_COMMON="$ROOT_DIR/../orkyo-foundation/scripts/dev-common.sh"
if [[ ! -f "$DEV_COMMON" ]]; then
  echo "error: shared dev script missing: $DEV_COMMON (is orkyo-foundation a sibling checkout?)" >&2
  exit 1
fi
# shellcheck source=/dev/null
source "$DEV_COMMON"

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

  infra     Start infrastructure only: db, valkey, keycloak, mailhog
            (use with host-process commands below for active development)

Host processes (fast hot-reload, run after ./dev.sh infra):
  migrator  Run the database migrator on the host
  api       Run the API on the host
  worker    Run the background worker on the host
  frontend  Run the Vite dev server on the host
  seed      Seed the database with realistic data
            e.g.: ./dev.sh seed --profile manufacturing --scale medium
                  ./dev.sh seed --profile camping --scale tiny --random

Other:
  doctor    Show startup sequences and runtime URLs
  help      Show this help
EOF
}

load_env() {
  ensure_env

  load_dotenv "$ROOT_DIR/.env"

  # Derived vars assembled from .env values — no defaults; missing vars fail loudly.
  export ASPNETCORE_ENVIRONMENT=Development
  export ASPNETCORE_URLS="http://localhost:${API_PORT}"
  # Single community DB — aliased to both names so foundation's validator is satisfied.
  local _cs="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export ConnectionStrings__DefaultConnection="$_cs"
  export ConnectionStrings__Postgres="$_cs"
  export VITE_API_BASE_URL="http://localhost:${API_PORT}"

  # Valkey — used by API for BFF sessions and Data Protection keys
  export VALKEY_CONNECTION="localhost:${VALKEY_PORT},password=${VALKEY_PASSWORD},abortConnect=false"

  # BFF auth constants for host-mode API (fixed dev values, not user-configurable)
  export BFF_ENABLED=true
  export BFF_COOKIE_DOMAIN=""
  export BFF_COOKIE_SECURE=false
  export BFF_REDIRECT_URI="http://localhost:${API_PORT}/api/auth/bff/callback"
  export BFF_ALLOWED_HOSTS="localhost,*.localhost"

  mkdir -p "$ROOT_DIR/.local/logs"
}

print_stack_urls() {
  echo "Frontend: http://localhost:${FRONTEND_PORT}  (Community)"
  echo "API:      http://localhost:${API_PORT}"
  echo "Swagger:  http://localhost:${API_PORT}/swagger"
  echo "Keycloak: http://localhost:${KEYCLOAK_PORT}"
  echo "MailHog:  http://localhost:${MAILHOG_UI_PORT}"
}

cmd_up() {
  ensure_local_compose
  load_env
  sync_assets
  check_env_or_confirm

  log "Starting full stack in containers (build may take a minute the first time)"
  "${COMPOSE_CMD[@]}" up -d --remove-orphans

  wait_for_url "http://localhost:${KEYCLOAK_MGMT_PORT}/health/ready" "Keycloak"
  wait_for_url "http://localhost:${API_PORT}/health" "API"

  success "Full stack is up"
  print_stack_urls
}

cmd_infra() {
  ensure_local_compose
  load_env
  sync_assets
  check_env_or_confirm

  log "Starting infrastructure (db, valkey, keycloak, mailhog)"
  "${COMPOSE_CMD[@]}" up -d --remove-orphans db valkey keycloak mailhog

  wait_for_url "http://localhost:${KEYCLOAK_MGMT_PORT}/health/ready" "Keycloak"

  success "Infrastructure is up"
  echo "Postgres: localhost:${POSTGRES_PORT}"
  echo "Valkey:    localhost:${VALKEY_PORT}"
  echo "Keycloak: http://localhost:${KEYCLOAK_PORT}"
  echo "MailHog:  http://localhost:${MAILHOG_UI_PORT}"
  echo ""
  cmd_doctor
}

# Unlike SaaS (single `up -d --build --force-recreate`), community rebuilds via
# `build` + cmd_up — kept as-is to preserve behavior (no forced recreate).
cmd_rebuild() {
  ensure_local_compose
  load_env
  log "Rebuilding containers..."
  "${COMPOSE_CMD[@]}" build
  cmd_up
}

cmd_migrator() {
  run_dotnet_project "$ROOT_DIR/backend/migrator" migrate --target all
}

cmd_doctor() {
  cat <<EOF
── Standard workflow (fully containerised) ─────────────────────────────────
  1. ./dev.sh up           # Start everything in Docker

── Active development workflow (host processes) ─────────────────────────────
  1. ./dev.sh infra        # Start db, valkey, keycloak, mailhog in Docker
  2. ./dev.sh migrator     # Apply DB migrations on host
  3. ./dev.sh api          # Start API on host
  4. ./dev.sh worker       # Start background worker on host (optional)
  5. ./dev.sh frontend     # Start Vite dev server on host

── Runtime URLs ─────────────────────────────────────────────────────────────
  Frontend: http://localhost:${FRONTEND_PORT}
  API:      http://localhost:${API_PORT}
  Swagger:  http://localhost:${API_PORT}/swagger
  Keycloak: http://localhost:${KEYCLOAK_PORT}  (admin: admin / changeme)
  MailHog:  http://localhost:${MAILHOG_UI_PORT}
EOF
}

dev_dispatch "$@"
