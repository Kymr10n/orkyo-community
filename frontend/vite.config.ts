import react from "@vitejs/plugin-react";
import { existsSync } from "node:fs";
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";

// Dual-mode foundation consumption (mirrors the .NET `<When Exists>` pattern):
//   - If the `orkyo-foundation` sibling checkout is present, alias `@foundation/*`
//     to its source tree for fast local iteration.
//   - Otherwise, fall back to the published `@orkyo/foundation` npm package
//     (resolved via node_modules), so SaaS can build standalone in CI/Docker
//     without requiring the sibling repo.
const foundationSiblingContracts = fileURLToPath(
  new URL("../../orkyo-foundation/frontend/contracts", import.meta.url),
);
const foundationSiblingSrc = fileURLToPath(
  new URL("../../orkyo-foundation/frontend/src", import.meta.url),
);
const useSiblingFoundation =
  existsSync(foundationSiblingContracts) && existsSync(foundationSiblingSrc);

const foundationAliases = useSiblingFoundation
  ? {
      "@foundation/contracts": foundationSiblingContracts,
      "@foundation/src": foundationSiblingSrc,
    }
  : {
      "@foundation/contracts": "@orkyo/foundation/contracts",
      "@foundation/src": "@orkyo/foundation/src",
    };

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
