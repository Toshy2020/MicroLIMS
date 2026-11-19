using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Mfa;

/// <summary>
/// Administrator operations for multi-factor authentication: reset, hardware token assignment,
/// and viewing/revoking a user's trusted computers.
/// </summary>
public sealed class MfaAdminService : IMfaAdminService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IMfaService _mfaService;
    private readonly IAuthenticationService _authService;
    private readonly ISecretProtector _secretProtector;
    private readonly ISecurityAuditService _securityAudit;
    private readonly MfaPolicyService _mfaPolicy;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<MfaAdminService> _logger;
    private readonly TimeProvider _time;

    public MfaAdminService(
        IMicroLimsDbContext db,
        IMfaService mfaService,
        IAuthenticationService authService,
        ISecretProtector secretProtector,
        ISecurityAuditService securityAudit,
        MfaPolicyService mfaPolicy,
        IEmailSender emailSender,
        ILogger<MfaAdminService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _mfaService = mfaService;
        _authService = authService;
        _secretProtector = secretProtector;
        _securityAudit = securityAudit;
        _mfaPolicy = mfaPolicy;
        _emailSender = emailSender;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task ResetAsync(int targetUserId, string reason, int actingUserId, CancellationToken ct = default)
    {
        if (actingUserId == targetUserId)
            throw new BusinessRuleException(
                MessageCodes.AuthMfaCannotResetOwnAccount,
                "System Administrators cannot reset multi-factor authentication for their own account.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException(
                MessageCodes.AuthMfaResetReasonRequired,
                "A reason is required to reset multi-factor authentication.");

        var trimmedReason = reason.Trim();
        string? notifyEmail = null;

        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
                ?? throw new NotFoundException(
                    MessageCodes.UserNotFound,
                    $"User {targetUserId} not found.",
                    new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

            if (!user.IsActive)
                throw new BusinessRuleException(
                    MessageCodes.AuthMfaCannotResetDisabledUser,
                    "Cannot reset multi-factor authentication for a disabled user account. Please enable the account first.");

            var nowUtc = _time.GetUtcNow().UtcDateTime;
            _db.CurrentReason = trimmedReason;

            var credentials = await _db.UserMfaCredentials
                .Where(c => c.UserId == targetUserId
                    && (c.Status == MfaCredentialStatus.Active || c.Status == MfaCredentialStatus.Pending))
                .ToListAsync(ct);

            foreach (var credential in credentials)
            {
                credential.Status = MfaCredentialStatus.Revoked;
                credential.RevokedAt = nowUtc;
                credential.RevokedByUserId = actingUserId;
                credential.RevokeReason = trimmedReason;
            }

            var unusedCodes = await _db.MfaRecoveryCodes
                .Where(r => r.UserId == targetUserId && r.UsedAt == null)
                .ToListAsync(ct);
            if (unusedCodes.Count > 0)
                _db.MfaRecoveryCodes.RemoveRange(unusedCodes);

            await _mfaService.RevokeTrustedDevicesAsync(targetUserId, actingUserId, ct);
            await _authService.RevokeAllRefreshTokensAsync(targetUserId);

            // Set MfaRequiredSince back by the full grace duration so next login requires immediate enrolment.
            var graceDays = await _mfaPolicy.GetEnrolmentGraceDaysAsync(ct);
            user.MfaRequiredSince = graceDays > 0 ? nowUtc.AddDays(-graceDays) : nowUtc;

            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = nameof(User),
                EntityId = targetUserId.ToString(),
                Action = "MFA_RESET_BY_ADMIN",
                PreviousValue = null,
                NewValue = JsonSerializer.Serialize(new
                {
                    TargetUserId = targetUserId,
                    TargetUsername = user.Username,
                    Reason = trimmedReason
                }),
                Reason = trimmedReason,
                UserId = actingUserId,
                Timestamp = nowUtc
            });

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.MfaResetByAdmin,
                SecurityEventOutcome.Success,
                ActorUserId: actingUserId,
                TargetUserId: targetUserId,
                TargetUsername: user.Username,
                Reason: trimmedReason));

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            notifyEmail = user.Email;
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(notifyEmail))
        {
            try
            {
                await _emailSender.SendAsync(
                    notifyEmail,
                    "MicroLIMS: Two-factor authentication was reset",
                    "An administrator reset multi-factor authentication on your MicroLIMS account. You will be required to enrol a new second factor on your next sign-in.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send MFA reset email notification for user {UserId}.", targetUserId);
            }
        }
    }

    public async Task AssignHardwareTokenAsync(
        int targetUserId,
        string serialNumber,
        string seed,
        bool seedIsHex,
        int stepSeconds,
        string reason,
        int actingUserId,
        CancellationToken ct = default)
    {
        if (actingUserId == targetUserId)
            throw new BusinessRuleException(
                MessageCodes.AuthMfaCannotAssignTokenToOwnAccount,
                "System Administrators cannot assign a hardware token to their own account.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException(
                MessageCodes.AuthMfaTokenAssignReasonRequired,
                "A reason is required to assign a hardware token.");

        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new BusinessRuleException(
                MessageCodes.AuthMfaTokenSerialRequired,
                "The hardware token serial number is required.");

        if (stepSeconds is not (30 or 60))
            throw new BusinessRuleException(
                MessageCodes.AuthMfaTotpStepInvalid,
                "TOTP step must be 30 or 60 seconds.");

        if (string.IsNullOrWhiteSpace(seed))
            throw new BusinessRuleException(
                MessageCodes.AuthMfaTokenSeedInvalid,
                "The hardware token seed is not valid.");

        byte[] seedBytes = DecodeSeedOrThrow(seed, seedIsHex);
        string protectedSecret;
        try
        {
            protectedSecret = _secretProtector.Protect(seedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seedBytes);
        }

        var trimmedSerial = serialNumber.Trim();
        var trimmedReason = reason.Trim();

        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
                ?? throw new NotFoundException(
                    MessageCodes.UserNotFound,
                    $"User {targetUserId} not found.",
                    new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

            if (!user.IsActive)
                throw new BusinessRuleException(
                    MessageCodes.AuthMfaCannotAssignTokenToDisabledUser,
                    "Cannot assign a hardware token for a disabled user account. Please enable the account first.");

            var hasActive = await _db.UserMfaCredentials
                .AsNoTracking()
                .AnyAsync(c => c.UserId == targetUserId && c.Status == MfaCredentialStatus.Active, ct);
            if (hasActive)
                throw new BusinessRuleException(
                    MessageCodes.AuthMfaAlreadyActiveForHardwareToken,
                    "Two-factor authentication is already active for this user. Reset it before assigning a hardware token.");

            var existingPending = await _db.UserMfaCredentials
                .Where(c => c.UserId == targetUserId && c.Status == MfaCredentialStatus.Pending)
                .ToListAsync(ct);
            if (existingPending.Count > 0)
            {
                _db.UserMfaCredentials.RemoveRange(existingPending);
                await _db.SaveChangesAsync(ct);
            }

            var nowUtc = _time.GetUtcNow().UtcDateTime;
            _db.CurrentReason = trimmedReason;

            _db.UserMfaCredentials.Add(new UserMfaCredential
            {
                UserId = targetUserId,
                Type = MfaCredentialType.HardwareToken,
                SecretProtected = protectedSecret,
                StepSeconds = stepSeconds,
                SerialNumber = trimmedSerial,
                Status = MfaCredentialStatus.Pending,
                CreatedAt = nowUtc,
                AssignedByUserId = actingUserId
            });

            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = nameof(User),
                EntityId = targetUserId.ToString(),
                Action = "MFA_TOKEN_ASSIGNED",
                PreviousValue = null,
                NewValue = JsonSerializer.Serialize(new
                {
                    TargetUserId = targetUserId,
                    TargetUsername = user.Username,
                    SerialNumber = trimmedSerial,
                    StepSeconds = stepSeconds,
                    Reason = trimmedReason
                }),
                Reason = trimmedReason,
                UserId = actingUserId,
                Timestamp = nowUtc
            });

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.MfaTokenAssigned,
                SecurityEventOutcome.Success,
                ActorUserId: actingUserId,
                TargetUserId: targetUserId,
                TargetUsername: user.Username,
                Reason: trimmedReason,
                Metadata: JsonSerializer.Serialize(new { serialNumber = trimmedSerial, stepSeconds })));

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<TrustedDeviceInfo>> ListTrustedDevicesAsync(int targetUserId, CancellationToken ct = default)
    {
        if (!await _db.Users.AsNoTracking().AnyAsync(u => u.Id == targetUserId, ct))
            throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        return await _db.TrustedDevices
            .AsNoTracking()
            .Where(d => d.UserId == targetUserId && d.RevokedAt == null && d.ExpiresAt > nowUtc)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new TrustedDeviceInfo(d.Id, d.CreatedAt, d.ExpiresAt, d.CreatedIp, d.UserAgent))
            .ToListAsync(ct);
    }

    public async Task RevokeTrustedDeviceAsync(int targetUserId, int deviceId, int actingUserId, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var device = await _db.TrustedDevices
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == targetUserId && d.RevokedAt == null, ct)
                ?? throw new NotFoundException(
                    MessageCodes.AuthTrustedDeviceNotFound,
                    "Trusted device not found.");

            device.RevokedAt = _time.GetUtcNow().UtcDateTime;
            device.RevokedByUserId = actingUserId;

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.TrustedDeviceRevoked,
                SecurityEventOutcome.Success,
                ActorUserId: actingUserId,
                TargetUserId: targetUserId,
                Metadata: $"{{\"revokedCount\":1,\"deviceId\":{device.Id}}}"));

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    private static byte[] DecodeSeedOrThrow(string seed, bool seedIsHex)
    {
        byte[] decoded;
        try
        {
            decoded = seedIsHex ? Totp.FromHex(seed) : Totp.FromBase32(seed);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException(
                MessageCodes.AuthMfaTokenSeedInvalid,
                "The hardware token seed is not valid.");
        }

        if (decoded.Length == 0)
            throw new BusinessRuleException(
                MessageCodes.AuthMfaTokenSeedInvalid,
                "The hardware token seed is not valid.");

        return decoded;
    }

    private async Task<(bool OwnsTransaction, IDbContextTransaction? Transaction)> BeginOwnedTransactionAsync(CancellationToken ct)
    {
        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
        var tx = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        return (ownsTransaction, tx);
    }
}
