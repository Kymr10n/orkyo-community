import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { mockAuth, resetMockAuth } from '@/test/mocks/foundation-auth';
import { tabFailure } from '@/test/mocks/admin-tabs';
import { renderAdminPage, openUserMenu } from '@/test/render';

const mockNavigate = vi.hoisted(() => vi.fn());

vi.mock('react-router', async (importOriginal) => {
  const actual = await importOriginal<object>();
  return { ...actual, useNavigate: () => mockNavigate };
});

vi.mock('@kymr10n/foundation/src/contexts/AuthContext', () => import('@/test/mocks/foundation-auth'));

// The foundation tabs fetch on mount; the stubs only say which tab is shown. The
// Configuration tab is Community's own static card and renders for real.
vi.mock('@kymr10n/foundation/src/components/admin/SettingsTab', () => import('@/test/mocks/admin-tabs'));
vi.mock('@kymr10n/foundation/src/components/admin/DiagnosticsTab', () => import('@/test/mocks/admin-tabs'));
vi.mock('@kymr10n/foundation/src/components/admin/AnnouncementsTab', () => import('@/test/mocks/admin-tabs'));
vi.mock('@kymr10n/foundation/src/components/admin/AuditLogTab', () => import('@/test/mocks/admin-tabs'));
vi.mock('@kymr10n/foundation/src/components/admin/FeedbackTab', () => import('@/test/mocks/admin-tabs'));

describe('CommunityAdminPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetMockAuth();
  });

  afterEach(() => {
    tabFailure.settings = null;
  });

  it('renders the shell for the signed-in admin with the Configuration tab open', async () => {
    renderAdminPage();

    expect(screen.getByText('Orkyo')).toBeInTheDocument();
    expect(screen.getByText('Administration')).toBeInTheDocument();
    expect(screen.getByRole('tabpanel')).toContainElement(
      screen.getByRole('heading', { name: 'Configuration' }),
    );

    await openUserMenu();
    expect(screen.getByText('Alex Admin')).toBeInTheDocument();
    expect(screen.getByText('alex@example.com')).toBeInTheDocument();
  });

  it.each([
    ['Settings', 'settings-tab'],
    ['Diagnostics', 'diagnostics-tab'],
    ['Announcements', 'announcements-tab'],
    ['Audit Log', 'audit-log-tab'],
    ['Feedback', 'feedback-tab'],
  ])('switches to the %s tab', async (name, testId) => {
    renderAdminPage();

    await userEvent.click(screen.getByRole('tab', { name }));

    expect(screen.getByTestId(testId)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Configuration' })).not.toBeInTheDocument();
  });

  it('selects the tab from the ?tab= query param (deep-link)', () => {
    renderAdminPage('/?tab=diagnostics');

    expect(screen.getByTestId('diagnostics-tab')).toBeInTheDocument();
  });

  it('shows no tab body for an unknown ?tab= value', () => {
    // useTabParam takes the URL as the source of truth and does not validate it; a stale or
    // mistyped deep link leaves the tab strip usable and the body empty.
    renderAdminPage('/?tab=bogus');

    expect(screen.getAllByRole('tab')).toHaveLength(6);
    expect(screen.queryByRole('tabpanel')).not.toBeInTheDocument();
  });

  it('navigates to / when Open Application is clicked', async () => {
    renderAdminPage();

    await userEvent.click(screen.getByRole('button', { name: /open application/i }));

    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it.each([
    ['no app user', null],
    ['an empty display name', { displayName: '', email: 'admin@example.com' }],
  ])('falls back to "Admin" in the user menu with %s', async (_, appUser) => {
    mockAuth.appUser = appUser;
    renderAdminPage();

    await openUserMenu();

    expect(screen.getByText('Admin')).toBeInTheDocument();
    if (appUser === null) expect(screen.queryByText(/@/)).not.toBeInTheDocument();
  });

  it('calls logout when Sign out is clicked', async () => {
    renderAdminPage();
    await openUserMenu();

    await userEvent.click(screen.getByText('Sign out'));

    expect(mockAuth.logout).toHaveBeenCalledOnce();
  });

  it('navigates to /account when Manage Account is clicked', async () => {
    renderAdminPage();
    await openUserMenu();

    await userEvent.click(screen.getByText('Manage Account'));

    expect(mockNavigate).toHaveBeenCalledWith('/account');
  });

  it('contains a tab that throws to its own error boundary and keeps the header', async () => {
    tabFailure.settings = new Error('settings exploded');
    // React reports the caught render error on console.error; the boundary handles it.
    vi.spyOn(console, 'error').mockImplementation(() => {});
    renderAdminPage();

    await userEvent.click(screen.getByRole('tab', { name: 'Settings' }));

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByText(/The Settings view\s+failed to render/)).toBeInTheDocument();
    expect(screen.getByText('settings exploded')).toBeInTheDocument();
    expect(screen.getByText('Orkyo')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Settings' })).toBeInTheDocument();
  });

  it('has no detectable a11y violations with the default tab, and with the user menu open', async () => {
    const { container } = renderAdminPage();
    expect(await axe(container)).toHaveNoViolations();

    await openUserMenu();
    expect(await axe(container)).toHaveNoViolations();
  });
});
