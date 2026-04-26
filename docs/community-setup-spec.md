# Implementation Spec: Establish Working `orkyo-community`

## Objective

Create a working `orkyo-community` repository that hosts and deploys a **single-tenant self-hosted instance of Orkyo**.

The current known-good runtime reference is `orkyo-saas`. The original `orkyo-core` repository remains available as a historical reference for missing behavior, but it must not become the architectural baseline for the community edition.

The target outcome is:

- `orkyo-community` builds independently.
- `orkyo-community` runs a complete single-tenant Orkyo instance locally.
- `orkyo-community` consumes shared code from `orkyo-foundation`.
- `orkyo-community` does not depend on `orkyo-saas`, `orkyo-infra`, or `orkyo-core`.
- `orkyo-core` is used only for reconciliation and missing-feature comparison.

---

## Repository Context

Expected workspace during carveout:

```text
orkyo-infra/
orkyo-saas/
orkyo-foundation/
orkyo-community/
orkyo-core/
```

---

## Target Repository Responsibilities

### `orkyo-foundation`

Owns reusable, environment-agnostic code only:

- Domain primitives
- Shared contracts
- Validators
- Shared DTOs
- Scheduler/domain logic that is not SaaS-specific
- Reusable frontend libraries/components where appropriate
- Shared migration infrastructure if already established there

### `orkyo-saas`

Owns SaaS-specific runtime behavior:

- Multi-tenant resolution
- Tenant control-plane integration
- Tenant database routing
- SaaS admin operations
- SaaS deployment assumptions
- SaaS billing/subscription hooks if present

### `orkyo-community`

Owns single-tenant runtime behavior:

- Single-tenant API host
- Single-tenant frontend application
- Single database deployment model
- Community migrator wiring
- Community seed/bootstrap logic
- Self-hosted Docker Compose deployment
- Optional OIDC/Keycloak integration
- Local admin bootstrap
- Community-specific documentation

### `orkyo-core`

Historical reference only:

- Compare missing behavior
- Compare routes/pages/endpoints
- Compare migrations
- Compare scripts and docs
- Do not copy architecture blindly
- Do not introduce dependencies from community to core

---

## Hard Architectural Rules

### Allowed Dependencies

```text
orkyo-community -> orkyo-foundation
orkyo-community -> public NuGet/npm packages
```

### Forbidden Dependencies

```text
orkyo-community -> orkyo-saas
orkyo-community -> orkyo-infra
orkyo-community -> orkyo-core
```

### Do Not

- Do not copy SaaS tenant control-plane behavior into community.
- Do not require subdomain-based tenant resolution in community.
- Do not require one database per tenant in community.
- Do not duplicate foundation code into community to fix build errors.
- Do not change `orkyo-foundation` public APIs without explicit approval.
- Do not introduce workaround code before determining the root cause.
- Do not hide failures by weakening tests, disabling analyzers, or suppressing warnings without justification.
- Do not make `orkyo-community` dependent on the `orkyo-infra` solution.

### Prefer

- Adapters over conditionals.
- Shared abstractions over duplicated implementation.
- One database for community.
- Explicit configuration over hardcoded values.
- KISS and DRY over speculative extensibility.
- A working vertical slice before broad feature reconciliation.

---

## Implementation Strategy

Use `orkyo-saas` as the implementation template because it currently runs successfully.

Use `orkyo-core` only after the community runtime works, to identify missing legacy behavior.

---

## Phase 1 — Repository and Solution Skeleton ✅

**Goal:** Create a clean, independent `orkyo-community` solution.

**Completed:**

- `orkyo-community/` repository initialised with `main` branch
- `Orkyo.Community.slnx` solution file
- Backend projects: `Orkyo.Community.Api`, `Orkyo.Community` (src), `Orkyo.Community.Migrator`, `Orkyo.Community.Migrations`
- Foundation project references (local dev) with NuGet fallback (CI/Docker)
- `README.md`, `.env.template`, `.gitignore`
- Added to `orkyo.code-workspace`

**Acceptance criteria:** ✅ `dotnet build Orkyo.Community.slnx` — 0 errors, 0 warnings.

---

## Phase 2 — Copy and Adapt the Working SaaS Runtime Shape ✅

**Goal:** Use `orkyo-saas` as the template, then simplify for single tenancy.

**Completed:**

- API host structure adapted from saas
- `appsettings.json` / `appsettings.Development.json`
- Docker Compose stack (db, keycloak, mailhog, migrator, api, frontend)
- `dev.sh` with `up` / `down` / `infra` / `api` / `migrator` / `frontend` commands
- Dockerfiles for api and migrator using parent-directory build context
- Frontend skeleton with `vite.config.ts`, `tailwind.config.js`, `postcss.config.js`, `index.html`

**Removed from SaaS:**
- Redis / break-glass sessions
- Rate limiting / bot protection
- Tenant provisioning and tenant service
- `TenantEndpoints`, `InterestEndpoints`, `UserAdminEndpoints`, `MembershipAdminEndpoints`
- `TenantActivityFlushService`, `ControlPlaneAuditService`
- `TenantResolutionStrategy`, `SubdomainResolutionStrategy`

**Acceptance criteria:** ✅ Community API starts with single-tenant configuration.

---

## Phase 3 — Implement Single-Tenant Context ✅

**Goal:** Replace multi-tenant mechanics with a deterministic single-tenant adapter.

**Completed:**

- `SingleTenantOptions` — bound from `Community:*` config section:
  ```env
  COMMUNITY__TENANTID=00000000-0000-0000-0000-000000000001
  COMMUNITY__TENANTSLUG=community
  COMMUNITY__TENANTNAME=Orkyo Community
  ```
- `SingleTenantResolver` — implements `ITenantResolver`; returns fixed `TenantContext` from config
- `SingleTenantMiddleware` — sets per-request `TenantContext` without subdomain/header resolution; honours `[SkipTenantResolution]`
- `TenantContext.Tier = Enterprise` — no resource limits for community
- `CommunityQuotaEnforcer` — implements `IQuotaEnforcer`; all limits return `-1` (unlimited)

**Acceptance criteria:** ✅ API request handling has a stable tenant context. No SaaS control-plane dependency.

---

## Phase 4 — Single Database Model and Migrations ✅

**Goal:** Community runs on one Postgres database by default.

**Completed:**

- `CommunityDbConnectionFactory` — implements `IDbConnectionFactory`; maps control-plane **and** tenant connections to `ConnectionStrings__DefaultConnection`
- `CommunityMigrationModule` — `Order=3000`, loads `sql/tenant/*.sql` from this assembly
- `CommunityTenantRegistry` — implements `ITenantRegistry`; returns one `TenantDatabase` pointing at the deployment DB
- `CommunityMigrationRegistration` — DI extension registering the module
- Migrator `Program.cs` — `AddOrkyoMigrationPlatform() + AddFoundationMigrations() + AddCommunityMigrations()`
- Placeholder migration `3000.community.bootstrap.sql`
- Single database name: `orkyo_community`

**Acceptance criteria:** ✅ `dotnet build` succeeds. Migrator wired to run foundation (1000) then community (3000) migrations against one DB.

---

## Phase 5 — Authentication for Community

**Goal:** Make authentication production-capable but simple for self-hosting.

**Status:** ✅ Infrastructure in place. Keycloak realm JSON copied from `orkyo-saas` (includes seed users, service account roles, required actions, client scopes). BFF auth wired.

**Remaining:**
- Verify end-to-end login works for community instance
- Consider a community-specific realm JSON (different `displayName`, potentially different client ID)
- Document auth setup in `docs/authentication.md`

**Acceptance criteria:**
- User can authenticate via Keycloak BFF flow
- API accepts valid tokens; rejects invalid ones
- Frontend login/logout works

---

## Phase 6 — Local Docker Deployment

**Status:** Docker Compose stack created (`infra/compose/docker-compose.yml`). Services: db, keycloak, mailhog, migrator, api, frontend.

**Remaining:**
- Verify `./dev.sh up` completes end-to-end on a clean checkout
- Ensure migrator completes before API starts (already wired via `depends_on`)
- Smoke test: health endpoint, login, create site/space

**Acceptance criteria:**
```bash
cp .env.template .env
./dev.sh up
# http://localhost:5173 is accessible and login works
```

---

## Phase 7 — Frontend Community Adaptation

**Goal:** Make the frontend run as a single-tenant self-hosted application.

**Status:** Skeleton created (`main.tsx`, `App.tsx`, `index.html`, config files).

**Remaining:**
- `App.tsx` needs community-appropriate routing (no `TenantSelectPage`, no SaaS admin)
- Hide/remove SaaS-only UI: tenant switching, tenant suspension, billing, SaaS org provisioning
- Verify `@foundation` component imports resolve correctly
- Run `npm install && npm run typecheck && npm run build`
- Test golden path: login → site selection → spaces → utilization → requests

**Acceptance criteria:**
- Frontend builds (`vite build`)
- Login works
- Core Orkyo workflows reachable
- No visible SaaS-only dead navigation

---

## Phase 8 — Worker and Background Jobs ✅

**Goal:** Decide whether community requires a background worker.

**Status:** ✅ Complete.

**Decision:**
- `TenantLifecycleService` — **SaaS-only, excluded.** No multi-tenant lifecycle in a single-tenant deployment.
- `UserLifecycleService` (GDPR inactivity management) — **Required for community.**
  Moved from `orkyo-saas/backend/worker` to `orkyo-foundation/backend/src/Services/`. Both saas and community workers now consume it from foundation. `IDbConnectionFactory.CreateControlPlaneConnection()` maps to the single community DB transparently.

**Completed:**
- `Orkyo.Community.Worker` project created and added to solution
- `CommunityWorkerService` runs GDPR user lifecycle daily via foundation's `UserLifecycleService`
- `CommunityDbConnectionFactory` wired as `IDbConnectionFactory` — no SaaS configuration needed
- Saas worker updated to reference foundation (was using only `Orkyo.Shared`)

**Acceptance criteria:** ✅ Worker starts without SaaS configuration. GDPR lifecycle runs against single community DB.

---

## Phase 9 — Reconcile Against `orkyo-core` ✅

**Goal:** After the community runtime works, compare against the original codebase for missing behavior.

**Status:** ✅ Complete. See `docs/core-reconciliation.md`.

**Completed:**
- All 34 core endpoints classified (27 present, 6 SaaS-only, 1 resolved)
- All 16 frontend pages classified (14 present, 2 SaaS-only)
- All 27 API clients confirmed present in foundation
- Worker jobs classified and acted on
- Migrations, config, scripts, and test coverage documented

**Items resolved during Phase 9:**
- `UserAdminEndpoints` moved to foundation, wired in community — closes the "needs architectural review" gap
- `UserLifecycleService` moved to foundation — resolved as part of Phase 8 completion
- Demo seed migration `3010.community.demo_seed.sql` added
- Community backend test project created (`Orkyo.Community.Tests`)

---

## Phase 10 — CI, Quality Gates, and Smoke Tests ✅

**Goal:** Make community independently buildable and testable.

**Status:** ✅ Complete. See `.github/workflows/ci.yml`.

**Completed:**
- `frontend` job: npm ci → lint → typecheck → vite build (with foundation sibling checkout and asset sync)
- `backend` job: dotnet restore → build → migrator smoke test (Postgres service container) → API `--validate`

**Acceptance criteria:** ✅ CI runs from community repo alone. No private SaaS/infra secrets required.

---

## Phase 11 — Documentation

**Goal:** Make community understandable for external/self-hosted users.

**Status:** ✅ Core docs written.

**Required docs:**
```text
README.md                    ✅
docs/community-setup-spec.md ✅
docs/architecture.md         ✅
docs/configuration.md        ✅
docs/migrations.md           ✅
docs/core-reconciliation.md  ✅
docs/deployment-docker.md    🔲 (covers production self-hosting)
docs/authentication.md       🔲 (covers production Keycloak setup)
```

---

## Final Acceptance Criteria

| # | Criterion | Status |
|---|---|---|
| 1 | `Orkyo.Community.slnx` exists | ✅ |
| 2 | Community builds independently | ✅ |
| 3 | No dependency on saas/infra/core | ✅ |
| 4 | Consumes `orkyo-foundation` cleanly | ✅ |
| 5 | Single-tenant context adapter | ✅ |
| 6 | One Postgres database by default | ✅ |
| 7 | Migrator can migrate an empty database | ✅ CI smoke test passes against Postgres container |
| 8 | API starts locally | ✅ CI --validate passes; ./dev.sh up verified |
| 9 | Frontend starts locally | ✅ vite build + tsc + lint clean |
| 10 | Login works | 🔲 end-to-end browser smoke test pending |
| 11 | Core Orkyo workflows reachable | 🔲 end-to-end browser smoke test pending |
| 12 | Docker Compose launches local stack | ✅ compose config valid + manual up verified |
| 13 | CI validates build/test/lint | ✅ .github/workflows/ci.yml |
| 14 | `docs/core-reconciliation.md` exists | ✅ all areas classified |
| 15 | No SaaS-only runtime dependency mandatory | ✅ |
