# Orkyo Community — Quick Start

## Prerequisites

- Docker 24+ with Docker Compose V2 (`docker compose version`)
- 2 GB RAM available to Docker
- Ports 80, 8080, and 9080 free on the host

## 1. Configure

Copy the environment template and fill in every `CHANGE_ME_*` value:

```bash
cp .env.template .env
nano .env          # or your preferred editor
```

Required values to set:

| Variable | Description |
|----------|-------------|
| `POSTGRES_PASSWORD` | Database password |
| `REDIS_PASSWORD` | Redis password |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin console password |
| `KEYCLOAK_BACKEND_CLIENT_SECRET` | Secret for the `orkyo-backend` OIDC client |
| `SMTP_HOST` | Outbound mail server hostname |
| `SMTP_FROM_EMAIL` | Sender address for system emails |
| `APP_BASE_URL` | Public URL users will access (e.g. `https://community.example.com`) |
| `KEYCLOAK_URL` | Public URL for Keycloak (e.g. `http://localhost:9080` or `https://auth.example.com`) |
| `OIDC_AUTHORITY` | `${KEYCLOAK_URL}/realms/orkyo-community` |

## 2. Start

```bash
bash scripts/bootstrap.sh
```

The script validates your `.env`, pulls images, starts all services, and confirms the API is healthy. On first run this takes 2–3 minutes.

To start manually instead:

```bash
docker compose up -d
```

## 3. Access

| Service | URL |
|---------|-----|
| Application | `http://localhost` |
| Keycloak admin | `http://localhost:9080` — sign in with `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD` |
| API health | `http://localhost:8080/api/health` |

Default test accounts (change passwords in Keycloak before going to production):

| Username | Role |
|----------|------|
| `admin` | Site admin |
| `editor` | Editor |
| `viewer` | Viewer |

## 4. Portainer

To deploy via Portainer Stacks:

1. Open Portainer → **Stacks** → **Add stack**
2. Paste the contents of `compose.yml` into the Web editor
3. Under **Environment variables**, add each key/value from your `.env` file
4. Click **Deploy the stack**

## 5. HTTPS / Reverse Proxy

The frontend listens on host port **80** and internally proxies `/api/` to the backend — no external routing split is required.

To add TLS, place a reverse proxy (nginx, Caddy, Traefik) in front of port 80. A reference nginx configuration is provided in `nginx/community.conf.example`.

Alternatively, use Caddy for automatic TLS:

```
community.example.com {
    reverse_proxy localhost:80
}
```

## 6. Upgrade

```bash
bash scripts/upgrade.sh 1.2.0
```

The script backs up the database, updates the image version, pulls new images, re-runs migrations, and restarts all services.

## 7. Backup

```bash
bash scripts/backup.sh
```

Backups are written to `./backups/` as gzipped SQL dumps.

## Troubleshooting

```bash
# View logs for all services
docker compose logs -f

# View logs for a specific service
docker compose logs -f api

# Check container health
docker compose ps

# Restart a service
docker compose restart api
```
