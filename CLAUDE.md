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
- **Never reference a foundation API that is not in the PINNED package version** — local project references compile against foundation `main`; CI builds the package and fails. Downstream changes depending on new foundation APIs land only after the version bump does.
- **Releasing**: this repo is tagged by `orkyo-infra`'s `release-promote.yml` alongside orkyo-saas (runbook: `orkyo-infra/docs/runbooks/deploy.md`); never tag by hand. The `v*` tag run builds and publishes the bundle.
- **Documentation language is ASD-STE100 Simplified Technical English.** Applies to `docs/` and
  `release/docs/`. `release/docs/QUICKSTART.md` and `release/docs/OPERATIONS.md` are procedural:
  imperative, one instruction per sentence, 20 words maximum, condition before the command.
  Everything else is descriptive: simple tenses, 25 words maximum. Approved modals are
  can/will/must. The 53 rules are in `.claude/skills/simple-english/SKILL.md`; the scope table
  and the Orkyo term list are in `orkyo-documentation/docs/LANGUAGE-STANDARD.md`. A
  `PostToolUse` hook (`.claude/hooks/ste-check.py`) reports violations — advisory, no CI gate.
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

## Test coverage (every code change)

**Every change ships at or above 80% patch coverage.** Measure locally before opening a PR —
codecov reporting it afterwards means a review round trip that a two-minute local run avoids:

```bash
dotnet test backend/tests/Orkyo.Community.Tests.csproj --collect:"XPlat Code Coverage"
# then read the cobertura report for the files you touched
npx vitest run --coverage        # frontend
```

Cover the paths that carry risk first: rejection and error branches, authorization decisions,
and anything a security control depends on. A happy path with no failure case tested is the
usual reason a patch lands under 80%.

Three exemptions, and nothing else. State the reason in the PR:

- **Unreachable code.** If a branch cannot execute, the fix is to delete it, not to test it —
  see "no error handling for impossible scenarios" below. A coverage gap is often how dead
  code announces itself.
- **Composition-root wiring** whose only assertion would restate the registration line.
  Registrations that encode a rule — which implementation a config key selects — are behavior
  and must be tested.
- **Third-party integration surfaces** that cannot run under the test host: a browser widget
  loading a remote script, a container entrypoint. Test the decision around them (does the
  widget render at all?) rather than the vendor's code.

This is a rule of practice, not a CI gate. Codecov reports the number and does not block the
merge; deliberately, because a hard threshold turns into a treadmill of tests written to move
a percentage rather than to catch a defect.

## Things not to do

- Don't add tenant-shaped code here. This is single-tenant.
- Don't add Community-only features that should live in foundation.
- Don't ship the release bundle without smoke-testing the self-hosted path.

## Explicit-registration rule

If `Program.cs` calls `UseX()`, it must call `AddX()` in the same file — never rely on `AddFoundationServices` to register a service that the API project uses directly. Foundation owns implementations; the API project owns how it exposes them. This rule exists because Foundation is consumed as a NuGet package in CI/Docker, and there is always a window between a Foundation change landing and the package being published. Any implicit dependency on Foundation registering a service will silently break CI during that window.

## Documentation impact (enforced)

Every commit touching a user-visible surface must record its documentation impact as a
commit trailer — the Definition of Done from orkyo-documentation `SPECIFICATION.md` §12:

```
Docs-impact: none
Docs-impact: docs/user-guide/insights.md
Docs-impact: orkyo-documentation#12
```

`none` is a legitimate answer; the point is a recorded decision, not a mandatory edit.
A `commit-msg` hook (`scripts/check-docs-impact.sh`) blocks commits that touch endpoints,
domain models, components or pages without one. `test:`/`ci:`/`build:`/`docs:` commits and
merges are exempt.

This exists because the published documentation quotes UI strings verbatim, so a behaviour
change falsifies pages silently. The rule was already written down and unenforced, and one
afternoon of work left four pages describing things the product no longer does.
