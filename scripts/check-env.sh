#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ENV_FILE="$PROJECT_ROOT/.env"
TEMPLATE_FILE="$PROJECT_ROOT/.env.template"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

if [[ ! -f "$TEMPLATE_FILE" ]]; then
  echo -e "${RED}[check-env] .env.template not found${NC}" >&2
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo -e "${RED}[check-env] .env not found${NC}" >&2
  echo "Create it with: cp .env.template .env"
  exit 1
fi

extract_vars() {
  grep -E '^[A-Z_][A-Z0-9_]*=' "$1" | cut -d= -f1 | sort || true
}

TEMPLATE_VARS="$(extract_vars "$TEMPLATE_FILE")"
ENV_VARS="$(extract_vars "$ENV_FILE")"

MISSING_VARS="$(comm -23 <(echo "$TEMPLATE_VARS") <(echo "$ENV_VARS"))"
EXTRA_VARS="$(comm -13 <(echo "$TEMPLATE_VARS") <(echo "$ENV_VARS"))"

TEMPLATE_MODIFIED="$(stat -c %Y "$TEMPLATE_FILE" 2>/dev/null || echo 0)"
ENV_MODIFIED="$(stat -c %Y "$ENV_FILE" 2>/dev/null || echo 0)"

EXIT_CODE=0

echo -e "${BLUE}[check-env] Checking .env against .env.template${NC}"

if [[ -n "$MISSING_VARS" ]]; then
  echo -e "${RED}[check-env] Missing variables:${NC}"
  while read -r var; do
    [[ -z "$var" ]] && continue
    template_value="$(grep -E "^${var}=" "$TEMPLATE_FILE" | head -1 | cut -d= -f2-)"
    echo "  ${var}=${template_value}"
  done <<< "$MISSING_VARS"
  EXIT_CODE=1
fi

if [[ -n "$EXTRA_VARS" ]]; then
  echo -e "${YELLOW}[check-env] Extra variables in .env (informational):${NC}"
  while read -r var; do
    [[ -n "$var" ]] && echo "  $var"
  done <<< "$EXTRA_VARS"
fi

if [[ "$TEMPLATE_MODIFIED" -gt "$ENV_MODIFIED" ]]; then
  echo -e "${YELLOW}[check-env] Warning: .env.template is newer than .env${NC}"
  EXIT_CODE=1
fi

if [[ $EXIT_CODE -eq 0 ]]; then
  echo -e "${GREEN}[check-env] .env looks up to date${NC}"
else
  echo -e "${YELLOW}[check-env] Please update .env before continuing${NC}"
fi

exit $EXIT_CODE
