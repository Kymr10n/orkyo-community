import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CommunityAdminPage } from './CommunityAdminPage';

// ── Mutable auth state (per-test override) ────────────────────────────────────
const mockAuth = {
  appUser: { displayName: 'Alex Admin', email: 'alex@example.com' } as
    | { displayName: string; email: string }
    | null,
  logout: vi.fn(),
};

const mockNavigate = vi.fn();

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<object>();
  return { ...actual, useNavigate: () => mockNavigate };
});

vi.mock('@kymr10n/foundation/src/contexts/AuthContext', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('@kymr10n/foundation/src/components/layout/ThemeToggle', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle" />,
}));

// Mock tabs that have their own API calls / complex state
vi.mock('@kymr10n/foundation/src/components/admin/SettingsTab', () => ({
  SettingsTab: () => <div data-testid="settings-tab">Settings</div>,
}));

vi.mock('@kymr10n/foundation/src/components/admin/DiagnosticsTab', () => ({
  DiagnosticsTab: () => <div data-testid="diagnostics-tab">Diagnostics</div>,
}));

vi.mock('@kymr10n/foundation/src/components/admin/AnnouncementsTab', () => ({
  AnnouncementsTab: () => <div data-testid="announcements-tab">Announcements</div>,
}));

// Test CommunityConfigurationTab in isolation; mock here to keep test boundaries clean
vi.mock('@/components/admin/CommunityConfigurationTab', () => ({
  CommunityConfigurationTab: () => <div data-testid="configuration-tab">Configuration</div>,
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Opens the user menu popover. The trigger is the only icon-only (no text) button. */
async function openUserMenu() {
  const allButtons = screen.getAllByRole('button');
  const userMenuButton = allButtons.find((btn) => !btn.textContent?.trim());
  if (!userMenuButton) throw new Error('User menu button not found');
  await userEvent.click(userMenuButton);
}

/** Render inside a router so the page's useSearchParams (?tab=) has context. */
function renderPage(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <CommunityAdminPage />
    </MemoryRouter>,
  );
}

// ─────────────────────────────────────────────────────────────────────────────

describe('CommunityAdminPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth.appUser = { displayName: 'Alex Admin', email: 'alex@example.com' };
    mockAuth.logout = vi.fn();
  });

  // ── Header ──────────────────────────────────────────────────────────────────

  it('renders the Orkyo brand in the header', () => {
    renderPage();
    expect(screen.getByText('Orkyo')).toBeInTheDocument();
  });

  it('renders the Administration label', () => {
    renderPage();
    expect(screen.getByText('Administration')).toBeInTheDocument();
  });

  it('navigates to / when Open Application is clicked', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('button', { name: /open application/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  // ── User menu ───────────────────────────────────────────────────────────────

  it('shows user display name in the user menu', async () => {
    renderPage();
    await openUserMenu();
    expect(screen.getByText('Alex Admin')).toBeInTheDocument();
  });

  it('shows user email in the user menu', async () => {
    renderPage();
    await openUserMenu();
    expect(screen.getByText('alex@example.com')).toBeInTheDocument();
  });

  it('falls back to "Admin" when display name is empty', async () => {
    mockAuth.appUser = { displayName: '', email: 'admin@example.com' };
    renderPage();
    await openUserMenu();
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('calls logout when Sign out is clicked', async () => {
    renderPage();
    await openUserMenu();
    await userEvent.click(screen.getByText('Sign out'));
    expect(mockAuth.logout).toHaveBeenCalledOnce();
  });

  // ── Tabs ────────────────────────────────────────────────────────────────────

  it('shows the Configuration tab by default', () => {
    renderPage();
    expect(screen.getByTestId('configuration-tab')).toBeInTheDocument();
  });

  it('switches to Settings tab', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('tab', { name: 'Settings' }));
    expect(screen.getByTestId('settings-tab')).toBeInTheDocument();
  });

  it('switches to Diagnostics tab', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('tab', { name: 'Diagnostics' }));
    expect(screen.getByTestId('diagnostics-tab')).toBeInTheDocument();
  });

  it('switches to Announcements tab', async () => {
    renderPage();
    await userEvent.click(screen.getByRole('tab', { name: 'Announcements' }));
    expect(screen.getByTestId('announcements-tab')).toBeInTheDocument();
  });

  it('selects the tab from the ?tab= query param (deep-link)', () => {
    renderPage('/?tab=diagnostics');
    expect(screen.getByTestId('diagnostics-tab')).toBeInTheDocument();
  });
});
