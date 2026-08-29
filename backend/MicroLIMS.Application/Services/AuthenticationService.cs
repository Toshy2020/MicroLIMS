using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Validation;
using System.Security.Cryptography;
using System.Text;

namespace MicroLIMS.Application.Services;

// Login + account locking + login history + JWT refresh + password
// reset (gap analysis "Missing Security").
public class AuthenticationService : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private const int PasswordHistoryLimit = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly MicroLimsDbContext _db;
    private readonly Func<string, string, IEnumerable<string>, string> _tokenIssuer; // (userId, role, permissionCodes) -> JWT
    private readonly PermissionService _permissionService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(MicroLimsDbContext db, Func<string, string, IEnumerable<string>, string> tokenIssuer, PermissionService permissionService, IEmailSender emailSender, ILogger<AuthenticationService> logger)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _permissionService = permissionService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            await RecordLoginAsync(null, username, false, "User not found", ipAddress);
            return new LoginOutcome(false, null, null, "Invalid username or password.");
        }

        if (user.IsLocked)
        {
            await RecordLoginAsync(user.Id, username, false, "Account locked", ipAddress);
            return new LoginOutcome(false, null, null, $"Account is locked until {user.LockedUntil:u}.");
        }

        if (!user.IsActive || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockedUntil = DateTime.UtcNow.Add(LockDuration);

            await _db.SaveChangesAsync();
            await RecordLoginAsync(user.Id, username, false, user.IsActive ? "Wrong password" : "Account inactive", ipAddress);
            return new LoginOutcome(false, null, null, "Invalid username or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await RecordLoginAsync(user.Id, username, true, null, ipAddress);

        var permissionCodes = user.Role is not null ? await _permissionService.GetPermissionCodesForRoleAsync(user.Role.Id) : new List<string>();
        var token = _tokenIssuer(user.Id.ToString(), user.Role?.Type.ToString() ?? "Analyst", permissionCodes);
        var refreshToken = await IssueRefreshTokenAsync(user.Id);

        return new LoginOutcome(true, token, refreshToken, null, user.MustChangePassword);
    }

    public async Task<LoginOutcome> RefreshAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.Include(r => r.User).ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored is null || !stored.IsActive)
            return new LoginOutcome(false, null, null, "Refresh token is invalid or expired.");

        // Rotate: revoke the used token and issue a new one.
        stored.RevokedAt = DateTime.UtcNow;
        var newRefreshToken = await IssueRefreshTokenAsync(stored.UserId);
        await _db.SaveChangesAsync();

        var permissionCodes = stored.User?.Role is not null ? await _permissionService.GetPermissionCodesForRoleAsync(stored.User.Role.Id) : new List<string>();
        var token = _tokenIssuer(stored.UserId.ToString(), stored.User?.Role?.Type.ToString() ?? "Analyst", permissionCodes);
        return new LoginOutcome(true, token, newRefreshToken, null, stored.User?.MustChangePassword ?? false);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;

        await ValidateAndApplyNewPasswordAsync(user, newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    // Delivers the raw (unhashed) reset token to the user's email - never
    // returned to the caller and never logged. If the user has no email
    // on file, the token is still created (so a subsequent request
    // doesn't behave differently) but delivery is impossible; that's
    // logged server-side as a warning so an admin can notice and fix it.
    public async Task RequestPasswordResetAsync(string username)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username)
            ?? throw new InvalidOperationException("If that account exists, a reset link has been sent."); // don't leak existence

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.Add(PasswordResetTokenLifetime);
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = expiresAt
        });
        await _db.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Password reset requested for user {Username} (Id {UserId}) but no email is on file - the reset link could not be delivered.", user.Username, user.Id);
            return;
        }

        var body = $"A password reset was requested for your MicroLIMS account.\n\n" +
                   $"Reset token: {rawToken}\n" +
                   $"This token expires at {expiresAt:u}.\n\n" +
                   $"If you did not request this, you can ignore this email.";
        await _emailSender.SendAsync(user.Email, "MicroLIMS Password Reset", body);
    }

    public async Task<bool> ConfirmPasswordResetAsync(string resetToken, string newPassword)
    {
        var hash = Hash(resetToken);
        var record = await _db.PasswordResetTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (record is null || !record.IsValid) return false;

        await ValidateAndApplyNewPasswordAsync(record.User!, newPassword);
        record.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CurrentUserInfo?> GetCurrentUserAsync(int userId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;
        return new CurrentUserInfo(user.Id, user.Username, user.FullName, user.Role?.Type.ToString() ?? "Analyst",
            user.JobTitle, user.LastLoginAt, user.PasswordChangedAt, user.MustChangePassword);
    }

    // Shared by ChangePasswordAsync and ConfirmPasswordResetAsync: enforces
    // the password policy, rejects reuse of the last N hashes, then applies
    // the new hash and records/prunes history. Caller is responsible for
    // SaveChangesAsync so it can be combined with its own changes (e.g.
    // marking a reset token used) in one transaction.
    private async Task ValidateAndApplyNewPasswordAsync(User user, string newPassword)
    {
        var failures = PasswordPolicy.Validate(newPassword);
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        var existingHistory = await _db.PasswordHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        if (existingHistory.Take(PasswordHistoryLimit).Any(h => BCrypt.Net.BCrypt.Verify(newPassword, h.PasswordHash)))
            throw new InvalidOperationException($"New password must not match any of your last {PasswordHistoryLimit} passwords.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordHash = newHash;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;

        _db.PasswordHistories.Add(new PasswordHistory { UserId = user.Id, PasswordHash = newHash });
        if (existingHistory.Count >= PasswordHistoryLimit)
            _db.PasswordHistories.RemoveRange(existingHistory.Skip(PasswordHistoryLimit - 1));
    }

    private async Task<string> IssueRefreshTokenAsync(int userId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime)
        });
        await _db.SaveChangesAsync();
        return rawToken;
    }

    private async Task RecordLoginAsync(int? userId, string username, bool success, string? reason, string? ipAddress)
    {
        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            Username = username,
            Success = success,
            FailureReason = reason,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
