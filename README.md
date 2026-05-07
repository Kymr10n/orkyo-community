<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset=".github/orkyo-logo-dark.png">
    <img src=".github/orkyo-logo-light.png" alt="Orkyo" width="120">
  </picture>
</p>

# Orkyo Community

Self-hosted, single-tenant edition of Orkyo — production space planning and scheduling.

## What it is

A complete Orkyo instance for a single organisation, running on one Postgres database. No multi-tenancy, no control plane, no subscription management.

## What it is not

Not a replacement for `orkyo-saas`. Community runs one organisation; SaaS manages many tenants from a shared control plane.

## Prerequisites

- Docker + Docker Compose
- .NET 10 SDK (for host-process development)
- Node.js 22 (for host-process frontend development)
- `orkyo-foundation` cloned as a sibling directory

## Quick Start

```bash
cp .env.template .env
# Edit .env — change passwords at minimum
./dev.sh up
```

Then open http://localhost:5173 and log in with `admin@example.com` / `admin123`.

## Development Workflows

**Fully containerised** (recommended for testing):
```bash
./dev.sh up       # starts everything
./dev.sh down     # stops everything
```

**Host processes** (recommended for active development, hot-reload):
```bash
./dev.sh infra      # start db + keycloak + mailhog in Docker
./dev.sh migrator   # apply DB migrations
./dev.sh api        # start API (dotnet run)
./dev.sh frontend   # start Vite dev server
```

## Runtime URLs

| Service  | URL |
|---|---|
| Frontend | http://localhost:5173 |
| API      | http://localhost:8080 |
| Swagger  | http://localhost:8080/swagger |
| Keycloak | http://localhost:8180 |
| MailHog  | http://localhost:8025 |

## Default Credentials

| | |
|---|---|
| App login | `admin@example.com` / `admin123` |
| Keycloak admin | `admin` / `changeme` |

## Configuration

Copy `.env.template` to `.env` and edit. Key variables:

- `POSTGRES_PASSWORD` — change before any non-local deployment
- `KEYCLOAK_ADMIN_PASSWORD` / `KEYCLOAK_BACKEND_CLIENT_SECRET` — change in production
- `COMMUNITY__TENANTNAME` — display name for your organisation
- `OIDC_AUTHORITY` / `KEYCLOAK_URL` — set to your public Keycloak URL in production

## Self-host deployment

Production deployments use the bundle in [release/](release/), which ships as a single `compose.yml` with no host-side scripts or bind mounts.

**Portainer Stacks** — paste [release/compose.yml](release/compose.yml) into Portainer's web editor (or point at this repo as a Git source with `Compose path: release/compose.yml`), fill in the env vars Portainer prompts for, and deploy.

**Docker Compose CLI** — download a release bundle from [Releases](https://github.com/Kymr10n/orkyo-community/releases), `cp .env.template .env`, edit, `docker compose up -d`.

See [release/docs/QUICKSTART.md](release/docs/QUICKSTART.md) for the full required-variable list and [release/docs/OPERATIONS.md](release/docs/OPERATIONS.md) for backup, upgrade, and rollback.

## Architecture

```
orkyo-community/
  backend/api/        — ASP.NET Core 10 API host
  backend/src/        — Community-specific adapters (single-tenant context, DB factory)
  backend/migrations/ — Community migration module (runs after foundation migrations)
  backend/migrator/   — CLI migrator entry point
  frontend/           — Vite + React frontend (consumes orkyo-foundation components)
  infra/compose/      — Docker Compose stack
```

Shared domain logic, repositories, endpoints, and UI components come from `orkyo-foundation`.
Community provides single-tenant adapters so foundation code runs without multi-tenant machinery.
