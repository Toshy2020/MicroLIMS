using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Mfa;

/// <summary>
/// Handles MFA login challenges and TOTP verification.
/// </summary>
public sealed class MfaService : IMfaService
{
    public const int ChallengeTokenBytes = 32;
    public const int MaxChallengeFailedAttempts = 5;
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    internal const string VerificationFailedMessage = "The code is not correct.";

    private readonly IMicroLimsDbContext _db;
    private readonly AuthenticationService _authService;
    private readonly ISecretProtector _secretProtector;
    private readonly ISecurityAuditService _securityAudit;
    private readonly ILogger<MfaService> _logger;
    private readonly TimeProvider _time;

    public MfaService(
        IMicroLimsDbContext db,
        AuthenticationService authService,
        ISecretProtector secretProtector,
        ISecurityAuditService securityAudit,
        ILogger<MfaService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _authService = authService;
        _secretProtector = secretProtector;
        _securityAudit = securityAudit;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> CreateChallengeAsync(int userId, MfaChallengePurpose purpose, CancellationToken ct = default)
    {
        var rawToken = CreateChallengeCore(_db, _time, _securityAudit, userId, purpose);
        await _db.SaveChangesAsync(ct);
        return rawToken;
    }

    public Task<bool> IsTrustedDeviceAsync(int userId, string? deviceToken, CancellationToken ct = default)
        => Task.FromResult(false); // Part B

    public Task<int> RevokeTrustedDevicesAsync(int userId, int? revokedByUserId, CancellationToken ct = default)
        => Task.FromResult(0); // Part B

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

        if (string.IsNullOrWhiteSpace(code))
        {
            if (!string.IsNullOrWhiteSpace(recoveryCode))
                return Fail(); // Part B

            return Fail();
        }

        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

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

            var credential = await _db.UserMfaCredentials
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Status == MfaCredentialStatus.Active, ct);

            if (credential is null)
                return Fail();

            byte[] secret;
            try
            {
                secret = _secretProtector.Unprotect(credential.SecretProtected);
            }
            catch (SecretProtectionException ex)
            {
                _logger.LogError(ex, "Failed to unprotect MFA credential {CredentialId} for user {UserId}.", credential.Id, user.Id);
                return Fail();
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
            {
                challenge.FailedAttempts++;
                await _authService.ApplyFailedLoginAttemptAndLockoutAsync(user, SecurityEventCodes.MfaFailed, ct);
                await _db.SaveChangesAsync(ct);

                if (tx is not null)
                    await tx.CommitAsync(ct);

                return Fail();
            }

            credential.LastAcceptedStep = matchedStep.Value;
            challenge.ConsumedAt = nowUtc;

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.MfaVerified,
                SecurityEventOutcome.Success,
                ActorUserId: user.Id,
                TargetUserId: user.Id,
                TargetUsername: user.Username));

            var outcome = await _authService.CompleteLoginAsync(user, ip, userAgent, mfaVerified: true);

            if (tx is not null)
                await tx.CommitAsync(ct);

            return outcome;
        }
        catch (DbUpdateConcurrencyException) when (ownsTransaction)
        {
            await tx!.RollbackAsync(CancellationToken.None);
            return Fail();
        }
    }

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
