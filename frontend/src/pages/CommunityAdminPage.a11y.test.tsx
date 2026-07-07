import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { axe, toHaveNoViolations } from 'jest-axe';
import { CommunityAdminPage } from './CommunityAdminPage';

expect.extend(toHaveNoViolations);

// ── Mutable auth state (per-test override) ────────────────────────────────────
const mockAuth = {
  appUser: { displayName: 'Alex Admin', email: 'alex@example.com' } as
    | { displayName: string; email: string }
    | null,
  logout: vi.fn(),
};

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<object>();
  return { ...actual, useNavigate: () => vi.fn() };
});

vi.mock('@kymr10n/foundation/src/contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('@kymr10n/foundation/src/components/layout/ThemeToggle', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle" />,
}));

// Mock tabs that have their own API calls / complex state — same boundary as
// CommunityAdminPage.test.tsx, this test only covers the page shell + default tab.
vi.mock('@kymr10n/foundation/src/components/admin/SettingsTab', () => ({
  SettingsTab: () => <div data-testid="settings-tab">Settings</div>,
}));

vi.mock('@kymr10n/foundation/src/components/admin/DiagnosticsTab', () => ({
  DiagnosticsTab: () => <div data-testid="diagnostics-tab">Diagnostics</div>,
}));

vi.mock('@kymr10n/foundation/src/components/admin/AnnouncementsTab', () => ({
  AnnouncementsTab: () => <div data-testid="announcements-tab">Announcements</div>,
}));

vi.mock('@kymr10n/foundation/src/components/admin/AuditLogTab', () => ({
  AuditLogTab: () => <div data-testid="audit-log-tab">Audit Log</div>,
}));

vi.mock('@kymr10n/foundation/src/components/admin/FeedbackTab', () => ({
  FeedbackTab: () => <div data-testid="feedback-tab">Feedback</div>,
}));

vi.mock('@/components/admin/CommunityConfigurationTab', () => ({
  CommunityConfigurationTab: () => <div data-testid="configuration-tab">Configuration</div>,
}));

function renderPage(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <CommunityAdminPage />
    </MemoryRouter>,
  );
}

describe('CommunityAdminPage a11y', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth.appUser = { displayName: 'Alex Admin', email: 'alex@example.com' };
    mockAuth.logout = vi.fn();
  });

  it('has no detectable a11y violations on the default (Configuration) tab', async () => {
    const { container, getByTestId } = renderPage();
    expect(getByTestId('configuration-tab')).toBeInTheDocument();
    expect(await axe(container)).toHaveNoViolations();
  });
});
