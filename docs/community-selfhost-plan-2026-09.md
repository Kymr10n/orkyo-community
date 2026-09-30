Status: proposed (2026-09-30)

# Community self-host setup plan

## Context

The Community bundle needs 11 values that the operator types by hand. Five of them are secrets that nobody chooses. Three of them derive from one public URL. One of them is the version that CI stamps. Only the SMTP values are real user input.

The bundle leaves TLS to the operator, with an nginx example. There is no long-lived Community instance, so nobody tests the upgrade path on accumulated data before a release.

This plan reduces the required input to one URL and adds an optional TLS profile. It makes mail optional and adds a nightly image channel for a home test rig. The production VPS does not change.

The plan touches four repositories. The sections below are in dependency order.

## Key decisions

1. **No new tool.** A bash script in the release bundle writes the `.env` file. The script needs only bash and `openssl`. A browser-based generator is deferred until support requests show that the script is not enough.
2. **One required input.** After this plan, the operator supplies `APP_BASE_URL`. Every other value is generated, derived, or optional.
3. **Derivation happens where it is cheapest.** `KEYCLOAK_URL` and `OIDC_AUTHORITY` derive in compose with the nested default form that `release/compose.yml` already uses for `CORS_ALLOWED_ORIGINS`. `BFF_ALLOWED_HOSTS` derives in the backend from the host of `APP_BASE_URL`, because compose cannot extract a host. `BFF_COOKIE_DOMAIN` becomes optional without derivation. The backend already treats an empty value as a host-only cookie, which is the stricter default.
4. **SMTP is optional at the transport layer.** `IEmailService` has about 25 methods, and every one of them calls `SendEmailAsync`. The SMTP block moves behind a new `IEmailTransport` with two implementations. The registration follows the Turnstile pattern in foundation: a set `SMTP_HOST` selects SMTP, an unset one selects the log-only transport. The admin UI already shows `SmtpConfigured`, so the UI does not change.
5. **TLS is a compose profile in the same file.** A Portainer stack is one file, and the bundle promises no bind mounts. Caddy runs under `profiles: ["tls"]` with the command `caddy reverse-proxy --from ${APP_BASE_URL} --to http://frontend:8080`. An `https://` URL gives automatic certificates and the redirect from HTTP. Compose interpolates the whole file regardless of profiles, so the profile must not introduce a `:?` variable.
6. **A bind variable resolves the port conflict.** Caddy needs host ports 80 and 443. The frontend mapping becomes `"${FRONTEND_BIND:-0.0.0.0}:${FRONTEND_PORT:-80}:8080"`. The default is unchanged. In TLS mode the generator writes `FRONTEND_BIND=127.0.0.1` and `FRONTEND_PORT=8081`. An override file was rejected because it breaks the single-file Portainer path.
7. **The nightly channel is a re-tag, not a rebuild.** The scheduled run builds no images today. A new job re-tags the `sha-<sha>` images of `main` as `nightly` with `docker buildx imagetools create`. This is the same pattern as the "Re-tag images (no rebuild)" step in `release-ci.yml`. The provenance gate is unchanged.
8. **The NAS refresh is a compose cron, not Watchtower.** Watchtower updates running containers only. The migrator is a one-shot container, so a Watchtower restart starts the new API before the new migrations run. When the image changes, the command `docker compose pull && docker compose up -d` recreates the migrator and obeys `depends_on`. No inbound access to the NAS is necessary.
9. **The VPS does not change.** The production host runs two environments of 14 containers plus the monitoring stack. Its deploy workflow carries SaaS-specific gates. The infra guide forbids new running services. If a public Community demo is ever wanted, it is a separate small instance and out of scope.

## 1. orkyo-foundation (first)

### Derived allowed hosts

File: `backend/src/Configuration/BffAuthenticationServiceExtensions.cs`.

If `BFF_ALLOWED_HOSTS` is empty and `APP_BASE_URL` is set, the allowed hosts become the host of `APP_BASE_URL`. An explicit value always wins. If both values are empty, the list stays empty, as today.

### Email transport

Files: `backend/core/Services/EmailService.cs`, new `IEmailTransport`, `SmtpEmailTransport`, and `LogOnlyEmailTransport` in `backend/core/Services/`.

`EmailService` receives an `IEmailTransport`. The method `SendEmailAsync` builds the `MimeMessage`. The throttle and the retry stay in `EmailService`. `SmtpEmailTransport` holds the MailKit code that is in `EmailService` today. `LogOnlyEmailTransport` writes the recipient, the subject, and the text body to the log at Information level.

The registration in `AddFoundationServices` and in `AddFoundationWorkerServices` selects the transport. A set `SMTP_HOST` selects `SmtpEmailTransport`. An unset one selects `LogOnlyEmailTransport`. At startup, the log-only transport writes one Warning: `Email delivery is log-only: SMTP_HOST is not set`.

### Optional SMTP keys

Files: `backend/core/Configuration/DeploymentConfig.cs`, `backend/core/Configuration/ConfigurationValidator.cs`.

The five SMTP keys leave `DeploymentConfig.RequiredKeys`. The properties `SmtpHost` and `SmtpFromEmail` read with `GetOptionalString`. The validator gets one conditional rule: if `SMTP_HOST` is set, `SMTP_FROM_EMAIL` must be set. A set but incomplete SMTP block is a startup error, in the same spirit as the duration parser in the BFF options.

### Tests

The patterns to copy are in `backend/tests/`:

- `Configuration/ConfigurationValidatorTests.cs` for the remove-one-key pattern and the new conditional rule.
- `Shared/Keycloak/KeycloakOptionsTests.cs` for the derived-default tests.
- `Configuration/BffAuthenticationServiceExtensionsTests.cs` for the three allowed-hosts cases: explicit wins, derived from the URL, and both empty.
- `Services/EmailServiceTests.cs`. The unreachable-SMTP test moves to `SmtpEmailTransport`. A new test class covers `LogOnlyEmailTransport`.
- `Hosting/OrkyoWorkerHostTests.cs` for the registration rule. The rule selects an implementation from a configuration key, so it is behavior and gets a test.

The script `scripts/ci/check-dead-registrations.py` must find a consumer for each new registration.

### Version and trailer

The change is additive and backward-compatible. The package gets a minor bump. The commit trailer is `Docs-impact: orkyo-documentation src/content/docs/community/install.md`.

## 2. orkyo-community (after the foundation pin lands)

### release/compose.yml

- `KEYCLOAK_URL` on `api` and `worker` becomes `${KEYCLOAK_URL:-${APP_BASE_URL}/auth}`. `OIDC_AUTHORITY` and `KC_HOSTNAME` use the same expression.
- `BFF_COOKIE_DOMAIN`, `BFF_ALLOWED_HOSTS`, `SMTP_HOST`, and `SMTP_FROM_EMAIL` change from `:?` to `:-` on `api` and `worker`.
- The `worker` service gets `APP_BASE_URL`. Today the worker does not receive this value. Its lifecycle emails read the value outside the try block in `EmailService`, so they throw at runtime.
- The frontend port mapping becomes `"${FRONTEND_BIND:-0.0.0.0}:${FRONTEND_PORT:-80}:8080"`.
- A new `caddy` service with the image `caddy:2-alpine` and `profiles: ["tls"]`. It publishes the ports `80:80`, `443:443`, and `443:443/udp`. It mounts the volumes `orkyo_community_caddy_data:/data` and `orkyo_community_caddy_config:/config`. It joins the network `orkyo-public`, has `depends_on: [frontend]`, and `restart: unless-stopped`.
- The header comment lists the required values: `ORKYO_VERSION`, the five secrets, and `APP_BASE_URL`.

### frontend/nginx/default.conf

When the upstream value is present, the two `X-Forwarded-Proto` lines pass it through. A `map` block gives `$http_x_forwarded_proto` with `$scheme` as the fallback. Today the value is always `http` behind Caddy or any other proxy.

### release/generate-env.sh (new)

The script lands in the bundle through `scripts/ci/assemble-release.sh`, which copies the whole `release/` directory.

Interactive prompts:

- The public URL. The script accepts `http://` or `https://` with a host and an optional port, without a trailing slash.
- "Terminate TLS in the stack?" The script offers this prompt only for an `https://` URL.
- SMTP: host, port (587), TLS (no), username, password, from-address, and from-name. The operator can skip the block. Then mail is log-only.

Flags for CI and scripts: `--url`, `--tls`, `--smtp-host`, `--smtp-from`, `--no-smtp`, `--output PATH`, and `--force`.

Generated values:

- `POSTGRES_PASSWORD`, `VALKEY_PASSWORD`, `KEYCLOAK_ADMIN_PASSWORD`, and `KEYCLOAK_BACKEND_CLIENT_SECRET` come from `openssl rand -hex 32`. Hex is safe for compose interpolation and for the realm `sed` in `backend/keycloak/docker-entrypoint.sh`.
- `ORKYO_MASTER_ENCRYPTION_KEY` comes from `openssl rand -base64 32`.

Output rules:

- The script copies `.env.template` and replaces values with `sed`. The template stays the single list of keys. The stamped `ORKYO_VERSION` is kept.
- TLS mode adds `COMPOSE_PROFILES=tls`, `FRONTEND_BIND=127.0.0.1`, and `FRONTEND_PORT=8081`.
- The script refuses to overwrite an existing `.env` without `--force`. The message names the encryption key as the reason. A rotated key makes the encrypted data unreadable.
- The output file gets mode `600`. The script prints the Keycloak admin password once, and one line with the next step.

Portainer users can run the script on any machine with bash, for example a laptop. The stack editor loads the result with "Load variables from .env file".

### release/.env.template

Only `ORKYO_VERSION`, the five secrets, and `APP_BASE_URL` keep the REQUIRED mark. `KEYCLOAK_URL`, the `BFF_*` values, and the SMTP values become commented optional lines with the derivation stated. New commented lines exist for `COMPOSE_PROFILES` and `FRONTEND_BIND`.

### Bundle docs and README

Files: `release/docs/QUICKSTART.md`, `release/docs/OPERATIONS.md`, and the root `README.md`.

- The flow starts with the generator.
- The "Required configuration" table shrinks to the seven values.
- The TLS section gets "Option A: built-in Caddy (`COMPOSE_PROFILES=tls`)" before the options with a proxy in front.
- The log-only mail mode is explained, with the place where invitation links appear.

Three errors found during research are corrected in the same change:

- The download URL in QUICKSTART must be `releases/latest/download/orkyo-community.zip`. A versioned file name does not resolve through `/latest/`.
- The README says that three variables are required. The correct list is the seven values above.
- The nginx example listens on host port 80 and proxies to port 80. The frontend container takes port 80 by default, so the example works only with a different `FRONTEND_PORT`.

QUICKSTART and OPERATIONS are procedural documents in Simplified Technical English.

### .github/workflows/release-ci.yml

Job `smoke-selfhosted`:

- The `.env` heredoc is replaced by `bash generate-env.sh --url http://localhost --no-smtp --output .env` plus the CI-only overrides `BFF_COOKIE_SECURE=false` and `KEYCLOAK_URL=http://localhost:9080`. Every existing assertion stays.
- A new assertion: the API log contains `Email delivery is log-only`.
- A second bring-up in the same job uses `generate-env.sh --url https://localhost --tls --no-smtp`. Caddy issues a local-CA certificate for `localhost`. The job asserts that `https://localhost/health` and the OpenID configuration under `/auth/` return 200, and that `http://localhost/` redirects to HTTPS. The job tears down with `-v` between the two runs.

New job `publish-nightly`:

- When `github.event_name == 'schedule'`, it runs after the CI and smoke gates. On other events it is skipped.
- It loops over the five services and runs `docker buildx imagetools create -t ghcr.io/kymr10n/orkyo-community-<svc>:nightly ghcr.io/kymr10n/orkyo-community-<svc>:sha-${GITHUB_SHA}`.
- The existing `nightly-change-check` skip applies. An unchanged `main` publishes nothing.

### docs/nightly-testbed.md (new)

This descriptive document defines the NAS rig:

- The bundle is unpacked under a fixed path. The `.env` comes from the generator with `ORKYO_VERSION=nightly`, a LAN URL, and log-only mail.
- A cron entry runs `scripts/nightly-refresh.sh`. The script runs `docker compose pull && docker compose up -d --remove-orphans` and then `curl -sf $APP_BASE_URL/health`.
- The document names where the logs are.
- The rig tests the upgrade path on accumulated data. The Portainer path is tested by hand on each release. The rig does not test TLS from a public CA.

## 3. orkyo-documentation

Files: `src/content/docs/community/install.md` and `src/content/docs/community/operations.md`.

The requirements paragraph and the two deployment paths describe the generator, the seven required values, the built-in TLS option, and the log-only mail mode. The frontmatter changes only in `updated`. Prettier runs after the edit.

## 4. orkyo-infra

No stack change. This plan records the decision not to host Community on the VPS, and the reasons in decision 9.

## Sequencing

1. The foundation PR merges: derived allowed hosts, email transport, and optional SMTP. The release train publishes the package. The auto-bump lands the pin in Community.
2. The Community PR opens after the pin. The compose change that drops the SMTP `:?` needs a backend that tolerates an unset `SMTP_HOST`. The generator, the doc fixes, and the nightly job are in the same PR, so the bundle docs and the compose file never disagree.
3. The documentation PR follows the Community release that carries the bundle changes. The public pages never describe an unreleased bundle.
4. The NAS rig is set up by hand from the released bundle and the `nightly` tag.

## Verification

- Foundation: `dotnet test` on the new tests, `scripts/ci/patch-coverage.sh --backend` at 80% or more, and `scripts/ci/check-dead-registrations.py`.
- Community: the `smoke-selfhosted` job on a `v*` tag proves plain HTTP and the TLS profile. The Portainer path is tested by hand once with an uploaded `.env`. The script declares `#!/usr/bin/env bash` and is tested on bash 4 or later.
- Nightly: after the first scheduled run, `docker manifest inspect ghcr.io/kymr10n/orkyo-community-api:nightly` resolves to the digest of `sha-<main>`.
- NAS: the first refresh applies the migrations before the API starts, visible in `docker compose logs migrator`. A second refresh on an unchanged tag changes nothing.

## Risks

- The derived `BFF_ALLOWED_HOSTS` changes the behavior of a deployment that left the value empty on purpose. Such a deployment rejects every `returnTo` today, so the change turns a broken state into a working one.
- Log-only mail on an internet-facing install writes invitation tokens to the log. The Warning at startup and a QUICKSTART note state that log-only mail is for evaluation.
- When the host already runs a proxy, Caddy on ports 80 and 443 does not start. The generator offers TLS mode only on request, and QUICKSTART tells which option applies.
- When `main` is unchanged, the nightly re-tag is skipped. A NAS on the `nightly` tag sees at most one image set per day.
