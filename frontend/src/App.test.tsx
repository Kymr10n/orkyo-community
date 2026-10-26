import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AUTH_STAGES, ROUTE_SITE_ADMIN } from '@kymr10n/foundation/src/constants/auth';
import { mockAuth, resetMockAuth } from '@/test/mocks/foundation-auth';
import App from './App';

const mockLocation = vi.hoisted(() => ({ pathname: '/' }));

// The admin page is code-split; the chunk resolves when a test says so, which is how the
// Suspense fallback gets observed once before the module is cached for the rest of the file.
const adminChunk = vi.hoisted(() => {
  let resolve!: (module: unknown) => void;
  const promise = new Promise<unknown>((r) => {
    resolve = r;
  });
  return { promise, resolve };
});

vi.mock('react-router', async (importOriginal) => {
  const actual = await importOriginal<object>();
  return {
    ...actual,
    // Replace BrowserRouter so tests don't need a real browser history
    BrowserRouter: ({ children }: { children: React.ReactNode }) => <>{children}</>,
    useLocation: () => mockLocation,
    useNavigate: () => vi.fn(),
  };
});

vi.mock('@kymr10n/foundation/src/contexts/AuthContext', () => import('@/test/mocks/foundation-auth'));

vi.mock('@kymr10n/foundation/src/components/auth/ApexGateway', () => ({
  // The pipeline itself is foundation's; the shell's only contract with it is the admin slot.
  ApexGateway: ({ renderAdminPage }: { renderAdminPage?: () => React.ReactNode }) => (
    <div data-testid="apex-gateway" data-admin-slot={renderAdminPage ? 'present' : 'missing'} />
  ),
}));

vi.mock('@kymr10n/foundation/src/components/auth/TenantApp', () => ({
  TenantApp: () => <div data-testid="tenant-app" />,
}));

vi.mock('@/pages/CommunityAdminPage', () => adminChunk.promise);

const ADMIN_PAGE = { CommunityAdminPage: () => <div data-testid="community-admin-page" /> };

function ready(pathname: string, canAccessAdminPage: boolean) {
  mockAuth.authStage = AUTH_STAGES.READY;
  mockAuth.canAccessAdminPage = canAccessAdminPage;
  mockLocation.pathname = pathname;
}

describe('App / CommunityShell routing', () => {
  beforeEach(() => {
    mockLocation.pathname = '/';
    resetMockAuth();
  });

  it('renders the auth pipeline with the admin slot and a theme toggle until auth is ready', () => {
    render(<App />);

    expect(screen.getByTestId('apex-gateway')).toHaveAttribute('data-admin-slot', 'present');
    expect(screen.getByRole('button', { name: /toggle theme/i })).toBeInTheDocument();
    expect(screen.queryByTestId('tenant-app')).not.toBeInTheDocument();
  });

  it('shows the loading spinner while the admin chunk loads, then the admin page', async () => {
    // First render of the lazy page in this file: the chunk is still pending.
    ready(ROUTE_SITE_ADMIN, true);
    render(<App />);

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByTestId('community-admin-page')).not.toBeInTheDocument();

    adminChunk.resolve(ADMIN_PAGE);

    expect(await screen.findByTestId('community-admin-page')).toBeInTheDocument();
    expect(screen.queryByTestId('tenant-app')).not.toBeInTheDocument();
    expect(screen.queryByTestId('apex-gateway')).not.toBeInTheDocument();
  });

  it('renders the admin page on an admin sub-path once ready', async () => {
    ready(`${ROUTE_SITE_ADMIN}/settings`, true);
    render(<App />);

    expect(await screen.findByTestId('community-admin-page')).toBeInTheDocument();
  });

  it.each([
    ['the admin route without access', ROUTE_SITE_ADMIN, false],
    ['a path that only starts with the admin route', `${ROUTE_SITE_ADMIN}istrator`, true],
    ['a non-admin route with access', '/dashboard', true],
    ['the root route', '/', false],
  ])('renders the tenant app for %s once ready', (_, pathname, canAccessAdminPage) => {
    ready(pathname, canAccessAdminPage);
    render(<App />);

    expect(screen.getByTestId('tenant-app')).toBeInTheDocument();
    expect(screen.queryByTestId('community-admin-page')).not.toBeInTheDocument();
    expect(screen.queryByTestId('apex-gateway')).not.toBeInTheDocument();
  });
});
