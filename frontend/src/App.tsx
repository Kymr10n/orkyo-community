import { ApexGateway } from "@foundation/src/components/auth/ApexGateway";
import { LocalDevShell } from "@foundation/src/components/auth/ApexGateway";
import { TenantApp } from "@foundation/src/components/auth/TenantApp";

const IS_LOCAL_DEV =
  typeof window !== "undefined" &&
  window.location.hostname === "localhost" &&
  import.meta.env.DEV;

export default function App() {
  if (IS_LOCAL_DEV) {
    return <LocalDevShell />;
  }

  return (
    <ApexGateway
      renderTenantApp={() => <TenantApp />}
    />
  );
}
