namespace MicroLIMS.Application.Interfaces;

// RoleName is the role's display name (Role.Name), as opposed to the base
// type carried in the token's role claim.
public record LoginOutcome(bool Success, string? Token, string? RefreshToken, string? FailureReason, bool MustChangePassword = false, string? RoleName = null);

public record CurrentUserInfo(int UserId, string Username, string FullName, string Role, string? JobTitle, DateTime? LastLoginAt, DateTime? PasswordChangedAt, bool MustChangePassword, string RoleName);

// What an access token is issued for. RoleId and PermissionVersion are signed
// into the token so the API can tell, on every request, whether the user's
// role or its permission grants have changed since it was issued.
public record AccessTokenSubject(string UserId, string Role, int RoleId, int PermissionVersion, IReadOnlyCollection<string> PermissionCodes);

// Wired to IJwtTokenService in the API project so Application does not
// reference Infrastructure directly.
public delegate string AccessTokenIssuer(AccessTokenSubject subject);

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null, string? userAgent = null);
    Task<LoginOutcome> RefreshAsync(string refreshToken, string? ipAddress = null, string? userAgent = null);

    // Ends the caller's session. When refreshToken identifies one of this
    // user's own tokens only that session is revoked; otherwise every
    // session the account has is revoked, so a client that cannot name its
    // session still logs out rather than silently staying logged in.
    Task LogoutAsync(int userId, string? refreshToken);

    // Marks every still-active refresh token for the user revoked WITHOUT
    // calling SaveChangesAsync, so a caller can commit the revocation in
    // the same transaction as the state change that caused it (account
    // disabled, locked, password changed). Returns how many were revoked.
    Task<int> RevokeAllRefreshTokensAsync(int userId);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task RequestPasswordResetAsync(string username);
    Task<bool> ConfirmPasswordResetAsync(string resetToken, string newPassword);
    Task<CurrentUserInfo?> GetCurrentUserAsync(int userId);
}
