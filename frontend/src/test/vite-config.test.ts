import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, it, expect } from "vitest";

// Regression guard for the @fs/foundation/... NS_ERROR_CORRUPTED_CONTENT
// failure observed in Docker dev: Vite 5 enforces server.fs.allow strictly.
// When the foundation source is mounted at /foundation (outside the Vite
// root /app) and aliased via the /orkyo-foundation/frontend symlink, the
// fs.allow block in vite.config.ts is the only thing that keeps Vite from
// blocking those URLs with an empty MIME response.
//
// This test does NOT reproduce the runtime failure — vitest can't
// programmatically import vite.config.ts because its loader doesn't supply
// a file:// scheme to import.meta.url, which vite.config uses for path
// resolution. Instead we assert the config source still declares the
// fs.allow block. That's enough to catch a deletion-regression.
describe("vite.config", () => {
  it("declares server.fs.allow (regression guard for symlinked-foundation /@fs)", () => {
    // vitest runs with cwd at the frontend root, where vite.config.ts lives.
    const source = readFileSync(path.join(process.cwd(), "vite.config.ts"), "utf-8");
    expect(source, "vite.config.ts must declare server.fs.allow").toMatch(
      /server:\s*\{[\s\S]*?fs:\s*\{[\s\S]*?allow:/,
    );
  });
});

// ── FE3: dedupe invariant guard ─────────────────────────────────────────────
// Every React-context / singleton-state dependency foundation ships (direct or
// peer) must be listed in vite.config's `dedupe`, or the product bundle can end
// up with two copies (the dual-React class of runtime bug). Patterns (not a fixed
// list) so a NEW @radix-ui/* or @tanstack/* foundation dep is caught automatically;
// stateless utilities (date-fns, clsx, jspdf, …) are intentionally not required.
const CONTEXT_DEP_PATTERNS = [
  /^react$/, /^react-dom$/, /^react-router-dom$/, /^react-day-picker$/,
  /^@radix-ui\//, /^@tanstack\/react-/, /^@dnd-kit\//, /^@fullcalendar\//,
  /^@xstate\//, /^xstate$/, /^zustand$/, /^sonner$/, /^recharts$/, /^lucide-react$/,
];

describe("vite.config dedupe", () => {
  it("dedupes every context-carrying foundation dependency", () => {
    const source = readFileSync(path.join(process.cwd(), "vite.config.ts"), "utf-8");
    const dedupeBlock = /dedupe:\s*\[([\s\S]*?)\]/.exec(source)?.[1] ?? "";
    const deduped = new Set([...dedupeBlock.matchAll(/"([^"]+)"/g)].map((m) => m[1]));

    const pkg = JSON.parse(
      readFileSync(
        path.join(process.cwd(), "node_modules/@kymr10n/foundation/package.json"),
        "utf-8",
      ),
    );
    const foundationDeps = Object.keys({ ...pkg.dependencies, ...pkg.peerDependencies });
    const mustDedupe = foundationDeps.filter((d) =>
      CONTEXT_DEP_PATTERNS.some((re) => re.test(d)),
    );

    const missing = mustDedupe.filter((d) => !deduped.has(d));
    expect(
      missing,
      `foundation ships context-carrying deps not in vite.config dedupe: ${missing.join(", ")}`,
    ).toEqual([]);
  });
});
