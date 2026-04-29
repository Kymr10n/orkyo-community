import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import App from './App';

// ── Mutable state (per-test override) ─────────────────────────────────────────
const mockAuthState = {
  authStage: 'initializing' as string,
  canAccessAdminPage: false,
};

const mockLocation = { pathname: '/' };

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<object>();
  return {
    ...actual,
    // Replace BrowserRouter so tests don't need a real browser history
    BrowserRouter: ({ children }: { children: React.ReactNode }) => <>{children}</>,
    useLocation: () => mockLocation,
    useNavigate: () => vi.fn(),
  };
});

vi.mock('@foundation/src/contexts/AuthContext', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAuth: () => mockAuthState,
}));

vi.mock('@foundation/src/components/auth/ApexGateway', () => ({
  ApexGateway: () => <div data-testid="apex-gateway" />,
}));

vi.mock('@foundation/src/components/auth/TenantApp', () => ({
  TenantApp: () => <div data-testid="tenant-app" />,
}));

vi.mock('@foundation/src/components/layout/ThemeToggle', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle" />,
}));

vi.mock('@/pages/CommunityAdminPage', () => ({
  CommunityAdminPage: () => <div data-testid="community-admin-page" />,
}));

// ─────────────────────────────────────────────────────────────────────────────

describe('App / CommunityShell routing', () => {
  beforeEach(() => {
    mockLocation.pathname = '/';
    mockAuthState.authStage = 'initializing';
    mockAuthState.canAccessAdminPage = false;
  });

  // ── Auth pipeline not ready ──────────────────────────────────────────────────

  it('shows ApexGateway when auth is not ready', () => {
    render(<App />);
    expect(screen.getByTestId('apex-gateway')).toBeInTheDocument();
  });

  it('shows ThemeToggle alongside ApexGateway when not ready', () => {
    render(<App />);
    expect(screen.getByTestId('theme-toggle')).toBeInTheDocument();
  });

  it('does not show TenantApp when auth is not ready', () => {
    render(<App />);
    expect(screen.queryByTestId('tenant-app')).not.toBeInTheDocument();
  });

  // ── Auth pipeline ready ───────────────────────────────────────────────────────

  it('shows TenantApp when auth stage is ready', () => {
    mockAuthState.authStage = 'ready';
    render(<App />);
    expect(screen.getByTestId('tenant-app')).toBeInTheDocument();
  });

  it('does not show ApexGateway when auth stage is ready', () => {
    mockAuthState.authStage = 'ready';
    render(<App />);
    expect(screen.queryByTestId('apex-gateway')).not.toBeInTheDocument();
  });

  // ── Admin route ───────────────────────────────────────────────────────────────

  it('shows CommunityAdminPage on /admin when canAccessAdminPage is true', () => {
    mockLocation.pathname = '/admin';
    mockAuthState.canAccessAdminPage = true;
    render(<App />);
    expect(screen.getByTestId('community-admin-page')).toBeInTheDocument();
  });

  it('shows CommunityAdminPage on /admin/* when canAccessAdminPage is true', () => {
    mockLocation.pathname = '/admin/settings';
    mockAuthState.canAccessAdminPage = true;
    render(<App />);
    expect(screen.getByTestId('community-admin-page')).toBeInTheDocument();
  });

  it('does not show CommunityAdminPage on /admin when canAccessAdminPage is false', () => {
    mockLocation.pathname = '/admin';
    mockAuthState.canAccessAdminPage = false;
    render(<App />);
    expect(screen.queryByTestId('community-admin-page')).not.toBeInTheDocument();
  });

  it('does not show CommunityAdminPage on non-admin routes even with access', () => {
    mockLocation.pathname = '/dashboard';
    mockAuthState.canAccessAdminPage = true;
    mockAuthState.authStage = 'ready';
    render(<App />);
    expect(screen.queryByTestId('community-admin-page')).not.toBeInTheDocument();
    expect(screen.getByTestId('tenant-app')).toBeInTheDocument();
  });

  it('shows CommunityAdminPage regardless of auth stage when on /admin with access', () => {
    mockLocation.pathname = '/admin';
    mockAuthState.canAccessAdminPage = true;
    mockAuthState.authStage = 'ready'; // even when ready, admin takes priority
    render(<App />);
    expect(screen.getByTestId('community-admin-page')).toBeInTheDocument();
    expect(screen.queryByTestId('tenant-app')).not.toBeInTheDocument();
  });
});
