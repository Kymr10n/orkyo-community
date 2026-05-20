import "@fontsource-variable/inter";
import { QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./index.css";
import { queryClient } from "@kymr10n/foundation/src/lib/core/query-client";
import { initRUM } from "@kymr10n/foundation/src/lib/core/rum";
import { initTheme } from "@kymr10n/foundation/src/lib/core/theme";

initTheme();
initRUM();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
