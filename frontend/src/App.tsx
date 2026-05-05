import { BrowserRouter, useLocation } from "react-router-dom";
import { AuthProvider, useAuth } from "@kymr10n/foundation/src/contexts/AuthContext";
import { ApexGateway } from "@kymr10n/foundation/src/components/auth/ApexGateway";
import { TenantApp } from "@kymr10n/foundation/src/components/auth/TenantApp";
import { ThemeToggle } from "@kymr10n/foundation/src/components/layout/ThemeToggle";
import { AUTH_STAGES } from "@kymr10n/foundation/src/constants/auth";
import { CommunityAdminPage } from "@/pages/CommunityAdminPage";

/**
 * Community shell — single-tenant, no subdomain routing.
 *
 * Rendering priority:
 *  1. /admin + canAccessAdminPage → CommunityAdminPage (independent of auth stage)
 *  2. authStage !== READY          → ApexGateway (auth pipeline)
 *  3. authStage === READY          → TenantApp (main application)
 */
function CommunityShell() {
  const { authStage, canAccessAdminPage } = useAuth();
  const { pathname } = useLocation();

  const isAdminRoute = pathname === '/admin' || pathname.startsWith('/admin/');

  if (isAdminRoute && canAccessAdminPage) {
    return <CommunityAdminPage />;
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
