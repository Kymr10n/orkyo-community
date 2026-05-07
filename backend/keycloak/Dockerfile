# syntax=docker/dockerfile:1.7
#
# Custom Keycloak image with the Orkyo Community realm pre-imported.
# Bakes config at image-build time so operators don't bind-mount realm.json.
#
# Build context: repo root (so release/config/keycloak/realm.json is reachable).

# ── Stage 1: build (config baking) ────────────────────────────────────────────
FROM quay.io/keycloak/keycloak:26.1 AS builder

ENV KC_DB=postgres \
    KC_HEALTH_ENABLED=true \
    KC_METRICS_ENABLED=true \
    KC_HTTP_ENABLED=true \
    KC_FEATURES=token-exchange

COPY release/config/keycloak/realm.json /opt/keycloak/data/import/realm.json

RUN /opt/keycloak/bin/kc.sh build

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM quay.io/keycloak/keycloak:26.1
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community

COPY --from=builder /opt/keycloak/ /opt/keycloak/

# `--optimized` skips re-running build (stage 1 already did it).
# `--import-realm` is idempotent: skipped if the realm already exists.
ENTRYPOINT ["/opt/keycloak/bin/kc.sh", "start", "--optimized", "--import-realm"]
