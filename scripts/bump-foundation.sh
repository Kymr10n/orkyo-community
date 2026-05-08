#!/usr/bin/env bash
# bump-foundation.sh <version>
#
# Pins all Orkyo.Foundation NuGet + npm package references to <version>
# and regenerates lock files. No git operations — commit manually after.
#
# Usage:
#   scripts/bump-foundation.sh 0.1.19

set -euo pipefail
cd "$(dirname "$0")/.."

# ── Load .env if present (provides auth token for GitHub Packages) ────────────
if [[ -f .env ]]; then
  while IFS= read -r _line || [[ -n "$_line" ]]; do
    [[ "$_line" =~ ^[[:space:]]*# ]] && continue
    [[ -z "${_line// }" ]] && continue
    [[ "$_line" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] || continue
    export "$_line"
  done < .env
fi

# ── Colours ───────────────────────────────────────────────────────────────────
BLUE='\033[0;34m'; GREEN='\033[0;32m'; RED='\033[0;31m'; BOLD='\033[1m'; NC='\033[0m'
log()     { echo -e "${BLUE}[bump-foundation]${NC} $*"; }
success() { echo -e "${GREEN}[bump-foundation]${NC} $*"; }
die()     { echo -e "${RED}[bump-foundation]${NC} $*" >&2; exit 1; }

# ── Argument validation ───────────────────────────────────────────────────────
VERSION="${1:-}"
[[ -n "$VERSION" ]] || die "Usage: scripts/bump-foundation.sh <version>  (e.g. 0.1.19)"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Version must be X.Y.Z semver (got: '$VERSION')"

# ── Bump .csproj references ───────────────────────────────────────────────────
log "Bumping NuGet references to ${BOLD}${VERSION}${NC}..."
BUMPED=()
while IFS= read -r -d '' CSPROJ; do
  if grep -qE "Include=\"Orkyo\.(Foundation|Foundation\.Migrations|Migration\.Abstractions|Migrator|Shared)\"" "$CSPROJ"; then
    sed -i -E \
      "s@(Include=\"Orkyo\.(Foundation|Foundation\.Migrations|Migration\.Abstractions|Migrator|Shared)\" Version=\")[^\"]*\"@\1${VERSION}\"@g" \
      "$CSPROJ"
    BUMPED+=("$CSPROJ")
    log "  bumped: $CSPROJ"
  fi
done < <(find backend -name "*.csproj" -print0)

[[ ${#BUMPED[@]} -gt 0 ]] || die "No .csproj files found with foundation package references"

# ── Bump frontend/package.json ────────────────────────────────────────────────
PACKAGE_JSON="frontend/package.json"
if grep -q '"@kymr10n/foundation"' "$PACKAGE_JSON"; then
  sed -i "s|\"@kymr10n/foundation\": \"[^\"]*\"|\"@kymr10n/foundation\": \"${VERSION}\"|" "$PACKAGE_JSON"
  BUMPED+=("$PACKAGE_JSON")
  log "  bumped: $PACKAGE_JSON"
fi

# ── Regenerate .NET lock files ────────────────────────────────────────────────
log "Restoring .NET packages..."
command -v dotnet > /dev/null 2>&1 || die ".NET SDK not found"
dotnet restore Orkyo.Community.slnx --verbosity quiet

# ── Regenerate npm lock file ──────────────────────────────────────────────────
log "Installing npm packages..."
command -v npm > /dev/null 2>&1 || die "npm not found"
_NPM_AUTH_TOKEN="${NODE_AUTH_TOKEN:-${GITHUB_TOKEN:-${GHCR_TOKEN:-}}}"
if [[ -n "$_NPM_AUTH_TOKEN" ]]; then
  NPMRC_FILE="frontend/.npmrc"
  trap 'rm -f "$NPMRC_FILE"' EXIT
  { echo "@kymr10n:registry=https://npm.pkg.github.com"
    echo "//npm.pkg.github.com/:_authToken=${_NPM_AUTH_TOKEN}"; } > "$NPMRC_FILE"
fi
npm install --prefix frontend --silent

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
success "Foundation pinned to ${BOLD}${VERSION}${NC}. Files updated:"
for f in "${BUMPED[@]}"; do echo "  $f"; done
echo ""
echo "Review the changes, then commit:"
echo "  git add -A && git commit -m \"chore: bump foundation to ${VERSION}\""
echo ""
