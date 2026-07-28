#!/usr/bin/env bash
# check-foundation-parity.sh [foundation-version]
#
# Builds the backend the way CI does — against the PINNED foundation package
# instead of the sibling working-tree checkout (foundation#99). With the
# sibling present, every local build compiles against working-tree foundation,
# so a foundation API change that is not yet released is invisible locally and
# first fails in CI. This script forces the csprojs' package branch
# (-p:OrkyoUseFoundationPackage=true) and builds against the version pinned in
# Directory.Build.props, or an explicit version argument.
#
# Needs a GitHub Packages token: GITHUB_TOKEN, a .env with one, or `gh auth token`.
#
# Usage:
#   scripts/check-foundation-parity.sh                # pinned version
#   scripts/check-foundation-parity.sh 0.10.3         # explicit version
#
# Runs from the pre-push hook; skip with SKIP=foundation-package-parity git push.

set -euo pipefail
cd "$(dirname "$0")/.."

# ── Load .env if present (provides auth token for GitHub Packages) ────────────
if [[ -f .env ]]; then
  while IFS= read -r _line || [[ -n "$_line" ]]; do
    [[ "$_line" =~ ^[[:space:]]*# ]] && continue
    [[ -z "${_line// }" ]] && continue
    [[ "$_line" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] || continue
    # shellcheck disable=SC2163  # exporting the KEY=VALUE line is the point
    export "$_line"
  done < .env
fi

# ── Colours ───────────────────────────────────────────────────────────────────
BLUE='\033[0;34m'; GREEN='\033[0;32m'; RED='\033[0;31m'; NC='\033[0m'
log()     { echo -e "${BLUE}[foundation-parity]${NC} $*"; }
success() { echo -e "${GREEN}[foundation-parity]${NC} $*"; }
fail()    { echo -e "${RED}[foundation-parity]${NC} $*" >&2; exit 1; }

# ── Resolve inputs ────────────────────────────────────────────────────────────
SLN=$(find . -maxdepth 1 -name '*.slnx' | head -1)
[[ -n "${SLN}" ]] || fail "no .slnx at the repo root — run from a repo checkout"

VERSION="${1:-}"
if [[ -z "${VERSION}" ]]; then
  VERSION=$(sed -n 's/.*<OrkyoFoundationVersion[^>]*>\([^<]*\)<.*/\1/p' Directory.Build.props | head -1)
  [[ -n "${VERSION}" ]] || fail "could not read OrkyoFoundationVersion from Directory.Build.props"
fi

TOKEN="${GITHUB_TOKEN:-}"
if [[ -z "${TOKEN}" ]] && command -v gh >/dev/null 2>&1; then
  TOKEN=$(gh auth token 2>/dev/null || true)
fi
[[ -n "${TOKEN}" ]] || fail "no GitHub Packages token (set GITHUB_TOKEN, put one in .env, or 'gh auth login')"

# ── Temp NuGet config with the GitHub feed ────────────────────────────────────
# The repo's nuget.config deliberately has no GitHub source (absent credentials
# would break normal restores); package-mode restore needs one, so it goes in a
# throwaway config that never touches the tree.
TMP_CFG=$(mktemp -d)
trap 'rm -rf "${TMP_CFG}"' EXIT
export ORKYO_PARITY_TOKEN="${TOKEN}"
cat > "${TMP_CFG}/nuget.config" <<'CFG'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/kymr10n/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="kymr10n" />
      <add key="ClearTextPassword" value="%ORKYO_PARITY_TOKEN%" />
    </github>
  </packageSourceCredentials>
</configuration>
CFG

# ── Restore + build in package mode ───────────────────────────────────────────
log "restoring ${SLN} against Orkyo.Foundation ${VERSION} (package mode)…"
dotnet restore "${SLN}" --nologo \
  --configfile "${TMP_CFG}/nuget.config" \
  -p:OrkyoUseFoundationPackage=true \
  -p:OrkyoFoundationVersion="${VERSION}"

log "building (Release, warnaserror)…"
if ! dotnet build "${SLN}" -c Release --no-restore --nologo -warnaserror \
    -p:OrkyoUseFoundationPackage=true \
    -p:OrkyoFoundationVersion="${VERSION}"; then
  # Leave the tree usable before failing (see below).
  dotnet restore "${SLN}" --nologo >/dev/null || true
  fail "package-mode build FAILED — this change depends on unreleased sibling foundation code"
fi

# Package-mode restore rewrote every obj/project.assets.json; put the tree back
# in sibling mode so the next normal build doesn't silently use the package.
log "restoring sibling mode…"
dotnet restore "${SLN}" --nologo >/dev/null

success "CI parity OK — builds cleanly against Orkyo.Foundation ${VERSION}"
