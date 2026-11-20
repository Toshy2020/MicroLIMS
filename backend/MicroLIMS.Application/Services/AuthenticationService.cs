using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Application.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Validation;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MicroLIMS.Application.Services;

// Login + account locking + login history + JWT refresh + password
// reset (gap analysis "Missing Security").
public class AuthenticationService : IAuthenticationService
{
    public const int MaxFailedAttempts = 5;
    public const int PasswordHistoryLimit = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    // Every failed login - unknown user, locked, disabled, wrong password -
    // gets this same message and costs the same one BCrypt check, so the
    // response says nothing about whether, or in what state, an account
    // exists. (The frontend already shows one message for every 401; the
    // real reason is in the security audit trail and login history.)
    internal const string LoginFailedMessage = "Invalid username or password.";

    // LoginHistory.Method for a sign-in verified by the company directory.
    internal const string LdapLoginMethod = "Ldap";

    // A non-emergency user signing in while Active Directory mode is on was verified by
    // the directory, including after the second factor. Local sign-ins record no method.
    internal static string? LoginMethodFor(SsoMode mode, User user) =>
        mode == SsoMode.ActiveDirectory && !user.IsEmergencyAccount ? LdapLoginMethod : null;

    // A stored hash BCrypt cannot parse is a password that cannot match -
    // not a server error. Now that locked and disabled accounts are also
    // checked (above), such an account must still get the ordinary refusal.
    internal static bool PasswordMatches(string password, string storedHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    // Verified against when the username does not exist, so an unknown user
    // costs the same BCrypt work as a real one. Default work factor, as used
    // for real password hashes; created once, never matches anything.
    private static readonly Lazy<string> TimingEqualiserHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    private readonly IMicroLimsDbContext _db;
    private readonly AccessTokenIssuer _tokenIssuer;
    private readonly PermissionService _permissionService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly ISecurityAuditService _securityAudit;
    private readonly TimeProvider _time;
    private readonly ISystemSettingsReader? _settings;
    private readonly MfaPolicyService _mfaPolicy;
    private readonly SsoModeService _ssoMode;
    private readonly IDirectoryPasswordSignIn? _directorySignIn;

    public AuthenticationService(IMicroLimsDbContext db, AccessTokenIssuer tokenIssuer, PermissionService permissionService, IEmailSender emailSender, ILogger<AuthenticationService> logger, ISecurityAuditService securityAudit, TimeProvider? timeProvider = null, ISystemSettingsReader? settings = null, MfaPolicyService? mfaPolicy = null, SsoModeService? ssoMode = null, IDirectoryPasswordSignIn? directorySignIn = null)
    {
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _tokenIssuer = tokenIssuer;
        _permissionService = permissionService;
        _emailSender = emailSender;
        _logger = logger;
        _securityAudit = securityAudit;
        _mfaPolicy = mfaPolicy ?? new MfaPolicyService(settings);
        _ssoMode = ssoMode ?? new SsoModeService(db, settings, securityAudit);
        _directorySignIn = directorySignIn;
    }

    public Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null, string? userAgent = null, string? deviceToken = null) =>
        UnitOfWork.RunAsync(_db, () => LoginCoreAsync(username, password, ipAddress, userAgent, deviceToken));

    private async Task<LoginOutcome> LoginCoreAsync(string username, string password, string? ipAddress = null, string? userAgent = null, string? deviceToken = null)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username);

        // In Active Directory mode the form is shared and the name typed is the
        // company username. Only an emergency account (looked up by its MicroLIMS
        // username) keeps the local path; everyone else goes to the directory,
        // whether or not a MicroLIMS user with that exact username exists.
        var ssoMode = await _ssoMode.GetModeAsync();
        if (ssoMode == SsoMode.ActiveDirectory && _directorySignIn is not null && user is not { IsEmergencyAccount: true })
            return await _directorySignIn.SignInAsync(username, password, ipAddress, userAgent, deviceToken);

        if (user is null)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LoginFailedUnknownUser, SecurityEventOutcome.Failure,
                TargetUsername: username));
            await RecordLoginAsync(null, username, false, "User not found", ipAddress);
            BCrypt.Net.BCrypt.Verify(password, TimingEqualiserHash.Value);
            return new LoginOutcome(false, null, null, LoginFailedMessage);
        }

        // Checked for every existing account, whatever its state, before the
        // state is looked at - so locked and disabled accounts take as long
        // to refuse as a wrong password does.
        var passwordMatches = PasswordMatches(password, user.PasswordHash);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        if (user.IsLockedAt(nowUtc))
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LoginFailedAccountLocked, SecurityEventOutcome.Failure,
                TargetUserId: user.Id, TargetUsername: username));
            await RecordLoginAsync(user.Id, username, false, "Account locked", ipAddress);
            return new LoginOutcome(false, null, null, LoginFailedMessage);
        }

        if (!user.IsActive || !passwordMatches)
        {
            await ApplyFailedLoginAttemptAndLockoutAsync(
                user,
                user.IsActive ? SecurityEventCodes.LoginFailedBadCredentials
                              : SecurityEventCodes.LoginFailedAccountDisabled);

            await _db.SaveChangesAsync();
            await RecordLoginAsync(user.Id, username, false, user.IsActive ? "Wrong password" : "Account inactive", ipAddress);
            return new LoginOutcome(false, null, null, LoginFailedMessage);
        }

        if (!_ssoMode.IsLocalLoginAllowed(ssoMode, user))
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LocalLoginRefusedSsoOn,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: $"Local login refused while SSO mode is {ssoMode}."));
            await RecordLoginAsync(user.Id, username, false, $"Local login refused while SSO mode is {ssoMode}", ipAddress);
            return new LoginOutcome(false, null, null, LoginFailedMessage);
        }

        return await ContinueAfterPrimaryAuthenticationAsync(user, ssoMode, ipAddress, userAgent, deviceToken, loginHistoryMethod: null);
    }

    // Everything after the first factor has been verified (a MicroLIMS password, or the
    // company directory's): the second-factor requirement, trusted computer, then login.
    internal async Task<LoginOutcome> ContinueAfterPrimaryAuthenticationAsync(
        User user, SsoMode ssoMode, string? ipAddress, string? userAgent, string? deviceToken, string? loginHistoryMethod)
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var hasVerifiableCredential = await _db.UserMfaCredentials
            .AsNoTracking()
            .AnyAsync(c => c.UserId == user.Id
                && (c.Status == MfaCredentialStatus.Active
                    || (c.Status == MfaCredentialStatus.Pending && c.Type == MfaCredentialType.HardwareToken)));

        var roleType = user.Role?.Type.ToString() ?? string.Empty;
        var requirement = await _mfaPolicy.EvaluateAsync(roleType, hasVerifiableCredential, user.MfaRequiredSince, nowUtc);
        var isEmergencyLogin = ssoMode != SsoMode.Off && user.IsEmergencyAccount;

        if (isEmergencyLogin && !hasVerifiableCredential)
        {
            var mfaToken = MfaService.CreateChallengeCore(_db, _time, _securityAudit, user.Id, MfaChallengePurpose.Enrol);
            await _db.SaveChangesAsync();
            return new LoginOutcome(true, null, null, null, MfaEnrolmentRequired: true, MfaToken: mfaToken);
        }

        if (isEmergencyLogin && requirement.Kind != MfaLoginRequirement.Verify)
            requirement = new MfaRequirement(MfaLoginRequirement.Verify, null);

        if (!isEmergencyLogin
            && requirement.Kind == MfaLoginRequirement.Verify
            && await MfaService.IsTrustedDeviceCoreAsync(_db, _time, _mfaPolicy, user.Id, deviceToken))
        {
            return await CompleteLoginAsync(user, ipAddress, userAgent, mfaVerified: true, loginHistoryMethod);
        }

        switch (requirement.Kind)
        {
            case MfaLoginRequirement.Verify:
            {
                var mfaToken = MfaService.CreateChallengeCore(_db, _time, _securityAudit, user.Id, MfaChallengePurpose.Verify);
                await _db.SaveChangesAsync();
                return new LoginOutcome(true, null, null, null, MfaRequired: true, MfaToken: mfaToken);
            }

            case MfaLoginRequirement.EnrolNow:
            {
                var mfaToken = MfaService.CreateChallengeCore(_db, _time, _securityAudit, user.Id, MfaChallengePurpose.Enrol);
                await _db.SaveChangesAsync();
                return new LoginOutcome(true, null, null, null, MfaEnrolmentRequired: true, MfaToken: mfaToken);
            }

            case MfaLoginRequirement.EnrolWithinGrace:
            {
                user.MfaRequiredSince ??= nowUtc;
                var outcome = await CompleteLoginAsync(user, ipAddress, userAgent, mfaVerified: false, loginHistoryMethod);
                return outcome with { MfaEnrolmentDueAt = requirement.EnrolmentDueAtUtc };
            }

            case MfaLoginRequirement.None:
            default:
                return await CompleteLoginAsync(user, ipAddress, userAgent, mfaVerified: false, loginHistoryMethod);
        }
    }

    public async Task<LoginOutcome> CompleteLoginAsync(User user, string? ip, string? userAgent, bool mfaVerified, string? loginHistoryMethod = null)
    {
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        await RecordLoginAsync(user.Id, user.Username, true, null, ip, loginHistoryMethod);

        var token = await IssueAccessTokenAsync(user);

        // Self-service: the actor and the target are the same person.
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.LoginSuccess, SecurityEventOutcome.Success,
            ActorUserId: user.Id, TargetUserId: user.Id, TargetUsername: user.Username));
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.RefreshTokenIssued, SecurityEventOutcome.Success,
            ActorUserId: user.Id, TargetUserId: user.Id, TargetUsername: user.Username));

        var refreshToken = await IssueRefreshTokenAsync(user.Id, ip, userAgent, usedNow: false, mfaVerified: mfaVerified, sessionStartedAt: _time.GetUtcNow().UtcDateTime);
        var signingPasswordRequired = user.SignInMethod == SignInMethod.Directory && user.SigningPasswordSetAt is null;

        return new LoginOutcome(
            true,
            token,
            refreshToken,
            null,
            user.MustChangePassword,
            user.Role?.Name,
            SigningPasswordRequired: signingPasswordRequired);
    }

    internal async Task ApplyFailedLoginAttemptAndLockoutAsync(User user, string failureEventCode, CancellationToken ct = default)
    {
        user.FailedLoginAttempts++;

        _securityAudit.Record(new SecurityEventRequest(
            failureEventCode,
            SecurityEventOutcome.Failure,
            TargetUserId: user.Id, TargetUsername: user.Username));

        var maxFailedAttempts = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.LockoutMaxFailedAttempts, ct);
        if (user.FailedLoginAttempts >= maxFailedAttempts)
        {
            var lockMinutes = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.LockoutDurationMinutes, ct);
            user.LockedUntil = _time.GetUtcNow().UtcDateTime.AddMinutes(lockMinutes);
            // Locking the account has to end the sessions it already
            // has, or the lockout only blocks the login form while an
            // existing refresh token keeps minting new access tokens.
            var revoked = await RevokeAllRefreshTokensAsync(user.Id);

            // The system locked the account, not a person - so there is
            // no actor user, and ActorIsSystem says why it is absent.
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.AccountLocked, SecurityEventOutcome.Success,
                ActorIsSystem: true, TargetUserId: user.Id, TargetUsername: user.Username,
                Reason: $"Automatic lockout after {maxFailedAttempts} failed login attempts.",
                Metadata: $"{{\"revokedSessionCount\":{revoked}}}"));
        }
    }

    public Task<LoginOutcome> RefreshAsync(string refreshToken, string? ipAddress = null, string? userAgent = null) =>
        UnitOfWork.RunAsync(_db, () => RefreshCoreAsync(refreshToken, ipAddress, userAgent));

    private async Task<LoginOutcome> RefreshCoreAsync(string refreshToken, string? ipAddress, string? userAgent)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.Include(r => r.User).ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored is null || !stored.IsActiveAt(_time.GetUtcNow().UtcDateTime))
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
        if (user is null || !user.IsActive || user.IsLockedAt(_time.GetUtcNow().UtcDateTime))
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.RefreshRejectedAccountBlocked, SecurityEventOutcome.Failure,
                TargetUserId: stored.UserId, TargetUsername: user?.Username,
                Reason: user is null ? "Account no longer exists."
                    : !user.IsActive ? "Account is disabled." : "Account is locked."));
            await _db.SaveChangesAsync();
            return new LoginOutcome(false, null, null, "Refresh token is invalid or expired.");
        }

        // Company sign-in sessions end at a fixed age. In OpenID Connect mode the provider
        // never tells MicroLIMS an account was disabled, so the only control is to send the
        // user back to the provider regularly. Emergency accounts and local users are never
        // capped; in Active Directory mode the leaver worker checks the directory instead.
        var nowForCap = _time.GetUtcNow().UtcDateTime;
        // A row from before the cap existed has no start: its own creation time is the
        // earliest the session can have begun, and it is carried forward so the cap bites.
        var sessionStartedAt = stored.SessionStartedAt ?? stored.CreatedAt;
        if (user.SignInMethod == SignInMethod.Directory
            && !user.IsEmergencyAccount
            && await _ssoMode.GetModeAsync() == SsoMode.OpenIdConnect)
        {
            var maxHours = Math.Clamp(await _settings.GetIntOrDefaultAsync(SystemSettingKeys.SsoSessionMaxHours), 1, 24);
            if (nowForCap - sessionStartedAt > TimeSpan.FromHours(maxHours))
            {
                // This session only: other sessions carry their own start and are capped on their own.
                stored.RevokedAt = nowForCap;
                _securityAudit.Record(new SecurityEventRequest(
                    SecurityEventCodes.RefreshRejectedInvalidToken, SecurityEventOutcome.Failure,
                    TargetUserId: user.Id, TargetUsername: user.Username,
                    Reason: "session_cap",
                    Metadata: $"{{\"sessionMaxHours\":{maxHours}}}"));
                await _db.SaveChangesAsync();
                return new LoginOutcome(false, null, null, "Refresh token is invalid or expired.");
            }
        }

        // Rotate: revoke the used token and issue a new one. One event,
        // not three - the caller performed a single meaningful action.
        stored.LastUsedAt = _time.GetUtcNow().UtcDateTime;
        stored.RevokedAt = stored.LastUsedAt;
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.RefreshTokenRotated, SecurityEventOutcome.Success,
            ActorUserId: user.Id, TargetUserId: user.Id, TargetUsername: user.Username));

        var newRefreshToken = await IssueRefreshTokenAsync(stored.UserId, ipAddress, userAgent, usedNow: true, mfaVerified: stored.MfaVerified, sessionStartedAt: sessionStartedAt);
        await _db.SaveChangesAsync();

        var token = await IssueAccessTokenAsync(user);
        return new LoginOutcome(true, token, newRefreshToken, null, user.MustChangePassword, user.Role?.Name);
    }

    // The role's CURRENT permission set and PermissionVersion are read in the
    // same call, so the token's pv claim describes the codes it carries.
    // Login and refresh both go through here; AccessTokenRevalidator holds
    // every later request to the pv this stamps.
    //
    // Fails closed: an account whose role could not be loaded gets no token,
    // never a guessed one. This used to fall back to an "Analyst" role claim,
    // which would hand [Authorize(Roles = "Analyst")] endpoints to an account
    // nobody granted that role. The throw rolls back the surrounding unit of
    // work, so the login is not recorded as a success either.
    private async Task<string> IssueAccessTokenAsync(User user)
    {
        var role = user.Role
            ?? throw new UnauthorizedAccessException("This account has no role assigned. Contact your System Administrator.");
        var permissionCodes = await _permissionService.GetPermissionCodesForRoleAsync(role.Id);
        return _tokenIssuer(new AccessTokenSubject(
            user.Id.ToString(), role.Type.ToString(),
            user.RoleId, role.PermissionVersion, permissionCodes));
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
        user.SigningPasswordSetAt = _time.GetUtcNow().UtcDateTime;
        await MfaService.RevokeTrustedDevicesCoreAsync(_db, _time, _securityAudit, user.Id, revokedByUserId: user.Id);

        // Self-service change: actor and target are the same person.
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.PasswordChanged, SecurityEventOutcome.Success,
            ActorUserId: userId, TargetUserId: user.Id, TargetUsername: user.Username));

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SetInitialSigningPasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {userId} not found.",
                new Dictionary<string, string> { ["userId"] = userId.ToString(CultureInfo.InvariantCulture) });

        if (user.SignInMethod != SignInMethod.Directory)
            throw new BusinessRuleException(
                MessageCodes.AuthDirectoryOnlyInitialSigningPassword,
                "Only directory accounts can set an initial signing password.");

        if (user.SigningPasswordSetAt is not null)
            throw new BusinessRuleException(
                MessageCodes.AuthSigningPasswordAlreadySet,
                "A signing password has already been set. Use change password instead.");

        await ValidateAndApplyNewPasswordAsync(user, newPassword, revokeExistingSessions: false);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        user.SigningPasswordSetAt = nowUtc;
        _db.CurrentUserId = userId;
        _db.CurrentReason = "Initial signing password set.";

        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.PasswordChanged,
            SecurityEventOutcome.Success,
            ActorUserId: userId,
            TargetUserId: user.Id,
            TargetUsername: user.Username,
            Reason: "Initial signing password set."));

        await _db.SaveChangesAsync(ct);
    }

    // Delivers the raw (unhashed) reset token to the user's email - never
    // returned to the caller and never logged. If the user has no email
    // on file, the token is still created (so a subsequent request
    // doesn't behave differently) but delivery is impossible; that's
    // logged server-side as a warning so an admin can notice and fix it.
    public async Task RequestPasswordResetAsync(string username)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username)
            ?? throw new BusinessRuleException(
                MessageCodes.AuthPasswordResetIfAccountExists,
                "If that account exists, a reset link has been sent."); // don't leak existence

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = _time.GetUtcNow().UtcDateTime.AddMinutes(await _settings.GetIntOrDefaultAsync(SystemSettingKeys.PasswordResetLifetimeMinutes));
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
        if (record is null || !record.IsValidAt(_time.GetUtcNow().UtcDateTime)) return false;

        await ValidateAndApplyNewPasswordAsync(record.User!, newPassword);
        record.UsedAt = _time.GetUtcNow().UtcDateTime;
        await MfaService.RevokeTrustedDevicesCoreAsync(_db, _time, _securityAudit, record.User!.Id, revokedByUserId: null);

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
        return new CurrentUserInfo(
            user.Id,
            user.Username,
            user.FullName,
            user.Role?.Type.ToString() ?? "Analyst",
            user.JobTitle,
            user.LastLoginAt,
            user.PasswordChangedAt,
            user.MustChangePassword,
            user.Role?.Name ?? string.Empty,
            user.SignInMethod.ToString(),
            user.SigningPasswordSetAt is not null);
    }

    // Shared by ChangePasswordAsync, SetInitialSigningPasswordAsync, and ConfirmPasswordResetAsync:
    // enforces the password policy, rejects reuse of the last N hashes, then applies
    // the new hash and records/prunes history. Caller is responsible for
    // SaveChangesAsync so it can be combined with its own changes in one transaction.
    private async Task ValidateAndApplyNewPasswordAsync(User user, string newPassword, bool revokeExistingSessions = true)
    {
        var failures = PasswordPolicy.ValidateCoded(newPassword, await _settings.GetIntOrDefaultAsync(SystemSettingKeys.PasswordMinimumLength));
        if (failures.Count > 0)
            throw BusinessRuleException.FromDetails(failures);

        var historyLimit = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.PasswordHistoryLimit);
        var existingHistory = await _db.PasswordHistories
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        if (existingHistory.Take(historyLimit).Any(h => BCrypt.Net.BCrypt.Verify(newPassword, h.PasswordHash)))
            throw new BusinessRuleException(
                MessageCodes.AuthPasswordHistoryReused,
                $"New password must not match any of your last {historyLimit} passwords.",
                new Dictionary<string, string> { ["historyLimit"] = historyLimit.ToString(CultureInfo.InvariantCulture) });

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordHash = newHash;
        if (revokeExistingSessions)
            user.PasswordChangedAt = _time.GetUtcNow().UtcDateTime;
        user.MustChangePassword = false;

        _db.PasswordHistories.Add(new PasswordHistory { UserId = user.Id, PasswordHash = newHash });
        if (existingHistory.Count >= historyLimit)
            _db.PasswordHistories.RemoveRange(existingHistory.Skip(historyLimit - 1));

        if (revokeExistingSessions)
        {
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
                stored.RevokedAt ??= _time.GetUtcNow().UtcDateTime;
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
        var now = _time.GetUtcNow().UtcDateTime;
        var active = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > now)
            .ToListAsync();

        foreach (var token in active)
            token.RevokedAt = now;

        return active.Count;
    }

    internal static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private async Task<string> IssueRefreshTokenAsync(int userId, string? ipAddress = null, string? userAgent = null, bool usedNow = false, bool mfaVerified = false, DateTime? sessionStartedAt = null)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = _time.GetUtcNow().UtcDateTime.AddDays(await _settings.GetIntOrDefaultAsync(SystemSettingKeys.RefreshTokenLifetimeDays)),
            CreatedIp = Truncate(ipAddress, RefreshToken.MaxIpLength),
            UserAgent = Truncate(userAgent, RefreshToken.MaxUserAgentLength),
            LastUsedAt = usedNow ? _time.GetUtcNow().UtcDateTime : null,
            MfaVerified = mfaVerified,
            SessionStartedAt = sessionStartedAt
        });
        await _db.SaveChangesAsync();
        return rawToken;
    }

    internal async Task RecordLoginAsync(int? userId, string username, bool success, string? reason, string? ipAddress, string? method = null)
    {
        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            Username = username,
            Success = success,
            FailureReason = reason,
            IpAddress = ipAddress,
            Method = method
        });
        await _db.SaveChangesAsync();
    }

    internal static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
