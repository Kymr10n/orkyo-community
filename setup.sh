#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
  echo -e "${BLUE}[setup]${NC} $*"
}

success() {
  echo -e "${GREEN}[setup]${NC} $*"
}

warn() {
  echo -e "${YELLOW}[setup]${NC} $*"
}

error() {
  echo -e "${RED}[setup]${NC} $*" >&2
}

check_cmd() {
  if command -v "$1" >/dev/null 2>&1; then
    success "$1 found"
  else
    error "$1 not found"
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
