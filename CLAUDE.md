# Claude guide — orkyo-community

## What this repo is

The single-tenant, self-hostable Community edition of Orkyo. Consumes `Orkyo.Foundation` (NuGet) and `@kymr10n/foundation` (npm) and wires them for a single organisation, with an **embedded Keycloak** provider and a self-hosted release bundle.

## Placement rule

> **Code with a SaaS analogue belongs in orkyo-foundation.** Single-tenancy alone is NOT a reason to keep code here.

When in doubt, check the foundation README's placement rule.

## Local dev

```
./dev.sh up      # bring up postgres + keycloak + api + frontend
./dev.sh down    # tear down
./dev.sh logs    # follow container logs
./dev.sh test    # run backend + frontend tests
./dev.sh psql    # psql shell into the local db
```

Local ports: API `5002` · Keycloak `8082` · Postgres `5433` · Frontend `5174` (different from SaaS to allow both stacks side-by-side).

## Conventions to follow

- **Foundation reference is conditional**: project ref in local dev, NuGet pin in CI / Docker. Don't change the csproj conditional without coordination.
- **Migrations** must carry the `-- @migration-class:` header. See `orkyo-infra/docs/migrations/classification.md`.
- **Release bundle** (`release/`) is the self-hosted artifact. Changes there are user-facing for self-hosters; smoke-test before tagging.
- **Observability is not yet wired** here. Foundation will gain a shared observability helper that this repo will then consume; expect that PR.

## Where things live

- Backend (api / worker / migrator / src / migrations): `backend/`
- Embedded Keycloak (Dockerfile + config): `backend/keycloak/`
- Frontend: `frontend/`
- Local compose stack: `compose.local.yml`
- Self-hosted release bundle: `release/`

## What to read first

1. `README.md` — quick start and self-host guide
2. `release/docs/QUICKSTART.md` and `release/docs/OPERATIONS.md` — the self-hoster experience
3. `.github/workflows/release-ci.yml` — release model
4. `orkyo-infra/docs/structural-hardening-2026-05.md` — current cross-repo hardening plan

## Things not to do

- Don't add tenant-shaped code here. This is single-tenant.
- Don't add Community-only features that should live in foundation.
- Don't ship the release bundle without smoke-testing the self-hosted path.
