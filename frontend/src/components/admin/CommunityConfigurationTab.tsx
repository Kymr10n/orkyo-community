/**
 * Configuration tab for the community admin page.
 * In community there is a single tenant; this tab surfaces its configurable settings
 * through the tenant-settings API (same surface as the in-app Settings panel,
 * but accessible here to the site-admin without needing to be a tenant member first).
 */

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@kymr10n/foundation/src/components/ui/card';
import { Settings2 } from 'lucide-react';

export function CommunityConfigurationTab() {
  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold">Configuration</h2>
        <p className="text-sm text-muted-foreground">
          Platform-level configuration for this community instance
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-sm flex items-center gap-2">
            <Settings2 className="h-4 w-4" />
            Tenant Settings
          </CardTitle>
          <CardDescription className="text-xs">
            Per-tenant settings are managed from within the application.
            Navigate to the application using the <strong>Open Application</strong> button,
            then go to Settings.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            In the community edition, platform configuration is applied through
            environment variables at startup (see <code>.env</code> / <code>.env.template</code>).
            Runtime-adjustable settings are available in the in-application Settings panel.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
