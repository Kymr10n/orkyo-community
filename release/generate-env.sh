#!/usr/bin/env bash
# Write a complete .env for the Orkyo Community bundle.
#
# You supply one public URL. The five secrets are generated here, and everything
# else either derives from the URL in compose.yml or stays at its default.
#
# Needs bash 4 or later and openssl. Run it anywhere — a laptop is fine — and
# paste the result into Portainer with "Load variables from .env file".
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE="$SCRIPT_DIR/.env.template"

URL=""
TLS=""
SMTP_HOST=""
SMTP_PORT="587"
SMTP_USE_SSL="false"
SMTP_USERNAME=""
SMTP_PASSWORD=""
SMTP_FROM_EMAIL=""
SMTP_FROM_NAME="Orkyo Community"
NO_SMTP=""
FRONTEND_PORT=""
OUTPUT="$SCRIPT_DIR/.env"
FORCE=""
INTERACTIVE="yes"

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

usage() {
    cat <<'USAGE'
Usage: generate-env.sh [options]

  --url URL          Public app URL, e.g. https://community.example.com
  --tls              Terminate TLS in the stack with Caddy (https:// URL only)
  --smtp-host HOST   SMTP server; without it mail is written to the log
  --smtp-port PORT   SMTP port (default 587)
  --smtp-ssl         Use STARTTLS
  --smtp-user USER   SMTP username
  --smtp-pass PASS   SMTP password
  --smtp-from ADDR   Sender address, e.g. noreply@example.com
  --smtp-from-name N Sender display name (default "Orkyo Community")
  --no-smtp          Skip mail configuration without prompting
  --frontend-port N  Host port for the app when a proxy of your own forwards to
                     it (default 80; not with --tls)
  --output PATH      Where to write (default: .env beside this script)
  --force            Overwrite an existing output file
  -h, --help         This text

With no --url the script prompts for what it needs.
USAGE
}

while [ $# -gt 0 ]; do
    case "$1" in
        --url)            URL="${2:?--url needs a value}"; INTERACTIVE=""; shift 2 ;;
        --tls)            TLS="yes"; shift ;;
        --smtp-host)      SMTP_HOST="${2:?--smtp-host needs a value}"; shift 2 ;;
        --smtp-port)      SMTP_PORT="${2:?--smtp-port needs a value}"; shift 2 ;;
        --smtp-ssl)       SMTP_USE_SSL="true"; shift ;;
        --smtp-user)      SMTP_USERNAME="${2:?--smtp-user needs a value}"; shift 2 ;;
        --smtp-pass)      SMTP_PASSWORD="${2:?--smtp-pass needs a value}"; shift 2 ;;
        --smtp-from)      SMTP_FROM_EMAIL="${2:?--smtp-from needs a value}"; shift 2 ;;
        --smtp-from-name) SMTP_FROM_NAME="${2:?--smtp-from-name needs a value}"; shift 2 ;;
        --no-smtp)        NO_SMTP="yes"; shift ;;
        --frontend-port)  FRONTEND_PORT="${2:?--frontend-port needs a value}"; shift 2 ;;
        --output)         OUTPUT="${2:?--output needs a value}"; shift 2 ;;
        --force)          FORCE="yes"; shift ;;
        -h|--help)        usage; exit 0 ;;
        *)                die "unknown option: $1 (try --help)" ;;
    esac
done

command -v openssl >/dev/null 2>&1 || die "openssl is not installed"
[ -f "$TEMPLATE" ] || die "template not found: $TEMPLATE"

# ── Public URL ────────────────────────────────────────────────────────────────

# http(s)://host, an optional :port, and no path or trailing slash. The bundle
# serves Keycloak under /auth on this origin, so a path here would break it.
valid_url() {
    [[ "$1" =~ ^https?://[A-Za-z0-9._-]+(:[0-9]+)?$ ]]
}

if [ -z "$URL" ]; then
    printf 'Public URL where people reach Orkyo, with no trailing slash.\n'
    printf 'Example: https://community.example.com\n\n'
    read -r -p 'Public URL: ' URL
fi
valid_url "$URL" || die "not a usable public URL: '$URL' (want http(s)://host[:port], no path, no trailing slash)"

# ── TLS ───────────────────────────────────────────────────────────────────────

if [ -z "$TLS" ] && [ -n "$INTERACTIVE" ] && [[ "$URL" == https://* ]]; then
    printf '\nThe stack can terminate TLS itself with Caddy and get certificates\n'
    printf 'automatically. Say no if a proxy of your own already owns ports 80 and 443.\n'
    read -r -p 'Terminate TLS in the stack? [y/N]: ' answer
    [[ "$answer" =~ ^[Yy] ]] && TLS="yes"
fi
if [ -n "$TLS" ] && [[ "$URL" != https://* ]]; then
    die "--tls needs an https:// URL (got '$URL')"
fi

# ── Host port ─────────────────────────────────────────────────────────────────
# The common self-host shape is a proxy that already owns port 80 (Nginx Proxy
# Manager, Traefik, ...) and forwards to the app on another port. In TLS mode the
# port is not a choice: Caddy owns 80/443 and the frontend sits behind it on 8081.

if [ -n "$TLS" ] && [ -n "$FRONTEND_PORT" ]; then
    die "--frontend-port has no effect with --tls: Caddy takes ports 80 and 443, and the frontend moves to 127.0.0.1:8081"
fi
if [ -z "$TLS" ] && [ -z "$FRONTEND_PORT" ] && [ -n "$INTERACTIVE" ]; then
    printf '\nHost port for the app. Keep 80 unless a proxy of your own already owns\n'
    printf 'that port and forwards to another one.\n'
    read -r -p 'Host port [80]: ' FRONTEND_PORT
fi
if [ -n "$FRONTEND_PORT" ] && ! [[ "$FRONTEND_PORT" =~ ^[0-9]{1,5}$ ]]; then
    die "not a port number: '$FRONTEND_PORT'"
fi

# ── SMTP ──────────────────────────────────────────────────────────────────────

if [ -z "$NO_SMTP" ] && [ -z "$SMTP_HOST" ] && [ -n "$INTERACTIVE" ]; then
    printf '\nMail is optional. Without it the app writes each message to its log,\n'
    printf 'invitation links included — fine to evaluate with, not to run on.\n'
    read -r -p 'Configure SMTP now? [y/N]: ' answer
    if [[ "$answer" =~ ^[Yy] ]]; then
        read -r -p 'SMTP host: ' SMTP_HOST
        read -r -p "SMTP port [$SMTP_PORT]: " reply; SMTP_PORT="${reply:-$SMTP_PORT}"
        read -r -p 'Use STARTTLS? [y/N]: ' reply
        [[ "$reply" =~ ^[Yy] ]] && SMTP_USE_SSL="true"
        read -r -p 'SMTP username (blank for none): ' SMTP_USERNAME
        read -r -s -p 'SMTP password (blank for none): ' SMTP_PASSWORD; printf '\n'
        read -r -p 'Sender address: ' SMTP_FROM_EMAIL
        read -r -p "Sender name [$SMTP_FROM_NAME]: " reply; SMTP_FROM_NAME="${reply:-$SMTP_FROM_NAME}"
    fi
fi
[ -n "$NO_SMTP" ] && SMTP_HOST=""

# The backend enforces this too; catching it here beats a container that will
# not start. A host with no sender is the one incomplete block people hit.
if [ -n "$SMTP_HOST" ] && [ -z "$SMTP_FROM_EMAIL" ]; then
    die "--smtp-host needs --smtp-from as well (a sender address is required once mail is on)"
fi

# ── Output guard ──────────────────────────────────────────────────────────────

if [ -e "$OUTPUT" ] && [ -z "$FORCE" ]; then
    die "$OUTPUT already exists. Re-running generates a NEW ORKYO_MASTER_ENCRYPTION_KEY,
       and data encrypted with the old key becomes unreadable. Pass --force only if
       this deployment holds no data you want to keep."
fi

# ── Write ─────────────────────────────────────────────────────────────────────

# Hex, not base64: the value travels through compose interpolation and through
# the realm `sed` in the Keycloak entrypoint, and hex has nothing either treats
# specially. The master key is the exception — the backend wants base64.
secret() { openssl rand -hex 32; }

TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT
cp "$TEMPLATE" "$TMP"

# Set KEY=VALUE whether the template line is active, commented out, or absent.
# The value goes in through a sed `r`-style replacement that treats it as literal
# text, so a password with & or / in it survives.
set_var() {
    local key="$1" value="$2"
    local escaped
    escaped="$(printf '%s' "$value" | sed -e 's/[&|\\]/\\&/g')"
    if grep -qE "^[#[:space:]]*${key}=" "$TMP"; then
        sed -i -E "s|^[#[:space:]]*${key}=.*|${key}=${escaped}|" "$TMP"
    else
        printf '%s=%s\n' "$key" "$value" >> "$TMP"
    fi
}

set_var APP_BASE_URL "$URL"
set_var POSTGRES_PASSWORD "$(secret)"
set_var VALKEY_PASSWORD "$(secret)"
set_var KEYCLOAK_BACKEND_CLIENT_SECRET "$(secret)"

KEYCLOAK_ADMIN_PASSWORD="$(secret)"
set_var KEYCLOAK_ADMIN_PASSWORD "$KEYCLOAK_ADMIN_PASSWORD"
set_var ORKYO_MASTER_ENCRYPTION_KEY "$(openssl rand -base64 32)"

if [ -n "$SMTP_HOST" ]; then
    set_var SMTP_HOST "$SMTP_HOST"
    set_var SMTP_PORT "$SMTP_PORT"
    set_var SMTP_USE_SSL "$SMTP_USE_SSL"
    set_var SMTP_USERNAME "$SMTP_USERNAME"
    set_var SMTP_PASSWORD "$SMTP_PASSWORD"
    set_var SMTP_FROM_EMAIL "$SMTP_FROM_EMAIL"
    set_var SMTP_FROM_NAME "$SMTP_FROM_NAME"
fi

if [ -n "$TLS" ]; then
    set_var COMPOSE_PROFILES tls
    set_var FRONTEND_BIND 127.0.0.1
    set_var FRONTEND_PORT 8081
elif [ -n "$FRONTEND_PORT" ]; then
    set_var FRONTEND_PORT "$FRONTEND_PORT"
fi

# 600 before the content lands: the file carries five secrets.
install -m 600 /dev/null "$OUTPUT"
cat "$TMP" > "$OUTPUT"

printf '\nWrote %s (mode 600).\n\n' "$OUTPUT"
printf 'Keycloak admin password (shown once): %s\n' "$KEYCLOAK_ADMIN_PASSWORD"
if [ -z "$SMTP_HOST" ]; then
    printf 'Mail is log-only. Find invitation links with: docker compose logs api\n'
fi
printf '\nNext: docker compose up -d\n'
