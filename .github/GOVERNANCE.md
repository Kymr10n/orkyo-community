# Governance

Orkyo Community is currently maintained by a single maintainer ([@Kymr10n](https://github.com/Kymr10n),
see [CODEOWNERS](CODEOWNERS)). This document is deliberately light — it describes how decisions are made
today, not aspirational process.

## Decisions

- The maintainer reviews and merges all changes. There is no formal committee.
- **Placement first:** any change is evaluated against the [placement rule](CONTRIBUTING.md) — behaviour
  shared with the hosted edition belongs in [orkyo-foundation](https://github.com/Kymr10n/orkyo-foundation);
  only single-tenant/self-host specifics live here.
- Features are weighed against the [roadmap](../ROADMAP.md) and real usage. Bug fixes and well-scoped PRs
  are the fastest path to merge.

## Proposing & escalating

- Use [Discussions](https://github.com/Kymr10n/orkyo-community/discussions) for questions and ideas, and
  [Issues](https://github.com/Kymr10n/orkyo-community/issues) for concrete bugs and proposals.
- Disagreement on scope or placement is resolved in the issue/PR thread; the maintainer makes the final
  call and records the reasoning.
- Security issues follow [SECURITY.md](SECURITY.md), not public issues.

## Status & continuity

Orkyo Community is pre-1.0 and community-supported ([SUPPORT.md](SUPPORT.md)) — best-effort, no SLA. It is
AGPL-3.0 licensed, so the source will always be forkable. If maintainership needs to grow or change, this
document will be updated to match reality.
