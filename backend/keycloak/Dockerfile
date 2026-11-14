# syntax=docker/dockerfile:1.7
#
# Custom Keycloak image for Orkyo Community.
# Layers on top of the foundation Keycloak image (which bakes in the Orkyo theme)
# and adds the token-exchange feature required by Community.
#
# Build context: repo root (so release/config/keycloak/realm.json is reachable).

# The foundation Keycloak image for the pinned foundation version, by digest. Written by
# scripts/bump-foundation.sh together with the NuGet and npm pins — never edit by hand, and
# never override it with a build arg in CI: KeycloakBasePinTests fails if it drifts from
# Directory.Build.props.
ARG FOUNDATION_KC_IMAGE=ghcr.io/kymr10n/keycloak:26.8-orkyo-1.10.1-nightly.20261114.0c2d2a6@sha256:b0314cea3981448fe7e5b8fad80796888243858f4af8263e7a36a720cc7b027e

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

# Must match the features the builder stage compiled in. `start --optimized`
# refuses to boot if a build-time option (here kc.features) differs from what
# was persisted, so re-declare it in the runtime stage.
ENV KC_FEATURES=token-exchange,recovery-codes

# Store the realm as a template; docker-entrypoint.sh runs envsubst at startup
# to inject APP_BASE_URL before Keycloak imports it.
COPY release/config/keycloak/realm.json /opt/keycloak/data/import/realm.json.template
COPY backend/keycloak/docker-entrypoint.sh /docker-entrypoint.sh

USER root
RUN chmod +x /docker-entrypoint.sh
# COPY lands import/ + the template as root, but the entrypoint (UID 1000) renders
# realm.json into this dir at startup — so it must be writable by the keycloak user.
RUN chown -R 1000:0 /opt/keycloak/data/import

# Run as the non-root keycloak user (UID 1000) — matches the upstream image default.
USER 1000

# Entrypoint substitutes APP_BASE_URL into the realm template, then starts Keycloak.
# `--optimized` skips re-running build (stage 1 already did it).
# `--import-realm` is idempotent: skipped if the realm already exists.
ENTRYPOINT ["/docker-entrypoint.sh"]
