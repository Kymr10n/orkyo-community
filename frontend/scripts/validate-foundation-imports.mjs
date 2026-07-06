import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

// Foundation import discipline:
//   - `@kymr10n/foundation/src/...` and `@kymr10n/foundation/contracts/...` are
//     the only permitted forms. They use the published package name so resolution
//     flows through the package's `exports` map in every tool (TS, Vite, Vitest),
//     with a sibling-checkout override applied at the Vite layer for local dev.
//   - The legacy `@foundation/*` alias is forbidden — TS `paths` does not honor
//     `exports`, which silently produces `any`-typed imports in CI.
//   - Raw repo-relative paths into orkyo-foundation are forbidden — they bypass
//     all resolution layers and break standalone CI/Docker builds.

const SRC_ROOT = new URL("../src", import.meta.url).pathname;
const FORBIDDEN_SNIPPETS = [
  "@foundation/",
  "orkyo-foundation/frontend",
  "../../../../../orkyo-foundation",
  "../../../../orkyo-foundation",
  "../../../orkyo-foundation",
];
const ALLOWED_EXTENSIONS = new Set([".ts", ".tsx"]);

function walk(dir, files = []) {
  for (const entry of readdirSync(dir)) {
    const fullPath = join(dir, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) {
      walk(fullPath, files);
    } else {
      files.push(fullPath);
    }
  }
  return files;
}

const offenses = [];

for (const filePath of walk(SRC_ROOT)) {
  const extension = filePath.slice(filePath.lastIndexOf("."));
  if (!ALLOWED_EXTENSIONS.has(extension)) {
    continue;
  }

  const text = readFileSync(filePath, "utf8");
  const lines = text.split(/\r?\n/);

  lines.forEach((line, index) => {
    const isImport =
      /^\s*(import|export)\b/.test(line) ||
      /\b(require|import)\s*\(/.test(line);
    if (!isImport) return;
    for (const forbidden of FORBIDDEN_SNIPPETS) {
      if (line.includes(forbidden)) {
        offenses.push({
          filePath,
          lineNumber: index + 1,
          line,
          forbidden,
        });
      }
    }
  });
}

if (offenses.length > 0) {
  console.error("Found forbidden foundation imports. Use the published package name `@kymr10n/foundation/src/*` or `@kymr10n/foundation/contracts/*` — resolution flows through the package's `exports` map in every tool (TS, Vite, Vitest), with sibling-checkout dev mode applied via Vite alias.\n");
  for (const offense of offenses) {
    const relativePath = offense.filePath.replace(`${process.cwd()}/`, "");
    console.error(`${relativePath}:${offense.lineNumber}`);
    console.error(`  matched: ${offense.forbidden}`);
    console.error(`  ${offense.line.trim()}\n`);
  }
  process.exit(1);
}

console.log("No forbidden raw cross-repo foundation imports found.");
