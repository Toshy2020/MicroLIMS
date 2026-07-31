using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using System.Security.Cryptography;
using System.Text;

namespace MicroLIMS.Application.Services;

// Login + account locking + login history + JWT refresh + password
// reset (gap analysis "Missing Security").
public class AuthenticationService : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly MicroLimsDbContext _db;
    private readonly Func<string, string, string> _tokenIssuer; // (userId, role) -> JWT

    public AuthenticationService(MicroLimsDbContext db, Func<string, string, string> tokenIssuer)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
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
        await _db.SaveChangesAsync();
        await RecordLoginAsync(user.Id, username, true, null, ipAddress);

        var token = _tokenIssuer(user.Id.ToString(), user.Role?.Type.ToString() ?? "Analyst");
        var refreshToken = await IssueRefreshTokenAsync(user.Id);

        return new LoginOutcome(true, token, refreshToken, null);
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

        var token = _tokenIssuer(stored.UserId.ToString(), stored.User?.Role?.Type.ToString() ?? "Analyst");
        return new LoginOutcome(true, token, newRefreshToken, null);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    // Returns the raw (unhashed) reset token - caller is responsible for
    // delivering it out-of-band (email) via IEmailSender. Never log it.
    public async Task<string> RequestPasswordResetAsync(string username)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username)
            ?? throw new InvalidOperationException("If that account exists, a reset link has been sent."); // don't leak existence

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(PasswordResetTokenLifetime)
        });
        await _db.SaveChangesAsync();
        return rawToken;
    }

    public async Task<bool> ConfirmPasswordResetAsync(string resetToken, string newPassword)
    {
        var hash = Hash(resetToken);
        var record = await _db.PasswordResetTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (record is null || !record.IsValid) return false;

        record.User!.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        record.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
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
