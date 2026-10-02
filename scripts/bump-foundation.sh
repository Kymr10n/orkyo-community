#!/usr/bin/env bash
# bump-foundation.sh <version>
#
# Pins Orkyo.Foundation to <version>: the NuGet packages (Directory.Build.props), the npm
# package (frontend/package.json + regenerated lock), and the Keycloak base image
# (backend/keycloak/Dockerfile). The three are one release of foundation and move together.
# No git operations — commit manually after.
#
# The Keycloak image is not composed from a naming rule here. Foundation publishes exactly one
# per release, ghcr.io/kymr10n/keycloak:<keycloak line>-orkyo-<version>; this script asks the
# registry which tag carries <version> and pins it by digest. So a Keycloak minor bump in
# foundation (a new tag line) needs no edit in this repo, and a release without a Keycloak
# image stops the bump before any file is touched.
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

# ── Pre-flight: resolve the Keycloak image foundation published for this version ──
KC_DOCKERFILE="backend/keycloak/Dockerfile"
KC_REPOSITORY="ghcr.io/kymr10n/keycloak"
[[ -f "$KC_DOCKERFILE" ]] || die "$KC_DOCKERFILE not found"
grep -qE '^ARG FOUNDATION_KC_IMAGE=' "$KC_DOCKERFILE" \
  || die "$KC_DOCKERFILE does not declare 'ARG FOUNDATION_KC_IMAGE=' — refusing to bump"
command -v node > /dev/null 2>&1 || die "node not found"

# Prints <tag>@<digest>. The package-versions API returns each image's digest with its tags,
# newest first, so the wanted release is on the first page in practice.
KC_REF="$(KC_TOKEN="$_NPM_AUTH_TOKEN" node --input-type=module - "$VERSION" <<'JS'
const version = process.argv[2];
const wanted = new RegExp(`^\\d+\\.\\d+-orkyo-${version.replace(/[.+]/g, '\\$&')}$`);
const matches = [];
for (let page = 1; page <= 10 && matches.length === 0; page++) {
  const response = await fetch(
    `https://api.github.com/users/Kymr10n/packages/container/keycloak/versions?per_page=100&page=${page}`,
    { headers: { Authorization: `Bearer ${process.env.KC_TOKEN}`, Accept: 'application/vnd.github+json' } });
  if (!response.ok) {
    console.error(`GitHub packages API answered ${response.status} (the token needs read:packages)`);
    process.exit(2);
  }
  const versions = await response.json();
  if (versions.length === 0) break;
  for (const image of versions) {
    for (const tag of image.metadata?.container?.tags ?? []) {
      if (wanted.test(tag)) matches.push(`${tag}@${image.name}`);
    }
  }
}
if (matches.length !== 1) {
  console.error(matches.length === 0
    ? `no Keycloak image is tagged <line>-orkyo-${version}`
    : `more than one Keycloak image is tagged for ${version}: ${matches.join(', ')}`);
  process.exit(3);
}
console.log(matches[0]);
JS
)" || die "Could not resolve the foundation Keycloak image for ${VERSION} (reason above). Nothing was changed. Every foundation release publishes one, so a missing tag means that release is incomplete — do not pin it."
[[ "$KC_REF" =~ ^[0-9]+\.[0-9]+-orkyo-.+@sha256:[0-9a-f]{64}$ ]] || die "Unexpected Keycloak image reference: '$KC_REF'"
log "  resolved: ${KC_REPOSITORY}:${KC_REF}"

BUMPED=()

# ── Bump the Keycloak base image (same release as the packages below) ─────────
sed -i -E "s|^(ARG FOUNDATION_KC_IMAGE=).*|\\1${KC_REPOSITORY}:${KC_REF}|" "$KC_DOCKERFILE"
grep -qF "ARG FOUNDATION_KC_IMAGE=${KC_REPOSITORY}:${KC_REF}" "$KC_DOCKERFILE" || die "$KC_DOCKERFILE bump did not take effect"
BUMPED+=("$KC_DOCKERFILE")
log "  bumped: $KC_DOCKERFILE → ${KC_REF%%@*}"

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
  npm install --prefix frontend --include=optional
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
