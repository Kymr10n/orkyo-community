using Api.Integrations.Keycloak;
using Microsoft.AspNetCore.Http;

namespace Orkyo.Tests.Mocks;

/// <summary>
/// Mock implementation of IKeycloakAdminService for testing security endpoints
/// without requiring a real Keycloak instance.
///
/// All methods throw <see cref="KeycloakAdminException"/> with the configured
/// error message when their *Success flag is false.
///
/// Byte-identical across orkyo-foundation, orkyo-saas and orkyo-community
/// (G4 synced-files manifest; source: orkyo-foundation). The shared
/// Orkyo.Tests.Mocks namespace exists so the three copies stay identical.
/// </summary>
public class MockKeycloakAdminService : IKeycloakAdminService
{
    // ── Change password ───────────────────────────────────────────
    public bool ChangePasswordSuccess { get; set; } = true;
    public string? ChangePasswordError { get; set; }
    public int ChangePasswordCallCount { get; private set; }
    public (string? keycloakSub, string? currentPassword, string? newPassword) LastChangePasswordCall { get; private set; }

    public Task ChangePasswordAsync(string keycloakSub, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ChangePasswordCallCount++;
        LastChangePasswordCall = (keycloakSub, currentPassword, newPassword);
        if (!ChangePasswordSuccess)
            throw new KeycloakAdminException(ChangePasswordError ?? "Failed to change password", StatusCodes.Status400BadRequest);
        return Task.CompletedTask;
    }

    // ── Sessions ──────────────────────────────────────────────────
    public List<KeycloakSession> MockSessions { get; set; } = new();
    public int GetSessionsCallCount { get; private set; }
    public int RevokeSessionCallCount { get; private set; }
    public int LogoutAllCallCount { get; private set; }
    public string? LastRevokedSessionId { get; private set; }

    public Task<List<KeycloakSession>> GetUserSessionsAsync(string keycloakSub, CancellationToken ct = default)
    {
        GetSessionsCallCount++;
        return Task.FromResult(MockSessions);
    }

    public Task RevokeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        RevokeSessionCallCount++;
        LastRevokedSessionId = sessionId;
        MockSessions.RemoveAll(s => s.Id == sessionId);
        return Task.CompletedTask;
    }

    public Task LogoutAllSessionsAsync(string keycloakSub, CancellationToken ct = default)
    {
        LogoutAllCallCount++;
        MockSessions.Clear();
        return Task.CompletedTask;
    }

    // ── Federation ────────────────────────────────────────────────
    public bool IsFederatedUser { get; set; } = false;
    public string? FederatedIdentityProvider { get; set; }

    public Task<FederationStatus> GetUserFederationStatusAsync(string keycloakSub, CancellationToken ct = default) =>
        Task.FromResult(new FederationStatus(IsFederatedUser, FederatedIdentityProvider));

    // ── Create user ───────────────────────────────────────────────
    public bool CreateUserSuccess { get; set; } = true;
    public string? CreateUserError { get; set; }
    public int CreateUserCallCount { get; private set; }
    public (string? email, string? password, string? firstName, string? lastName, bool emailVerified) LastCreateUserCall { get; private set; }

    public Task CreateUserAsync(string email, string password, string? firstName = null, string? lastName = null, bool emailVerified = false, CancellationToken ct = default)
    {
        CreateUserCallCount++;
        LastCreateUserCall = (email, password, firstName, lastName, emailVerified);
        if (!CreateUserSuccess)
        {
            var msg = CreateUserError ?? "Failed to create user account";
            var status = msg.Contains("already", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            throw new KeycloakAdminException(msg, status);
        }
        return Task.CompletedTask;
    }

    // ── User exists ───────────────────────────────────────────────
    public bool UserExistsResult { get; set; } = false;
    public int UserExistsCallCount { get; private set; }

    public Task<bool> UserExistsAsync(string email, CancellationToken ct = default)
    {
        UserExistsCallCount++;
        return Task.FromResult(UserExistsResult);
    }

    // ── Disable user ──────────────────────────────────────────────
    public bool DisableUserSuccess { get; set; } = true;
    public string? DisableUserError { get; set; }
    public int DisableUserCallCount { get; private set; }
    public string? LastDisabledKeycloakId { get; private set; }

    public Task DisableUserAsync(string keycloakId, CancellationToken ct = default)
    {
        DisableUserCallCount++;
        LastDisabledKeycloakId = keycloakId;
        if (!DisableUserSuccess)
            throw new KeycloakAdminException(DisableUserError ?? "Failed to disable user");
        return Task.CompletedTask;
    }

    // ── Enable user ───────────────────────────────────────────────
    public bool EnableUserSuccess { get; set; } = true;
    public string? EnableUserError { get; set; }
    public int EnableUserCallCount { get; private set; }
    public string? LastEnabledKeycloakId { get; private set; }

    public Task EnableUserAsync(string keycloakId, CancellationToken ct = default)
    {
        EnableUserCallCount++;
        LastEnabledKeycloakId = keycloakId;
        if (!EnableUserSuccess)
            throw new KeycloakAdminException(EnableUserError ?? "Failed to enable user");
        return Task.CompletedTask;
    }

    // ── Delete user ───────────────────────────────────────────────
    public bool DeleteUserSuccess { get; set; } = true;
    public string? DeleteUserError { get; set; }
    public int DeleteUserCallCount { get; private set; }
    public string? LastDeletedKeycloakId { get; private set; }

    public Task DeleteUserAsync(string keycloakId, CancellationToken ct = default)
    {
        DeleteUserCallCount++;
        LastDeletedKeycloakId = keycloakId;
        if (!DeleteUserSuccess)
            throw new KeycloakAdminException(DeleteUserError ?? "Failed to delete user");
        return Task.CompletedTask;
    }

    // ── MFA status ────────────────────────────────────────────────
    public MfaStatus MockMfaStatus { get; set; } = new() { TotpEnabled = false, RecoveryCodesConfigured = false };
    public string? GetMfaStatusError { get; set; }
    public int GetMfaStatusCallCount { get; private set; }

    public Task<MfaStatus> GetMfaStatusAsync(string keycloakSub, CancellationToken ct = default)
    {
        GetMfaStatusCallCount++;
        if (GetMfaStatusError != null)
            throw new KeycloakAdminException(GetMfaStatusError);
        return Task.FromResult(MockMfaStatus);
    }

    // ── Delete credential ─────────────────────────────────────────
    public bool DeleteCredentialSuccess { get; set; } = true;
    public string? DeleteCredentialError { get; set; }
    public int DeleteCredentialCallCount { get; private set; }
    public string? LastDeletedCredentialId { get; private set; }

    public Task DeleteUserCredentialAsync(string keycloakSub, string credentialId, CancellationToken ct = default)
    {
        DeleteCredentialCallCount++;
        LastDeletedCredentialId = credentialId;
        if (!DeleteCredentialSuccess)
            throw new KeycloakAdminException(DeleteCredentialError ?? "Failed to remove credential");
        return Task.CompletedTask;
    }

    // ── User profile ──────────────────────────────────────────────
    public UserProfile MockUserProfile { get; set; } = new() { Email = "test@example.com", FirstName = "Test", LastName = "User", EmailVerified = true };
    public string? GetUserProfileError { get; set; }
    public int GetUserProfileCallCount { get; private set; }

    public Task<UserProfile> GetUserProfileAsync(string keycloakSub, CancellationToken ct = default)
    {
        GetUserProfileCallCount++;
        if (GetUserProfileError != null)
            throw new KeycloakAdminException(GetUserProfileError);
        return Task.FromResult(MockUserProfile);
    }

    // ── Update profile ────────────────────────────────────────────
    public bool UpdateProfileSuccess { get; set; } = true;
    public string? UpdateProfileError { get; set; }
    public int UpdateProfileCallCount { get; private set; }
    public (string? firstName, string? lastName) LastUpdateProfileCall { get; private set; }

    public Task UpdateUserProfileAsync(string keycloakSub, string firstName, string lastName, CancellationToken ct = default)
    {
        UpdateProfileCallCount++;
        LastUpdateProfileCall = (firstName, lastName);
        if (!UpdateProfileSuccess)
            throw new KeycloakAdminException(UpdateProfileError ?? "Failed to update profile");
        return Task.CompletedTask;
    }

    // ── Update email ──────────────────────────────────────────────
    public bool UpdateEmailSuccess { get; set; } = true;
    public string? UpdateEmailError { get; set; }
    public int UpdateEmailCallCount { get; private set; }
    public (string? keycloakSub, string? newEmail) LastUpdateEmailCall { get; private set; }
    public int UpdateEmailForAccountCallCount { get; private set; }
    public (string? keycloakSub, string? currentEmail, string? newEmail) LastUpdateEmailForAccountCall { get; private set; }

    public Task UpdateEmailAsync(string keycloakSub, string newEmail, CancellationToken ct = default)
    {
        UpdateEmailCallCount++;
        LastUpdateEmailCall = (keycloakSub, newEmail);
        if (!UpdateEmailSuccess)
            throw new KeycloakAdminException(UpdateEmailError ?? "Failed to update email");
        return Task.CompletedTask;
    }

    public Task UpdateEmailForAccountAsync(string? keycloakSub, string currentEmail, string newEmail, CancellationToken ct = default)
    {
        UpdateEmailForAccountCallCount++;
        LastUpdateEmailForAccountCall = (keycloakSub, currentEmail, newEmail);
        if (!UpdateEmailSuccess)
            throw new KeycloakAdminException(UpdateEmailError ?? "Failed to update email");
        return Task.CompletedTask;
    }

    // ── Enable MFA ────────────────────────────────────────────────
    public bool EnableMfaSuccess { get; set; } = true;
    public string? EnableMfaError { get; set; }
    public int EnableMfaCallCount { get; private set; }

    public Task EnableMfaAsync(string keycloakSub, CancellationToken ct = default)
    {
        EnableMfaCallCount++;
        if (!EnableMfaSuccess)
            throw new KeycloakAdminException(EnableMfaError ?? "Failed to enable MFA");
        return Task.CompletedTask;
    }

    // ── Realm roles ───────────────────────────────────────────────
    public HashSet<string> MockRealmRoles { get; set; } = new();
    public bool HasRealmRoleError_ { get; set; } = false;
    public int? HasRealmRoleErrorStatusCode { get; set; }
    public bool AssignRealmRoleSuccess { get; set; } = true;
    public string? AssignRealmRoleError { get; set; }
    public bool RevokeRealmRoleSuccess { get; set; } = true;
    public string? RevokeRealmRoleError { get; set; }
    public int HasRealmRoleCallCount { get; private set; }
    public int AssignRealmRoleCallCount { get; private set; }
    public int RevokeRealmRoleCallCount { get; private set; }
    public (string? keycloakId, string? roleName) LastAssignRealmRoleCall { get; private set; }
    public (string? keycloakId, string? roleName) LastRevokeRealmRoleCall { get; private set; }

    public Task<bool> HasRealmRoleAsync(string keycloakId, string roleName, CancellationToken ct = default)
    {
        HasRealmRoleCallCount++;
        if (HasRealmRoleError_)
            throw new KeycloakAdminException("Failed to check realm roles", HasRealmRoleErrorStatusCode ?? StatusCodes.Status502BadGateway);
        return Task.FromResult(MockRealmRoles.Contains(roleName));
    }

    public Task AssignRealmRoleAsync(string keycloakId, string roleName, CancellationToken ct = default)
    {
        AssignRealmRoleCallCount++;
        LastAssignRealmRoleCall = (keycloakId, roleName);
        if (!AssignRealmRoleSuccess)
            throw new KeycloakAdminException(AssignRealmRoleError ?? "Failed to assign role");
        MockRealmRoles.Add(roleName);
        return Task.CompletedTask;
    }

    public Task RevokeRealmRoleAsync(string keycloakId, string roleName, CancellationToken ct = default)
    {
        RevokeRealmRoleCallCount++;
        LastRevokeRealmRoleCall = (keycloakId, roleName);
        if (!RevokeRealmRoleSuccess)
            throw new KeycloakAdminException(RevokeRealmRoleError ?? "Failed to revoke role");
        MockRealmRoles.Remove(roleName);
        return Task.CompletedTask;
    }

    public int CountRealmRoleMembersResult { get; set; } = 2;
    public string? CountRealmRoleMembersError { get; set; }

    public Task<int> CountRealmRoleMembersAsync(string roleName, CancellationToken ct = default)
    {
        if (CountRealmRoleMembersError != null)
            throw new KeycloakAdminException(CountRealmRoleMembersError);
        return Task.FromResult(CountRealmRoleMembersResult);
    }

    public void Reset()
    {
        ChangePasswordSuccess = true;
        ChangePasswordError = null;
        IsFederatedUser = false;
        FederatedIdentityProvider = null;
        MockSessions = new List<KeycloakSession>();
        ChangePasswordCallCount = 0;
        GetSessionsCallCount = 0;
        RevokeSessionCallCount = 0;
        LogoutAllCallCount = 0;
        LastRevokedSessionId = null;
        LastChangePasswordCall = default;

        CreateUserSuccess = true;
        CreateUserError = null;
        CreateUserCallCount = 0;
        LastCreateUserCall = default;

        UserExistsResult = false;
        UserExistsCallCount = 0;

        DisableUserSuccess = true;
        DisableUserError = null;
        DisableUserCallCount = 0;
        LastDisabledKeycloakId = null;

        EnableUserSuccess = true;
        EnableUserError = null;
        EnableUserCallCount = 0;
        LastEnabledKeycloakId = null;

        DeleteUserSuccess = true;
        DeleteUserError = null;
        DeleteUserCallCount = 0;
        LastDeletedKeycloakId = null;

        MockMfaStatus = new MfaStatus { TotpEnabled = false, RecoveryCodesConfigured = false };
        GetMfaStatusError = null;
        GetMfaStatusCallCount = 0;
        DeleteCredentialSuccess = true;
        DeleteCredentialError = null;
        DeleteCredentialCallCount = 0;
        LastDeletedCredentialId = null;

        MockUserProfile = new UserProfile { Email = "test@example.com", FirstName = "Test", LastName = "User", EmailVerified = true };
        GetUserProfileError = null;
        GetUserProfileCallCount = 0;
        UpdateProfileSuccess = true;
        UpdateProfileError = null;
        UpdateProfileCallCount = 0;
        LastUpdateProfileCall = default;

        UpdateEmailSuccess = true;
        UpdateEmailError = null;
        UpdateEmailCallCount = 0;
        LastUpdateEmailCall = default;
        UpdateEmailForAccountCallCount = 0;
        LastUpdateEmailForAccountCall = default;

        EnableMfaSuccess = true;
        EnableMfaError = null;
        EnableMfaCallCount = 0;

        MockRealmRoles = new HashSet<string>();
        HasRealmRoleError_ = false;
        HasRealmRoleErrorStatusCode = null;
        AssignRealmRoleSuccess = true;
        AssignRealmRoleError = null;
        RevokeRealmRoleSuccess = true;
        RevokeRealmRoleError = null;
        HasRealmRoleCallCount = 0;
        AssignRealmRoleCallCount = 0;
        RevokeRealmRoleCallCount = 0;
        LastAssignRealmRoleCall = default;
        LastRevokeRealmRoleCall = default;
        CountRealmRoleMembersResult = 2;
        CountRealmRoleMembersError = null;
    }

    public static KeycloakSession CreateMockSession(string id, string ipAddress = "192.168.1.1")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new KeycloakSession
        {
            Id = id,
            Username = "testuser",
            IpAddress = ipAddress,
            Start = now - 3600000,
            LastAccess = now,
            Clients = new Dictionary<string, string> { ["orkyo-frontend"] = "Orkyo" }
        };
    }
}
