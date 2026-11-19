using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Raised when an OpenID Connect sign-in endpoint is invoked while SSO mode is not <see cref="SsoMode.OpenIdConnect"/>.
/// </summary>
public sealed class SsoModeNotEnabledException : Exception
{
    public SsoModeNotEnabledException(SsoMode currentMode)
        : base($"OpenID Connect sign-in is not enabled (current SSO mode is {currentMode}).")
    {
    }
}

/// <summary>
/// Coordinates the OpenID Connect authorization-code + PKCE sign-in flow:
/// <list type="number">
/// <item><see cref="StartAsync"/> creates a 10-minute single-use <see cref="SsoPendingRequest"/> and returns the IdP authorize URI.</item>
/// <item><see cref="HandleCallbackAsync"/> atomically consumes the pending request, redeems the code, matches an existing local user without creating accounts, optionally binds <see cref="User.DirectorySubjectId"/>, and issues a 60-second single-use <see cref="SsoExchangeCode"/>.</item>
/// <item><see cref="ExchangeAsync"/> atomically consumes the exchange code and completes login via <see cref="AuthenticationService.CompleteLoginAsync"/> with <c>mfaVerified: true</c> and <c>LoginHistory.Method = "Oidc"</c>.</item>
/// </list>
/// </summary>
public sealed class OidcSignInService
{
    private static readonly TimeSpan PendingRequestLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ExchangeCodeLifetime = TimeSpan.FromSeconds(60);

    private readonly IMicroLimsDbContext _db;
    private readonly IOidcProviderClient _oidcClient;
    private readonly SsoModeService _ssoMode;
    private readonly AuthenticationService _authService;
    private readonly ISecurityAuditService _securityAudit;
    private readonly TimeProvider _time;
    private readonly ILogger<OidcSignInService> _logger;

    public OidcSignInService(
        IMicroLimsDbContext db,
        IOidcProviderClient oidcClient,
        SsoModeService ssoMode,
        AuthenticationService authService,
        ISecurityAuditService securityAudit,
        TimeProvider? timeProvider = null,
        ILogger<OidcSignInService>? logger = null)
    {
        _db = db;
        _oidcClient = oidcClient;
        _ssoMode = ssoMode;
        _authService = authService;
        _securityAudit = securityAudit;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<OidcSignInService>.Instance;
    }

    public async Task<Uri> StartAsync(string redirectUri, CancellationToken ct = default)
    {
        await EnsureOidcModeAsync(ct);

        var state = GenerateRandomBase64Url(32);
        var nonce = GenerateRandomBase64Url(32);
        var codeVerifier = GenerateRandomBase64Url(32);
        var codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        _db.SsoPendingRequests.Add(new SsoPendingRequest
        {
            StateHash = AuthenticationService.Hash(state),
            Nonce = nonce,
            CodeVerifier = codeVerifier,
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc.Add(PendingRequestLifetime)
        });
        await _db.SaveChangesAsync(ct);

        return await _oidcClient.BuildAuthorizeUriAsync(state, nonce, codeChallenge, redirectUri, ct);
    }

    public async Task<Uri> HandleCallbackAsync(
        string? state,
        string? code,
        string? error,
        string redirectUri,
        string frontendBaseUrl,
        string? ipAddress,
        CancellationToken ct = default)
    {
        await EnsureOidcModeAsync(ct);

        if (!string.IsNullOrWhiteSpace(error))
        {
            await RecordFailureAsync(
                SecurityEventCodes.SsoLoginFailedTokenInvalid,
                reason: $"Provider returned error: {Truncate(error, 200)}",
                ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }

        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
        {
            await RecordFailureAsync(
                SecurityEventCodes.SsoLoginFailedTokenInvalid,
                reason: "Callback missing state or authorization code.",
                ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }

        var stateHash = AuthenticationService.Hash(state);
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        var consumed = await _db.SsoPendingRequests
            .Where(r => r.StateHash == stateHash && r.ConsumedAt == null && r.ExpiresAt > nowUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ConsumedAt, nowUtc), ct);

        if (consumed != 1)
        {
            await RecordFailureAsync(
                SecurityEventCodes.SsoLoginFailedTokenInvalid,
                reason: "Pending SSO request missing, expired, or already consumed.",
                ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }

        var pending = await _db.SsoPendingRequests
            .AsNoTracking()
            .FirstAsync(r => r.StateHash == stateHash, ct);

        DirectoryIdentity identity;
        try
        {
            identity = await _oidcClient.RedeemAsync(code, pending.CodeVerifier, pending.Nonce, redirectUri, ct);
        }
        catch (OidcValidationException ex)
        {
            var eventCode = ex.ReasonCode == OidcValidationException.CodeMfaClaimMissing
                ? SecurityEventCodes.SsoLoginFailedMfaClaimMissing
                : SecurityEventCodes.SsoLoginFailedTokenInvalid;

            _logger.LogWarning(ex, "OIDC callback redemption failed with reason {ReasonCode}.", ex.ReasonCode);
            await RecordFailureAsync(eventCode, reason: ex.ReasonCode, ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OIDC callback redemption failed unexpectedly.");
            await RecordFailureAsync(
                SecurityEventCodes.SsoLoginFailedTokenInvalid,
                reason: "OIDC token redemption failed.",
                ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }

        var bySubject = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.DirectorySubjectId == identity.SubjectId, ct);

        User? byUsername = null;
        if (bySubject is null && !string.IsNullOrWhiteSpace(identity.Username))
        {
            var normalizedUsername = identity.Username.Trim().ToLowerInvariant();
            byUsername = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(
                    u => u.DirectoryUsername != null && u.DirectoryUsername.ToLower() == normalizedUsername,
                    ct);
        }

        var match = DirectoryUserMatcher.Match(identity, bySubject, byUsername);
        switch (match.Failure)
        {
            case MatchFailure.NotLinked:
                await RecordFailureAsync(
                    SecurityEventCodes.SsoLoginFailedNotLinked,
                    targetUsername: identity.Username,
                    reason: "No local account is linked to this directory identity.",
                    ct: ct);
                return BuildFailureRedirect(frontendBaseUrl);

            case MatchFailure.SubjectMismatch:
                // A different person now holds this company username: the administrator must see it.
                if (match.User is not null)
                    await DirectoryAccountActions.MarkSignInRefusedAsync(
                        _db, match.User, nowUtc, "OidcSignIn", "Directory subject ID does not match the bound user", ct);
                await RecordFailureAsync(
                    SecurityEventCodes.SsoLoginFailedSubjectMismatch,
                    targetUserId: match.User?.Id,
                    targetUsername: match.User?.Username,
                    reason: "Directory subject ID does not match the bound user.",
                    ct: ct);
                return BuildFailureRedirect(frontendBaseUrl);

            case MatchFailure.UserInactive:
                await RecordFailureAsync(
                    SecurityEventCodes.LoginFailedAccountDisabled,
                    targetUserId: match.User?.Id,
                    targetUsername: match.User?.Username,
                    reason: "User account is inactive.",
                    ct: ct);
                return BuildFailureRedirect(frontendBaseUrl);
        }

        var user = match.User;
        if (user is null)
            return BuildFailureRedirect(frontendBaseUrl);

        if (user.IsLockedAt(nowUtc))
        {
            await RecordFailureAsync(
                SecurityEventCodes.LoginFailedAccountLocked,
                targetUserId: user.Id,
                targetUsername: user.Username,
                reason: "User account is locked.",
                ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }

        // Only accounts set to company sign-in may use it. Emergency accounts are
        // always Local (database check), so they are refused here as well.
        if (user.SignInMethod != SignInMethod.Directory || user.IsEmergencyAccount)
        {
            await RecordFailureAsync(
                SecurityEventCodes.SsoLoginFailedNotLinked,
                targetUserId: user.Id,
                targetUsername: user.Username,
                reason: user.IsEmergencyAccount
                    ? "Emergency accounts must sign in locally."
                    : "Account is not set to company sign-in.",
                ct: ct);
            return BuildFailureRedirect(frontendBaseUrl);
        }

        var rawExchangeCode = GenerateRandomBase64Url(32);
        await UnitOfWork.RunAsync(_db, async () =>
        {
            if (match.BindSubjectNow)
            {
                _db.CurrentUserId = user.Id;
                _db.CurrentReason = "User directory linked";
                user.DirectorySubjectId = identity.SubjectId;

                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.UserDirectoryLinked,
                    SecurityEventOutcome.Success,
                    ActorUserId: user.Id,
                    TargetUserId: user.Id,
                    TargetUsername: user.Username));
            }

            _db.SsoExchangeCodes.Add(new SsoExchangeCode
            {
                UserId = user.Id,
                CodeHash = AuthenticationService.Hash(rawExchangeCode),
                CreatedAt = nowUtc,
                ExpiresAt = nowUtc.Add(ExchangeCodeLifetime)
            });

            await _db.SaveChangesAsync(ct);
        });

        var baseUrl = frontendBaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}/sso-complete#code={Uri.EscapeDataString(rawExchangeCode)}");
    }

    public async Task<LoginOutcome> ExchangeAsync(
        string? exchangeCode,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        await EnsureOidcModeAsync(ct);

        if (string.IsNullOrWhiteSpace(exchangeCode))
            return new LoginOutcome(false, null, null, "Invalid or expired SSO exchange code.");

        var codeHash = AuthenticationService.Hash(exchangeCode);
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        var consumed = await _db.SsoExchangeCodes
            .Where(c => c.CodeHash == codeHash && c.ConsumedAt == null && c.ExpiresAt > nowUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedAt, nowUtc), ct);

        if (consumed != 1)
        {
            await RecordFailureAsync(
                SecurityEventCodes.SsoLoginFailedTokenInvalid,
                reason: "SSO exchange code missing, expired, or already used.",
                ct: ct);
            return new LoginOutcome(false, null, null, "Invalid or expired SSO exchange code.");
        }

        var codeRow = await _db.SsoExchangeCodes
            .AsNoTracking()
            .FirstAsync(c => c.CodeHash == codeHash, ct);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == codeRow.UserId, ct);

        if (user is null || !user.IsActive || user.IsLockedAt(nowUtc) || user.IsEmergencyAccount || user.SignInMethod != SignInMethod.Directory)
        {
            await RecordFailureAsync(
                SecurityEventCodes.LoginFailedAccountDisabled,
                targetUserId: user?.Id,
                targetUsername: user?.Username,
                reason: "User ineligible to complete SSO exchange.",
                ct: ct);
            return new LoginOutcome(false, null, null, "Invalid or expired SSO exchange code.");
        }

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var outcome = await _authService.CompleteLoginAsync(
                user,
                ipAddress,
                userAgent,
                mfaVerified: true,
                loginHistoryMethod: "Oidc");

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.SsoLoginSuccess,
                SecurityEventOutcome.Success,
                ActorUserId: user.Id,
                TargetUserId: user.Id,
                TargetUsername: user.Username));
            await _db.SaveChangesAsync(ct);

            return outcome;
        });
    }

    private async Task EnsureOidcModeAsync(CancellationToken ct)
    {
        var mode = await _ssoMode.GetModeAsync(ct);
        if (mode != SsoMode.OpenIdConnect)
            throw new SsoModeNotEnabledException(mode);
    }

    private async Task RecordFailureAsync(
        string eventCode,
        int? targetUserId = null,
        string? targetUsername = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        _securityAudit.Record(new SecurityEventRequest(
            eventCode,
            SecurityEventOutcome.Failure,
            TargetUserId: targetUserId,
            TargetUsername: targetUsername,
            Reason: reason));
        await _db.SaveChangesAsync(ct);
    }

    private static Uri BuildFailureRedirect(string frontendBaseUrl) =>
        new($"{frontendBaseUrl.TrimEnd('/')}/sso-complete#error=signin_failed");

    private static string GenerateRandomBase64Url(int byteCount) =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
