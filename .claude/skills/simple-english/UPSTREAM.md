# Vendored skill: simple-english (ASD-STE100)

This directory is a verbatim copy of a third-party agent skill. Do not edit the files in it.
Local rules belong in `orkyo-documentation/docs/LANGUAGE-STANDARD.md`.

## Provenance

| Field | Value |
|---|---|
| Upstream | https://github.com/numericOverflow/SimpleEnglishAISkill-asd-ste100 |
| Pinned commit | `379728b51981b6d2ee1de0f201164483a9648972` |
| License | MIT (see `LICENSE`) |
| Standard | ASD-STE100 Issue 9 (2025-01-15) |

Vendored files: `SKILL.md`, `references/checklist.md`, `references/use-cases.md`,
`ste_lint.py` (upstream path `evals/ste_lint.py`), `LICENSE`.

Each repo carries its own copy on purpose. The four-repo placement rules forbid hidden
coupling between the repos, so a shared package is worse than this duplication.

## How to refresh

```sh
SRC=/path/to/SimpleEnglishAISkill-asd-ste100
git -C "$SRC" pull
cp "$SRC/skills/simple-english/SKILL.md" .claude/skills/simple-english/SKILL.md
cp "$SRC/skills/simple-english/references/"*.md .claude/skills/simple-english/references/
cp "$SRC/evals/ste_lint.py" .claude/skills/simple-english/ste_lint.py
cp "$SRC/LICENSE" .claude/skills/simple-english/LICENSE
```

Then record the new commit in the table above, and repeat in the other four repos.

## Two limitations of the hook that uses this

The `PostToolUse` hook in `.claude/hooks/ste-check.py` runs `ste_lint.py` on documentation
files and reports violations back to the agent.

1. Project settings load from the session's primary working directory. The hook fires when
   this repo is the project root. It does not fire when this repo is only an additional
   working directory of a session rooted in another repo.
2. The settings watcher only watches directories that held a settings file when the session
   started. If this repo received `.claude/settings.json` for the first time, the hook starts
   after a Claude Code restart.

## What the linter cannot do

`ste_lint.py` is a regex pass, not a grammar parser. It undercounts: it does not detect
passive voice and it does not check parts of speech. It can also miscount sentence bounds in
unusual markdown. Use the numbers to compare two versions of the same text. The numbers are
not a compliance verdict. No tool can guarantee ASD-STE100 compliance.
