<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset=".github/orkyo-logo-dark.png">
    <img src=".github/orkyo-logo-light.png" alt="Orkyo" width="120">
  </picture>
</p>

<h3 align="center">Orkyo Community Edition</h3>
<p align="center">Self-hostable production floor planning for manufacturing teams.<br>Coordinate spaces, equipment, and people — without the spreadsheets.</p>

<p align="center">
  <a href="https://orkyo.com">Website</a> ·
  <a href="https://orkyo.com/pricing">Pricing</a> ·
  <a href="https://github.com/Kymr10n/orkyo-community/issues">Issues</a> ·
  <a href="https://github.com/Kymr10n/orkyo-community/discussions">Discussions</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="AGPL-3.0">
  <img src="https://img.shields.io/badge/.NET-10-purple" alt=".NET 10">
  <img src="https://img.shields.io/badge/React-19-blue" alt="React 19">
</p>

---

## What is Orkyo?

Manufacturing teams often coordinate production areas, equipment, and skilled people using spreadsheets, emails, and meetings. As operations grow, this leads to scheduling conflicts, unused capacity, and avoidable delays.

Orkyo is a lightweight resource orchestration platform that gives your team a shared, visual plan — requests, assignments, conflicts, and utilisation — all in one place.

**Community Edition** is the self-hosted, single-tenant version. One organisation, one database, full control over your data. No subscription required.

> **Looking for a hosted option?** [Orkyo Cloud](https://orkyo.com) offers managed hosting with automatic updates, backups, and email support.

## Features

- **Visual space planning** — drag-and-drop layout editor with real-time allocation status
- **People and equipment scheduling** — capability matching, absence handling, conflict detection
- **Multi-site management** — manage layouts and resources across all your facilities
- **Conflict detection** — catch double-bookings and overloads before they're committed
- **Utilisation tracking** — see where capacity is going and where it's wasted
- **Request workflow** — teams submit production requests; Orkyo matches them to available resources

## Quick start (Docker Compose)

**Requirements:** Docker Engine 24+ with Compose v2.

```bash
# Download the latest release bundle
curl -sL https://github.com/Kymr10n/orkyo-community/releases/latest/download/orkyo-community.tar.gz \
  | tar xz && cd orkyo-community

# Configure
cp .env.template .env
# Edit .env — change POSTGRES_PASSWORD, KEYCLOAK_ADMIN_PASSWORD,
# and KEYCLOAK_BACKEND_CLIENT_SECRET at minimum

# Start
docker compose up -d

# Open
open http://localhost:3000
```

Default login: `admin@example.com` / `admin123` — **change this before any non-local deployment**.

See [release/docs/QUICKSTART.md](release/docs/QUICKSTART.md) for the full variable reference and [release/docs/OPERATIONS.md](release/docs/OPERATIONS.md) for backup, upgrade, and rollback procedures.

## Development setup

**Requirements:** Docker, .NET 10 SDK, Node.js 22, `orkyo-foundation` cloned as a sibling directory (`../orkyo-foundation`).

```bash
git clone https://github.com/Kymr10n/orkyo-community.git
git clone https://github.com/Kymr10n/orkyo-foundation.git  # sibling directory

cd orkyo-community
cp .env.template .env
# Edit .env

./dev.sh infra       # Postgres + Keycloak + MailHog in Docker
./dev.sh migrator    # Apply DB migrations
./dev.sh api         # API (dotnet run, hot-reload)
./dev.sh frontend    # Vite dev server (http://localhost:5173)
```

| Service  | URL |
|---|---|
| Frontend | http://localhost:5173 |
| API      | http://localhost:5002 |
| Keycloak | http://localhost:8082 |
| MailHog  | http://localhost:8025 |

Run all tests:

```bash
dotnet test backend/tests/          # unit + integration (requires Docker for integration tests)
cd frontend && npm test -- --run    # frontend unit tests
```

## Architecture

```
orkyo-community/
  backend/api/        — ASP.NET Core 10 API host (single-tenant adapters)
  backend/src/        — Community-specific services and context wiring
  backend/migrations/ — DB migration module (runs after foundation migrations)
  backend/migrator/   — CLI migrator entry point
  frontend/           — Vite + React 19 frontend
  infra/compose/      — Docker Compose stack (dev + local)
  release/            — Production release bundle (Docker Compose, env template, docs)
```

Shared domain logic — endpoints, repositories, scheduling, UI components — lives in [`orkyo-foundation`](https://github.com/Kymr10n/orkyo-foundation). Community provides single-tenant adapters so foundation code runs without multi-tenant machinery.

## Self-hosting in production

**Portainer Stacks** — paste [`release/compose.yml`](release/compose.yml) into Portainer's stack editor (or point it at this repo with `Compose path: release/compose.yml`), fill in the prompted env vars, and deploy.

**VPS / Docker CLI** — download a release bundle from [Releases](https://github.com/Kymr10n/orkyo-community/releases), configure `.env`, run `docker compose up -d`.

All images are published to GitHub Container Registry (`ghcr.io/kymr10n/orkyo-community-*`).

### Upgrading

```bash
docker compose pull
docker compose up -d
```

Migrations run automatically on startup. See [release/docs/OPERATIONS.md](release/docs/OPERATIONS.md) for rollback procedures.

## Roadmap

Orkyo is in active development. Near-term priorities:

- Absence and leave management improvements
- Reporting and capacity export (CSV / BI integration)
- Notification and reminder system
- Calendar and iCal integration

Have a feature request? [Open an issue](https://github.com/Kymr10n/orkyo-community/issues) or start a [discussion](https://github.com/Kymr10n/orkyo-community/discussions).

## Support

Community Edition is **community-supported** on a best-effort basis.

| Channel | What it covers |
|---|---|
| [GitHub Issues](https://github.com/Kymr10n/orkyo-community/issues) | Bug reports, reproducible defects |
| [GitHub Discussions](https://github.com/Kymr10n/orkyo-community/discussions) | Questions, setup help, ideas |

**Not included:** response time guarantees, installation support, custom development, SLA. For hosted-managed support, see [Orkyo Cloud](https://orkyo.com/support).

## Contributing

See [CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines. Bug reports and well-scoped pull requests are welcome. Feature requests are reviewed against the product roadmap — a subscription does not guarantee implementation.

## License

Orkyo Community Edition is licensed under the [GNU Affero General Public License v3.0](LICENSE). In brief: you can use, modify, and self-host it freely, but if you offer it as a network service you must make your modifications available under the same licence.

For commercial licensing or OEM enquiries, contact [contact@orkyo.com](mailto:contact@orkyo.com).
