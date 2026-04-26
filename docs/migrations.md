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
3. Run `./dev.sh migrator` to apply

## Community-Specific Tables

Currently community adds no schema beyond the foundation baseline. The placeholder migration `3000.community.bootstrap.sql` is a no-op comment. Add community-specific tables here as the product evolves.
