#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

log()     { echo "[setup] $*"; }
success() { echo "[setup] $*"; }
warn()    { echo "[setup] WARNING: $*" >&2; }
error()   { echo "[setup] ERROR: $*" >&2; }

check_cmd() {
  if command -v "$1" >/dev/null 2>&1; then
    success "$1 found"
  else
    error "$1 not found — please install it before continuing"
    exit 1
  fi
}

log "Installing git hooks"
git config core.hooksPath .githooks
success "git hooks installed (.githooks/pre-push)"

log "Checking prerequisites"
check_cmd dotnet
check_cmd node
check_cmd npm

log "Restoring backend dependencies"
dotnet restore Orkyo.Community.slnx

log "Installing frontend dependencies"
cd frontend && npm ci && cd ..

success "Setup complete — run 'dotnet build Orkyo.Community.slnx' to verify"
