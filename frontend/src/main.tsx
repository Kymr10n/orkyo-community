import "@fontsource-variable/inter";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./index.css";
import { initRUM } from "@kymr10n/foundation/src/lib/core/rum";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000 },
  },
});
import { STORAGE_KEYS } from "@kymr10n/foundation/src/constants/storage";
import { COOKIE_NAMES } from "@kymr10n/foundation/src/constants/http";

if (typeof document !== "undefined") {
  const stored = localStorage.getItem(STORAGE_KEYS.THEME) || "system";
  let isDark: boolean;
  if (stored === "system") {
    isDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  } else {
    isDark = stored === "dark";
  }
  document.documentElement.classList.toggle("dark", isDark);
  document.cookie = `${COOKIE_NAMES.THEME}=${isDark ? "dark" : "light"};path=/;max-age=31536000;SameSite=Lax`;
}

initRUM();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
