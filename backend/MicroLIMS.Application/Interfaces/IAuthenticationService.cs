namespace MicroLIMS.Application.Interfaces;

public record LoginOutcome(bool Success, string? Token, string? RefreshToken, string? FailureReason);

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null);
    Task<LoginOutcome> RefreshAsync(string refreshToken);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task<string> RequestPasswordResetAsync(string username);
    Task<bool> ConfirmPasswordResetAsync(string resetToken, string newPassword);
}
