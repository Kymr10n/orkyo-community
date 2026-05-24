#!/bin/sh
# Substitute operator-supplied env vars into the realm template before Keycloak
# imports it. Only interpolates named vars to avoid expanding other ${...}
# patterns in the JSON (e.g. Keycloak's own expressions).
set -e

envsubst '${APP_BASE_URL} ${KEYCLOAK_BACKEND_CLIENT_SECRET}' \
  < /opt/keycloak/data/import/realm.json.template \
  > /opt/keycloak/data/import/realm.json

exec /opt/keycloak/bin/kc.sh start --optimized --import-realm
