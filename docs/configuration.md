# Configuration Reference

All configuration is via environment variables. Copy `.env.template` to `.env` and edit.

## Database

| Variable | Description | Example |
|---|---|---|
| `POSTGRES_USER` | Database username | `postgres` |
| `POSTGRES_PASSWORD` | Database password | — change this |
| `POSTGRES_DB` | Database name | `orkyo_community` |
| `POSTGRES_PORT` | Host port for Postgres | `5432` |

The API receives `ConnectionStrings__DefaultConnection` assembled from these values. The control-plane and tenant connection strings are aliased to the same database automatically.

## Community Tenant Identity

| Variable | Description | Default |
|---|---|---|
| `COMMUNITY__TENANTID` | Fixed UUID for the community tenant | `00000000-0000-0000-0000-000000000001` |
| `COMMUNITY__TENANTSLUG` | URL-safe tenant slug | `community` |
| `COMMUNITY__TENANTNAME` | Display name | `Orkyo Community` |

These identify the single-tenant context. You can leave `TENANTID` at its default — it is only used internally.

## API

| Variable | Description |
|---|---|
| `API_PORT` | Host port for the API | 
| `APP_BASE_URL` | Public base URL of the API (used in email links) |
| `CORS_ALLOWED_ORIGINS` | Comma-separated allowed frontend origins |
| `FILE_STORAGE_PATH` | Directory for uploaded files (floorplans) |

## Authentication (Keycloak)

| Variable | Description |
|---|---|
| `KEYCLOAK_ADMIN` | Keycloak admin username |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin password — **change in production** |
| `KEYCLOAK_PORT` | Host port for Keycloak |
| `KEYCLOAK_URL` | Public Keycloak URL (browser-facing) |
| `KEYCLOAK_REALM` | Realm name (`orkyo`) |
| `KEYCLOAK_BACKEND_CLIENT_ID` | Confidential client ID |
| `KEYCLOAK_BACKEND_CLIENT_SECRET` | Client secret — **change in production** |
| `OIDC_AUTHORITY` | OIDC authority URL (must match `KEYCLOAK_URL`) |

## BFF Authentication

These are fixed dev values when using `./dev.sh api` (host mode). Set explicitly for containerised deployments.

| Variable | Description |
|---|---|
| `BFF_ENABLED` | Enable BFF cookie flow (`true`) |
| `BFF_COOKIE_DOMAIN` | Cookie domain (empty = exact request host) |
| `BFF_COOKIE_SECURE` | Require HTTPS for session cookie |
| `BFF_REDIRECT_URI` | Keycloak callback URL |
| `BFF_ALLOWED_HOSTS` | Allowed `returnTo` hosts |

## Email (SMTP)

| Variable | Description |
|---|---|
| `SMTP_HOST` | SMTP server hostname |
| `SMTP_PORT` | SMTP port |
| `SMTP_USE_SSL` | Use SSL/TLS |
| `SMTP_USERNAME` | SMTP username (optional) |
| `SMTP_PASSWORD` | SMTP password (optional) |
| `SMTP_FROM_EMAIL` | Sender address |
| `SMTP_FROM_NAME` | Sender display name |
| `MAILHOG_UI_PORT` | MailHog UI port (dev only) |

## Frontend

| Variable | Description |
|---|---|
| `FRONTEND_PORT` | Host port for the frontend dev server |
| `VITE_API_BASE_URL` | API URL visible to the browser |

## Build

| Variable | Description |
|---|---|
| `APP_VERSION` | Version label injected at build time (`dev` for local) |
