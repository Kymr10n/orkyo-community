#!/usr/bin/env bash
# Migration header linter for Orkyo SQL migration files.
#
# Usage: lint-migration-headers.sh <sql-dir>
#        lint-migration-headers.sh --self-test
#   <sql-dir>    Root directory containing migration SQL files (searched recursively)
#   --self-test  Run the destructive-statement detector against built-in fixtures
#                and exit non-zero on any mismatch. CI runs this before the lint.
#
# Validates:
#   1. Every NEW migration file has a -- @migration-class: header (within first 10 lines)
#   2. No destructive statement without @migration-class: contract — DROP COLUMN,
#      DROP TABLE, TRUNCATE, ALTER COLUMN ... TYPE, RENAME COLUMN / RENAME TO, and
#      ALTER COLUMN ... SET NOT NULL on a column the same file does not add. Matched
#      on the comment-stripped, whitespace-collapsed file, so a statement is caught
#      whether it sits on one line (`ALTER TABLE t DROP COLUMN c;`) or several.
#   3. No MODIFICATION of existing migration files
#   4. Sequential file numbering without gaps (per subdirectory)
#   5. All CREATE INDEX use CONCURRENTLY

set -euo pipefail

# ── Rule 2 detector ───────────────────────────────────────────────────────────
# Strip `--` and `/* */` comments and collapse whitespace, so statement matching
# does not depend on line layout and a comment that mentions DROP TABLE is not a drop.
flatten_sql() {
  sed -E 's/--.*$//' "$1" | tr '\n' ' ' | sed -E 's#/\*([^*]|\*+[^*/])*\*+/##g; s/[[:space:]]+/ /g'
}

# Prints one line per destructive statement in the flattened SQL; no output means none.
# These always narrow the schema contract (docs/migrations/classification.md in orkyo-infra).
destructive_statements() {
  grep -oiE \
    '\b(DROP[[:space:]]+(COLUMN|TABLE)[[:space:]]+(IF[[:space:]]+EXISTS[[:space:]]+)?[^ ,;]+|TRUNCATE[[:space:]]+(TABLE[[:space:]]+)?(ONLY[[:space:]]+)?[^ ,;]+|ALTER[[:space:]]+COLUMN[[:space:]]+[^ ]+[[:space:]]+(SET[[:space:]]+DATA[[:space:]]+)?TYPE\b|ALTER[[:space:]]+TABLE[[:space:]]+(IF[[:space:]]+EXISTS[[:space:]]+)?(ONLY[[:space:]]+)?[^ ]+[[:space:]]+RENAME[[:space:]]+((COLUMN[[:space:]]+)?[^ ]+[[:space:]]+TO|TO)\b)' \
    <<< "$1" || true
}

# ALTER COLUMN <col> SET NOT NULL narrows the contract unless this same file adds
# <col>: a column created and constrained in one migration is additive, because no
# older code writes to it.
not_null_statements() {
  local flat="$1" stmt col
  while IFS= read -r stmt; do
    [ -z "$stmt" ] && continue
    col=$(sed -E 's/^ALTER[[:space:]]+COLUMN[[:space:]]+([^ ]+).*$/\1/I' <<< "$stmt")
    if ! grep -qiE "ADD[[:space:]]+COLUMN[[:space:]]+(IF[[:space:]]+NOT[[:space:]]+EXISTS[[:space:]]+)?${col}\b" <<< "$flat"; then
      echo "$stmt"
    fi
  done < <(grep -oiE '\bALTER[[:space:]]+COLUMN[[:space:]]+[^ ]+[[:space:]]+SET[[:space:]]+NOT[[:space:]]+NULL\b' <<< "$flat" || true)
}

# All Rule 2 findings for one file.
destructive_in_file() {
  local flat
  flat=$(flatten_sql "$1")
  destructive_statements "$flat"
  not_null_statements "$flat"
}

# ── Self-test ─────────────────────────────────────────────────────────────────
# Each fixture is `expect|sql`; expect is `hit` or `none`. Runs with no git state.
self_test() {
  local failures=0 expect sql tmp found verdict
  tmp=$(mktemp)
  trap 'rm -f "$tmp"' RETURN
  while IFS='|' read -r expect sql; do
    [ -z "$expect" ] && continue
    printf '%b\n' "$sql" > "$tmp"
    found=$(destructive_in_file "$tmp")
    verdict=$([ -n "$found" ] && echo hit || echo none)
    if [ "$verdict" = "$expect" ]; then
      echo "  ok   ($expect) $sql"
    else
      echo "  FAIL (expected $expect, got $verdict) $sql"
      failures=$((failures + 1))
    fi
  done <<'FIXTURES'
hit|ALTER TABLE users DROP COLUMN preferred_language;
hit|ALTER TABLE users\n    DROP COLUMN IF EXISTS preferred_language;
hit|DROP TABLE IF EXISTS legacy_feedback;
hit|TRUNCATE TABLE audit_events;
hit|TRUNCATE ONLY sessions;
hit|ALTER TABLE requests ALTER COLUMN priority TYPE bigint;
hit|ALTER TABLE requests ALTER COLUMN priority SET DATA TYPE bigint USING priority::bigint;
hit|ALTER TABLE spaces RENAME COLUMN group_id TO resource_group_id;
hit|ALTER TABLE spaces RENAME TO resources;
hit|ALTER TABLE ONLY spaces RENAME TO resources;
hit|ALTER TABLE resource_groups ALTER COLUMN resource_type_id SET NOT NULL;
none|ALTER TABLE resource_types ADD COLUMN IF NOT EXISTS display_name_plural VARCHAR(100);\nUPDATE resource_types SET display_name_plural = display_name;\nALTER TABLE resource_types ALTER COLUMN display_name_plural SET NOT NULL;
none|ALTER TABLE users ADD COLUMN locale text NOT NULL DEFAULT 'en';
none|-- a later contract migration will DROP COLUMN preferred_language\nALTER TABLE users ADD COLUMN locale text;
none|/* DROP TABLE users was considered and rejected */\nCREATE TABLE audit_events (id uuid PRIMARY KEY);
none|SELECT date_trunc('day', now());
none|DROP INDEX CONCURRENTLY IF EXISTS idx_requests_site;
none|ALTER TABLE requests DROP CONSTRAINT requests_site_fk;
none|ALTER TABLE resource_groups ALTER COLUMN resource_type_id DROP DEFAULT;
none|ALTER INDEX idx_old RENAME TO idx_new;
none|CREATE INDEX CONCURRENTLY idx_requests_site ON requests (site_id);
FIXTURES
  if [ "$failures" -gt 0 ]; then
    echo "::error::lint-migration-headers self-test failed: $failures fixture(s)"
    return 1
  fi
  echo "lint-migration-headers self-test passed."
}

if [ "${1:-}" = "--self-test" ]; then
  self_test
  exit $?
fi

SQL_DIR="${1:?Usage: $0 <sql-dir> | --self-test}"
ERRORS=0
BASE_REF="${GITHUB_BASE_REF:-main}"

# ── Determine changed files ───────────────────────────────────────────────────
# On push: the workflow passes github.event.before as MIGRATION_LINT_BASE, so every
#   commit of the push is linted (origin/main...HEAD is empty once main has moved).
#   A base that is set but unresolvable (force-push, GC) lints the last commit —
#   origin/main...HEAD would be empty there and skip the lint silently.
# On PR: compare against base branch
# Otherwise: compare last two commits
if [ -n "${MIGRATION_LINT_BASE:-}" ] && [ "${MIGRATION_LINT_BASE}" != "0000000000000000000000000000000000000000" ]; then
  if git cat-file -e "${MIGRATION_LINT_BASE}^{commit}" 2>/dev/null; then
    DIFF_BASE="$MIGRATION_LINT_BASE"
  else
    echo "::warning::MIGRATION_LINT_BASE ${MIGRATION_LINT_BASE} is not a reachable commit — linting HEAD~1..HEAD only"
    DIFF_BASE="HEAD~1"
  fi
elif git rev-parse "origin/$BASE_REF" > /dev/null 2>&1; then
  DIFF_BASE="origin/$BASE_REF"
else
  DIFF_BASE="HEAD~1"
fi

CHANGED_SQL=$(git diff --name-only "$DIFF_BASE"...HEAD -- ":(glob)$SQL_DIR/**/*.sql" 2>/dev/null || \
              git diff --name-only "$DIFF_BASE" HEAD -- ":(glob)$SQL_DIR/**/*.sql" 2>/dev/null || true)

if [ -z "$CHANGED_SQL" ]; then
  echo "No migration SQL files changed — skipping lint."
  exit 0
fi

echo "Checking migration files:"
echo "$CHANGED_SQL"
echo ""

# ── Rule 3: No modification of existing files ─────────────────────────────────
MODIFIED=$(git diff --name-only --diff-filter=M "$DIFF_BASE"...HEAD -- ":(glob)$SQL_DIR/**/*.sql" 2>/dev/null || true)
if [ -n "$MODIFIED" ]; then
  echo "::error::VIOLATION — existing migration files must never be modified:"
  echo "$MODIFIED"
  ERRORS=$((ERRORS + 1))
fi

# ── Check new files only ──────────────────────────────────────────────────────
NEW_FILES=$(git diff --name-only --diff-filter=A "$DIFF_BASE"...HEAD -- ":(glob)$SQL_DIR/**/*.sql" 2>/dev/null || true)

for FILE in $NEW_FILES; do
  [ -f "$FILE" ] || continue

  echo "--- $FILE ---"

  # Rule 1: @migration-class header must appear within first 10 lines
  HEADER=$(head -n 10 "$FILE" | grep -E -m1 '^--[[:space:]]*@migration-class:' || true)
  if [[ -z "$HEADER" ]]; then
    echo "::error file=$FILE::Missing classification header within first 10 lines. Add:"
    echo "::error file=$FILE::  -- @migration-class: expand | data | contract | none"
    ERRORS=$((ERRORS + 1))
  else
    CLASSIFICATION=$(echo "$HEADER" | sed -E 's/^--[[:space:]]*@migration-class:[[:space:]]*//' | tr -d '[:space:]')
    echo "  @migration-class: $CLASSIFICATION"

    case "$CLASSIFICATION" in
      expand|data|contract|none) ;;
      *)
        echo "::error file=$FILE::Invalid @migration-class value '$CLASSIFICATION'. Must be: expand | data | contract | none"
        ERRORS=$((ERRORS + 1))
        ;;
    esac

    if [ "$CLASSIFICATION" = "contract" ]; then
      echo "::warning file=$FILE::contract migration detected — the deploy workflow will require approve_unsafe_migration=true (not rollback-safe)"
    fi

    # Rule 2: destructive statements require contract classification
    DESTRUCTIVE=$(destructive_in_file "$FILE")
    if [ -n "$DESTRUCTIVE" ] && [ "$CLASSIFICATION" != "contract" ]; then
      echo "::error file=$FILE::destructive statement(s) require @migration-class: contract —"
      while IFS= read -r STMT; do
        echo "::error file=$FILE::  $STMT"
      done <<< "$DESTRUCTIVE"
      ERRORS=$((ERRORS + 1))
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
