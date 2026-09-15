import { lazy, Suspense } from "react";
import { BrowserRouter, useLocation } from "react-router";
import { AuthProvider, useAuth } from "@kymr10n/foundation/src/contexts/AuthContext";
import { ApexGateway } from "@kymr10n/foundation/src/components/auth/ApexGateway";
import { TenantApp } from "@kymr10n/foundation/src/components/auth/TenantApp";
import { ThemeToggle } from "@kymr10n/foundation/src/components/layout/ThemeToggle";
import { LoadingSpinner } from "@kymr10n/foundation/src/components/ui/LoadingSpinner";
import { AUTH_STAGES, ROUTE_SITE_ADMIN } from "@kymr10n/foundation/src/constants/auth";

// Admin page is admin-only — code-split out of the initial bundle.
const CommunityAdminPage = lazy(() =>
  import("@/pages/CommunityAdminPage").then((m) => ({ default: m.CommunityAdminPage })),
);

// The admin page is injected through the same render slot SaaS uses, so both products
// extend the shell one way (2026-09 review, F1). ApexGateway renders the slot itself at
// /site-admin for a site admin in any pipeline stage.
const renderAdminPage = () => (
  <Suspense fallback={<LoadingSpinner />}>
    <CommunityAdminPage />
  </Suspense>
);

/**
 * Community shell — single-tenant, no subdomain routing.
 *
 * Rendering priority:
 *  1. authStage !== READY              → ApexGateway (auth pipeline; renders the admin slot at /site-admin)
 *  2. READY + /site-admin + admin      → the same slot, because TenantApp has no admin route
 *  3. authStage === READY              → TenantApp (main application)
 *
 * The admin route reuses foundation's ROUTE_SITE_ADMIN so it always matches the shared TopBar's
 * admin menu item (Community has a single admin surface; there's no separate site-admin tier).
 * Step 2 is the one place this shell still branches on the pathname; it goes when TenantApp
 * gains the slot.
 */
function CommunityShell() {
  const { authStage, canAccessAdminPage } = useAuth();
  const { pathname } = useLocation();

  if (authStage !== AUTH_STAGES.READY) {
    return (
      <>
        <ThemeToggle variant="floating" />
        <ApexGateway renderAdminPage={renderAdminPage} />
      </>
    );
  }

  const isAdminRoute = pathname === ROUTE_SITE_ADMIN || pathname.startsWith(`${ROUTE_SITE_ADMIN}/`);
  if (isAdminRoute && canAccessAdminPage) return renderAdminPage();

  return <TenantApp />;
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <CommunityShell />
      </AuthProvider>
    </BrowserRouter>
  );
}
