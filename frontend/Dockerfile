FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:22-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
RUN apk upgrade --no-cache

COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY runtime_config.tpl.js /runtime_config.tpl.js
COPY docker-entrypoint-prod.sh /docker-entrypoint-prod.sh
RUN chmod +x /docker-entrypoint-prod.sh \
    && chown -R nginx:nginx /usr/share/nginx/html /runtime_config.tpl.js \
    && chmod -R 755 /usr/share/nginx/html

USER nginx
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["/docker-entrypoint-prod.sh"]
