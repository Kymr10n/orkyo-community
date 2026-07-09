import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { existsSync, readFileSync } from "node:fs";
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vitest/config";

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
  define: {
    // Replaced at bundle time; AboutPage uses these for version display.
    __BUILD_TIME__: JSON.stringify(new Date().toISOString()),
    __APP_VERSION__: JSON.stringify(
      JSON.parse(readFileSync(
        new URL("./node_modules/@kymr10n/foundation/package.json", import.meta.url),
        "utf-8"
      )).version
    ),
  },
  plugins: [tailwindcss(), react()],
  build: {
    // Browser floor = Tailwind v4 requirement; see foundation docs/UI-GUIDELINES.md#browser-support
    target: ['chrome111', 'edge111', 'firefox128', 'safari16.4'],
    rollupOptions: {
      output: {
        // Split large, stable vendors into their own long-cached chunks so the
        // per-route chunks (see TenantApp's lazy routes) don't each re-bundle
        // them. Grouping mirrors the dedupe list below.
        manualChunks: (id: string) => {
          if (!id.includes("node_modules")) return undefined;
          if (/[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/.test(id))
            return "vendor-react";
          if (id.includes("@radix-ui")) return "vendor-radix";
          if (id.includes("@fullcalendar")) return "vendor-fullcalendar";
          if (id.includes("@dnd-kit")) return "vendor-dnd";
          if (id.includes("@tanstack/react-query")) return "vendor-query";
          if (id.includes("@tanstack")) return "vendor-tanstack";
          if (id.includes("jspdf")) return "vendor-jspdf";
          if (id.includes("recharts") || id.includes("d3-")) return "vendor-recharts";
          return undefined;
        },
      },
    },
  },
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
      "@fullcalendar/core",
      "@fullcalendar/daygrid",
      "@fullcalendar/timegrid",
      "@fullcalendar/interaction",
      "@fullcalendar/react",
      "@tanstack/react-query",
      "@tanstack/react-table",
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
      "sonner",
      "react-day-picker",
      "@dnd-kit/core",
      "@dnd-kit/sortable",
      "@dnd-kit/utilities",
      "lucide-react",
      "recharts",
    ],
  },
  optimizeDeps: {},
  server: {
    host: true,
    port: 5173,
    strictPort: true,
    fs: {
      // Foundation source lands outside the Vite root (/app) in the dev
      // container: the compose volume mounts it at /foundation, and
      // Dockerfile.dev symlinks /orkyo-foundation/frontend → /foundation so the
      // vite.config alias path resolves correctly. Vite 5 follows symlinks to
      // the real path before evaluating fs.allow, so /foundation/* would be
      // blocked without an explicit entry. We allow both the symlink target and
      // the real path so Vite accepts either form at build and HMR time.
      allow: useSiblingFoundation
        ? [".", foundationSiblingSrc, foundationSiblingContracts]
        : ["."],
    },
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
