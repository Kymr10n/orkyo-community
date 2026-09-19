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
# pre-commit installs into .git/hooks, which git ignores whenever core.hooksPath is set.
# An earlier version of this script pointed core.hooksPath at .githooks/ and so silently
# disabled every pre-commit hook, including the commit-msg docs-impact check. Clear it first.
if git config --get core.hooksPath >/dev/null 2>&1; then
  git config --unset core.hooksPath
  log "Cleared core.hooksPath; pre-commit owns the hooks now"
fi
if command -v pre-commit >/dev/null 2>&1; then
  pre-commit install --install-hooks
  success "git hooks installed (pre-commit, commit-msg, pre-push)"
else
  error "pre-commit not found — run: pip install pre-commit && ./setup.sh"
  exit 1
fi

log "Checking prerequisites"
check_cmd dotnet
check_cmd node
check_cmd npm

# The backend consumes Orkyo.Foundation through the sibling checkout, not the package
# feed: the csproj picks a ProjectReference when ../orkyo-foundation exists and falls back
# to a PackageReference otherwise. Without the sibling AND without feed credentials the
# restore below dies in an opaque NU1101, so say what is actually wrong.
if [ ! -f "../orkyo-foundation/backend/src/Orkyo.Foundation.Web.csproj" ]; then
  error "orkyo-foundation is not checked out next to this repo."
  error "  expected: $(cd .. && pwd)/orkyo-foundation"
  error "  clone it there, or restore in package mode with feed credentials:"
  error "    OrkyoUseFoundationPackage=true dotnet restore Orkyo.Community.slnx"
  exit 1
fi

if [[ ! -f .env ]]; then
  warn ".env not found"
  cp .env.template .env
  success "Created .env from .env.template"
  warn "Review .env before starting local services"
fi

if ! ./scripts/check-env.sh; then
  echo ""
  read -r -p "Continue anyway? (y/N) " reply
  if [[ ! "$reply" =~ ^[Yy]$ ]]; then
    error "Aborted"
    exit 1
  fi
fi

log "Restoring backend dependencies"
dotnet restore Orkyo.Community.slnx

log "Installing frontend dependencies"
cd frontend && npm ci && cd ..

success "Setup complete — run 'dotnet build Orkyo.Community.slnx' to verify"
