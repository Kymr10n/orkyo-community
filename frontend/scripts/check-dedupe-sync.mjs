// Guard against dedupe drift.
//
// vite.config.ts `dedupe` forces a single instance of every context-bearing /
// module-registry library shared between this app and foundation; a duplicate
// instance silently breaks hooks, React context, and global registries. The list
// is hand-maintained, so it can fall behind foundation's peer dependencies when a
// new shared stateful dep is added there.
//
// This check fails (exit 1) when a foundation peerDependency that MUST be deduped
// is absent from the vite dedupe list. Utility-only peers (pure functions, no
// shared runtime state) are intentionally NOT deduped and are listed below.
// Extra dedupe entries beyond foundation's peers (e.g. sonner, a direct foundation
// dep with a module-level toast registry) are fine — over-deduping is safe.
import { readFileSync, readdirSync, statSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { join } from "node:path";

// Foundation peers that are safe to load as duplicate copies (no React context,
// no module-level state). Keep in sync with the omissions documented in
// vite.config.ts `dedupe`.
const UTILITY_EXCLUSIONS = new Set(["date-fns", "date-fns-tz"]);

function readFoundationPeerDeps() {
  // Prefer the installed package (present in every install, incl. CI); fall back
  // to the sibling checkout for pre-install / local runs.
  const candidates = [
    new URL("../node_modules/@kymr10n/foundation/package.json", import.meta.url),
    new URL("../../../orkyo-foundation/frontend/package.json", import.meta.url),
  ];
  for (const url of candidates) {
    try {
      const pkg = JSON.parse(readFileSync(fileURLToPath(url), "utf8"));
      return pkg.peerDependencies ?? {};
    } catch {
      /* try next candidate */
    }
  }
  return null;
}

function readDedupeList() {
  const vite = readFileSync(
    fileURLToPath(new URL("../vite.config.ts", import.meta.url)),
    "utf8",
  );
  const match = vite.match(/dedupe:\s*\[([^\]]*)\]/s);
  if (!match) {
    console.error("[check:dedupe-sync] could not find `dedupe: [...]` in vite.config.ts");
    process.exit(1);
  }
  return new Set([...match[1].matchAll(/["']([^"']+)["']/g)].map((m) => m[1]));
}

const peerDeps = readFoundationPeerDeps();
if (peerDeps === null) {
  console.log("[check:dedupe-sync] foundation package.json not found — skipping.");
  process.exit(0);
}

const dedupe = readDedupeList();
const required = Object.keys(peerDeps).filter((name) => !UTILITY_EXCLUSIONS.has(name));
const missing = required.filter((name) => !dedupe.has(name));

if (missing.length > 0) {
  console.error(
    "[check:dedupe-sync] FAIL — foundation peer deps missing from vite.config.ts `dedupe`:",
  );
  for (const name of missing) console.error(`  - ${name}`);
  console.error(
    "\nAdd each to `dedupe` (single shared instance), or to UTILITY_EXCLUSIONS in\n" +
      "this script if it is genuinely stateless and safe to load twice.",
  );
  process.exit(1);
}

console.log(`[check:dedupe-sync] OK — all ${required.length} required foundation peers are deduped.`);

// ── Unused dependencies ──────────────────────────────────────────────────────
// A dependency that no source file imports and that foundation does not list as a peer is
// dead weight the products pay for on every install (2026-09 review, F9: two radix packages
// foundation had replaced with native elements were still declared in both products).
// CSS-only packages are found through their @import; anything reached another way is named
// in INDIRECT_DEPENDENCIES with the reason.
const INDIRECT_DEPENDENCIES = new Map([
  ["@kymr10n/foundation", "the shared package itself, imported by path under @kymr10n/foundation/*"],
]);

function readProductDependencies() {
  const pkg = JSON.parse(readFileSync(fileURLToPath(new URL("../package.json", import.meta.url)), "utf8"));
  return Object.keys(pkg.dependencies ?? {});
}

function* walk(dir) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) yield* walk(full);
    else if (/\.(ts|tsx|css|html|mjs)$/.test(entry)) yield full;
  }
}

function importedPackages() {
  const root = fileURLToPath(new URL("..", import.meta.url));
  const files = [...walk(join(root, "src")), join(root, "index.html"), join(root, "vite.config.ts")];
  const found = new Set();
  const specifier = /(?:from\s*|import\s*\(?\s*|@import\s*(?:url\()?\s*)["']([^"'./][^"']*)["']/g;
  for (const file of files) {
    let text;
    try { text = readFileSync(file, "utf8"); } catch { continue; }
    for (const m of text.matchAll(specifier)) {
      const spec = m[1];
      found.add(spec.startsWith("@") ? spec.split("/").slice(0, 2).join("/") : spec.split("/")[0]);
    }
  }
  return found;
}

const imported = importedPackages();
const unused = readProductDependencies().filter(
  (name) => !imported.has(name) && !(name in peerDeps) && !INDIRECT_DEPENDENCIES.has(name),
);
if (unused.length > 0) {
  console.error("[check:dedupe-sync] FAIL — dependencies nothing imports and foundation does not list as a peer:");
  for (const name of unused) console.error(`  - ${name}`);
  console.error("\nRemove each with `npm uninstall <name>`, or name it in INDIRECT_DEPENDENCIES with the reason it is needed.");
  process.exit(1);
}
console.log("[check:dedupe-sync] OK — every declared dependency is imported or a foundation peer.");
