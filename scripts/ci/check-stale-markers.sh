#!/usr/bin/env bash
# check-stale-markers.sh — refuse new comments that will rot.
#
# Two patterns turned up all over the 2026-09 declutter and never carry information
# for a later reader:
#   * a dated TODO whose date has passed ("remove after one release", a year later)
#   * a refactor wave or phase tag (a phase or wave number, or a W-number, in a comment)
# The check looks only at ADDED lines, so existing history is untouched, and only at
# comment-shaped lines, so UI copy is not affected. Plans, archives and changelogs are
# exempt because there the tags ARE the content.
#
# Usage:
#   scripts/ci/check-stale-markers.sh <file>...          # staged changes (pre-commit)
#   scripts/ci/check-stale-markers.sh --base <ref> [<file>...]   # diff against a ref (CI)
set -euo pipefail

BASE=""
if [[ "${1:-}" == "--base" ]]; then
  BASE="${2:?--base needs a ref}"
  shift 2
fi

EXEMPT='^(docs/plans/|docs/audit/|docs/design-review|requirements/|CHANGELOG|.*\.lock$|.*package-lock\.json$)'
COMMENT='(^|[[:space:]])(//|#|/\*|\*|<!--|--)'
TODAY="$(date +%F)"
TODO_RE='TODO[[:space:]:(-]*([0-9]{4}-[0-9]{2}-[0-9]{2})'
TAG_RE='(^|[^[:alnum:]])(Phase[[:space:]]+[0-9]+|Wave[[:space:]]+[0-9]+|W[0-9]+\.[0-9]+[a-z]?)([^[:alnum:]]|$)'
status=0

diff_added_lines() {
  local file="$1"
  if [[ -n "${BASE}" ]]; then
    git diff -U0 "${BASE}" -- "${file}"
  else
    git diff --cached -U0 -- "${file}"
  fi | awk '
    /^@@/ { split($3, a, ","); n = substr(a[1], 2); next }
    /^\+\+\+/ { next }
    /^\+/ { print n ":" substr($0, 2); n++; next }
    /^-/ { next }
    { n++ }'
}

files=("$@")
if [[ ${#files[@]} -eq 0 ]]; then
  if [[ -n "${BASE}" ]]; then
    mapfile -t files < <(git diff --name-only --diff-filter=AM "${BASE}")
  else
    mapfile -t files < <(git diff --cached --name-only --diff-filter=AM)
  fi
fi

for file in "${files[@]}"; do
  [[ "${file}" =~ ${EXEMPT} ]] && continue
  [[ -f "${file}" ]] || continue
  while IFS= read -r entry; do
    line_no="${entry%%:*}"
    text="${entry#*:}"
    [[ "${text}" =~ ${COMMENT} ]] || continue
    if [[ "${text}" =~ ${TODO_RE} ]]; then
      due="${BASH_REMATCH[1]}"
      if [[ "${due}" < "${TODAY}" ]]; then
        echo "${file}:${line_no}: dated TODO is overdue (${due}) — do it or drop the date"
        status=1
      fi
    fi
    if [[ "${text}" =~ ${TAG_RE} ]]; then
      echo "${file}:${line_no}: refactor wave/phase tag in a comment — say what the code does instead"
      status=1
    fi
  done < <(diff_added_lines "${file}")
done

if [[ ${status} -eq 0 ]]; then
  echo "check-stale-markers: OK"
fi
exit ${status}
