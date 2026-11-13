using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Mfa;

/// <summary>
/// Handles MFA login challenges, TOTP verification, recovery codes, and trusted devices.
/// </summary>
public sealed class MfaService : IMfaService
{
    public const int ChallengeTokenBytes = 32;
    public const int DeviceTokenBytes = 32;
    public const int RecoveryCodeLength = 10;
    public const int MaxChallengeFailedAttempts = 5;
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    internal const string VerificationFailedMessage = "The code is not correct.";

    private readonly IMicroLimsDbContext _db;
    private readonly AuthenticationService _authService;
    private readonly ISecretProtector _secretProtector;
    private readonly ISecurityAuditService _securityAudit;
    private readonly ILogger<MfaService> _logger;
    private readonly MfaPolicyService _mfaPolicy;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _time;

    public MfaService(
        IMicroLimsDbContext db,
        AuthenticationService authService,
        ISecretProtector secretProtector,
        ISecurityAuditService securityAudit,
        ILogger<MfaService> logger,
        MfaPolicyService mfaPolicy,
        IEmailSender emailSender,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _authService = authService;
        _secretProtector = secretProtector;
        _securityAudit = securityAudit;
        _logger = logger;
        _mfaPolicy = mfaPolicy;
        _emailSender = emailSender;
        _time = timeProvider ?? TimeProvider.System;
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

    public async Task<int> RevokeTrustedDevicesAsync(int userId, int? revokedByUserId, CancellationToken ct = default)
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var liveDevices = await _db.TrustedDevices
            .Where(d => d.UserId == userId && d.RevokedAt == null && d.ExpiresAt > nowUtc)
            .ToListAsync(ct);

        if (liveDevices.Count == 0)
            return 0;

        foreach (var device in liveDevices)
        {
            device.RevokedAt = nowUtc;
            device.RevokedByUserId = revokedByUserId;
        }

        _securityAudit.Record(new SecurityEventRequest(
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

        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        string? recoveryNotifyEmail = null;
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

            int? recoveryCodesRemaining = null;

            if (hasCode)
            {
                var verified = await TryVerifyTotpAsync(user, code!, nowUtc, ct);
                if (!verified)
                {
                    await RecordSecondFactorFailureAsync(challenge, user, ip, ct);
                    if (tx is not null)
                        await tx.CommitAsync(ct);
                    return Fail();
                }

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

            string? deviceToken = null;
            if (trustDevice)
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

            var outcome = await _authService.CompleteLoginAsync(user, ip, userAgent, mfaVerified: true);

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
                RecoveryCodesRemaining = recoveryCodesRemaining
            };
        }
        catch (DbUpdateConcurrencyException) when (ownsTransaction)
        {
            await tx!.RollbackAsync(CancellationToken.None);
            return Fail();
        }

        if (recoveryNotifyEmail is not null && !string.IsNullOrWhiteSpace(recoveryNotifyEmail))
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

        return result;
    }

    private async Task<bool> TryVerifyTotpAsync(User user, string code, DateTime nowUtc, CancellationToken ct)
    {
        var credential = await _db.UserMfaCredentials
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Status == MfaCredentialStatus.Active, ct);

        if (credential is null)
            return false;

        byte[] secret;
        try
        {
            secret = _secretProtector.Unprotect(credential.SecretProtected);
        }
        catch (SecretProtectionException ex)
        {
            _logger.LogError(ex, "Failed to unprotect MFA credential {CredentialId} for user {UserId}.", credential.Id, user.Id);
            return false;
        }

        long? matchedStep;
        try
        {
            matchedStep = Totp.Verify(secret, code.Trim(), nowUtc, credential.StepSeconds, credential.LastAcceptedStep);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        if (matchedStep is null)
            return false;

        credential.LastAcceptedStep = matchedStep.Value;
        return true;
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
