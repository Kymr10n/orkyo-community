FROM node:26-alpine@sha256:e88a35be04478413b7c71c455cd9865de9b9360e1f43456be5951032d7ac1a66 AS deps
WORKDIR /app
COPY package.json package-lock.json ./
# The postinstall hook (npm ci, below) runs scripts/sync-foundation-build.mjs, so the
# script must be present in this deps stage. It no-ops without a sibling foundation checkout.
COPY scripts ./scripts
# GitHub Packages auth is mounted as a BuildKit secret so the token never lands
# in an image layer. Required to fetch @kymr10n/foundation from npm.pkg.github.com.
RUN --mount=type=secret,id=ghcr_token \
    printf '@kymr10n:registry=https://npm.pkg.github.com\n//npm.pkg.github.com/:_authToken=%s\n' \
      "$(cat /run/secrets/ghcr_token)" > .npmrc && \
    npm ci && \
    rm -f .npmrc

FROM node:26-alpine@sha256:e88a35be04478413b7c71c455cd9865de9b9360e1f43456be5951032d7ac1a66 AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM nginx:1.30-alpine@sha256:0d3b80406a13a767339fbe2f41406d6c7da727ab89cf8fae399e81f780f814d1 AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
# Base image is digest-pinned; nightly security-refresh.yml re-resolves the
# pinned tag and rebuilds against the current upstream digest. No ad-hoc
# `apk upgrade` in build steps (it makes layers non-reproducible).

COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY runtime_config.tpl.js /runtime_config.tpl.js
COPY docker-entrypoint-prod.sh /docker-entrypoint-prod.sh
RUN chmod +x /docker-entrypoint-prod.sh \
    && chown -R nginx:nginx /usr/share/nginx/html /runtime_config.tpl.js \
    && chmod -R 755 /usr/share/nginx/html \
    && mkdir -p /var/cache/nginx/client_temp /var/cache/nginx/proxy_temp /var/cache/nginx/fastcgi_temp /var/cache/nginx/uwsgi_temp /var/cache/nginx/scgi_temp \
    && chown -R nginx:nginx /var/cache/nginx \
    && touch /var/run/nginx.pid \
    && chown nginx:nginx /var/run/nginx.pid

USER nginx
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["/docker-entrypoint-prod.sh"]
