# Claude guide — orkyo-community

## What this repo is

The single-tenant, self-hostable Community edition of Orkyo. Consumes `Orkyo.Foundation` (NuGet) and `@kymr10n/foundation` (npm) and wires them for a single organisation, with an **embedded Keycloak** provider and a self-hosted release bundle.

## Placement rule

> **Code with a SaaS analogue belongs in orkyo-foundation.** Single-tenancy alone is NOT a reason to keep code here.

When in doubt, check the foundation README's placement rule.

## Local dev

```
./dev.sh up         # full stack in containers
./dev.sh infra      # infra only (db/valkey/keycloak/mailhog) — pair with host processes below
./dev.sh api        # API on host (hot reload)
./dev.sh worker     # background worker on host
./dev.sh frontend   # Vite dev server on host
./dev.sh logs api   # stream a service's logs
./dev.sh doctor     # startup sequence + runtime URLs
./dev.sh help       # full list
```

Local ports: API `5002` · Keycloak `8082` · Postgres `5433` · Frontend `5174` (different from SaaS to allow both stacks side-by-side).

## Conventions to follow

- **Foundation reference is conditional**: project ref in local dev, NuGet pin in CI / Docker. Don't change the csproj conditional without coordination.
- **Migrations** must carry the `-- @migration-class:` header. See `orkyo-infra/docs/migrations/classification.md`.
- **Release bundle** (`release/`) is the self-hosted artifact. Changes there are user-facing for self-hosters; smoke-test before tagging.
- **Observability**: structured Serilog logging is wired (same foundation helper as SaaS — `OrkyoObservability.InitBootstrapLogger()` + `UseOrkyoLogging` in `backend/api/Program.cs`; the Loki sink comes transitively from foundation). Prometheus metrics are wired via the foundation helpers too: `UseOrkyoMetrics()` + `MapOrkyoMetricsEndpoint(METRICS_TOKEN)` in `backend/api/Program.cs`. The `/metrics` endpoint is fail-secure — with no `METRICS_TOKEN` configured it is not mapped at all (404), so self-hosters opt in explicitly (see `release/.env.template`).

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

## Explicit-registration rule

If `Program.cs` calls `UseX()`, it must call `AddX()` in the same file — never rely on `AddFoundationServices` to register a service that the API project uses directly. Foundation owns implementations; the API project owns how it exposes them. This rule exists because Foundation is consumed as a NuGet package in CI/Docker, and there is always a window between a Foundation change landing and the package being published. Any implicit dependency on Foundation registering a service will silently break CI during that window.
