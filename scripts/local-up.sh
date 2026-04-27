#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose -p orkyo-community -f compose.local.yml --env-file .env up -d --build
