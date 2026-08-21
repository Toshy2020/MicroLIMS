using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Validation;

namespace MicroLIMS.Application.Services;

public record CreateRecoveryResultDto(string RecoveryCode, DateTime ExpiresAt);

public class AdminPasswordRecoveryService
{
    private const string AllowedChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32 unambiguous chars
    private readonly MicroLimsDbContext _db;

    public AdminPasswordRecoveryService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public static string GenerateRecoveryCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        var sb = new StringBuilder(14);
        for (int i = 0; i < 12; i++)
        {
            if (i > 0 && i % 4 == 0)
                sb.Append('-');
            sb.Append(AllowedChars[bytes[i] % AllowedChars.Length]);
        }
        return sb.ToString(); // Format: XXXX-XXXX-XXXX
    }

    public static string HashRecoveryCode(string code)
    {
        var normalized = code.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public async Task<CreateRecoveryResultDto> CreateRecoveryRequestAsync(int targetUserId, string reason, int actingUserId)
    {
        if (actingUserId == targetUserId)
            throw new InvalidOperationException("System Administrators cannot initiate admin-assisted password recovery for their own account.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required for administrator-assisted password recovery.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        if (!user.IsActive)
            throw new InvalidOperationException("Cannot initiate password recovery for a disabled user account. Please enable the account first.");

        // Invalidate any existing active recovery codes for this user
        var activeRequests = await _db.AdminPasswordRecoveries
            .Where(r => r.UserId == targetUserId && r.Status == AdminPasswordRecoveryStatus.Pending)
            .ToListAsync();
        foreach (var r in activeRequests)
        {
            r.Status = AdminPasswordRecoveryStatus.Expired;
        }

        var plaintextCode = GenerateRecoveryCode();
        var codeHash = HashRecoveryCode(plaintextCode);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        var recovery = new AdminPasswordRecovery
        {
            UserId = targetUserId,
            CreatedByUserId = actingUserId,
            CodeHash = codeHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            FailedAttempts = 0,
            Status = AdminPasswordRecoveryStatus.Pending,
            Reason = reason
        };

        _db.AdminPasswordRecoveries.Add(recovery);

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(AdminPasswordRecovery),
            EntityId = targetUserId.ToString(),
            Action = "ADMIN_PASSWORD_RECOVERY_REQUESTED",
            PreviousValue = null,
            NewValue = JsonSerializer.Serialize(new { TargetUserId = targetUserId, TargetUsername = user.Username, Reason = reason }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return new CreateRecoveryResultDto(plaintextCode, expiresAt);
    }

    public async Task ConfirmRecoveryAsync(string username, string recoveryCode, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(recoveryCode))
            throw new InvalidOperationException("Recovery code is required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username)
            ?? throw new InvalidOperationException("Invalid recovery request details.");

        if (!user.IsActive)
            throw new InvalidOperationException("Cannot perform password recovery for a disabled user account.");

        var incomingHash = HashRecoveryCode(recoveryCode);

        var recovery = await _db.AdminPasswordRecoveries
            .Where(r => r.UserId == user.Id && r.Status == AdminPasswordRecoveryStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (recovery is null)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = nameof(AdminPasswordRecovery),
                EntityId = user.Id.ToString(),
                Action = "ADMIN_PASSWORD_RECOVERY_FAILED",
                PreviousValue = null,
                NewValue = JsonSerializer.Serialize(new { TargetUsername = username, FailureReason = "No active recovery request found" }),
                UserId = user.Id,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            throw new InvalidOperationException("Invalid or expired recovery code.");
        }

        if (DateTime.UtcNow > recovery.ExpiresAt)
        {
            recovery.Status = AdminPasswordRecoveryStatus.Expired;
            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = nameof(AdminPasswordRecovery),
                EntityId = user.Id.ToString(),
                Action = "ADMIN_PASSWORD_RECOVERY_EXPIRED",
                PreviousValue = null,
                NewValue = JsonSerializer.Serialize(new { TargetUserId = user.Id, TargetUsername = user.Username, FailureReason = "Expired" }),
                UserId = user.Id,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            throw new InvalidOperationException("Recovery code has expired. Please request a new recovery code.");
        }

        if (!string.Equals(recovery.CodeHash, incomingHash, StringComparison.OrdinalIgnoreCase))
        {
            recovery.FailedAttempts++;
            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = nameof(AdminPasswordRecovery),
                EntityId = user.Id.ToString(),
                Action = "ADMIN_PASSWORD_RECOVERY_FAILED",
                PreviousValue = null,
                NewValue = JsonSerializer.Serialize(new { TargetUserId = user.Id, TargetUsername = user.Username, FailedAttempts = recovery.FailedAttempts }),
                UserId = user.Id,
                Timestamp = DateTime.UtcNow
            });

            if (recovery.FailedAttempts >= 5)
            {
                recovery.Status = AdminPasswordRecoveryStatus.FailedLimitExceeded;
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = nameof(AdminPasswordRecovery),
                    EntityId = user.Id.ToString(),
                    Action = "ADMIN_PASSWORD_RECOVERY_EXPIRED",
                    PreviousValue = null,
                    NewValue = JsonSerializer.Serialize(new { TargetUserId = user.Id, TargetUsername = user.Username, FailureReason = "Failed limit exceeded" }),
                    UserId = user.Id,
                    Timestamp = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            throw new InvalidOperationException(recovery.FailedAttempts >= 5
                ? "Maximum failed recovery attempts exceeded. Recovery code invalidated."
                : "Invalid recovery code.");
        }

        // Code matches - Validate Password Policy
        var failures = PasswordPolicy.Validate(newPassword);
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        // Validate Password History (last 5 passwords)
        var historyHashes = await _db.PasswordHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.Id)
            .Take(5)
            .Select(h => h.PasswordHash)
            .ToListAsync();

        foreach (var oldHash in historyHashes)
        {
            if (BCrypt.Net.BCrypt.Verify(newPassword, oldHash))
                throw new InvalidOperationException("New password must not match any of your last 5 passwords.");
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordHash = newHash;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        _db.PasswordHistories.Add(new PasswordHistory { UserId = user.Id, PasswordHash = newHash });

        recovery.Status = AdminPasswordRecoveryStatus.Used;
        recovery.UsedAt = DateTime.UtcNow;

        // Invalidate active refresh tokens for the account
        var activeTokens = await _db.RefreshTokens
            .Where(r => r.UserId == user.Id && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
        foreach (var rt in activeTokens)
        {
            rt.RevokedAt = DateTime.UtcNow;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(AdminPasswordRecovery),
            EntityId = user.Id.ToString(),
            Action = "ADMIN_PASSWORD_RECOVERY_USED",
            PreviousValue = null,
            NewValue = JsonSerializer.Serialize(new { TargetUserId = user.Id, TargetUsername = user.Username, Method = "Administrator-Assisted Recovery" }),
            UserId = user.Id,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
