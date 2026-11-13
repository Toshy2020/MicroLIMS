using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Mfa;

/// <summary>
/// Handles MFA login challenges, TOTP verification, self-service enrolment, recovery codes, and trusted devices.
/// </summary>
public sealed class MfaService : IMfaService
{
    public const int ChallengeTokenBytes = 32;
    public const int DeviceTokenBytes = 32;
    public const int DefaultStepSeconds = 30;
    public const int RecoveryCodeLength = 10;
    public const int RecoveryCodeSetCount = 10;
    public const int MaxChallengeFailedAttempts = 5;
    public const string TotpIssuer = "MicroLIMS";
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    internal const string VerificationFailedMessage = "The code is not correct.";
    internal const string PasswordFailedMessage = "The password is not correct.";
    internal const string EnrolSessionInvalidMessage = "Your enrolment session has expired. Sign in again.";

    private readonly IMicroLimsDbContext _db;
    private readonly AuthenticationService _authService;
    private readonly ISecretProtector _secretProtector;
    private readonly ISecurityAuditService _securityAudit;
    private readonly ILogger<MfaService> _logger;
    private readonly MfaPolicyService _mfaPolicy;
    private readonly IEmailSender _emailSender;
    private readonly IQrCodeRenderer _qrCodeRenderer;
    private readonly TimeProvider _time;
    private readonly SsoModeService? _ssoMode;

    public MfaService(
        IMicroLimsDbContext db,
        AuthenticationService authService,
        ISecretProtector secretProtector,
        ISecurityAuditService securityAudit,
        ILogger<MfaService> logger,
        MfaPolicyService mfaPolicy,
        IEmailSender emailSender,
        IQrCodeRenderer qrCodeRenderer,
        TimeProvider? timeProvider = null,
        SsoModeService? ssoMode = null)
    {
        _db = db;
        _authService = authService;
        _secretProtector = secretProtector;
        _securityAudit = securityAudit;
        _logger = logger;
        _mfaPolicy = mfaPolicy;
        _emailSender = emailSender;
        _qrCodeRenderer = qrCodeRenderer;
        _time = timeProvider ?? TimeProvider.System;
        _ssoMode = ssoMode;
    }

    public async Task<string> CreateChallengeAsync(int userId, MfaChallengePurpose purpose, CancellationToken ct = default)
    {
        var rawToken = CreateChallengeCore(_db, _time, _securityAudit, userId, purpose);
        await _db.SaveChangesAsync(ct);
        return rawToken;
    }

    public Task<bool> IsTrustedDeviceAsync(int userId, string? deviceToken, CancellationToken ct = default) =>
        IsTrustedDeviceCoreAsync(_db, _time, _mfaPolicy, userId, deviceToken, ct);

    internal static async Task<bool> IsTrustedDeviceCoreAsync(
        IMicroLimsDbContext db,
        TimeProvider time,
        MfaPolicyService mfaPolicy,
        int userId,
        string? deviceToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return false;

        if (await mfaPolicy.GetTrustedDeviceHoursAsync(ct) <= 0)
            return false;

        var nowUtc = time.GetUtcNow().UtcDateTime;
        var tokenHash = AuthenticationService.Hash(deviceToken);

        return await db.TrustedDevices
            .AsNoTracking()
            .AnyAsync(
                d => d.UserId == userId
                  && d.TokenHash == tokenHash
                  && d.RevokedAt == null
                  && d.ExpiresAt > nowUtc,
                ct);
    }

    public Task<int> RevokeTrustedDevicesAsync(int userId, int? revokedByUserId, CancellationToken ct = default) =>
        RevokeTrustedDevicesCoreAsync(_db, _time, _securityAudit, userId, revokedByUserId, ct);

    internal static async Task<int> RevokeTrustedDevicesCoreAsync(
        IMicroLimsDbContext db,
        TimeProvider time,
        ISecurityAuditService? securityAudit,
        int userId,
        int? revokedByUserId,
        CancellationToken ct = default)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var liveDevices = await db.TrustedDevices
            .Where(d => d.UserId == userId && d.RevokedAt == null && d.ExpiresAt > nowUtc)
            .ToListAsync(ct);

        if (liveDevices.Count == 0)
            return 0;

        foreach (var device in liveDevices)
        {
            device.RevokedAt = nowUtc;
            device.RevokedByUserId = revokedByUserId;
        }

        securityAudit?.Record(new SecurityEventRequest(
            SecurityEventCodes.TrustedDeviceRevoked,
            SecurityEventOutcome.Success,
            ActorUserId: revokedByUserId,
            TargetUserId: userId,
            Metadata: $"{{\"revokedCount\":{liveDevices.Count}}}"));

        return liveDevices.Count;
    }

    public async Task<LoginOutcome> VerifyAsync(
        string mfaToken,
        string? code,
        string? recoveryCode,
        bool trustDevice,
        string? ip,
        string? userAgent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mfaToken))
            return Fail();

        var hasCode = !string.IsNullOrWhiteSpace(code);
        var hasRecoveryCode = !string.IsNullOrWhiteSpace(recoveryCode);
        if (hasCode == hasRecoveryCode)
            return Fail();

        var (ownsTransaction, tx) = await BeginOwnedTransactionAsync(ct);

        string? recoveryNotifyEmail = null;
        string? emergencyNotifyUsername = null;
        List<string>? emergencyNotifyEmails = null;
        LoginOutcome result;

        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var tokenHash = AuthenticationService.Hash(mfaToken);

            var challenge = await _db.MfaChallenges
                .Include(c => c.User)
                .ThenInclude(u => u!.Role)
                .FirstOrDefaultAsync(c => c.TokenHash == tokenHash, ct);

            if (challenge is null
                || challenge.Purpose != MfaChallengePurpose.Verify
                || challenge.ConsumedAt is not null
                || challenge.ExpiresAt <= nowUtc
                || challenge.FailedAttempts >= MaxChallengeFailedAttempts)
            {
                return Fail();
            }

            var user = challenge.User;
            if (user is null || !user.IsActive || user.IsLockedAt(nowUtc))
                return Fail();

            List<string>? activatedRecoveryCodes = null;
            if (hasCode)
            {
                var (verified, recoveryCodes) = await TryVerifyTotpAsync(user, code!, nowUtc, ct);
                if (!verified)
                {
                    await RecordSecondFactorFailureAsync(challenge, user, ip, ct);
                    if (tx is not null)
                        await tx.CommitAsync(ct);
                    return Fail();
                }

                activatedRecoveryCodes = recoveryCodes;
                challenge.ConsumedAt = nowUtc;
                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.MfaVerified,
                    SecurityEventOutcome.Success,
                    ActorUserId: user.Id,
                    TargetUserId: user.Id,
                    TargetUsername: user.Username));
            }
            else
            {
                var recoveryHash = HashRecoveryCode(recoveryCode!);
                var matchedCode = await _db.MfaRecoveryCodes
                    .FirstOrDefaultAsync(r => r.UserId == user.Id && r.CodeHash == recoveryHash && r.UsedAt == null, ct);

                if (matchedCode is null)
                {
                    await RecordSecondFactorFailureAsync(challenge, user, ip, ct);
                    if (tx is not null)
                        await tx.CommitAsync(ct);
                    return Fail();
                }

                matchedCode.UsedAt = nowUtc;
                challenge.ConsumedAt = nowUtc;
                recoveryNotifyEmail = user.Email;

                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.MfaRecoveryCodeUsed,
                    SecurityEventOutcome.Success,
                    ActorUserId: user.Id,
                    TargetUserId: user.Id,
                    TargetUsername: user.Username));
            }

            var currentSsoMode = _ssoMode is null ? SsoMode.Off : await _ssoMode.GetModeAsync(ct);
            var isEmergencyLogin = user.IsEmergencyAccount && currentSsoMode != SsoMode.Off;

            string? deviceToken = null;
            if (!isEmergencyLogin && trustDevice)
            {
                var trustedHours = await _mfaPolicy.GetTrustedDeviceHoursAsync(ct);
                if (trustedHours > 0)
                {
                    deviceToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(DeviceTokenBytes));
                    _db.TrustedDevices.Add(new TrustedDevice
                    {
                        UserId = user.Id,
                        TokenHash = AuthenticationService.Hash(deviceToken),
                        CreatedAt = nowUtc,
                        ExpiresAt = nowUtc.AddHours(trustedHours),
                        CreatedIp = AuthenticationService.Truncate(ip, RefreshToken.MaxIpLength),
                        UserAgent = AuthenticationService.Truncate(userAgent, RefreshToken.MaxUserAgentLength)
                    });

                    _securityAudit.Record(new SecurityEventRequest(
                        SecurityEventCodes.TrustedDeviceAdded,
                        SecurityEventOutcome.Success,
                        ActorUserId: user.Id,
                        TargetUserId: user.Id,
                        TargetUsername: user.Username));
                }
            }

            if (isEmergencyLogin)
            {
                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.EmergencyLogin,
                    SecurityEventOutcome.Success,
                    ActorUserId: user.Id,
                    TargetUserId: user.Id,
                    TargetUsername: user.Username,
                    Reason: $"Emergency administrator sign-in while SSO mode is {currentSsoMode}."));

                emergencyNotifyUsername = user.Username;
                emergencyNotifyEmails = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive
                                && u.Role != null
                                && u.Role.Type == RoleType.SystemAdministrator
                                && u.Email != null
                                && u.Email != "")
                    .Select(u => u.Email!)
                    .Distinct()
                    .ToListAsync(ct);
            }

            var outcome = await _authService.CompleteLoginAsync(user, ip, userAgent, mfaVerified: true);

            int? recoveryCodesRemaining = null;
            if (hasRecoveryCode)
            {
                recoveryCodesRemaining = await _db.MfaRecoveryCodes
                    .AsNoTracking()
                    .CountAsync(r => r.UserId == user.Id && r.UsedAt == null, ct);
            }

            if (tx is not null)
                await tx.CommitAsync(ct);

            result = outcome with
            {
                DeviceToken = deviceToken,
                RecoveryCodesRemaining = recoveryCodesRemaining,
                RecoveryCodes = activatedRecoveryCodes
            };
        }
        catch (DbUpdateConcurrencyException) when (ownsTransaction)
        {
            await tx!.RollbackAsync(CancellationToken.None);
            return Fail();
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(recoveryNotifyEmail))
        {
            try
            {
                await _emailSender.SendAsync(
                    recoveryNotifyEmail,
                    "MicroLIMS: MFA recovery code used",
                    "A multi-factor authentication recovery code was used to sign in to your MicroLIMS account. If this was not you, contact an administrator immediately.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send MFA recovery code usage email notification.");
            }
        }

        if (emergencyNotifyEmails is { Count: > 0 })
        {
            var subject = "MicroLIMS: Emergency administrator sign-in";
            var body = $"Emergency administrator account '{emergencyNotifyUsername}' signed in while single sign-on is enabled.";
            foreach (var adminEmail in emergencyNotifyEmails)
            {
                if (string.IsNullOrWhiteSpace(adminEmail))
                    continue;
                try
                {
                    await _emailSender.SendAsync(adminEmail, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send emergency login notification email to {Recipient}.", adminEmail);
                }
            }
        }

        return result;
    }

    public async Task<EnrolmentStart> StartEnrolmentWithPasswordAsync(int userId, string password, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var user = await LoadAndVerifyPasswordAsync(userId, password, nowUtc, includeRole: false, tx, ct);
            var started = await StartEnrolmentCoreAsync(user, nowUtc, ct);
            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);
            return started;
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public async Task<EnrolmentStart> StartEnrolmentWithTokenAsync(string mfaToken, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var challenge = await LoadValidEnrolChallengeAsync(mfaToken, nowUtc, includeRole: false, ct);
            var started = await StartEnrolmentCoreAsync(challenge.User!, nowUtc, ct);
            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);
            return started;
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public async Task<EnrolmentResult> ConfirmEnrolmentAsync(int userId, string code, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var user = await LoadActiveUserAsync(userId, nowUtc, includeRole: false, ct);
            var recoveryCodes = await TryConfirmEnrolmentCoreAsync(user, code, nowUtc, ct);
            if (recoveryCodes is null)
            {
                await RecordAuthenticatedMfaFailureAndCommitAsync(userId, user, "Code verification failed.", tx, ct);
                throw new BusinessRuleException(VerificationFailedMessage);
            }

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            return new EnrolmentResult(recoveryCodes, Login: null);
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public async Task<EnrolmentResult> ConfirmEnrolmentWithTokenAsync(
        string mfaToken,
        string code,
        string? ip,
        string? userAgent,
        CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var challenge = await LoadValidEnrolChallengeAsync(mfaToken, nowUtc, includeRole: true, ct);
            var user = challenge.User!;

            var recoveryCodes = await TryConfirmEnrolmentCoreAsync(user, code, nowUtc, ct);
            if (recoveryCodes is null)
            {
                challenge.FailedAttempts++;
                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.MfaFailed,
                    SecurityEventOutcome.Failure,
                    ActorUserId: user.Id,
                    TargetUserId: user.Id,
                    TargetUsername: user.Username));
                await _db.SaveChangesAsync(ct);
                if (tx is not null)
                    await tx.CommitAsync(ct);

                throw new BusinessRuleException(VerificationFailedMessage);
            }

            challenge.ConsumedAt = nowUtc;
            var login = await _authService.CompleteLoginAsync(user, ip, userAgent, mfaVerified: true);

            if (tx is not null)
                await tx.CommitAsync(ct);

            return new EnrolmentResult(recoveryCodes, login with { RecoveryCodesRemaining = recoveryCodes.Count });
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(int userId, string password, string code, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var user = await LoadAndVerifyPasswordAsync(userId, password, nowUtc, includeRole: false, tx, ct);

            var credential = await _db.UserMfaCredentials
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Active, ct)
                ?? throw new BusinessRuleException("Two-factor authentication is not active.");

            var matchedStep = VerifyTotpCodeAgainstCredential(credential, code, nowUtc);
            if (matchedStep is null)
            {
                await RecordAuthenticatedMfaFailureAndCommitAsync(userId, user, "Code verification failed.", tx, ct);
                throw new BusinessRuleException(VerificationFailedMessage);
            }

            credential.LastAcceptedStep = matchedStep.Value;

            var codes = await ReplaceRecoveryCodesAsync(userId, nowUtc, removeOnlyUnused: false, ct);

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.MfaRecoveryCodesRegenerated,
                SecurityEventOutcome.Success,
                ActorUserId: userId,
                TargetUserId: userId,
                TargetUsername: user.Username));

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            return codes;
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    public async Task RemoveOwnFactorAsync(int userId, string password, string code, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var user = await LoadAndVerifyPasswordAsync(userId, password, nowUtc, includeRole: true, tx, ct);

            var credential = await _db.UserMfaCredentials
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Active, ct)
                ?? throw new BusinessRuleException("Two-factor authentication is not active.");

            var roleType = user.Role?.Type.ToString() ?? string.Empty;
            if (await _mfaPolicy.IsRequiredForRoleAsync(roleType, ct))
                throw new BusinessRuleException("Your role requires two-factor authentication, so you cannot remove it.");

            var matchedStep = VerifyTotpCodeAgainstCredential(credential, code, nowUtc);
            if (matchedStep is null)
            {
                await RecordAuthenticatedMfaFailureAndCommitAsync(userId, user, "Code verification failed.", tx, ct);
                throw new BusinessRuleException(VerificationFailedMessage);
            }

            credential.LastAcceptedStep = matchedStep.Value;
            credential.Status = MfaCredentialStatus.Revoked;
            credential.RevokedAt = nowUtc;
            credential.RevokedByUserId = userId;
            credential.RevokeReason = "Removed by user.";

            var unusedCodes = await _db.MfaRecoveryCodes
                .Where(r => r.UserId == userId && r.UsedAt == null)
                .ToListAsync(ct);
            if (unusedCodes.Count > 0)
                _db.MfaRecoveryCodes.RemoveRange(unusedCodes);

            await RevokeTrustedDevicesAsync(userId, userId, ct);

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.MfaRemovedByUser,
                SecurityEventOutcome.Success,
                ActorUserId: userId,
                TargetUserId: userId,
                TargetUsername: user.Username));

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

    public async Task<MfaStatus> GetStatusAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        var nowUtc = _time.GetUtcNow().UtcDateTime;

        var credential = await _db.UserMfaCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Active, ct);

        var recoveryCodesRemaining = await _db.MfaRecoveryCodes
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.UsedAt == null, ct);

        var roleType = user.Role?.Type.ToString() ?? string.Empty;
        var required = await _mfaPolicy.IsRequiredForRoleAsync(roleType, ct);
        var requirement = await _mfaPolicy.EvaluateAsync(roleType, credential is not null, user.MfaRequiredSince, nowUtc, ct);

        var trustedDevices = await _db.TrustedDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.RevokedAt == null && d.ExpiresAt > nowUtc)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new TrustedDeviceInfo(d.Id, d.CreatedAt, d.ExpiresAt, d.CreatedIp, d.UserAgent))
            .ToListAsync(ct);

        return new MfaStatus(
            Enrolled: credential is not null,
            Type: credential?.Type.ToString(),
            ConfirmedAt: credential?.ConfirmedAt,
            RecoveryCodesRemaining: recoveryCodesRemaining,
            Required: required,
            EnrolmentDueAt: requirement.EnrolmentDueAtUtc,
            TrustedDevices: trustedDevices);
    }

    public async Task ForgetOwnTrustedDeviceAsync(int userId, int trustedDeviceId, CancellationToken ct = default)
    {
        var (_, tx) = await BeginOwnedTransactionAsync(ct);
        try
        {
            var device = await _db.TrustedDevices
                .FirstOrDefaultAsync(d => d.Id == trustedDeviceId && d.UserId == userId && d.RevokedAt == null, ct)
                ?? throw new NotFoundException("Trusted device not found.");

            device.RevokedAt = _time.GetUtcNow().UtcDateTime;
            device.RevokedByUserId = userId;

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.TrustedDeviceRevoked,
                SecurityEventOutcome.Success,
                ActorUserId: userId,
                TargetUserId: userId,
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

    private async Task<(bool OwnsTransaction, IDbContextTransaction? Transaction)> BeginOwnedTransactionAsync(CancellationToken ct)
    {
        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
        var tx = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        return (ownsTransaction, tx);
    }

    private async Task<User> LoadActiveUserAsync(int userId, DateTime nowUtc, bool includeRole, CancellationToken ct)
    {
        IQueryable<User> query = _db.Users;
        if (includeRole)
            query = query.Include(u => u.Role);

        var user = await query.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive || user.IsLockedAt(nowUtc))
            throw new BusinessRuleException("Your account is not available.");

        return user;
    }

    private async Task<User> LoadAndVerifyPasswordAsync(
        int userId,
        string password,
        DateTime nowUtc,
        bool includeRole,
        IDbContextTransaction? tx,
        CancellationToken ct)
    {
        IQueryable<User> query = _db.Users;
        if (includeRole)
            query = query.Include(u => u.Role);

        var user = await query.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null
            || !user.IsActive
            || user.IsLockedAt(nowUtc)
            || string.IsNullOrEmpty(password)
            || !AuthenticationService.PasswordMatches(password, user.PasswordHash))
        {
            await RecordAuthenticatedMfaFailureAndCommitAsync(userId, user, "Password verification failed.", tx, ct);
            throw new BusinessRuleException(PasswordFailedMessage);
        }

        return user;
    }

    private async Task RecordAuthenticatedMfaFailureAndCommitAsync(
        int userId,
        User? user,
        string reason,
        IDbContextTransaction? tx,
        CancellationToken ct)
    {
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.MfaFailed,
            SecurityEventOutcome.Failure,
            ActorUserId: userId,
            TargetUserId: user?.Id ?? userId,
            TargetUsername: user?.Username,
            Reason: reason));
        await _db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);
    }

    private async Task<MfaChallenge> LoadValidEnrolChallengeAsync(string mfaToken, DateTime nowUtc, bool includeRole, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mfaToken))
            throw new BusinessRuleException(EnrolSessionInvalidMessage);

        var tokenHash = AuthenticationService.Hash(mfaToken);
        IQueryable<MfaChallenge> query = includeRole
            ? _db.MfaChallenges.Include(c => c.User).ThenInclude(u => u!.Role)
            : _db.MfaChallenges.Include(c => c.User);

        var challenge = await query.FirstOrDefaultAsync(c => c.TokenHash == tokenHash, ct);
        if (challenge is null
            || challenge.Purpose != MfaChallengePurpose.Enrol
            || challenge.ConsumedAt is not null
            || challenge.ExpiresAt <= nowUtc
            || challenge.FailedAttempts >= MaxChallengeFailedAttempts
            || challenge.User is null
            || !challenge.User.IsActive
            || challenge.User.IsLockedAt(nowUtc))
        {
            throw new BusinessRuleException(EnrolSessionInvalidMessage);
        }

        return challenge;
    }

    private async Task<EnrolmentStart> StartEnrolmentCoreAsync(User user, DateTime nowUtc, CancellationToken ct)
    {
        var hasActive = await _db.UserMfaCredentials
            .AsNoTracking()
            .AnyAsync(c => c.UserId == user.Id && c.Status == MfaCredentialStatus.Active, ct);
        if (hasActive)
            throw new BusinessRuleException("Two-factor authentication is already active. Remove your existing factor before setting up a new one.");

        var pending = await _db.UserMfaCredentials
            .Where(c => c.UserId == user.Id && c.Status == MfaCredentialStatus.Pending)
            .ToListAsync(ct);
        if (pending.Count > 0)
        {
            _db.UserMfaCredentials.RemoveRange(pending);
            await _db.SaveChangesAsync(ct);
        }

        var secret = Totp.NewSecret();
        string manualKey;
        string protectedSecret;
        try
        {
            manualKey = Totp.ToBase32(secret);
            protectedSecret = _secretProtector.Protect(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        _db.UserMfaCredentials.Add(new UserMfaCredential
        {
            UserId = user.Id,
            Type = MfaCredentialType.AuthenticatorApp,
            SecretProtected = protectedSecret,
            StepSeconds = DefaultStepSeconds,
            Status = MfaCredentialStatus.Pending,
            CreatedAt = nowUtc
        });

        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.MfaEnrolmentStarted,
            SecurityEventOutcome.Success,
            ActorUserId: user.Id,
            TargetUserId: user.Id,
            TargetUsername: user.Username));

        var otpAuthUri = Totp.OtpAuthUri(TotpIssuer, user.Username, manualKey, DefaultStepSeconds);
        var qrSvg = _qrCodeRenderer.RenderSvg(otpAuthUri);
        return new EnrolmentStart(manualKey, otpAuthUri, qrSvg);
    }

    private async Task<List<string>?> TryConfirmEnrolmentCoreAsync(User user, string code, DateTime nowUtc, CancellationToken ct)
    {
        var hasActive = await _db.UserMfaCredentials
            .AsNoTracking()
            .AnyAsync(c => c.UserId == user.Id && c.Status == MfaCredentialStatus.Active, ct);
        if (hasActive)
            throw new BusinessRuleException("Two-factor authentication is already active.");

        var pending = await _db.UserMfaCredentials
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Status == MfaCredentialStatus.Pending, ct)
            ?? throw new BusinessRuleException("Start two-factor setup before confirming a code.");

        var matchedStep = VerifyTotpCodeAgainstCredential(pending, code, nowUtc);
        if (matchedStep is null)
            return null;

        pending.Status = MfaCredentialStatus.Active;
        pending.ConfirmedAt = nowUtc;
        pending.LastAcceptedStep = matchedStep.Value;

        var recoveryCodes = await ReplaceRecoveryCodesAsync(user.Id, nowUtc, removeOnlyUnused: false, ct);

        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.MfaEnrolled,
            SecurityEventOutcome.Success,
            ActorUserId: user.Id,
            TargetUserId: user.Id,
            TargetUsername: user.Username));

        return recoveryCodes;
    }

    private async Task<List<string>> ReplaceRecoveryCodesAsync(int userId, DateTime nowUtc, bool removeOnlyUnused, CancellationToken ct)
    {
        var existing = await _db.MfaRecoveryCodes
            .Where(r => r.UserId == userId && (!removeOnlyUnused || r.UsedAt == null))
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            _db.MfaRecoveryCodes.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }

        var codes = new List<string>(RecoveryCodeSetCount);
        var seenHashes = new HashSet<string>(RecoveryCodeSetCount, StringComparer.Ordinal);
        while (codes.Count < RecoveryCodeSetCount)
        {
            var rawCode = GenerateRecoveryCode();
            var hash = HashRecoveryCode(rawCode);
            if (!seenHashes.Add(hash))
                continue;

            codes.Add(rawCode);
            _db.MfaRecoveryCodes.Add(new MfaRecoveryCode
            {
                UserId = userId,
                CodeHash = hash,
                CreatedAt = nowUtc
            });
        }

        return codes;
    }

    private async Task<(bool Verified, List<string>? ActivatedRecoveryCodes)> TryVerifyTotpAsync(
        User user,
        string code,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var credential = await _db.UserMfaCredentials
            .Where(c => c.UserId == user.Id
                && (c.Status == MfaCredentialStatus.Active
                    || (c.Status == MfaCredentialStatus.Pending && c.Type == MfaCredentialType.HardwareToken)))
            .OrderBy(c => c.Status == MfaCredentialStatus.Active ? 0 : 1)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
            return (false, null);

        var matchedStep = VerifyTotpCodeAgainstCredential(credential, code, nowUtc);
        if (matchedStep is null)
            return (false, null);

        credential.LastAcceptedStep = matchedStep.Value;

        if (credential.Status != MfaCredentialStatus.Pending)
            return (true, null);

        credential.Status = MfaCredentialStatus.Active;
        credential.ConfirmedAt = nowUtc;

        var recoveryCodes = await ReplaceRecoveryCodesAsync(user.Id, nowUtc, removeOnlyUnused: false, ct);
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.MfaEnrolled,
            SecurityEventOutcome.Success,
            ActorUserId: user.Id,
            TargetUserId: user.Id,
            TargetUsername: user.Username));

        return (true, recoveryCodes);
    }

    private long? VerifyTotpCodeAgainstCredential(UserMfaCredential credential, string? code, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        byte[] secret;
        try
        {
            secret = _secretProtector.Unprotect(credential.SecretProtected);
        }
        catch (SecretProtectionException ex)
        {
            _logger.LogError(ex, "Failed to unprotect MFA credential {CredentialId} for user {UserId}.", credential.Id, credential.UserId);
            return null;
        }

        try
        {
            return Totp.Verify(secret, code.Trim(), nowUtc, credential.StepSeconds, credential.LastAcceptedStep);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task RecordSecondFactorFailureAsync(MfaChallenge challenge, User user, string? ip, CancellationToken ct)
    {
        challenge.FailedAttempts++;
        await _authService.ApplyFailedLoginAttemptAndLockoutAsync(user, SecurityEventCodes.MfaFailed, ct);
        await _authService.RecordLoginAsync(user.Id, user.Username, false, "Wrong second factor", ip);
    }

    internal static string GenerateRecoveryCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(RecoveryCodeLength);
        var sb = new StringBuilder(RecoveryCodeLength + 1);
        for (var i = 0; i < RecoveryCodeLength; i++)
        {
            if (i == 5)
                sb.Append('-');
            sb.Append(AdminPasswordRecoveryService.AllowedChars[bytes[i] % AdminPasswordRecoveryService.AllowedChars.Length]);
        }
        return sb.ToString();
    }

    internal static string HashRecoveryCode(string code) =>
        AdminPasswordRecoveryService.HashRecoveryCode(code);

    internal static string CreateChallengeCore(
        IMicroLimsDbContext db,
        TimeProvider time,
        ISecurityAuditService securityAudit,
        int userId,
        MfaChallengePurpose purpose)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(ChallengeTokenBytes));
        var nowUtc = time.GetUtcNow().UtcDateTime;

        db.MfaChallenges.Add(new MfaChallenge
        {
            UserId = userId,
            TokenHash = AuthenticationService.Hash(rawToken),
            Purpose = purpose,
            ExpiresAt = nowUtc.Add(ChallengeLifetime),
            FailedAttempts = 0
        });

        securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.MfaChallengeIssued,
            SecurityEventOutcome.Success,
            TargetUserId: userId,
            Metadata: $"{{\"purpose\":\"{purpose}\"}}"));

        return rawToken;
    }

    private static LoginOutcome Fail() => new(false, null, null, VerificationFailedMessage);
}
