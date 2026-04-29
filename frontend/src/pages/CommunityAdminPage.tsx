/**
 * Community admin page — single-tenant administration.
 *
 * Intentionally omits Tenants, Users, and Memberships (SaaS multi-tenant concepts).
 * Retains Configuration, Settings, Diagnostics, and Announcements which are
 * meaningful for a self-hosted single-tenant deployment.
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@foundation/src/components/ui/tabs';
import { Button } from '@foundation/src/components/ui/button';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@foundation/src/components/ui/popover';
import { Separator } from '@foundation/src/components/ui/separator';
import { ArrowLeft, LogOut, Shield, User } from 'lucide-react';
import { useAuth } from '@foundation/src/contexts/AuthContext';
import { ThemeToggle } from '@foundation/src/components/layout/ThemeToggle';
import { CommunityConfigurationTab } from '@/components/admin/CommunityConfigurationTab';
import { SettingsTab } from '@foundation/src/components/admin/SettingsTab';
import { DiagnosticsTab } from '@foundation/src/components/admin/DiagnosticsTab';
import { AnnouncementsTab } from '@foundation/src/components/admin/AnnouncementsTab';

export function CommunityAdminPage() {
  const { appUser, logout } = useAuth();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState('configuration');

  return (
    <div className="min-h-screen bg-background">
      <header className="h-14 border-b bg-card flex items-center px-4 gap-4 sticky top-0 z-50">
        <div className="font-semibold text-base whitespace-nowrap">Orkyo</div>
        <div className="flex items-center gap-2 text-muted-foreground">
          <span className="text-sm">/</span>
          <Shield className="h-4 w-4" />
          <span className="text-sm font-medium">Administration</span>
        </div>

        <div className="flex-1" />

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            className="gap-2"
            onClick={() => navigate('/')}
          >
            <ArrowLeft className="h-4 w-4" />
            Open Application
          </Button>

          <ThemeToggle />

          <Popover>
            <PopoverTrigger asChild>
              <Button variant="ghost" size="icon">
                <User className="h-4 w-4" />
              </Button>
            </PopoverTrigger>
            <PopoverContent align="end" className="w-64">
              <div className="space-y-3">
                <div className="space-y-1">
                  <p className="text-sm font-medium">{appUser?.displayName || 'Admin'}</p>
                  <p className="text-xs text-muted-foreground">{appUser?.email}</p>
                </div>
                <Separator />
                <Button
                  variant="ghost"
                  className="w-full justify-start h-9 text-destructive hover:text-destructive"
                  onClick={logout}
                >
                  <LogOut className="h-4 w-4 mr-2" />
                  Sign out
                </Button>
              </div>
            </PopoverContent>
          </Popover>
        </div>
      </header>

      <div className="container mx-auto px-6 py-6">
        <Tabs value={activeTab} onValueChange={setActiveTab}>
          <TabsList className="grid w-full grid-cols-4 max-w-[560px]">
            <TabsTrigger value="configuration">Configuration</TabsTrigger>
            <TabsTrigger value="settings">Settings</TabsTrigger>
            <TabsTrigger value="diagnostics">Diagnostics</TabsTrigger>
            <TabsTrigger value="announcements">Announcements</TabsTrigger>
          </TabsList>

          <TabsContent value="configuration" className="mt-6">
            <CommunityConfigurationTab />
          </TabsContent>

          <TabsContent value="settings" className="mt-6">
            <SettingsTab />
          </TabsContent>

          <TabsContent value="diagnostics" className="mt-6">
            <DiagnosticsTab />
          </TabsContent>

          <TabsContent value="announcements" className="mt-6">
            <AnnouncementsTab />
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
