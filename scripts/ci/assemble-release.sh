#!/usr/bin/env bash
# Assemble the community self-host release bundle.
#
# Usage: assemble-release.sh <version> <output-dir>
#   version     Semver string (e.g. 1.2.0) — used in ZIP filename and docker image tags
#   output-dir  Directory to write the ZIP and checksum into

set -euo pipefail

VERSION="${1:?Usage: $0 <version> <output-dir>}"
OUTDIR="${2:?Usage: $0 <version> <output-dir>}"
BUNDLE_NAME="orkyo-community-v${VERSION}"
STAGING="$(mktemp -d)/bundle"

mkdir -p "$STAGING" "$OUTDIR"

echo "Assembling release bundle ${BUNDLE_NAME}..."

# ── Core files ────────────────────────────────────────────────────────────────
cp compose.local.yml "$STAGING/docker-compose.yml"
cp .env.template "$STAGING/.env.template"
[ -f README.md ] && cp README.md "$STAGING/README.md"
[ -f RELEASE_NOTES.md ] && cp RELEASE_NOTES.md "$STAGING/RELEASE_NOTES.md" || \
  echo "# Release Notes — v${VERSION}" > "$STAGING/RELEASE_NOTES.md"

# ── Config directories ────────────────────────────────────────────────────────
for dir in config/keycloak config/nginx; do
  if [ -d "$dir" ]; then
    mkdir -p "$STAGING/$dir"
    cp -r "$dir/." "$STAGING/$dir/"
  fi
done

# ── Lifecycle scripts ─────────────────────────────────────────────────────────
mkdir -p "$STAGING/scripts"
for script in scripts/bootstrap.sh scripts/upgrade.sh scripts/backup.sh; do
  if [ -f "$script" ]; then
    cp "$script" "$STAGING/scripts/"
    chmod +x "$STAGING/scripts/$(basename "$script")"
  fi
done

# ── Stamp image versions into docker-compose.yml ─────────────────────────────
# Replace :latest / :sha-* tags with the pinned version tag
sed -i "s|:sha-[a-f0-9]*|:${VERSION}|g" "$STAGING/docker-compose.yml"
sed -i "s|:latest|:${VERSION}|g" "$STAGING/docker-compose.yml"

# ── Package ───────────────────────────────────────────────────────────────────
ZIP_PATH="${OUTDIR}/${BUNDLE_NAME}.zip"
(cd "$(dirname "$STAGING")" && zip -r "$ZIP_PATH" "bundle/")
sha256sum "$ZIP_PATH" > "${ZIP_PATH}.sha256"

echo ""
echo "Bundle ready:"
echo "  ${ZIP_PATH}"
echo "  ${ZIP_PATH}.sha256"
sha256sum -c "${ZIP_PATH}.sha256"
