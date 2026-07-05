# Changelog

All notable changes to Orkyo Community Edition are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.14] — 2026-07-05

### Fixed
- Browser **back / forward / refresh** during sign-in no longer dead-ends on an unreachable page — the
  auth flow now redirects to a valid origin and shows a graceful "session expired, try again" page.
- The scheduling board no longer opens on a stale past week on a long-lived browser tab; it re-anchors to
  today when the tab is refocused.

### Changed
- Transient sign-in URLs no longer pollute browser history.
- The people-assignment dialog gained an **"Eligible only"** filter and now shows an error toast when an
  action fails instead of failing silently.

## [0.6.13] — 2026-07-04

### Fixed
- Floorplan image download (`GET /api/sites/{id}/floorplan`) returned **500 Internal Server Error** in the
  single-tenant Community build. Site and settings audit paths were affected the same way.
- `dev.sh` `.env` loading corrupted base64 values that end in `=` (e.g. the master encryption key), which
  could stop the API from starting in local development.

### Changed
- Rate-limit policies are now owned by the shared foundation layer, so every edition enforces an identical,
  complete set. This removes a class of drift where an endpoint could 500 on a policy the edition forgot to
  register.

## [0.6.12] — 2026-07-03

### Added
- Tenant **Audit Log** surfaced under Settings → Audit.

### Changed
- Audit events are now written to the tenant's own database.

---

Older releases are listed on the [Releases page](https://github.com/Kymr10n/orkyo-community/releases).

[0.6.14]: https://github.com/Kymr10n/orkyo-community/releases/tag/v0.6.14
[0.6.13]: https://github.com/Kymr10n/orkyo-community/releases/tag/v0.6.13
[0.6.12]: https://github.com/Kymr10n/orkyo-community/releases/tag/v0.6.12
