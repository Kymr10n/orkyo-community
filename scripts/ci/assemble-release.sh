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
# compose.yml keeps its fail-fast ${ORKYO_VERSION:?} references untouched — the
# operator's .env is the single place the version lives, so editing
# ORKYO_VERSION there and running `docker compose pull && docker compose up -d`
# performs a real upgrade. Only the bundled .env.template gets the released
# version stamped in as its default.
sed -i "s|^ORKYO_VERSION=.*|ORKYO_VERSION=${VERSION}|" "${STAGING_DIR}/${BUNDLE_NAME}/.env.template"

# ── 3. Copy README and LICENSE ───────────────────────────────────────────────
[ -f "${REPO_ROOT}/README.md" ] && cp "${REPO_ROOT}/README.md" "${STAGING_DIR}/${BUNDLE_NAME}/README.md"
[ -f "${REPO_ROOT}/LICENSE" ]   && cp "${REPO_ROOT}/LICENSE"   "${STAGING_DIR}/${BUNDLE_NAME}/LICENSE"

# ── 4. Package ────────────────────────────────────────────────────────────────
# Two formats, same content. The tar.gz is for Linux hosts without `unzip` (NAS
# appliances, minimal images); the zip stays for Windows and Portainer users.
mkdir -p "$OUTDIR"
(cd "$STAGING_DIR" \
  && zip -rq "${BUNDLE_NAME}.zip" "${BUNDLE_NAME}" \
  && tar -czf "${BUNDLE_NAME}.tar.gz" "${BUNDLE_NAME}")
for ext in zip tar.gz; do
  cp "${STAGING_DIR}/${BUNDLE_NAME}.${ext}" "${OUTDIR}/${BUNDLE_NAME}.${ext}"
  (cd "$OUTDIR" && sha256sum "${BUNDLE_NAME}.${ext}" > "${BUNDLE_NAME}.${ext}.sha256")
done

echo ""
echo "Bundle ready:"
for ext in zip tar.gz; do
  echo "  ${OUTDIR}/${BUNDLE_NAME}.${ext}"
  echo "  ${OUTDIR}/${BUNDLE_NAME}.${ext}.sha256"
done
(cd "$OUTDIR" && sha256sum -c "${BUNDLE_NAME}.zip.sha256" "${BUNDLE_NAME}.tar.gz.sha256")
