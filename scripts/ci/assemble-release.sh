#!/usr/bin/env bash
# Assemble the community self-host release bundle.
#
# Usage: assemble-release.sh <version> <output-dir>
#   version     Semver string (e.g. 1.2.0) — used in ZIP filename and image tags
#   output-dir  Directory to write the ZIP and checksum into

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
VERSION="${1:?Usage: $0 <version> <output-dir>}"
OUTDIR="${2:?Usage: $0 <version> <output-dir>}"
BUNDLE_NAME="orkyo-community-v${VERSION}"
STAGING_DIR="$(mktemp -d)"

cleanup() { rm -rf "$STAGING_DIR"; }
trap cleanup EXIT

echo "Assembling ${BUNDLE_NAME}.zip..."

# ── 1. Copy release/ directory ────────────────────────────────────────────────
cp -r "${REPO_ROOT}/release" "${STAGING_DIR}/${BUNDLE_NAME}"

# ── 2. Stamp version ──────────────────────────────────────────────────────────
sed -i "s|CHANGE_ME_VERSION|${VERSION}|g" "${STAGING_DIR}/${BUNDLE_NAME}/.env.template"
sed -i "s|\${ORKYO_VERSION}|${VERSION}|g" "${STAGING_DIR}/${BUNDLE_NAME}/compose.yml" || true

# ── 3. Copy README ────────────────────────────────────────────────────────────
[ -f "${REPO_ROOT}/README.md" ] && cp "${REPO_ROOT}/README.md" "${STAGING_DIR}/${BUNDLE_NAME}/README.md"
[ -f "${REPO_ROOT}/LICENSE" ]   && cp "${REPO_ROOT}/LICENSE"   "${STAGING_DIR}/${BUNDLE_NAME}/LICENSE"

# ── 4. Ensure scripts are executable ─────────────────────────────────────────
find "${STAGING_DIR}/${BUNDLE_NAME}" -name "*.sh" -exec chmod +x {} \;

# ── 5. Package ────────────────────────────────────────────────────────────────
mkdir -p "$OUTDIR"
(cd "$STAGING_DIR" && zip -rq "${BUNDLE_NAME}.zip" "${BUNDLE_NAME}")
cp "${STAGING_DIR}/${BUNDLE_NAME}.zip" "${OUTDIR}/${BUNDLE_NAME}.zip"
(cd "$OUTDIR" && sha256sum "${BUNDLE_NAME}.zip" > "${BUNDLE_NAME}.zip.sha256")

echo ""
echo "Bundle ready:"
echo "  ${OUTDIR}/${BUNDLE_NAME}.zip"
echo "  ${OUTDIR}/${BUNDLE_NAME}.zip.sha256"
(cd "$OUTDIR" && sha256sum -c "${BUNDLE_NAME}.zip.sha256")
