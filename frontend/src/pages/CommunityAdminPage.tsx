/**
 * Community admin page — single-tenant administration.
 *
 * Intentionally omits Tenants, Users, and Memberships (SaaS multi-tenant concepts).
 * Retains Configuration, Settings, Diagnostics, and Announcements which are
 * meaningful for a self-hosted single-tenant deployment.
 */

import { useNavigate } from 'react-router-dom';
import { TabsContent } from '@kymr10n/foundation/src/components/ui/tabs';
import { PageTabs } from '@kymr10n/foundation/src/components/layout/PageTabs';
import { Button } from '@kymr10n/foundation/src/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import { AdminPageShell } from '@kymr10n/foundation/src/components/admin/AdminPageShell';
import { RouteErrorBoundary } from '@kymr10n/foundation/src/components/ui/RouteErrorBoundary';
import { useTabParam } from '@kymr10n/foundation/src/hooks/useTabParam';
import { CommunityConfigurationTab } from '@/components/admin/CommunityConfigurationTab';
import { SettingsTab } from '@kymr10n/foundation/src/components/admin/SettingsTab';
import { DiagnosticsTab } from '@kymr10n/foundation/src/components/admin/DiagnosticsTab';
import { AnnouncementsTab } from '@kymr10n/foundation/src/components/admin/AnnouncementsTab';
import { FeedbackTab } from '@kymr10n/foundation/src/components/admin/FeedbackTab';
import { AuditLogTab } from '@kymr10n/foundation/src/components/admin/AuditLogTab';

export function CommunityAdminPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useTabParam('configuration');

  return (
    <AdminPageShell
      title="Administration"
      description="Manage configuration, settings, and diagnostics for this deployment"
      accountHref="/account"
      headerExtras={
        <Button
          variant="outline"
          size="sm"
          className="gap-2"
          onClick={() => navigate('/')}
        >
          <ArrowLeft className="h-4 w-4" />
          Open Application
        </Button>
      }
    >
      <PageTabs
        tabs={[
          { value: 'configuration', label: 'Configuration' },
          { value: 'settings', label: 'Settings' },
          { value: 'audit', label: 'Audit Log' },
          { value: 'diagnostics', label: 'Diagnostics' },
          { value: 'announcements', label: 'Announcements' },
          { value: 'feedback', label: 'Feedback' },
        ]}
        value={activeTab}
        onChange={setActiveTab}
      >
        <TabsContent value="configuration" className="mt-6">
          <RouteErrorBoundary label="Configuration"><CommunityConfigurationTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="settings" className="mt-6">
          <RouteErrorBoundary label="Settings"><SettingsTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="audit" className="mt-6">
          <RouteErrorBoundary label="Audit Log"><AuditLogTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="diagnostics" className="mt-6">
          <RouteErrorBoundary label="Diagnostics"><DiagnosticsTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="announcements" className="mt-6">
          <RouteErrorBoundary label="Announcements"><AnnouncementsTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="feedback" className="mt-6">
          <RouteErrorBoundary label="Feedback"><FeedbackTab /></RouteErrorBoundary>
        </TabsContent>
      </PageTabs>
    </AdminPageShell>
  );
}
