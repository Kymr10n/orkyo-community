/* eslint-disable react-refresh/only-export-components -- a test double is never hot-reloaded */
import type { ReactNode } from 'react';
import { vi } from 'vitest';

/**
 * The AuthContext double every page and shell test shares. Foundation's own `mockAuth()`
 * test-util is not in the published package, so the double lives here; mock the context with
 * `vi.mock('@kymr10n/foundation/src/contexts/AuthContext', () => import('@/test/mocks/foundation-auth'))`
 * and drive it through `mockAuth` (reset in `beforeEach` with `resetMockAuth`).
 */
export const mockAuth = {
  authStage: 'initializing' as string,
  canAccessAdminPage: false,
  appUser: { displayName: 'Alex Admin', email: 'alex@example.com' } as
    | { displayName: string; email: string }
    | null,
  logout: vi.fn(),
};

export function resetMockAuth() {
  mockAuth.authStage = 'initializing';
  mockAuth.canAccessAdminPage = false;
  mockAuth.appUser = { displayName: 'Alex Admin', email: 'alex@example.com' };
  mockAuth.logout = vi.fn();
}

export const useAuth = () => mockAuth;

export const AuthProvider = ({ children }: { children: ReactNode }) => <>{children}</>;
