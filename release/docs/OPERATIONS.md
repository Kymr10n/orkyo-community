# Orkyo Community — Operations

Day-2 operations for self-hosted deployments. All commands assume you're running them on the Docker host (or via Portainer's container console).

## Backup

A backup has two parts: the database dump and the `.env` file.

The database holds all data, including uploaded floorplan assets. Floorplan assets and
the assistant credential are encrypted at rest with `ORKYO_MASTER_ENCRYPTION_KEY` from
`.env`. If you lose the key, a restored dump holds data that nobody can read.

Copy `.env` with every dump. Store the two files together. Protect them like a password.

The shipped script does both: `scripts/backup.sh` (see
[Upgrade and backup scripts](#upgrade-and-backup-scripts)).

### Database dump

```bash
docker exec orkyo_community_db \
  pg_dumpall -U orkyo --clean --if-exists \
  > "orkyo-backup-$(date -u +%Y%m%dT%H%M%SZ).sql"
```

This dumps both the application database and the Keycloak database. Schedule it via cron / systemd timer:

```
0 3 * * * docker exec orkyo_community_db pg_dumpall -U orkyo --clean --if-exists | gzip > /var/backups/orkyo/$(date -u +\%Y\%m\%dT\%H\%M\%SZ).sql.gz
```

## Restore

> **Stop the stack before restoring.** Restoring against a running API can corrupt state.

```bash
# 1. Stop everything except the database
docker compose stop api worker frontend keycloak

# 2. Terminate any straggling connections — DROP DATABASE fails if anything is
#    still connected (e.g. a shell you left open, or a service still shutting down)
docker exec -i orkyo_community_db psql -U orkyo postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('orkyo_community','keycloak') AND pid <> pg_backend_pid()"

# 3. Drop and recreate (DESTRUCTIVE — make sure your backup is good)
docker exec -i orkyo_community_db psql -U orkyo postgres -c "DROP DATABASE orkyo_community"
docker exec -i orkyo_community_db psql -U orkyo postgres -c "DROP DATABASE keycloak"
docker exec -i orkyo_community_db psql -U orkyo postgres < orkyo-backup-<timestamp>.sql

# 4. Restart
docker compose up -d
```

If you restore onto a new host, put the `.env` from the same backup next to
`compose.yml` first. The `ORKYO_MASTER_ENCRYPTION_KEY` in it must be the key the dump
was written under.

## Upgrade

> **Always back up before upgrading.** Migrations are forward-only.

All images in `compose.yml` are pinned via `${ORKYO_VERSION:?}` — the version lives in exactly one place: your `.env` file (CLI) or the stack's environment variables (Portainer). Changing it and re-pulling is the whole upgrade.

### Portainer

1. Stack → **Environment variables** → change `ORKYO_VERSION` to the new version
2. **Update the stack** with **Re-pull image and redeploy** enabled

The migrator runs automatically before the API starts (gated by `service_completed_successfully` in the depends_on chain).

### Docker Compose CLI

```bash
# 1. Edit .env: set ORKYO_VERSION=<new-version>
# 2. Pull the new images and restart
docker compose pull
docker compose up -d
```

### Verifying the upgrade

```bash
# All services healthy?
docker compose ps

# API responding?
curl -sf http://localhost:8080/health

# Migrator log (should show "completed" entries)
docker logs orkyo_community_migrator
```

### Upgrade and backup scripts

The bundle ships two operator scripts in `scripts/`. Both find `compose.yml` and `.env`
in the bundle directory.

- `backup.sh` writes `pg_dumpall.sql`, a copy of `.env` named `env`, and a SHA-256
  checksum file to `backups/<timestamp>/`. It refuses to record an empty dump.
- `upgrade.sh <version>` runs `backup.sh` first. If the backup fails, the upgrade stops.
  Then it sets `ORKYO_VERSION` in `.env`, pulls the images, and runs `docker compose up -d`.
  The migrator runs inside that step, before the API starts.

```bash
bash scripts/backup.sh
bash scripts/upgrade.sh <new-version>
```

For the Docker Compose CLI, this is the recommended upgrade path. Portainer users
change the stack variable as described above.

## Rollback

If an upgrade fails, roll back the version the same way it was changed, and restore from your pre-upgrade backup (migrations are forward-only — the old application version may not run against the new schema):

```bash
# 1. Edit .env (or the Portainer stack's environment variables):
#    set ORKYO_VERSION back to the previous version
docker compose pull
# 2. Restore the database from the pre-upgrade dump (see Restore section)
# 3. Restart on the rolled-back version
docker compose up -d
```

## Diagnostics

```bash
docker compose ps                       # Container health
docker compose logs -f api              # Follow API logs
docker compose logs --tail 100 keycloak # Keycloak startup / realm import
docker compose logs migrator            # DB migration history
docker exec -it orkyo_community_db \    # SQL shell
  psql -U orkyo -d orkyo_community
```

## Email

The `.env` file controls mail. With no `SMTP_HOST`, the app writes each message
to the API log and sends nothing.

To find out which mode is active, run:

```bash
docker compose logs api | grep "log-only"
```

One warning line means log-only mode:

```
Email delivery is log-only: SMTP_HOST is not set
```

To read an invitation link in this mode, run:

```bash
docker compose logs api | grep -A5 "Email (log-only)"
```

To turn on delivery, set `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_SSL`,
`SMTP_FROM_EMAIL`, and `SMTP_FROM_NAME` in `.env`. Then run
`docker compose up -d`.

NOTE: `SMTP_HOST` makes the other four values required. If one is missing, the
API stops at startup and names it. A partial block is an error, because the app
must not send mail from a port or a sender that nobody chose.

## Configuration changes

The generator `generate-env.sh` writes `.env` one time, at install.

WARNING: Do not run the generator again on a live deployment. It writes a new
`ORKYO_MASTER_ENCRYPTION_KEY`, and data encrypted with the old key becomes
unreadable. Edit `.env` by hand instead. The script refuses to overwrite an
existing file without `--force`.

To change any value after install, edit `.env`. Then run
`docker compose up -d` to recreate the affected containers.

## Add sample data to a running installation

The starter setup is a one-time step at the first start. To add it later:

1. Set `ORKYO_STARTER_TEMPLATE=manufacturing` or `ORKYO_STARTER_TEMPLATE=office` in `.env`.
   In Portainer, set it in the stack variables.
2. Run `docker compose up -d`.

The API applies the setup at the next start. It records the setup under
**Settings → Presets** and does not apply it again. The setup never deletes
data. To remove the sample resources, delete them in the app.

NOTE: Installations created before the `office` starter existed contain a
"Demo Office" site with four spaces from an earlier seed. An upgrade keeps them.
The `office` setup adopts these four spaces by name and code, and does not
create them again.

## Terms of Service gate (optional)

Community does not show a Terms of Service acceptance page by default. To require
every user to accept terms before entering the app, set an environment variable on
the `api` service (e.g. via a compose override or the `environment:` block):

```yaml
ToS__RequiredVersion: "2026-01"   # any version label you choose
```

- When set, users must accept the built-in generic Terms of Service text once;
  acceptance is recorded per user and version in the `tos_acceptances` table.
- Changing the value to a new label forces all users to re-accept.
- Unsetting it disables the gate again (recorded acceptances are kept).

## Realm changes reach an existing installation

Keycloak imports the realm on the first boot only. Every later release can change the realm:
a client setting, the passkey policy, a step in the login flow. The `keycloak-config` service
in `compose.yml` applies these changes on every `docker compose up`. It runs after Keycloak is
healthy and before the API starts. It adds and updates, and it never deletes.

The service owns these properties and nothing else:

| Property | Source |
|---|---|
| `orkyo-backend` client secret | `KEYCLOAK_BACKEND_CLIENT_SECRET` in `.env` |
| `orkyo-backend` redirect URIs, web origins, post-logout URIs | `APP_BASE_URL` in `.env`, added to the existing list |
| WebAuthn Passwordless policy (passkeys) | The release. The relying party ID is the host of `APP_BASE_URL`. |
| "Condition - credential" step in the browser flow | The release |

Settings you change in the admin console outside this list stay as you set them.

To read what the last run did:

```bash
docker compose logs keycloak-config
```

The last line is `done: N change(s)`. A second run after a successful one reports zero
changes. If the service fails, the API does not start. Read the log; the message names the
missing variable or the Keycloak answer.

## Common issues

| Symptom | Likely cause |
|---|---|
| `compose up` fails with `set a strong DB password` | A required env var is unset. Check the message — it names the missing variable. |
| Keycloak fails healthcheck | First start can take 60–90s on slow hosts. Check `docker compose logs keycloak` for realm-import errors. |
| API returns 503 from `/health` | Database migrations not applied yet. Check `docker logs orkyo_community_migrator`. |
| BFF login redirect fails | `BFF_COOKIE_DOMAIN` doesn't match the host the user reaches the app on, or `BFF_COOKIE_SECURE=true` over HTTP. |
| API returns 401 at login after client secret change | The `keycloak-config` service did not run after the change. Run `docker compose up -d` and read `docker compose logs keycloak-config`. |
| QR scanner shows "Scanning needs HTTPS" | Users reach the app over plain HTTP. Browsers give camera access only over HTTPS or on `localhost`. Put a TLS reverse proxy in front of port 80. See [HTTPS / Reverse Proxy](QUICKSTART.md#https--reverse-proxy). |

## Rotating `KEYCLOAK_BACKEND_CLIENT_SECRET`

1. Set the new value in `.env`.
2. Run `docker compose up -d`. The `keycloak-config` service writes the new secret to the
   realm, and the API and worker restart with it.
3. Confirm with `docker compose logs keycloak-config`. The log shows `secret set from .env`.
