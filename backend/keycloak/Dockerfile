# syntax=docker/dockerfile:1.7
#
# Custom Keycloak image for Orkyo Community.
# Layers on top of the foundation Keycloak image (which bakes in the Orkyo theme)
# and adds the token-exchange feature required by Community.
#
# Build context: repo root (so release/config/keycloak/realm.json is reachable).

ARG FOUNDATION_KC_IMAGE=ghcr.io/kymr10n/keycloak:26.6-orkyo-0.2.2

# ── Stage 1: build (add token-exchange to foundation's pre-built server) ──────
# Foundation already includes: recovery-codes, postgres, health, and the Orkyo theme.
# Community also requires token-exchange for BFF ↔ Keycloak service accounts.
FROM ${FOUNDATION_KC_IMAGE} AS builder

ENV KC_FEATURES=token-exchange,recovery-codes

RUN /opt/keycloak/bin/kc.sh build

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
ARG FOUNDATION_KC_IMAGE
FROM ${FOUNDATION_KC_IMAGE}
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community

COPY --from=builder /opt/keycloak/ /opt/keycloak/

# Store the realm as a template; docker-entrypoint.sh runs envsubst at startup
# to inject APP_BASE_URL before Keycloak imports it.
COPY release/config/keycloak/realm.json /opt/keycloak/data/import/realm.json.template
COPY backend/keycloak/docker-entrypoint.sh /docker-entrypoint.sh

USER root
RUN chmod +x /docker-entrypoint.sh

# Run as the non-root keycloak user (UID 1000) — matches the upstream image default.
USER 1000

# Entrypoint substitutes APP_BASE_URL into the realm template, then starts Keycloak.
# `--optimized` skips re-running build (stage 1 already did it).
# `--import-realm` is idempotent: skipped if the realm already exists.
ENTRYPOINT ["/docker-entrypoint.sh"]
