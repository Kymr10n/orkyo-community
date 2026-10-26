/* eslint-disable react-refresh/only-export-components -- a test double is never hot-reloaded */
/**
 * Stubs for the foundation admin tabs that fetch on mount. One `vi.mock` line per tab module
 * in the test file points at this module; the page tests only need to know which tab is shown.
 */
export const tabFailure = { settings: null as Error | null };

export const SettingsTab = () => {
  if (tabFailure.settings) throw tabFailure.settings;
  return <div data-testid="settings-tab">Settings</div>;
};
export const DiagnosticsTab = () => <div data-testid="diagnostics-tab">Diagnostics</div>;
export const AnnouncementsTab = () => <div data-testid="announcements-tab">Announcements</div>;
export const AuditLogTab = () => <div data-testid="audit-log-tab">Audit Log</div>;
export const FeedbackTab = () => <div data-testid="feedback-tab">Feedback</div>;
