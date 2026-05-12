#!/usr/bin/env bash
# bump-foundation.sh <version>
#
# Pins Orkyo.Foundation to <version> by editing Directory.Build.props (backend)
# and frontend/package.json (frontend), then regenerates frontend/package-lock.json.
# No git operations — commit manually after.
#
# Accepts stable semver (0.1.24) or nightly tags (0.1.24-nightly.20260512.abc1234).
#
# Usage:
#   scripts/bump-foundation.sh 0.1.24
#   scripts/bump-foundation.sh 0.1.24-nightly.20260512.abc1234

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
[[ -n "$VERSION" ]] || die "Usage: scripts/bump-foundation.sh <version>"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] \
  || die "Version must be X.Y.Z or X.Y.Z-prerelease (got: '$VERSION')"

# ── Pre-flight: require auth token before touching any files ──────────────────
_NPM_AUTH_TOKEN="${NODE_AUTH_TOKEN:-${GITHUB_TOKEN:-${GHCR_TOKEN:-}}}"
[[ -n "$_NPM_AUTH_TOKEN" ]] || die "No auth token found. Set NODE_AUTH_TOKEN, GITHUB_TOKEN, or GHCR_TOKEN (or add it to .env) — required to pull @kymr10n/foundation from GitHub Packages."

BUMPED=()

# ── Bump Directory.Build.props (backend pin) ──────────────────────────────────
PROPS="Directory.Build.props"
[[ -f "$PROPS" ]] || die "$PROPS not found at repo root"
grep -q "<OrkyoFoundationVersion" "$PROPS" \
  || die "$PROPS does not declare <OrkyoFoundationVersion> — refusing to bump"
sed -i -E \
  "s|(<OrkyoFoundationVersion[^>]*>)[^<]*(</OrkyoFoundationVersion>)|\1${VERSION}\2|" \
  "$PROPS"
grep -qF ">${VERSION}<" "$PROPS" || die "$PROPS bump did not take effect"
BUMPED+=("$PROPS")
log "  bumped: $PROPS → ${VERSION}"

# ── Bump frontend/package.json + regenerate lock ──────────────────────────────
PACKAGE_JSON="frontend/package.json"
if grep -q '"@kymr10n/foundation"' "$PACKAGE_JSON"; then
  sed -i "s|\"@kymr10n/foundation\": \"[^\"]*\"|\"@kymr10n/foundation\": \"${VERSION}\"|" "$PACKAGE_JSON"
  BUMPED+=("$PACKAGE_JSON")
  log "  bumped: $PACKAGE_JSON → ${VERSION}"

  command -v npm > /dev/null 2>&1 || die "npm not found"
  NPMRC_FILE="frontend/.npmrc"
  trap 'rm -f "$NPMRC_FILE"' EXIT
  { echo "@kymr10n:registry=https://npm.pkg.github.com"
    echo "//npm.pkg.github.com/:_authToken=${_NPM_AUTH_TOKEN}"; } > "$NPMRC_FILE"
  log "Installing npm packages to regenerate package-lock.json..."
  npm install --prefix frontend --include=optional --silent
  BUMPED+=("frontend/package-lock.json")
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
success "Foundation pinned to ${BOLD}${VERSION}${NC}. Files updated:"
for f in "${BUMPED[@]}"; do echo "  $f"; done
echo ""
echo "Review the changes, then commit:"
echo "  git add -A && git commit -m \"chore: bump foundation to ${VERSION}\""
echo ""
