import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CommunityConfigurationTab } from './CommunityConfigurationTab';

describe('CommunityConfigurationTab', () => {
  it('points the site admin at environment variables and the in-app Settings panel', () => {
    // A static card with no branches: one render, the copy an operator relies on.
    render(<CommunityConfigurationTab />);

    expect(screen.getByRole('heading', { name: 'Configuration' })).toBeInTheDocument();
    expect(screen.getByText(/Platform-level configuration for this community instance/i)).toBeInTheDocument();
    expect(screen.getByText('Tenant Settings')).toBeInTheDocument();
    expect(screen.getByText(/environment variables at startup/i)).toBeInTheDocument();
    expect(screen.getByText(/in-application Settings panel/i)).toBeInTheDocument();
  });
});
