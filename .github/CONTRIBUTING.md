# Contributing to orkyo-community

`orkyo-community` is the single-tenant, self-hostable edition of Orkyo. It consumes `Orkyo.Foundation` (NuGet) and `@kymr10n/foundation` (npm) and wires them for a single organisation with an embedded Keycloak provider.

## Before You Start

**Check the placement rule first.**
Code that has an analogue in both Community and SaaS belongs in [`orkyo-foundation`](https://github.com/Kymr10n/orkyo-foundation), not here. Single-tenancy alone is not a reason to keep code in this repo. When in doubt, open an issue to discuss before writing code.

## Development Setup

```bash
./dev.sh up         # full stack in containers
./dev.sh infra      # infra only (db/keycloak/mailhog) — pair with host processes below
./dev.sh api        # API on host (hot reload)
./dev.sh frontend   # Vite dev server on host
./dev.sh doctor     # startup sequence + runtime URLs
./dev.sh help       # full list
```

Local ports: API `5002` · Keycloak `8082` · Postgres `5433` · Frontend `5174`.

## Making Changes

### Foundation reference

In local dev the backend projects use a project reference to `orkyo-foundation`. In CI and Docker builds they use the published NuGet package. Do not change the `csproj` conditional without coordinating with the foundation team.

### Migrations

- Every new migration file must carry a `-- @migration-class:` header (`expand`, `contract`, `data`, or `none`).
- Applied migrations are **immutable**. Never edit a file that has been merged to `main`. Write a follow-up migration instead.

### Release bundle

Changes to `release/` are user-facing for self-hosters. Smoke-test the self-hosted path (`release/docs/QUICKSTART.md`) before tagging a release.

## How changes land

`main` is protected — no direct pushes. Every change lands through a pull request:

1. Fork (external contributors) or branch (maintainers), then commit your change.
2. Open a PR against `main`.
3. CI must pass and a code owner (@Kymr10n) must approve; keep review threads resolved. New pushes
   dismiss stale approvals.
4. A maintainer merges. Maintainers/admins may bypass this only for emergency fixes.

## Pull Request Checklist

- [ ] Placement check: belongs in community, not in foundation.
- [ ] No tenant-shaped (multi-tenant) code introduced.
- [ ] Foundation-equivalent behaviour extracted to foundation if needed.
- [ ] Tests added or updated. `dotnet test` and frontend tests pass locally.
- [ ] Migration header present if a migration was added.
- [ ] Release bundle smoke-tested if `release/` was changed.

## Running Tests

```bash
dotnet test backend/tests/Orkyo.Community.Tests.csproj
```

## Questions

Open an issue or start a discussion. For questions about whether code belongs in foundation or community, describe the SaaS analogue in the issue.
