# Core Reconciliation: `orkyo-community` vs `orkyo-core`

**Date:** 2026-04-26
**Reference:** `orkyo-core` (pre-carveout monolith)
**Community runtime baseline:** `orkyo-saas` + `orkyo-foundation`

This document tracks every area of `orkyo-core` against `orkyo-community`, with an explicit decision for each gap.

---

## Backend API Endpoints

| Endpoint | In core | In community | Decision | Notes |
|---|---|---|---|---|
| `AccountLifecycleEndpoints` | ✅ | ✅ (foundation) | Already implemented | GDPR confirm-activity link |
| `AnnouncementEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `AuditEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `AutoScheduleEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `BffAuthEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `BreakGlassEndpoints` | ✅ | ❌ | SaaS-only, ignore for community | No multi-tenant break-glass in single-tenant |
| `ContactEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `CriteriaEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `DiagnosticsAdminEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `ExportEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `FeedbackEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `FloorplanEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `GroupCapabilityEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `InterestEndpoints` | ✅ | ❌ | SaaS-only, ignore for community | SaaS tier interest registration |
| `MembershipAdminEndpoints` | ✅ | ❌ | SaaS-only, ignore for community | Multi-tenant membership admin |
| `PresetEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `RequestEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SchedulingEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SearchEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SecurityEndpoints` | ✅ | ✅ (foundation) | Already implemented | Profile, sessions, MFA, password |
| `SessionEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SettingsAdminEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SettingsEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SiteEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SpaceCapabilityEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SpaceEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `SpaceGroupEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `TenantAdminEndpoints` | ✅ | ❌ | SaaS-only, ignore for community | Control-plane tenant admin |
| `TenantEndpoints` | ✅ | ❌ | SaaS-only, ignore for community | Multi-tenant CRUD |
| `TenantReactivationEndpoints` | ✅ | ❌ | SaaS-only, ignore for community | Multi-tenant lifecycle |
| `TemplateEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `UserAdminEndpoints` | ✅ | ❌ | Needs architectural review | Community needs user management; current saas version is multi-tenant shaped. Port a single-tenant version or add to foundation. |
| `UserAnnouncementEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `UserManagementEndpoints` | ✅ | ✅ (foundation) | Already implemented | |
| `UserPreferencesEndpoints` | ✅ | ✅ (foundation) | Already implemented | |

**Summary:** 27/34 endpoints present. 6 are SaaS-only (correct). 1 (`UserAdminEndpoints`) needs a community-shaped implementation.

---

## Frontend Pages

| Page | In core | In community | Decision | Notes |
|---|---|---|---|---|
| `AboutPage` | ✅ | ✅ (foundation) | Already implemented | |
| `AccountPage` | ✅ | ✅ (foundation) | Already implemented | |
| `AdminPage` | ✅ | ❌ | SaaS-only, ignore for community | Multi-tenant admin; community uses `SettingsAdminEndpoints` via settings page |
| `ConflictsPage` | ✅ | ✅ (foundation) | Already implemented | |
| `LoginPage` | ✅ | ✅ (foundation) | Already implemented | |
| `MessagesPage` | ✅ | ✅ (foundation) | Already implemented | |
| `OnboardingPage` | ✅ | ✅ (foundation) | Already implemented | |
| `RequestAccessPage` | ✅ | ✅ (foundation) | Already implemented | |
| `RequestsPage` | ✅ | ✅ (foundation) | Already implemented | |
| `SettingsPage` | ✅ | ✅ (foundation) | Already implemented | |
| `SignupPage` | ✅ | ✅ (foundation) | Already implemented | |
| `SpacesPage` | ✅ | ✅ (foundation) | Already implemented | |
| `TenantSelectPage` | ✅ | ❌ | SaaS-only, ignore for community | Single-tenant; no selection needed |
| `TenantSuspendedPage` | ✅ | ✅ (foundation) | Already implemented | |
| `TosPage` | ✅ | ✅ (foundation) | Already implemented | |
| `UtilizationPage` | ✅ | ✅ (foundation) | Already implemented | |

**Summary:** 14/16 pages present. 2 are SaaS-only (correct).

---

## Frontend API Clients

All 27 API clients from `orkyo-core` are present in `orkyo-foundation` and consumed by community via `@foundation/src/lib/api/`. No gaps.

Note: `tenant-account-api.ts` and `tenant-management-api.ts` exist in foundation but their endpoints (`TenantEndpoints`, `TenantAdminEndpoints`) are SaaS-only. The clients remain in foundation for completeness; they simply won't find matching community routes. This is harmless but could be refined in a later cleanup.

---

## Worker / Background Jobs

| Job | In core | In community | Decision | Notes |
|---|---|---|---|---|
| `UserLifecycleService` (GDPR inactivity) | ✅ | ⚠️ stub | Move to foundation then consume | Lives in `orkyo-saas/backend/worker`. Product-agnostic; both saas and community need it. Requires approval to move to foundation. |
| `TenantLifecycleService` (dormant tenants) | N/A (saas-only) | ❌ | SaaS-only, ignore for community | No multi-tenant lifecycle in single-tenant deployment |
| `DatabaseMigrationService` | ✅ | ✅ | Already implemented differently | Core ran migrations as a startup service; community uses a dedicated migrator CLI (better pattern) |

---

## Database Migrations

| Area | In core | In community | Decision | Notes |
|---|---|---|---|---|
| Users + identities | ✅ V001-V003 | ✅ foundation 1010-1020 | Already implemented | |
| Audit events | ✅ V003 | ✅ foundation 1030 | Already implemented | |
| ToS acceptances | ✅ | ✅ foundation 1040 | Already implemented | |
| Site settings | ✅ | ✅ foundation 1050 | Already implemented | |
| Announcements | ✅ | ✅ foundation 1060 | Already implemented | |
| Sites / spaces / groups | ✅ | ✅ foundation 1140-1160 | Already implemented | |
| Criteria / capabilities | ✅ | ✅ foundation 1170-1180 | Already implemented | |
| Templates / requests | ✅ | ✅ foundation 1190-1200 | Already implemented | |
| Scheduling | ✅ | ✅ foundation 1270 | Already implemented | |
| Search indexes | ✅ | ✅ foundation 1280 | Already implemented | |
| Tenants / memberships | ✅ | ❌ | SaaS-only, ignore for community | Multi-tenant tables not needed |
| Invitations | ✅ | ❌ | Needs architectural review | Single-tenant community may want invitations; not yet ported |
| Demo seed data | ✅ | ❌ | Port to community | Useful for first-run; add as `3010.community.demo_seed.sql` |

---

## Configuration

| Variable | In core | In community | Decision |
|---|---|---|---|
| `POSTGRES_*` | ✅ | ✅ (single DB vars) | Already implemented |
| `KEYCLOAK_*` / `OIDC_*` | ✅ | ✅ | Already implemented |
| `BFF_*` | ✅ | ✅ | Already implemented |
| `SMTP_*` | ✅ | ✅ | Already implemented |
| `FILE_STORAGE_PATH` | ✅ | ✅ | Already implemented |
| `COMMUNITY__*` (tenant identity) | N/A | ✅ | Community-specific addition |
| `REDIS_*` / `REDIS_CONNECTION` | ✅ | ❌ | SaaS-only (no Redis needed in community) |
| `METRICS_TOKEN` | ✅ | ❌ | Deferred — add when observability is implemented |

---

## Deployment Scripts

| Script | In core | In community | Decision |
|---|---|---|---|
| `dev.sh` | ✅ | ✅ | Already implemented |
| `check-env.sh` | ✅ | ✅ | Already implemented |
| `sync-assets.sh` | ✅ (foundation) | ✅ (via foundation) | Already implemented |
| `setup.sh` | ✅ | ❌ | Port to community |
| `generate-api-key.sh` | ✅ | ❌ | Port to community (useful for self-hosters) |
| `bw-load-env.sh` | ✅ | ❌ | Optional — Bitwarden dev tooling |

---

## Health Checks

| Check | In core | In community | Decision |
|---|---|---|---|
| `/health` endpoint | ✅ | ✅ | Already implemented |
| Postgres health check | ✅ | ✅ | Already implemented — uses `DefaultConnection` |

---

## Test Coverage

| Area | In core | In community | Decision |
|---|---|---|---|
| Foundation backend tests | N/A | ✅ 1471 passing (in foundation) | Already implemented |
| SaaS backend integration tests | N/A | N/A | SaaS-owned |
| Community backend tests | ✅ (147 in core) | ❌ | Port to community — create `backend/tests/` |
| Foundation frontend tests | N/A | ✅ 2204 passing (in foundation) | Already implemented |
| Community frontend tests | ✅ (in core) | ❌ | Port to community — community-specific components only |

---

## Open Items (Prioritised)

| Priority | Item | Decision | Effort |
|---|---|---|---|
| High | `UserAdminEndpoints` community version | Port to community (single-tenant shaped) | Medium |
| High | Community backend test project | Port to community | Medium |
| High | `UserLifecycleService` → foundation | Move to foundation then consume | Requires approval |
| Medium | Demo seed migration `3010.community.demo_seed.sql` | Port to community | Small |
| Medium | `setup.sh` | Port to community | Small |
| Medium | Invitations in community | Needs architectural review | Medium |
| Low | `generate-api-key.sh` | Port to community | Small |
| Low | `tenant-account-api.ts` / `tenant-management-api.ts` cleanup | Obsolete for community | Small |
| Low | Observability / `METRICS_TOKEN` | Deferred | Large |
