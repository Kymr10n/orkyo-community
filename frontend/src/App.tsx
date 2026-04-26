import { BrowserRouter } from "react-router-dom";
import { AuthProvider, useAuth } from "@foundation/src/contexts/AuthContext";
import { ApexGateway } from "@foundation/src/components/auth/ApexGateway";
import { TenantApp } from "@foundation/src/components/auth/TenantApp";
import { ThemeToggle } from "@foundation/src/components/layout/ThemeToggle";
import { AUTH_STAGES } from "@foundation/src/constants/auth";

/**
 * Community shell — single-tenant, no subdomain routing.
 * Renders ApexGateway for the auth pipeline and switches to TenantApp when ready.
 * SaaS render slots (admin page, tenant select, plan cards) are omitted.
 */
function CommunityShell() {
  const { authStage } = useAuth();

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
