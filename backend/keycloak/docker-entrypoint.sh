#!/bin/sh
# Substitute operator-supplied env vars into the realm template before Keycloak
# imports it. The Keycloak base image (ubi9) ships no package manager and no
# envsubst — only sed — so we use sed, escaping the sed-replacement specials
# (\, &, and the | delimiter) so URLs/secrets can't corrupt the substitution.
# Only these two named vars are replaced; any other ${...} in the JSON (e.g.
# Keycloak's own expressions) is left intact.
set -e

esc() { printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/&/\\&/g' -e 's/|/\\|/g'; }

sed \
  -e "s|\${APP_BASE_URL}|$(esc "$APP_BASE_URL")|g" \
  -e "s|\${KEYCLOAK_BACKEND_CLIENT_SECRET}|$(esc "$KEYCLOAK_BACKEND_CLIENT_SECRET")|g" \
  < /opt/keycloak/data/import/realm.json.template \
  > /opt/keycloak/data/import/realm.json

exec /opt/keycloak/bin/kc.sh start --optimized --import-realm
