import { lazy, Suspense } from "react";
import { BrowserRouter, useLocation } from "react-router-dom";
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

/**
 * Community shell — single-tenant, no subdomain routing.
 *
 * Rendering priority:
 *  1. /site-admin + canAccessAdminPage → CommunityAdminPage (independent of auth stage)
 *  2. authStage !== READY              → ApexGateway (auth pipeline)
 *  3. authStage === READY              → TenantApp (main application)
 *
 * The admin route reuses foundation's ROUTE_SITE_ADMIN so it always matches the shared TopBar's
 * admin menu item (Community has a single admin surface; there's no separate site-admin tier).
 */
function CommunityShell() {
  const { authStage, canAccessAdminPage } = useAuth();
  const { pathname } = useLocation();

  const isAdminRoute = pathname === ROUTE_SITE_ADMIN || pathname.startsWith(`${ROUTE_SITE_ADMIN}/`);

  if (isAdminRoute && canAccessAdminPage) {
    return (
      <Suspense fallback={<LoadingSpinner />}>
        <CommunityAdminPage />
      </Suspense>
    );
  }

  if (authStage !== AUTH_STAGES.READY) {
    return (
      <>
        <ThemeToggle variant="floating" />
        <ApexGateway />
      </>
    );
  }

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
