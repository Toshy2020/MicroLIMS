using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
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
    private readonly ISecurityAuditService _securityAudit;

    public AuthenticationService(MicroLimsDbContext db, Func<string, string, IEnumerable<string>, string> tokenIssuer, PermissionService permissionService, IEmailSender emailSender, ILogger<AuthenticationService> logger, ISecurityAuditService securityAudit)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _permissionService = permissionService;
        _emailSender = emailSender;
        _logger = logger;
        _securityAudit = securityAudit;
    }

    public async Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LoginFailedUnknownUser, SecurityEventOutcome.Failure,
                TargetUsername: username));
            await RecordLoginAsync(null, username, false, "User not found", ipAddress);
            return new LoginOutcome(false, null, null, "Invalid username or password.");
        }

        if (user.IsLocked)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LoginFailedAccountLocked, SecurityEventOutcome.Failure,
                TargetUserId: user.Id, TargetUsername: username));
            await RecordLoginAsync(user.Id, username, false, "Account locked", ipAddress);
            return new LoginOutcome(false, null, null, $"Account is locked until {user.LockedUntil:u}.");
        }

        if (!user.IsActive || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            _securityAudit.Record(new SecurityEventRequest(
                user.IsActive ? SecurityEventCodes.LoginFailedBadCredentials
                              : SecurityEventCodes.LoginFailedAccountDisabled,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id, TargetUsername: username));

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockDuration);
                // Locking the account has to end the sessions it already
                // has, or the lockout only blocks the login form while an
                // existing refresh token keeps minting new access tokens.
                var revoked = await RevokeAllRefreshTokensAsync(user.Id);

                // The system locked the account, not a person - so there is
                // no actor user, and ActorIsSystem says why it is absent.
                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.AccountLocked, SecurityEventOutcome.Success,
                    ActorIsSystem: true, TargetUserId: user.Id, TargetUsername: username,
                    Reason: $"Automatic lockout after {MaxFailedAttempts} failed login attempts.",
                    Metadata: $"{{\"revokedSessionCount\":{revoked}}}"));
            }

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

        // Self-service: the actor and the target are the same person.
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.LoginSuccess, SecurityEventOutcome.Success,
            ActorUserId: user.Id, TargetUserId: user.Id, TargetUsername: user.Username));
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.RefreshTokenIssued, SecurityEventOutcome.Success,
            ActorUserId: user.Id, TargetUserId: user.Id, TargetUsername: user.Username));

        var refreshToken = await IssueRefreshTokenAsync(user.Id);

        return new LoginOutcome(true, token, refreshToken, null, user.MustChangePassword);
    }

    public async Task<LoginOutcome> RefreshAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.Include(r => r.User).ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored is null || !stored.IsActive)
        {
            // The token value itself is never recorded - only that a
            // refresh was attempted with one that is not usable.
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.RefreshRejectedInvalidToken, SecurityEventOutcome.Failure,
                TargetUserId: stored?.UserId,
                Reason: stored is null ? "Unrecognised refresh token." : "Refresh token revoked or expired."));
            await _db.SaveChangesAsync();
            return new LoginOutcome(false, null, null, "Refresh token is invalid or expired.");
        }

        // A valid token row is not enough: the account behind it must still
        // be allowed to work. Without this, disabling or locking a user
        // blocked only the login form while their existing refresh token
        // went on minting fresh access tokens indefinitely. The failure
        // message deliberately matches the invalid-token one above so a
        // caller holding a stolen token cannot probe account state.
        var user = stored.User;
        if (user is null || !user.IsActive || user.IsLocked)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.RefreshRejectedAccountBlocked, SecurityEventOutcome.Failure,
                TargetUserId: stored.UserId, TargetUsername: user?.Username,
                Reason: user is null ? "Account no longer exists."
                    : !user.IsActive ? "Account is disabled." : "Account is locked."));
            await _db.SaveChangesAsync();
            return new LoginOutcome(false, null, null, "Refresh token is invalid or expired.");
        }

        // Rotate: revoke the used token and issue a new one. One event,
        // not three - the caller performed a single meaningful action.
        stored.RevokedAt = DateTime.UtcNow;
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.RefreshTokenRotated, SecurityEventOutcome.Success,
            ActorUserId: user.Id, TargetUserId: user.Id, TargetUsername: user.Username));

        var newRefreshToken = await IssueRefreshTokenAsync(stored.UserId);
        await _db.SaveChangesAsync();

        var permissionCodes = stored.User?.Role is not null ? await _permissionService.GetPermissionCodesForRoleAsync(stored.User.Role.Id) : new List<string>();
        var token = _tokenIssuer(stored.UserId.ToString(), stored.User?.Role?.Type.ToString() ?? "Analyst", permissionCodes);
        return new LoginOutcome(true, token, newRefreshToken, null, stored.User?.MustChangePassword ?? false);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.PasswordChangeFailed, SecurityEventOutcome.Failure,
                ActorUserId: userId, TargetUserId: user?.Id, TargetUsername: user?.Username,
                Reason: "Current password verification failed."));
            await _db.SaveChangesAsync();
            return false;
        }

        await ValidateAndApplyNewPasswordAsync(user, newPassword);

        // Self-service change: actor and target are the same person.
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.PasswordChanged, SecurityEventOutcome.Success,
            ActorUserId: userId, TargetUserId: user.Id, TargetUsername: user.Username));

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

        // Reset is performed by whoever held the reset token, so there is
        // no authenticated actor - only the account it was applied to.
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.PasswordReset, SecurityEventOutcome.Success,
            TargetUserId: record.User!.Id, TargetUsername: record.User.Username,
            Reason: "Password reset token redeemed."));

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

        // Changing or resetting the password ends every existing session:
        // whoever knew the old password must not keep a refresh token that
        // still mints access tokens. Both callers save, so this commits in
        // the same transaction as the new hash.
        var revoked = await RevokeAllRefreshTokensAsync(user.Id);
        if (revoked > 0)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.SessionRevoked, SecurityEventOutcome.Success,
                TargetUserId: user.Id, TargetUsername: user.Username,
                Reason: "All sessions ended because the password changed.",
                Metadata: $"{{\"revokedSessionCount\":{revoked}}}"));
        }
    }

    // Revokes the one session the caller names, or all of them when it
    // cannot be identified. See IAuthenticationService for why.
    public async Task LogoutAsync(int userId, string? refreshToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = Hash(refreshToken);
            // Scoped to the caller's own rows - a supplied token belonging
            // to somebody else must never revoke their session.
            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.TokenHash == hash && r.UserId == userId);

            if (stored is not null)
            {
                // Idempotent: logging out twice, or after the token already
                // expired, leaves the original revocation timestamp alone.
                stored.RevokedAt ??= DateTime.UtcNow;
                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.Logout, SecurityEventOutcome.Success,
                    ActorUserId: userId, TargetUserId: userId,
                    Reason: "Single session ended."));
                await _db.SaveChangesAsync();
                return;
            }
        }

        // No usable session identifier: fail safe by ending every session
        // this account has rather than none of them.
        var revokedCount = await RevokeAllRefreshTokensAsync(userId);
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.Logout, SecurityEventOutcome.Success,
            ActorUserId: userId, TargetUserId: userId,
            Reason: "All sessions ended - the client did not identify one.",
            Metadata: $"{{\"revokedSessionCount\":{revokedCount}}}"));
        await _db.SaveChangesAsync();
    }

    // Deliberately does not save - see IAuthenticationService. Mirrors the
    // revocation AdminPasswordRecoveryService already performs.
    public async Task<int> RevokeAllRefreshTokensAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var active = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > now)
            .ToListAsync();

        foreach (var token in active)
            token.RevokedAt = now;

        return active.Count;
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
