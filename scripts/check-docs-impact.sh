#!/usr/bin/env bash
# Fails a commit that changes user-visible behaviour without recording its documentation
# impact.
#
# The rule is not new — orkyo-documentation SPECIFICATION.md §12 ("Definition of Done for
# product changes") already requires every user-visible change to record one of: no impact,
# a documentation change, or an owned issue. Nothing enforced it, so it never happened: one
# afternoon of resource-model work left four published pages stating things the product no
# longer does.
#
# Record the decision as a commit trailer:
#
#   Docs-impact: none
#   Docs-impact: docs/user-guide/insights.md
#   Docs-impact: orkyo-documentation#12
#
# `none` is a perfectly good answer — the goal is a recorded decision, not a mandatory edit.
#
# Escape hatch: `git commit --no-verify`. Prefer `Docs-impact: none`, which leaves a trail.
set -euo pipefail

MSG_FILE="${1:?commit message file path expected as \$1}"

# Paths whose contents a user can observe: HTTP surface, domain shapes that cross it, and
# every rendered component or page. Deliberately broad — a false prompt costs one line,
# a missed one costs a wrong documentation page.
# No marketing tree in this edition; the admin console and shell are the user surface.
USER_VISIBLE_PATHS=(
  'backend/src/Endpoints/'
  'frontend/src/components/'
  'frontend/src/pages/'
)

# Conventional-commit types that cannot alter user experience by definition. Note `chore`
# and `refactor` are absent: both routinely change behaviour in practice.
EXEMPT_TYPES='^(test|ci|build|docs)(\(.+\))?!?:'

subject=$(head -1 "$MSG_FILE")

# Merges and reverts carry someone else's subject line; the underlying commits were gated.
case "$subject" in
  Merge*|Revert*|fixup!*|squash!*) exit 0 ;;
esac

if printf '%s' "$subject" | grep -qE "$EXEMPT_TYPES"; then
  exit 0
fi

staged=$(git diff --cached --name-only --diff-filter=ACMR)
[[ -z "$staged" ]] && exit 0

touched=()
for prefix in "${USER_VISIBLE_PATHS[@]}"; do
  while IFS= read -r file; do
    [[ -n "$file" ]] && touched+=("$prefix")
    break
  done < <(printf '%s\n' "$staged" | grep -F "$prefix" || true)
done

(( ${#touched[@]} == 0 )) && exit 0

# Trailer may appear anywhere in the body; grep the whole message, case-insensitively,
# and require a non-empty value.
if grep -qiE '^Docs-impact:[[:space:]]*[^[:space:]]+' "$MSG_FILE"; then
  exit 0
fi

cat >&2 <<EOF

  This commit touches user-visible surfaces:

$(printf '    %s\n' "${touched[@]}" | sort -u)

  but records no documentation impact.

  Add a trailer to the commit message — one of:

    Docs-impact: none
    Docs-impact: docs/user-guide/<page>.md
    Docs-impact: orkyo-documentation#<issue>

  Why: orkyo-documentation SPECIFICATION.md §12 makes this the Definition of Done for
  product changes. Documentation quotes UI strings verbatim, so a behaviour change
  falsifies pages silently unless the impact is recorded when the change is made.

EOF
exit 1
