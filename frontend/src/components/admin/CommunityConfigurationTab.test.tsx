import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CommunityConfigurationTab } from './CommunityConfigurationTab';

describe('CommunityConfigurationTab', () => {
  it('renders the Configuration heading', () => {
    render(<CommunityConfigurationTab />);
    expect(screen.getByText('Configuration')).toBeInTheDocument();
  });

  it('renders the section description', () => {
    render(<CommunityConfigurationTab />);
    expect(
      screen.getByText(/Platform-level configuration for this community instance/i),
    ).toBeInTheDocument();
  });

  it('renders the Tenant Settings card title', () => {
    render(<CommunityConfigurationTab />);
    expect(screen.getByText('Tenant Settings')).toBeInTheDocument();
  });

  it('mentions environment variables in the card body', () => {
    render(<CommunityConfigurationTab />);
    expect(screen.getByText(/environment variables at startup/i)).toBeInTheDocument();
  });

  it('mentions the in-application Settings panel', () => {
    render(<CommunityConfigurationTab />);
    expect(screen.getByText(/in-application Settings panel/i)).toBeInTheDocument();
  });
});
