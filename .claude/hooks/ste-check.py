#!/usr/bin/env python3
"""PostToolUse hook: report ASD-STE100 violations in documentation back to the agent.

Reads the Claude Code hook payload on stdin, resolves the edited file, and lints it with the
vendored ste_lint.py when the path is in scope for this repo. Emits nothing when the file is
out of scope or clean.

This hook is advisory. It never blocks a tool call, never fails a build, and always exits 0.
Scope and terminology rules: orkyo-documentation/docs/LANGUAGE-STANDARD.md

This file is identical in all five Orkyo repos. Keep it that way — edit once, copy across.
"""
import json
import sys
from pathlib import Path

# Per-repo scope, keyed by repository directory name.
# "include" and "exclude" are repo-relative path prefixes. "exclude" wins.
# "procedural" prefixes get the 20-word limit; everything else in scope gets 25.
SCOPE = {
    "orkyo-documentation": {
        "include": ["src/content/docs/", "src/content/guides/", "src/content/releases/"],
        "exclude": ["src/content/blog/"],
        "procedural": ["src/content/guides/", "src/content/docs/getting-started/"],
    },
    "orkyo-infra": {
        "include": ["docs/"],
        "exclude": ["infra/"],
        "procedural": ["docs/runbooks/"],
    },
    "orkyo-community": {
        "include": ["docs/", "release/docs/"],
        "exclude": [],
        "procedural": ["release/docs/QUICKSTART.md", "release/docs/OPERATIONS.md"],
    },
    "orkyo-saas": {
        "include": ["docs/"],
        "exclude": ["requirements/", "frontend/marketing/"],
        "procedural": [],
    },
    "orkyo-foundation": {
        "include": ["docs/", "frontend/docs/"],
        "exclude": ["requirements/"],
        "procedural": [],
    },
}

LABELS = {
    "sentence_over_limit": "sentences over the word limit",
    "contraction": "contractions",
    "banned_modal": "banned modals (should/would/may/might/could)",
    "perfect_tense": "perfect tenses",
    "ing_clause": '"-ing" clauses',
    "semicolon": "semicolons",
    "latin_abbrev": "Latin abbreviations",
    "slop_word": "filler words",
    "trailing_condition": "conditions after the command",
    "synonym_rotation": "synonym rotations",
}


def main():
    payload = json.load(sys.stdin)
    tool_input = payload.get("tool_input") or {}
    tool_response = payload.get("tool_response") or {}
    raw = tool_response.get("filePath") or tool_input.get("file_path")
    if not raw:
        return

    path = Path(raw)
    if path.suffix.lower() not in (".md", ".mdx"):
        return

    hooks_dir = Path(__file__).resolve().parent
    repo_root = hooks_dir.parent.parent
    scope = SCOPE.get(repo_root.name)
    if scope is None:
        return

    try:
        rel = path.resolve().relative_to(repo_root).as_posix()
    except ValueError:
        return  # edited file lives outside this repo

    if any(rel.startswith(p) for p in scope["exclude"]):
        return
    if not any(rel.startswith(p) for p in scope["include"]):
        return

    sys.path.insert(0, str(repo_root / ".claude" / "skills" / "simple-english"))
    from ste_lint import lint  # noqa: E402

    text = path.read_text(encoding="utf-8")
    kind = "procedural" if any(rel.startswith(p) for p in scope["procedural"]) else "descriptive"
    result = lint(text, kind)
    if result["violations_total"] == 0:
        return

    found = [
        f"{n}× {LABELS.get(key, key)}"
        for key, n in sorted(result["violations"].items(), key=lambda kv: -kv[1])
        if n
    ]
    limit = 20 if kind == "procedural" else 25
    message = (
        f"STE check on {rel} ({kind}, {limit}-word limit): "
        f"{', '.join(found)}. "
        f"Longest sentence {result['longest_sentence_words']} words, "
        f"{result['violations_per_100w']} violations per 100 words.\n"
        "Load the simple-english skill (.claude/skills/simple-english/SKILL.md), apply its "
        "self-check to this file, and re-read docs/LANGUAGE-STANDARD.md for the Orkyo term "
        "list. Some counts are false positives: check the 'Distinct meanings' table before "
        "collapsing a synonym, and leave code, UI labels, and frontmatter exact."
    )
    json.dump(
        {
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": message,
            },
            "suppressOutput": True,
        },
        sys.stdout,
    )


if __name__ == "__main__":
    try:
        main()
    except Exception:
        pass  # advisory only: never disturb the tool call
