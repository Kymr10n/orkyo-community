# Nightly testbed

## Purpose

Release CI starts the bundle from empty volumes on every tag. No long-lived
Community instance exists, so nobody tests an upgrade against accumulated data
before a release.

The nightly testbed is that instance. It runs on a NAS on the local network. It
follows the `nightly` image tag and refreshes itself once per day. The value is
the data it keeps: each refresh applies the new migrations to a database that
carries months of rows.

The testbed does not test TLS from a public certificate authority. It does not
test the Portainer path. A person tests the Portainer path by hand on each
release.

## The image tag

The scheduled run of `release-ci.yml` re-tags the `sha-<sha>` images of `main`
as `nightly`. The job re-tags and does not rebuild, so the `nightly` tag always
resolves to a digest that the container scan already covered.

The `nightly-change-check` job skips the pipeline when `main` is unchanged. The
testbed then sees no new images that day.

## Layout on the NAS

The bundle is unpacked under one fixed path, `/volume1/docker/orkyo-nightly`.
The path does not change between releases, because the refresh script and the
cron entry both name it.

The `.env` file comes from `generate-env.sh` with three differences from a
normal install:

- `ORKYO_VERSION=nightly` instead of a released version.
- `APP_BASE_URL` is the LAN address of the NAS, over HTTP.
- No SMTP values, so mail is log-only.

The five secrets are generated once, at first install. Later refreshes never
rewrite the `.env` file, because a new `ORKYO_MASTER_ENCRYPTION_KEY` makes the
accumulated data unreadable.

## The refresh

A cron entry runs `scripts/nightly-refresh.sh` once per day, after the
scheduled CI run finishes. The script runs these steps:

1. `docker compose pull`
2. `docker compose up -d --remove-orphans`
3. `curl -sf $APP_BASE_URL/health`

Compose recreates the migrator because its image changed. The `depends_on`
rules then hold the API until the migrator exits. This ordering is the reason
the testbed uses a compose refresh and not Watchtower: Watchtower restarts
running containers, and the migrator is a one-shot container, so the new API
starts before the new migrations run.

A refresh on an unchanged tag pulls nothing and recreates nothing.

## Logs

The script writes its output to `/volume1/docker/orkyo-nightly/refresh.log`.
The container logs stay in Docker and are read with `docker compose logs`.

The migrator log is the interesting one after a refresh. It shows which
migrations ran against the accumulated data:

```
docker compose logs migrator
```

## Network

The testbed needs no inbound access from the internet. It pulls images and
reaches nothing else. The `APP_BASE_URL` is a LAN address, so the browser test
happens from a machine on the same network.
