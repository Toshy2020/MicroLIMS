namespace MicroLIMS.Application.Interfaces;

public record LoginOutcome(bool Success, string? Token, string? RefreshToken, string? FailureReason, bool MustChangePassword = false);

public record CurrentUserInfo(int UserId, string Username, string FullName, string Role, string? JobTitle, DateTime? LastLoginAt, DateTime? PasswordChangedAt, bool MustChangePassword);

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null);
    Task<LoginOutcome> RefreshAsync(string refreshToken);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task RequestPasswordResetAsync(string username);
    Task<bool> ConfirmPasswordResetAsync(string resetToken, string newPassword);
    Task<CurrentUserInfo?> GetCurrentUserAsync(int userId);
}
