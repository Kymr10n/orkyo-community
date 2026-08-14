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

log "Restoring backend dependencies"
dotnet restore Orkyo.Community.slnx

log "Installing frontend dependencies"
cd frontend && npm ci && cd ..

success "Setup complete — run 'dotnet build Orkyo.Community.slnx' to verify"
