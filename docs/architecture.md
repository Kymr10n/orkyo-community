# Architecture

## Overview

Orkyo Community is a **single-tenant** self-hosted deployment of Orkyo. It shares domain logic, API endpoints, repositories, and UI components with `orkyo-foundation`. Community provides adapters that make foundation code run without multi-tenant machinery.

## Repository Relationships

```
orkyo-foundation/   ← shared domain, endpoints, UI components, migrations
orkyo-community/    ← single-tenant host, adapters, community migrations
```

Community **cannot** depend on `orkyo-saas`, `orkyo-infra`, or `orkyo-core`.

## Single-Tenant Adapter Pattern

Foundation services are designed to be consumed by both multi-tenant SaaS and single-tenant Community. Community registers adapters that satisfy the same interfaces:

| Interface | SaaS implementation | Community implementation |
|---|---|---|
| `ITenantResolver` | `TenantResolver` (DB lookup by subdomain/header) | `SingleTenantResolver` (fixed config) |
| `IDbConnectionFactory` | `DbConnectionFactory` (control-plane + per-tenant) | `CommunityDbConnectionFactory` (single DB) |
| `IQuotaEnforcer` | `TierBasedQuotaEnforcer` (tier limits) | `CommunityQuotaEnforcer` (unlimited) |
| `ITenantRegistry` | `SaasTenantRegistry` (queries tenant DB) | `CommunityTenantRegistry` (returns one entry) |

## Database Model

```
SaaS:                          Community:
  control_plane DB               one DB: orkyo_community
  tenant_acme DB                   foundation schema (users, sites, spaces, ...)
  tenant_beta DB                   community schema (bootstrap, future extensions)
  ...
```

Community's `CommunityDbConnectionFactory` maps every connection type to `ConnectionStrings__DefaultConnection`.

## Tenant Context

The community tenant is configured via environment variables:

```env
COMMUNITY__TENANTID=00000000-0000-0000-0000-000000000001
COMMUNITY__TENANTSLUG=community
COMMUNITY__TENANTNAME=Orkyo Community
```

`SingleTenantMiddleware` sets this as `HttpContext.Items["TenantContext"]` on every request (except endpoints marked `[SkipTenantResolution]`), so all foundation services receive a valid tenant context without any request header or subdomain.

## Migration Order

```
foundation (order 1000)  ← users, sites, spaces, requests, scheduling, etc.
community  (order 3000)  ← community-specific extensions (future)
```

The migrator runs against a single Postgres database.

## Authentication

Community uses the same BFF (Backend-for-Frontend) OIDC cookie flow as SaaS, backed by Keycloak. The Keycloak realm is configured in `infra/compose/keycloak/realm-orkyo.json` with:

- Seed users (`admin@example.com` / `admin123`)
- `orkyo-backend` confidential client with service account roles (`view-users`, `manage-users`)
- Required actions: `VERIFY_PROFILE` disabled to allow seed user login

## Project Structure

```
backend/
  api/          — ASP.NET Core 10 API host (Program.cs, appsettings)
  src/          — Community adapters (SingleTenantResolver, CommunityDbConnectionFactory, etc.)
  migrations/   — Community migration module and SQL scripts
  migrator/     — CLI migrator entry point
  worker/       — Background job host (stub; UserLifecycleService pending foundation move)

frontend/
  src/
    App.tsx     — CommunityShell: ApexGateway (auth) → TenantApp (ready)
    main.tsx    — React entry point, QueryClient
  index.html    — Favicons, loading spinner

infra/
  compose/      — Docker Compose stack
    keycloak/   — Realm JSON import
```
