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
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

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
