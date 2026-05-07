# Orkyo Community — Quick Start

Two supported deployment paths: **Portainer Stacks** (single-file paste, recommended) and **Docker Compose CLI**. Both consume the same [compose.yml](../compose.yml).

## Prerequisites

- Docker 24+ with Docker Compose V2
- 2 GB RAM available to Docker
- Ports 80, 8080, and 9080 free on the host (or override via `FRONTEND_PORT` / `API_PORT` / `KEYCLOAK_PORT`)

## Required configuration

These have no defaults — deploy will refuse to start without them:

| Variable | Purpose |
|---|---|
| `ORKYO_VERSION` | Image tag, e.g. `0.4.2` |
| `POSTGRES_PASSWORD` | Database password |
| `REDIS_PASSWORD` | Redis password |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin console password |
| `KEYCLOAK_BACKEND_CLIENT_SECRET` | Secret for the `orkyo-backend` OIDC client |
| `APP_BASE_URL` | Public URL where users reach the app, e.g. `https://community.example.com` |
| `KEYCLOAK_URL` | Public URL for Keycloak, e.g. `https://auth.example.com` |
| `BFF_COOKIE_DOMAIN` | Cookie domain, e.g. `community.example.com` |
| `SMTP_HOST` | Outbound mail server |
| `SMTP_FROM_EMAIL` | Sender address for system emails |

## Path A — Portainer Stacks (recommended)

1. Open Portainer → **Stacks** → **Add stack**
2. Name the stack `orkyo-community`
3. Choose **Repository** and point at the [orkyo-community](https://github.com/Kymr10n/orkyo-community) repo with `Compose path: release/compose.yml`. Or choose **Web editor** and paste the contents of `compose.yml`.
4. Under **Environment variables**, add the values listed above (Portainer will detect required vars and prompt for them)
5. Click **Deploy the stack**

On first deploy, Keycloak imports the realm and the migrator runs DB migrations. Allow 2–3 minutes.

## Path B — Docker Compose CLI

```bash
# 1. Get the bundle (or just compose.yml + .env.template from the repo)
wget https://github.com/Kymr10n/orkyo-community/releases/latest/download/orkyo-community-v<VERSION>.zip
unzip orkyo-community-v<VERSION>.zip
cd orkyo-community-v<VERSION>

# 2. Configure
cp .env.template .env
# edit .env — fill in every value listed above

# 3. Deploy
docker compose up -d
```

If a required value is missing, compose fails immediately with a message naming the variable.

## Access

| Service | URL |
|---|---|
| Application | `${APP_BASE_URL}` (or `http://localhost` for local) |
| Keycloak admin | `${KEYCLOAK_URL}` — sign in as `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD` |
| API health | `${APP_BASE_URL}/api/health` |

Default test accounts (pre-imported in the realm — change passwords before going to production):

| Username | Role |
|---|---|
| `admin` | Site admin |
| `editor` | Editor |
| `viewer` | Viewer |

## HTTPS / Reverse Proxy

The frontend listens on host port `80` and internally proxies `/api/` to the backend. Place a reverse proxy (nginx, Caddy, Traefik) in front of port 80 to terminate TLS. A reference nginx configuration is in [nginx/community.conf.example](../nginx/community.conf.example).

Caddy example (auto-TLS):

```
community.example.com {
    reverse_proxy localhost:80
}

auth.example.com {
    reverse_proxy localhost:9080
}
```

## Next steps

- [OPERATIONS.md](OPERATIONS.md) — backup, upgrade, restore
- [GitHub Issues](https://github.com/Kymr10n/orkyo-community/issues) — bugs and questions
