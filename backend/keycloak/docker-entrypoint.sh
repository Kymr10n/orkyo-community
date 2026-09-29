#!/bin/sh
# Substitute operator-supplied env vars into the realm template before Keycloak
# imports it. The Keycloak base image (ubi9) ships no package manager and no
# envsubst — only sed — so we use sed, escaping the sed-replacement specials
# (\, &, and the | delimiter) so URLs/secrets can't corrupt the substitution.
# Only these three named vars are replaced; any other ${...} in the JSON (e.g.
# Keycloak's own expressions) is left intact.
set -e

esc() { printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/&/\\&/g' -e 's/|/\\|/g'; }

# The WebAuthn relying-party ID must be the bare host of APP_BASE_URL (no
# scheme, path, or port), so derive APP_BASE_HOST with parameter expansion.
APP_BASE_HOST="${APP_BASE_URL#*://}"
APP_BASE_HOST="${APP_BASE_HOST%%/*}"
APP_BASE_HOST="${APP_BASE_HOST%%:*}"
if [ -z "$APP_BASE_HOST" ]; then
  echo "docker-entrypoint: cannot derive a host from APP_BASE_URL='${APP_BASE_URL}'" >&2
  exit 1
fi

sed \
  -e "s|\${APP_BASE_URL}|$(esc "$APP_BASE_URL")|g" \
  -e "s|\${APP_BASE_HOST}|$(esc "$APP_BASE_HOST")|g" \
  -e "s|\${KEYCLOAK_BACKEND_CLIENT_SECRET}|$(esc "$KEYCLOAK_BACKEND_CLIENT_SECRET")|g" \
  < /opt/keycloak/data/import/realm.json.template \
  > /opt/keycloak/data/import/realm.json

exec /opt/keycloak/bin/kc.sh start --optimized --import-realm
