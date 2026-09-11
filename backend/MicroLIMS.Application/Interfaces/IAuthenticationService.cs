namespace MicroLIMS.Application.Interfaces;

public record LoginOutcome(bool Success, string? Token, string? RefreshToken, string? FailureReason, bool MustChangePassword = false);

public record CurrentUserInfo(int UserId, string Username, string FullName, string Role, string? JobTitle, DateTime? LastLoginAt, DateTime? PasswordChangedAt, bool MustChangePassword);

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null);
    Task<LoginOutcome> RefreshAsync(string refreshToken);

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
