// Sync foundation's compiled output into node_modules so resolution works in both
// modes: sibling-dev (foundation checked out next to saas) and published-package.
// Vite/TS resolve `@kymr10n/foundation/src/*` via the package `exports` map, which
// points at `.tsbuild/`; this copies the freshly-built sibling output over the
// installed copy. No-op when the sibling checkout isn't present (standalone/CI
// installs already ship `.tsbuild` in the published tarball).
//
// Replaces the previous inline `node -e "..."` postinstall — extracted here so it
// is readable, fails loudly, and can be tested.
import { existsSync, rmSync, cpSync } from "node:fs";
import { fileURLToPath } from "node:url";

const src = fileURLToPath(
  new URL("../../../orkyo-foundation/frontend/.tsbuild", import.meta.url),
);
const dest = fileURLToPath(
  new URL("../node_modules/@kymr10n/foundation/.tsbuild", import.meta.url),
);

if (!existsSync(src)) {
  console.log(
    "[sync-foundation-build] sibling foundation .tsbuild not found — skipping (published-package mode).",
  );
  process.exit(0);
}

rmSync(dest, { recursive: true, force: true });
cpSync(src, dest, { recursive: true });
console.log(`[sync-foundation-build] copied ${src} -> ${dest}`);
