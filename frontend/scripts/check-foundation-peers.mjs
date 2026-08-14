// Fails when this repo declares a different major of a foundation peer dependency than the
// SIBLING foundation checkout expects.
//
// Why this exists: postinstall's sync-foundation-build.mjs copies the sibling's compiled
// output over the installed @kymr10n/foundation. In sibling-dev mode the code that actually
// runs is therefore foundation's working tree, while the peer packages it imports come from
// this repo's node_modules. When foundation takes a peer major that this repo has not taken
// yet, every import resolves against the wrong copy and the app dies at runtime with
// something unhelpful — "createCoreRowModel is not a function" was the real case that
// prompted this check, after foundation moved to @tanstack/react-table 9.
//
// npm's own peer resolution cannot catch it: it validates against the *published* package,
// which is exactly the copy sync-foundation-build.mjs overwrites.
//
// Published-package mode (no sibling checkout, e.g. CI) is skipped — npm already enforces
// the published peer ranges there.
import { existsSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const siblingPkgUrl = new URL(
  "../../../orkyo-foundation/frontend/package.json",
  import.meta.url,
);
const siblingPath = fileURLToPath(siblingPkgUrl);

if (!existsSync(siblingPath)) {
  process.exit(0); // published-package mode
}

const foundation = JSON.parse(readFileSync(siblingPath, "utf8"));
const self = JSON.parse(
  readFileSync(fileURLToPath(new URL("../package.json", import.meta.url)), "utf8"),
);

const declared = {
  ...(self.devDependencies ?? {}),
  ...(self.peerDependencies ?? {}),
  ...(self.dependencies ?? {}),
};

/**
 * Major version of a simple range (`^9.1.2`, `~9.1`, `9.1.2`, `9.x`).
 * Returns null for anything else — an unrecognised range is not evidence of a mismatch,
 * so it is skipped rather than guessed at.
 */
function major(range) {
  const m = /^\s*[\^~]?\s*(\d+)\./.exec(range) ?? /^\s*[\^~]?\s*(\d+)(?:\.x)?\s*$/.exec(range);
  return m ? m[1] : null;
}

const mismatches = [];
for (const [name, peerRange] of Object.entries(foundation.peerDependencies ?? {})) {
  const ourRange = declared[name];
  if (!ourRange) continue; // not redeclared here — nothing to drift
  const want = major(peerRange);
  const have = major(ourRange);
  if (want && have && want !== have) {
    mismatches.push({ name, peerRange, ourRange });
  }
}

if (mismatches.length > 0) {
  console.error(
    "\n[check:foundation-peers] FAIL — the sibling orkyo-foundation checkout expects peer\n" +
      "majors this repo does not declare. The synced foundation build will import the wrong\n" +
      "copy and fail at runtime.\n",
  );
  for (const { name, peerRange, ourRange } of mismatches) {
    console.error(`  ${name}\n    foundation peer: ${peerRange}\n    declared here:   ${ourRange}`);
  }
  console.error(
    "\nFix by taking the same major here, then reinstalling:\n" +
      mismatches.map((m) => `  npm install ${m.name}@${m.peerRange}`).join("\n") +
      "\n\nIf foundation's change is not published yet, that publish has to land first — the\n" +
      "package this repo installs in CI still carries the old peer range.\n",
  );
  process.exit(1);
}
