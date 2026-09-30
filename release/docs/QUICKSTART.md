# Orkyo Community — Quick Start

Two supported deployment paths: **Portainer Stacks** (single-file paste, recommended) and **Docker Compose CLI**. Both consume the same [compose.yml](../compose.yml).

## Prerequisites

- Docker 24+ with Docker Compose V2
- 2 GB RAM available to Docker
- Ports 80, 8080, and 9080 free on the host (or override via `FRONTEND_PORT` / `API_PORT` / `KEYCLOAK_PORT`)
- Port 443 free as well, if you use the built-in Caddy option
- `bash` and `openssl`, to run `generate-env.sh`

## Required configuration

The bundle needs seven values. The generator writes six of them for you.

| Variable | Purpose | Who supplies it |
|---|---|---|
| `ORKYO_VERSION` | Image tag, for example `0.4.2` | Stamped into the bundle |
| `APP_BASE_URL` | Public URL where people reach the app | **You** |
| `POSTGRES_PASSWORD` | Database password | Generated |
| `VALKEY_PASSWORD` | Valkey password | Generated |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin console password | Generated |
| `KEYCLOAK_BACKEND_CLIENT_SECRET` | Secret for the `orkyo-backend` OIDC client | Generated |
| `ORKYO_MASTER_ENCRYPTION_KEY` | AES-256-GCM master key (base64, 32 bytes) | Generated |

Every other value derives from `APP_BASE_URL`, or has a default. `KEYCLOAK_URL`
becomes `APP_BASE_URL` plus `/auth`. The allowed-host list for the login
redirect comes from the host of `APP_BASE_URL`.

## Write the configuration

Run the generator. It asks for your public URL and writes a complete `.env`:

```bash
./generate-env.sh
```

The script needs bash and `openssl`. Run it on any machine, for example a
laptop. For an unattended run, give the values as flags:

```bash
./generate-env.sh --url https://community.example.com --tls --no-smtp
```

CAUTION: Do not run the generator twice on a live deployment. A second run
writes a new `ORKYO_MASTER_ENCRYPTION_KEY`, and the old encrypted data becomes
unreadable. The script refuses to overwrite an existing `.env` without
`--force`.

The script prints the Keycloak admin password one time. Keep this password.

## Path A — Portainer Stacks (recommended)

1. Open Portainer → **Stacks** → **Add stack**
2. Name the stack `orkyo-community`
3. Choose **Repository** and point at the [orkyo-community](https://github.com/Kymr10n/orkyo-community) repo with `Compose path: release/compose.yml`. Or choose **Web editor** and paste the contents of `compose.yml`.
4. Run `generate-env.sh` on your own machine. Then load the result with **Load variables from .env file**.
5. Click **Deploy the stack**

On first deploy, Keycloak imports the realm and the migrator runs DB migrations. Allow 2–3 minutes.

## Path B — Docker Compose CLI

```bash
# 1. Get the bundle (tar.gz: every Docker host has tar, not every host has unzip)
curl -fsSL https://github.com/Kymr10n/orkyo-community/releases/latest/download/orkyo-community.tar.gz | tar xz
cd orkyo-community-v*

# 2. Write the configuration
./generate-env.sh

# 3. Deploy
docker compose up -d
```

The same bundle is available as `orkyo-community.zip` for Windows and Portainer users.

If a proxy of your own already owns port 80, the generator asks for another host
port. For an unattended run, pass `--frontend-port 8091`, for example. Then point
the proxy at that port.

If a required value is missing, compose stops at once. The message names the variable.

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

The frontend listens on host port `80` and internally proxies `/api/` to the backend and `/auth/` to Keycloak. One domain covers everything, because the frontend proxies `/auth/` to Keycloak. This is the layout that release CI smoke-tests.

### Option A — built-in Caddy (simplest)

The stack can terminate TLS itself. Caddy gets the certificates automatically
and redirects HTTP to HTTPS.

1. Make sure that `APP_BASE_URL` starts with `https://`.
2. Make sure that the host name resolves to this server, and that ports 80 and 443 are free.
3. Run `./generate-env.sh --tls`, or add these three lines to `.env`:

```bash
COMPOSE_PROFILES=tls
FRONTEND_BIND=127.0.0.1
FRONTEND_PORT=8081
```

Caddy takes host ports 80 and 443. The frontend moves behind it, on
`127.0.0.1:8081`, so the two do not compete for port 80.

If the host already runs a proxy on port 80 or 443, do not use this option.
Caddy will not start. Use Option B instead.

### Option B — your own reverse proxy

Place a reverse proxy (nginx, Caddy, Traefik) in front of port 80 to terminate TLS. A reference nginx configuration is in [nginx/community.conf.example](../nginx/community.conf.example). Leave `COMPOSE_PROFILES` unset.

**Single domain, Keycloak under `/auth`.** Set `KEYCLOAK_URL=https://community.example.com/auth`, or leave it unset and let it derive from `APP_BASE_URL`. Caddy example (auto-TLS):

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

**Traefik (v3) example (auto-TLS via Let's Encrypt).** Add labels to `compose.yml` on the `frontend` service, alongside an appropriately configured Traefik with an `https` entrypoint and a `letsencrypt` cert resolver:

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.orkyo.rule=Host(`community.example.com`)"
  - "traefik.http.routers.orkyo.entrypoints=https"
  - "traefik.http.routers.orkyo.tls.certresolver=letsencrypt"
  - "traefik.http.services.orkyo.loadbalancer.server.port=80"
```

**Nginx Proxy Manager (NPM).** Add a **Proxy Host**:

- *Domain Names:* `community.example.com`
- *Scheme:* `http`, *Forward Hostname / IP:* the docker host's LAN IP (not `127.0.0.1` — NPM runs in its own container), *Forward Port:* `80`
- *Block Common Exploits:* on. *Websockets Support:* on.
- *SSL* tab: request a new Let's Encrypt certificate, force SSL, HSTS enabled.

## Email

Mail is optional. The generator asks for SMTP details, and you can skip them.

With no `SMTP_HOST`, the app does not send mail. It writes each message to the
API log instead. The API logs one warning at startup:

```
Email delivery is log-only: SMTP_HOST is not set
```

To read an invitation link in this mode, run:

```bash
docker compose logs api | grep -A5 "Email (log-only)"
```

WARNING: Use log-only mail for evaluation only. Invitation and password links
in the log stay valid. A person who reads the log can use them.

To turn on mail later, set the SMTP values in `.env`. Then run
`docker compose up -d`. `SMTP_HOST` makes the rest of the block required. If
the block is incomplete, the API stops at startup and names the missing value.

## Next steps

- [OPERATIONS.md](OPERATIONS.md) — backup, upgrade, restore
- [GitHub Issues](https://github.com/Kymr10n/orkyo-community/issues) — bugs and questions
