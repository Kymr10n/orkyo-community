FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
# GitHub Packages auth is mounted as a BuildKit secret so the token never lands
# in an image layer. Required to fetch @kymr10n/foundation from npm.pkg.github.com.
RUN --mount=type=secret,id=ghcr_token \
    printf '@kymr10n:registry=https://npm.pkg.github.com\n//npm.pkg.github.com/:_authToken=%s\n' \
      "$(cat /run/secrets/ghcr_token)" > .npmrc && \
    npm ci && \
    rm -f .npmrc

FROM node:22-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime
RUN apk upgrade --no-cache

COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
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
