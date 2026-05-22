# Orkyo Community — Operations

Day-2 operations for self-hosted deployments. All commands assume you're running them on the Docker host (or via Portainer's container console).

## Backup

The database is the single source of truth, including uploaded floorplan assets.

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

# 2. Drop and recreate (DESTRUCTIVE — make sure your backup is good)
docker exec -i orkyo_community_db psql -U orkyo postgres -c "DROP DATABASE orkyo_community"
docker exec -i orkyo_community_db psql -U orkyo postgres -c "DROP DATABASE keycloak"
docker exec -i orkyo_community_db psql -U orkyo postgres < orkyo-backup-<timestamp>.sql

# 3. Restart
docker compose up -d
```

## Upgrade

> **Always back up before upgrading.** Migrations are forward-only.

### Portainer

1. Stack → **Editor** → change `ORKYO_VERSION` to the new version
2. **Update the stack** with **Re-pull image** enabled

The migrator runs automatically before the API starts (gated by `service_completed_successfully` in the depends_on chain).

### Docker Compose CLI

```bash
# Edit .env: set ORKYO_VERSION=<new-version>
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

## Rollback

If an upgrade fails, roll back the version and restore from your pre-upgrade backup:

```bash
# Edit .env / Portainer stack: set ORKYO_VERSION back to previous
docker compose pull
# Restore database from pre-upgrade dump (see Restore section)
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

## Common issues

| Symptom | Likely cause |
|---|---|
| `compose up` fails with `set a strong DB password` | A required env var is unset. Check the message — it names the missing variable. |
| Keycloak fails healthcheck | First start can take 60–90s on slow hosts. Check `docker compose logs keycloak` for realm-import errors. |
| API returns 503 from `/health` | Database migrations not applied yet. Check `docker logs orkyo_community_migrator`. |
| BFF login redirect fails | `BFF_COOKIE_DOMAIN` doesn't match the host the user reaches the app on, or `BFF_COOKIE_SECURE=true` over HTTP. |
