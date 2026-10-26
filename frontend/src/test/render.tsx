import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { CommunityAdminPage } from '@/pages/CommunityAdminPage';

/** Render the admin page inside a router so its `?tab=` state has context. */
export function renderAdminPage(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <CommunityAdminPage />
    </MemoryRouter>,
  );
}

/** Open the account popover in the page header. */
export async function openUserMenu() {
  await userEvent.click(screen.getByRole('button', { name: /open account menu/i }));
}
