# Migrations

## Overview

Community uses the Orkyo migration platform (`Orkyo.Migrator`) to manage database schema. All migrations run against a single Postgres database.

## Migration Order

| Module | Order | Location | Purpose |
|---|---|---|---|
| `foundation` | 1000–1999 | `orkyo-foundation/backend/migrations-foundation/sql/` | Shared schema: users, sites, spaces, requests, scheduling, etc. |
| `community` | 3000–3999 | `backend/migrations/sql/tenant/` | Community-specific extensions |

## Running Migrations

**Host mode:**
```bash
./dev.sh migrator
```

**Container mode (automatic on `./dev.sh up`):**
The `migrator` service runs before the API starts via Docker Compose `depends_on`.

**Manual:**
```bash
cd backend/migrator
dotnet run -- migrate --target all
```

## Migration Files

SQL files are embedded in the assembly and loaded by filename convention:

```
{order}.{module}.{description}.sql
```

Example: `3000.community.bootstrap.sql`

The `target` is determined by the subdirectory:
- `sql/tenant/` → runs against the community database (every tenant in SaaS, the single DB in community)

## Idempotency

The runner tracks applied migrations in `orkyo_schema_migrations`. Re-running the migrator is safe — already-applied migrations are skipped.

## Adding a Migration

1. Create `backend/migrations/sql/tenant/{order}.community.{description}.sql`
2. Use an order number in the 3000–3999 range
3. Add the required `-- @migration-class:` header as the first line — deploy tooling rejects migrations without it. See `orkyo-infra/docs/migrations/classification.md` for the classes and how to pick one
4. Run `./dev.sh migrator` to apply

## Retiring a Migration

A migration whose effect must stop for new installs, while existing installs keep its rows, is
**deleted**, never rewritten. The runner only visits scripts present in code, so a journal row
with no file is inert: an installation that ran the migration keeps its history and its data, and
a fresh installation never sees it. CI (`scripts/ci/lint-migration-headers.sh`) rejects edits to an
existing file and accepts deletions.

`-- @supersedes-checksum` is not the tool for this. It declares a text-equivalent edit of a
migration that already ran — the same rows, one table instead of two — and it counts as an edit.

Precedent: `3010.community.demo_seed.sql` and `3030.community.demo_seed_type.sql` seeded a
"Demo Office" on every install. They were deleted when the `office` starter preset replaced
them. The preset adopts the rows they left behind by name and code.

## Community-Specific Tables

Currently community adds no schema beyond the foundation baseline. The placeholder migration `3000.community.bootstrap.sql` is a no-op comment. Add community-specific tables here as the product evolves.
