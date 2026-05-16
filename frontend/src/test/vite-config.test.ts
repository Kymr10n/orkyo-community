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
