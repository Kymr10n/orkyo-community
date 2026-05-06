import react from "@vitejs/plugin-react";
import { existsSync } from "node:fs";
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";

// Foundation consumption strategy:
//   - Community imports foundation as `@kymr10n/foundation/src/...` and
//     `@kymr10n/foundation/contracts/...`.
//   - In sibling-dev mode, alias package paths to sibling source for fast
//     iteration. The sibling source still uses internal `@foundation/*` imports,
//     so those aliases are mounted in sibling-dev mode only.
const foundationSiblingContracts = fileURLToPath(
  new URL("../../orkyo-foundation/frontend/contracts", import.meta.url),
);
const foundationSiblingSrc = fileURLToPath(
  new URL("../../orkyo-foundation/frontend/src", import.meta.url),
);
const useSiblingFoundation =
  existsSync(foundationSiblingContracts) && existsSync(foundationSiblingSrc);

const foundationAliases: Record<string, string> = useSiblingFoundation
  ? {
      "@kymr10n/foundation/contracts": foundationSiblingContracts,
      "@kymr10n/foundation/src": foundationSiblingSrc,
      "@foundation/contracts": foundationSiblingContracts,
      "@foundation/src": foundationSiblingSrc,
    }
  : {};

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
      ...foundationAliases,
    },
    // Foundation is a sibling source checkout with its own node_modules.
    // When SaaS imports a foundation component via @foundation/src/*, Node's
    // default resolution walks from the foundation file upward and finds
    // foundation's own copies of React/react-dom/@radix-ui/zustand/etc.,
    // producing a second React instance and blowing up hooks inside any
    // context-carrying library. Dedupe forces these to resolve to SaaS's
    // single copy. The list covers every package from foundation/package.json
    // that either holds React context, React state, or a module-level
    // registry — utility-only libs (date-fns, clsx, rrule, jspdf, …) are
    // safe to load twice and omitted.
    dedupe: [
      "react",
      "react-dom",
      "react-router-dom",
      "@tanstack/react-query",
      "@tanstack/react-virtual",
      "@radix-ui/react-alert-dialog",
      "@radix-ui/react-checkbox",
      "@radix-ui/react-collapsible",
      "@radix-ui/react-dialog",
      "@radix-ui/react-dropdown-menu",
      "@radix-ui/react-label",
      "@radix-ui/react-popover",
      "@radix-ui/react-scroll-area",
      "@radix-ui/react-select",
      "@radix-ui/react-separator",
      "@radix-ui/react-slot",
      "@radix-ui/react-switch",
      "@radix-ui/react-tabs",
      "@radix-ui/react-tooltip",
      "@radix-ui/react-visually-hidden",
      "zustand",
      "xstate",
      "@xstate/react",
      "react-day-picker",
      "@dnd-kit/core",
      "@dnd-kit/sortable",
      "@dnd-kit/utilities",
      "lucide-react",
    ],
  },
  optimizeDeps: {
    esbuildOptions: {
      // Some pre-bundled deps (@dnd-kit, lucide-react) ship source map files
      // with empty "sources" arrays, causing browser DevTools to log
      // "No sources are declared in this source map." Disabling source maps
      // for pre-bundled deps eliminates those warnings without affecting
      // source maps for our own application code.
      sourcemap: false,
    },
  },
  server: {
    host: true,
    port: 5173,
    strictPort: true,
  },
  test: {
    environment: "happy-dom",
    globals: true,
    setupFiles: "./src/test/setup.ts",
    exclude: ["**/node_modules/**", "**/dist/**", "**/.tsbuild/**"],
    server: {
      deps: {
        // Foundation's .tsbuild output uses extensionless directory imports
        // (e.g. ../../lib/utils) which Node.js ESM rejects. Inlining through
        // Vite's bundler resolves them the same way as the production build.
        inline: [/@kymr10n\/foundation/],
      },
    },
    coverage: {
      reporter: ["text", "lcov"],
      include: ["src/**/*.{ts,tsx}"],
      exclude: ["src/main.tsx", "src/jsx-shim.d.ts", "src/test/**"],
      thresholds: {
        lines: 80,
        statements: 80,
        branches: 70,
        functions: 80,
      },
    },
  },
});
