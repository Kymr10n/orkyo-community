<!--
  Cross-repo implementation plan (identical copy committed to orkyo-foundation,
  orkyo-saas, and orkyo-community on branch claude/orkyo-mobile-responsiveness-e9vxvu).
  This repo's work: Track C (WP8 manifest, WP9 admin tables - Community parts).
  Executor sessions: pick up the work packages assigned to your model tier
  (Sonnet = mechanical/pattern-following, Opus = design judgment), work through
  them in track order, and delete this file before the branch is merged.
-->

# Orkyo phone/tablet responsiveness — implementation plan

## Context

Orkyo was designed for desktop but "almost works" on an iPhone, because foundation already ships a deliberate responsive system: `useBreakpoint` (phone <768 / tablet 768–1279 / desktop ≥1280), a phone drawer + tablet icon-rail shell in `AppLayout`, a phone card mode on `OrkyoDataTable` (`renderCard`), and drag-free phone fallbacks for the scheduler and floorplan. The gap is the long tail that never got the treatment: the TopBar overflows on phone, ~8 dialog form grids stay multi-column, wizard tab strips crowd, several tables lack card mode, charts/calendar have no phone tuning — and the **marketing site is the weakest area**: 10 static HTML pages with CSS copy-pasted 10×, three inconsistent breakpoints (600/640/768), **no mobile nav at all** (7–8 links overflow every page on phone), no tablet tier, and zero test/CI coverage. There is also no viewport-aware testing anywhere, so mobile can silently regress.

Goal: presentable and working at ~390px (phone) and 768–1024px (tablet) across the app (both editions) and the marketing site. Work packages are sized for follow-up sessions: **Sonnet** = mechanical/pattern-following, **Opus** = design judgment.

**Scope decisions (user-confirmed):**
- Marketing: responsiveness + shared-CSS extraction only. NO dark mode, NO font loading, NO design-token alignment.
- PWA: manifest + icons only, NO service worker.
- Product admin tabs → `OrkyoDataTable`: in scope, lowest priority (last package).

**Branches:** `claude/orkyo-mobile-responsiveness-e9vxvu` in all three repos (`orkyo-foundation`, `orkyo-saas`, `orkyo-community`). One PR per repo; foundation merges first (auto-bump then carries fixes to product Docker builds). No PRs unless requested.

**Verified facts the plan relies on:**
- Foundation `package.json` exports `"./src/*"` (wildcard → `.tsbuild`), so `OrkyoDataTable` is **already public API in the published package** — WP9 needs no new foundation release.
- `MARKETING_CONTENT_TYPES` in `orkyo-saas/frontend/vite.config.ts:32-37` maps only `.html/.js/.txt/.xml` — a shared `.css` would be served as `application/octet-stream` and blocked by browsers' strict stylesheet MIME check. One-line fix required.
- `orkyo-saas/frontend/Dockerfile:23` already root-copies `robots.txt`/`sitemap.xml`/`og-image.jpg` from `marketing/` — the proven delivery path for root-URL marketing assets (`/site.css`), avoiding any dependency on the edge-nginx snippet in the unavailable orkyo-infra repo.
- Playwright in foundation is **deliberately off-CI** (on-demand M7 cross-engine suite, `playwright.config.ts:3-11` + `e2e/README.md`). Respect that.
- Icon pack `orkyo-foundation/assets/orkyo-favicon-pack/` already has `orkyo-192.png`/`orkyo-512.png`; SaaS `prebuild` copies foundation assets into `public/`. **Verify Community has the same `prebuild`** — it did not show up in grep; mirror it if absent.

---

## Track A — Foundation app UI (`orkyo-foundation/frontend`)

### WP1 — Dialog grid responsiveness · **Sonnet** · S
Pure pattern application, following existing good examples (`settings/AvailabilityEventDialog.tsx:538,569`, `onboarding/StarterTemplatePicker.tsx:61`). Change unconditional grids to collapse on phone (re-grep exact lines first; they may drift):

- `grid-cols-2` → `grid-cols-1 sm:grid-cols-2`: `people/PersonEditDialog.tsx:369,458`, `people/PersonAbsenceEditDialog.tsx:102`, `requests/RequestConstraintsSection.tsx:32`, `requests/RequestScheduleSection.tsx:26`, `requests/RequestPeopleSection.tsx:303`, `admin/FeedbackTab.tsx:263`
- `grid-cols-3` → `grid-cols-1 sm:grid-cols-3`: `utilization/AutoSchedulePreviewDialog.tsx:53`, `settings/PresetSettings.tsx:285`
- If children use `col-span-2`, change to `sm:col-span-2` so phone stacks cleanly.

Verify: `npm run typecheck && npm test`; each dialog at 390px via SaaS sibling-dev (`./dev.sh frontend`), no horizontal scroll.

### WP2 — TopBar responsive redesign · **Opus** · M — *highest-impact app item*
File: `frontend/src/components/layout/TopBar.tsx` (render ~lines 147–380). Current row: hamburger (phone) · brand · org breadcrumb · date · site Select (`w-[180px]`) · Search/Import/Export/ThemeToggle/messages-badge/User — all `whitespace-nowrap`, overflows at 390px.

Implement with responsive Tailwind classes (`hidden md:flex` etc.), **not** `useBreakpoint()` branching (matches the hook's own guidance; avoids re-render churn).

- **Phone (<`md`):** keep hamburger, brand, spacer, Search, unread badge (when >0), User popover, plus a **new overflow menu** (Radix `DropdownMenu`, `EllipsisVertical` trigger, `md:hidden`, aria-label "More options"). Hide org breadcrumb, date, site Select, Import, Export, ThemeToggle from the bar.
- **Overflow menu contents:** (1) non-interactive `DropdownMenuLabel` header — org name (truncated) + anchor date; (2) separator + **site selection as `DropdownMenuRadioGroup`** (checked = `selectedSiteId`, `onValueChange={setSelectedSiteId}`, only when >1 sites) — do NOT embed the Radix `Select` inside the dropdown; (3) separator + menu items Import (disabled when `!canImport`), Export (disabled when `!canExport`), Toggle theme (expose ThemeToggle's toggle action; no button-in-button).
- **Tablet (768–1279):** everything inline; org breadcrumb `hidden md:flex max-w-[14rem] truncate`; date `hidden lg:flex`; site Select `hidden md:flex w-[140px] lg:w-[180px]`; overflow menu hidden.
- **Desktop (≥1280):** unchanged.
- **Constraints:** keep `data-testid` values (`user-menu-trigger`, `switch-organization-btn`, `admin-panel-btn`); keep `h-14 sticky top-0 z-50` shell; overflow items reuse `setImportDialogOpen`/`setExportDialogOpen`; only prop stays `onOpenMobileNav` (no public-API change). Extend TopBar vitest: overflow menu renders site radios; import item disabled without permission.

### WP3 — Wizard tabs + CriterionAssignmentEditor shell · **Sonnet** · S–M
1. `requests/RequestFormDialog.tsx:783-802` (5 tabs) and `people/PersonEditDialog.tsx:326-339` (3 tabs): wrap `TabsList` in `<div className="overflow-x-auto">`, give it `w-max min-w-full` with `shrink-0 whitespace-nowrap` triggers (switch `grid grid-cols-N` → `flex` if needed) — strip scrolls horizontally instead of squashing. Keep all labels.
2. `capabilities/CriterionAssignmentEditor.tsx:141`: migrate raw `max-w-2xl max-h-[90vh]` to the UI-GUIDELINES §3 recipe — copy `FormDialog.tsx`'s dvh shell classes verbatim + `ScrollableDialogBody`. Do NOT convert it to `FormDialog` wholesale (dialog-feedback.md exempts it); shell sizing/scroll only.

Verify: vitest; dialogs at 390×664 and keyboard-height ~390×450 — footer buttons reachable, tab strip scrolls.

### WP4 — `renderCard` fill for remaining tables · **Sonnet** · M
Model on `people/PersonList.tsx` / `requests/RequestListView.tsx`. Card rule: bold primary identifier, 2–3 muted secondary lines, row actions bottom-right, same click/edit affordance as the row. Files (non-admin first): `people/PersonAbsenceList.tsx`, `resource-groups/ResourceGroupList.tsx`, `settings/ReportingApiSettings.tsx`, then `admin/AnnouncementsTab.tsx`, `admin/AuditLogTab.tsx`, `admin/FeedbackTab.tsx`. Confirm each actually renders `OrkyoDataTable` before starting.

Verify: vitest; each list at 390px shows cards, actions work, empty state unaffected.

### WP5 — Charts + calendar phone tuning · **Opus** · M
1. `insights/InsightsTrendCharts.tsx` (recharts): on phone via `useBreakpoint()` (legit rendering-config branch) — tighter margins, smaller ticks, `interval="preserveStartEnd"`/`tickCount`, legend below plot; confirm `ResponsiveContainer` + `w-full`.
2. `utilization/RequestCalendar.tsx` (FullCalendar): on phone pick `listWeek` initial view **or** compressed `dayGridMonth` (judgment call after looking at real data density); trim `headerToolbar`; dvh-safe height. Tablet keeps grid views; toolbar must wrap, not overflow.
Guardrail: zero horizontal page scroll at 390px; all controls tappable.

### WP6 — Playwright mobile profiles + smoke suite · **Sonnet** · M — *after WP2 if TopBar check included*
`frontend/playwright.config.ts`: add projects `mobile-webkit` (iPhone 14), `mobile-chromium` (Pixel 7), `tablet-webkit` (iPad gen 7); scope with `testMatch: /.*mobile\.spec\.ts/` on new projects and matching `testIgnore` on the 3 desktop projects. Add `"test:mobile"` npm script.

New `frontend/e2e/mobile.spec.ts` against the existing `e2e/harness/` fixtures:
1. Card-mode table: add `renderCard` to harness fixture; mobile → cards render (no `<table>`), pagination works; tablet → table renders.
2. FormDialog at phone size: submit footer within viewport (dvh cap), Enter-submit works.
3. Wizard tab strip: last tab reachable via horizontal scroll and clickable (small harness fixture addition).
4. TopBar phone behavior: overflow trigger visible at phone / hidden at tablet; opening shows site radios. **Fallback:** if AuthContext stubbing needs more than ~1 focused mock module, drop this check and note it in `e2e/README.md`.
5. Agenda-on-phone: only if the harness already reaches the utilization surface; otherwise skip (vitest covers the `isPhone` branch).

**Stay off-CI** per the M7 on-demand policy; update config header + `e2e/README.md` (mention a future manual `workflow_dispatch` action as the right shape if automation is ever wanted — don't add it).

Verify: `npx playwright install --with-deps && npm run test:mobile` green; existing `npm run test:browsers` still green.

---

## Track B — Marketing site (`orkyo-saas/frontend/marketing/`) — *first-class priority, parallel to Track A*

### WP7a — Shared stylesheet + hamburger nav · **Opus** · M–L
New files: `marketing/site.css`, `marketing/site-nav.js`.

**Delivery (all three together):**
1. Each of the 10 pages: `<link rel="stylesheet" href="/site.css">` **before** its inline `<style>` (page styles keep override power) + `<script src="/site-nav.js" defer></script>`.
2. `vite.config.ts`: add `".css": "text/css; charset=utf-8"` to `MARKETING_CONTENT_TYPES` (required — stylesheets are MIME-blocked otherwise; the dev plugin's `existsSync` branch then serves `/site.css` like `/robots.txt` today).
3. `Dockerfile:23`: extend the root-copy line with `marketing/site.css marketing/site-nav.js`. Check how `contact-form.js`/`design-partner-form.js` are referenced and follow the same convention.

**Into `site.css`** (exactly the 10×-duplicated blocks): reset, `body` base, `.container`, `header`/`.header-content`/`.logo`/nav (replaced by new responsive rules), footer, `.demo-banner`, and the unified breakpoints — phone `@media (max-width: 767px)`, tablet `@media (max-width: 1023px)` — documented at the top with a "no new stops in page styles" comment.

**Nav spec:** ≥1024px — current inline link row unchanged. <1024px — logo left, **Sign In pill stays visible in the header** (primary conversion action, never buried), hamburger right toggling a full-width panel under the header (same dark gradient `#0f172a→#020817`), stacked links, 44px tap targets, closes on link tap. Mechanism: `site-nav.js` (~15 lines: real `<button aria-expanded aria-controls>` toggling an `.open` class) — chosen over the CSS checkbox hack for accessibility and because the repo already ships plain page JS. `<noscript>`-safe fallback: without the JS hook class, links render as a small wrapping row (degraded, functional). **Do NOT introduce a build/include step** — the no-build verbatim-copy deployment is load-bearing; nav markup stays duplicated (10 identical ~15-line blocks).

Apply fully to `index.html` (the template the others copy) and strip its now-shared inline rules.

### WP7b — Rollout to the other 9 pages + per-page fixes · **Sonnet** · M
For `pricing, features, faq, about, contact, terms, security, support, design-partners`.html:
- Replace header/nav/footer markup with the 7a canonical block; delete duplicated reset/container/header/footer/demo-banner CSS from the inline `<style>`; add link/script tags.
- Normalize page media queries to the 767/1023 stops (currently 600 in features/about, 640 in security, 768 elsewhere).
- Add the tablet tier: ≥3-col desktop grids → 2-col at 768–1023, 1-col <768. Fix `security.html:85` `.pillars-grid minmax(340px,1fr)` (overflows in the 641–768 band) → `minmax(280px,1fr)` or explicit `1fr 1fr` ≤1023px.
- Forms pages: inputs `width:100%` on phone; leave form JS untouched.

### WP7c — Marketing route-sync CI check · **Sonnet** · XS
New `frontend/scripts/check-marketing-routes.mjs` (dependency-free, fs + regex): every `marketing/*.html` ⇄ `MARKETING_ROUTES` entry both ways; every page has the `site.css` link and ≤1 `<style>` block. Wire as `npm run check:marketing` into the frontend CI job next to the existing checks.

**Verify (Track B):** dev server — every clean URL at 390/768/1024/1280: no horizontal scroll, hamburger opens/closes, Sign In visible at every width. `docker build frontend/` + `curl -I localhost/site.css` → `text/css`. Note in the PR description that no infra change is needed (assets only, no new routes) but `MARKETING_ROUTES` ⇄ orkyo-infra nginx snippet sync still applies.

---

## Track C — Products (anytime, parallel)

### WP8 — PWA manifest + icons (SaaS + Community) · **Sonnet** · S
Both repos: `frontend/public/manifest.webmanifest` — `name`/`short_name: "Orkyo"`, `display: "standalone"`, `theme_color: "#000000"` (matches existing meta), `background_color: "#ffffff"`, `start_url: "./"`, `scope: "./"`, icons `orkyo-192.png`/`orkyo-512.png` with **relative paths** (SaaS builds with `base: "/app/"` in prod, `/` in dev — relative resolves against the manifest URL in both). Add `<link rel="manifest" href="manifest.webmanifest">` (relative) to both `index.html`. Icons come from the foundation asset pack via SaaS `prebuild` — **verify Community has the same `prebuild`; mirror it if absent.** No service worker.

Verify: `npm run build` → `dist/manifest.webmanifest` + icons present; DevTools → Application → Manifest error-free; Lighthouse installable (SW warning expected/accepted).

### WP9 — Admin tables → OrkyoDataTable · **Sonnet** · L — *strictly last, lowest priority*
Import from `@kymr10n/foundation/src/components/ui/OrkyoDataTable` (already public via the exports wildcard — no foundation release needed). Port columns to `ColumnDef[]`, add `renderCard` per the WP4 card rule.
- SaaS `frontend/src/components/admin/`: `TenantsTab.tsx`, `UsersTab.tsx`, `MembershipsTab.tsx`, `TierDefaultsTab.tsx`, `DesignPartnersTab.tsx`, `TenantQuotaPanel.tsx`.
- Community: `src/pages/CommunityAdminPage.tsx`, `src/components/admin/CommunityConfigurationTab.tsx` — audit first; migrate only actual tabular surfaces (the config tab may be a form).
Keep existing tests passing (make queries layout-agnostic where needed); watch SaaS coverage thresholds (80/80/70/80).

---

## Sequencing & model summary

- **Parallel tracks:** A (foundation: WP1 → WP2 → WP3/4/5 → WP6), B (marketing: WP7a → 7b → 7c), C (WP8 anytime). WP9 strictly last.
- **No publish-window risk:** no package adds a new foundation export products depend on; WP1–6 are internal, WP9 uses an already-published export. The three PRs can merge independently; foundation first is still the polite order.
- **Opus:** WP2 (TopBar), WP5 (charts/calendar), WP7a (marketing shared layer). **Sonnet:** WP1, WP3, WP4, WP6, WP7b, WP7c, WP8, WP9.

## Things NOT to do

- Don't change foundation's public API (exports map, component props, `useBreakpoint` contract); TopBar's `onOpenMobileNav` prop survives.
- Don't touch backend in any repo.
- Don't introduce page-local breakpoint numbers in the app (use the hook or standard Tailwind stops) or new stops in marketing page styles.
- Don't use `vh` in dialogs — `dvh` per UI-GUIDELINES §3.
- Don't add a build step for marketing; don't reference `/marketing/site.css` (use the root-copy path).
- Don't wire Playwright into PR CI (M7 on-demand policy).
- Don't hand-roll dialog shells or toast/invalidate wiring in migrated components (FormDialog / `meta:{successMessage,invalidates}` conventions).

## End-to-end verification

1. Foundation: `npm run typecheck && npm test`; `npm run test:mobile` (new) + `npm run test:browsers` still green.
2. App manual pass via SaaS sibling-dev at 390/768/1280: shell drawer, TopBar overflow menu, Request/Person wizards, card-mode lists, Insights charts, calendar — no horizontal scroll anywhere.
3. Marketing: every clean URL at 390/768/1024/1280 in dev; `docker build` + `curl -I /site.css` → `text/css`.
4. Products: `npm test` + build in both; manifest validates in DevTools; admin pages at 390/768 after WP9.
