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
| `VALKEY_PASSWORD` | Valkey password |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin console password |
| `KEYCLOAK_BACKEND_CLIENT_SECRET` | Secret for the `orkyo-backend` OIDC client |
| `ORKYO_MASTER_ENCRYPTION_KEY` | AES-256-GCM master key (base64, 32 bytes) — generate with `openssl rand -base64 32` |
| `APP_BASE_URL` | Public URL where users reach the app, e.g. `https://community.example.com` |
| `KEYCLOAK_URL` | Public URL for Keycloak — recommended: the app domain + `/auth` path, e.g. `https://community.example.com/auth` (see [HTTPS / Reverse Proxy](#https--reverse-proxy)) |
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
| API health | `http://<host>:8080/health` — the API's own health endpoint on the `API_PORT` mapping (default `8080`) |
| Frontend liveness | `${APP_BASE_URL}/health` — static `OK` stub served by the frontend nginx; does **not** check the API |

Default accounts (pre-imported in the realm). Each carries the
`UPDATE_PASSWORD` required action, so Keycloak forces a password change at first
login and the shipped credentials cannot survive into a running deployment. The
new password must satisfy the realm policy (12+ characters, upper case, digit,
special character). Log in as each once and set a real password, or delete the
accounts you don't need:

| Username | Initial password | Role |
|---|---|---|
| `admin@example.com` | `ChangeMe-Admin-1` | Site admin |
| `editor@example.com` | `ChangeMe-Editor-1` | Editor |
| `viewer@example.com` | `ChangeMe-Viewer-1` | Viewer |

> **Upgrading from an earlier version?** Installs created before 0.12.0 shipped with
> self-registration enabled, which on an internet-reachable install allowed anyone to
> sign up and be granted admin. Changing the shipped default does not fix an existing
> install — see [SECURITY-ADVISORY-2026-07.md](SECURITY-ADVISORY-2026-07.md) for the
> check and the remediation steps.

**Self-registration is disabled by default.** Add people through Settings →
Users → Invite rather than a public sign-up page. This matters because every
user of a Community install is automatically an admin of the single
organisation — so an open sign-up page on an internet-reachable install would
let anyone become an admin. Enable registration only if that is genuinely what
you want (Keycloak admin console → Realm settings → Login → User registration).

## HTTPS / Reverse Proxy

The frontend listens on host port `80` and internally proxies `/api/` to the backend and `/auth/` to Keycloak. Place a reverse proxy (nginx, Caddy, Traefik) in front of port 80 to terminate TLS. A reference nginx configuration is in [nginx/community.conf.example](../nginx/community.conf.example).

**Recommended: single domain, Keycloak under `/auth`.** Because the frontend proxies `/auth/` to Keycloak, one domain covers everything — set `KEYCLOAK_URL=https://community.example.com/auth` (this is the layout release CI smoke-tests). Caddy example (auto-TLS):

```
community.example.com {
    reverse_proxy localhost:80
}
```

**Alternative: dedicated auth domain.** Expose Keycloak's own host port (`KEYCLOAK_PORT`, default `9080`) behind a second vhost and set `KEYCLOAK_URL=https://auth.example.com`:

```
auth.example.com {
    reverse_proxy localhost:9080
}
```

## Next steps

- [OPERATIONS.md](OPERATIONS.md) — backup, upgrade, restore
- [GitHub Issues](https://github.com/Kymr10n/orkyo-community/issues) — bugs and questions
