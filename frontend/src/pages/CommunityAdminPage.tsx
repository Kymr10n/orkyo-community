/**
 * Community admin page — single-tenant administration.
 *
 * Intentionally omits Tenants, Users, and Memberships (SaaS multi-tenant concepts).
 * Retains Configuration, Settings, Diagnostics, and Announcements which are
 * meaningful for a self-hosted single-tenant deployment.
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@kymr10n/foundation/src/components/ui/tabs';
import { Button } from '@kymr10n/foundation/src/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import { AdminPageShell } from '@kymr10n/foundation/src/components/admin/AdminPageShell';
import { RouteErrorBoundary } from '@kymr10n/foundation/src/components/ui/RouteErrorBoundary';
import { CommunityConfigurationTab } from '@/components/admin/CommunityConfigurationTab';
import { SettingsTab } from '@kymr10n/foundation/src/components/admin/SettingsTab';
import { DiagnosticsTab } from '@kymr10n/foundation/src/components/admin/DiagnosticsTab';
import { AnnouncementsTab } from '@kymr10n/foundation/src/components/admin/AnnouncementsTab';

export function CommunityAdminPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState('configuration');

  return (
    <AdminPageShell
      breadcrumbLabel="Administration"
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
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid w-full grid-cols-4 max-w-[560px]">
          <TabsTrigger value="configuration">Configuration</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
          <TabsTrigger value="diagnostics">Diagnostics</TabsTrigger>
          <TabsTrigger value="announcements">Announcements</TabsTrigger>
        </TabsList>

        <TabsContent value="configuration" className="mt-6">
          <RouteErrorBoundary label="Configuration"><CommunityConfigurationTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="settings" className="mt-6">
          <RouteErrorBoundary label="Settings"><SettingsTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="diagnostics" className="mt-6">
          <RouteErrorBoundary label="Diagnostics"><DiagnosticsTab /></RouteErrorBoundary>
        </TabsContent>

        <TabsContent value="announcements" className="mt-6">
          <RouteErrorBoundary label="Announcements"><AnnouncementsTab /></RouteErrorBoundary>
        </TabsContent>
      </Tabs>
    </AdminPageShell>
  );
}
