#!/usr/bin/env bash
# Migration header linter for Orkyo SQL migration files.
#
# Usage: lint-migration-headers.sh <sql-dir>
#   <sql-dir>  Root directory containing migration SQL files (searched recursively)
#
# Validates:
#   1. Every NEW migration file has a classification header
#   2. No DROP COLUMN / DROP TABLE / TRUNCATE without Classification: contract
#   3. No MODIFICATION of existing migration files
#   4. Sequential file numbering without gaps (per subdirectory)
#   5. All CREATE INDEX use CONCURRENTLY

set -euo pipefail

SQL_DIR="${1:?Usage: $0 <sql-dir>}"
ERRORS=0
BASE_REF="${GITHUB_BASE_REF:-main}"

# ── Determine changed files ───────────────────────────────────────────────────
# On PR: compare against base branch
# On push to main: compare last two commits
if git rev-parse "origin/$BASE_REF" > /dev/null 2>&1; then
  DIFF_BASE="origin/$BASE_REF"
else
  DIFF_BASE="HEAD~1"
fi

CHANGED_SQL=$(git diff --name-only "$DIFF_BASE"...HEAD -- "$SQL_DIR/**/*.sql" 2>/dev/null || \
              git diff --name-only "$DIFF_BASE" HEAD -- "*.sql" 2>/dev/null || true)

if [ -z "$CHANGED_SQL" ]; then
  echo "No migration SQL files changed — skipping lint."
  exit 0
fi

echo "Checking migration files:"
echo "$CHANGED_SQL"
echo ""

# ── Rule 3: No modification of existing files ─────────────────────────────────
MODIFIED=$(git diff --name-only --diff-filter=M "$DIFF_BASE"...HEAD -- "$SQL_DIR/**/*.sql" 2>/dev/null || true)
if [ -n "$MODIFIED" ]; then
  echo "::error::VIOLATION — existing migration files must never be modified:"
  echo "$MODIFIED"
  ERRORS=$((ERRORS + 1))
fi

# ── Check new files only ──────────────────────────────────────────────────────
NEW_FILES=$(git diff --name-only --diff-filter=A "$DIFF_BASE"...HEAD -- "$SQL_DIR/**/*.sql" 2>/dev/null || true)

for FILE in $NEW_FILES; do
  [ -f "$FILE" ] || continue

  echo "--- $FILE ---"

  # Rule 1: classification header
  if ! grep -qi "^-- Classification:" "$FILE"; then
    echo "::error file=$FILE::Missing classification header. Add:"
    echo "::error file=$FILE::  -- Classification: safe | expand | contract | unsafe"
    echo "::error file=$FILE::  -- Description: <one line>"
    echo "::error file=$FILE::  -- Rollback: <strategy or none-needed>"
    ERRORS=$((ERRORS + 1))
  else
    CLASSIFICATION=$(grep -i "^-- Classification:" "$FILE" | head -1 | sed 's/.*Classification: *//i' | tr '[:upper:]' '[:lower:]' | tr -d '[:space:]')
    echo "  Classification: $CLASSIFICATION"

    # Rule 2: destructive DDL requires contract classification
    if grep -qiE "^\s*(DROP\s+COLUMN|DROP\s+TABLE|TRUNCATE)" "$FILE"; then
      if [ "$CLASSIFICATION" != "contract" ]; then
        echo "::error file=$FILE::DROP COLUMN / DROP TABLE / TRUNCATE requires Classification: contract"
        ERRORS=$((ERRORS + 1))
      fi
    fi
  fi

  # Rule 5: CREATE INDEX must be CONCURRENTLY
  if grep -qiE "^\s*CREATE\s+INDEX\b" "$FILE"; then
    if ! grep -qiE "^\s*CREATE\s+(UNIQUE\s+)?INDEX\s+CONCURRENTLY\b" "$FILE"; then
      echo "::error file=$FILE::CREATE INDEX must use CONCURRENTLY to avoid table locks"
      ERRORS=$((ERRORS + 1))
    fi
  fi
done

# ── Rule 4: Sequential numbering (per immediate subdirectory) ─────────────────
for SUBDIR in $(find "$SQL_DIR" -mindepth 1 -maxdepth 2 -type d 2>/dev/null); do
  FILES=$(ls "$SUBDIR"/*.sql 2>/dev/null | sort || true)
  [ -z "$FILES" ] && continue

  PREV_NUM=0
  while IFS= read -r f; do
    BASENAME=$(basename "$f")
    # Extract leading number (e.g., V001, 001, 1, V1 all work)
    NUM=$(echo "$BASENAME" | grep -oE '^[Vv]?([0-9]+)' | grep -oE '[0-9]+' | sed 's/^0*//')
    [ -z "$NUM" ] && continue
    NUM=$((10#$NUM))  # strip leading zeros for arithmetic
    if [ $PREV_NUM -gt 0 ] && [ $NUM -ne $((PREV_NUM + 1)) ]; then
      echo "::warning::Gap in migration numbering in $SUBDIR: expected $((PREV_NUM + 1)), got $NUM ($BASENAME)"
    fi
    PREV_NUM=$NUM
  done <<< "$FILES"
done

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
if [ $ERRORS -gt 0 ]; then
  echo "::error::Migration lint failed with $ERRORS error(s)"
  exit 1
fi
echo "Migration lint passed."
