using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Active Directory sign-in on the shared login form. Order is fixed: find the MicroLIMS user by
/// company username, check MicroLIMS's own lockout (a locked account never reaches the directory, so
/// MicroLIMS cannot be used to lock out a company account by guessing), bind as the user, read the
/// directory's permanent id and disabled flag, match the user, then continue with the second-factor
/// policy and issue the session. The typed password is only ever passed to <see cref="ILdapDirectory.BindAsync"/>.
/// </summary>
public sealed class LdapSignInService : IDirectoryPasswordSignIn
{
    public const string UnavailableMessage = "Company sign-in is unavailable — contact an administrator.";

    // Unknown company usernames cost one BCrypt check, like an unknown local username does.
    private static readonly Lazy<string> TimingEqualiserHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    private readonly IMicroLimsDbContext _db;
    private readonly ILdapDirectory _directory;
    private readonly SsoModeService _ssoMode;
    private readonly Func<AuthenticationService> _auth;
    private readonly ISecurityAuditService _securityAudit;
    private readonly TimeProvider _time;
    private readonly ILogger<LdapSignInService> _logger;

    // The authentication service owns the login form and calls this service, so it is
    // supplied lazily to avoid a construction cycle.
    public LdapSignInService(
        IMicroLimsDbContext db,
        ILdapDirectory directory,
        SsoModeService ssoMode,
        Func<AuthenticationService> authService,
        ISecurityAuditService securityAudit,
        TimeProvider? timeProvider = null,
        ILogger<LdapSignInService>? logger = null)
    {
        _db = db;
        _directory = directory;
        _ssoMode = ssoMode;
        _auth = authService;
        _securityAudit = securityAudit;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<LdapSignInService>.Instance;
    }

    public async Task<LoginOutcome> SignInAsync(
        string username,
        string password,
        string? ipAddress,
        string? userAgent,
        string? deviceToken,
        CancellationToken ct = default)
    {
        var auth = _auth();
        var typed = username ?? string.Empty;
        var companyUsername = User.NormalizeDirectoryUsername(typed);

        var mode = await _ssoMode.GetModeAsync(ct);
        if (mode != SsoMode.ActiveDirectory)
        {
            // Defensive: only the login form calls this, and only in Active Directory mode.
            return Refused();
        }

        var user = companyUsername is null
            ? null
            : await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.DirectoryUsername == companyUsername, ct);

        if (user is null)
        {
            // No bind for a name nobody is linked to.
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.SsoLoginFailedNotLinked,
                SecurityEventOutcome.Failure,
                TargetUsername: typed,
                Reason: "No local account is linked to this company username."));
            await auth.RecordLoginAsync(null, typed, false, "Not linked", ipAddress);
            BCrypt.Net.BCrypt.Verify(password ?? string.Empty, TimingEqualiserHash.Value);
            return Refused();
        }

        var nowUtc = _time.GetUtcNow().UtcDateTime;

        if (user.SignInMethod != SignInMethod.Directory || user.IsEmergencyAccount)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.SsoLoginFailedNotLinked,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: user.IsEmergencyAccount
                    ? "Emergency accounts must sign in locally."
                    : "Account is not set to company sign-in."));
            await auth.RecordLoginAsync(user.Id, user.Username, false, "Not set to company sign-in", ipAddress);
            return Refused();
        }

        if (user.IsLockedAt(nowUtc))
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LoginFailedAccountLocked,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username));
            await auth.RecordLoginAsync(user.Id, user.Username, false, "Account locked", ipAddress);
            return Refused();
        }

        if (!user.IsActive)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.LoginFailedAccountDisabled,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username));
            await auth.RecordLoginAsync(user.Id, user.Username, false, "Account inactive", ipAddress);
            return Refused();
        }

        // The stored, normalised company username is what is looked up in the directory.
        var directoryName = user.DirectoryUsername!;

        bool bound;
        LdapEntry? entry = null;
        try
        {
            bound = await _directory.BindAsync(directoryName, password ?? string.Empty, ct);
            if (bound)
                entry = await _directory.FindAsync(directoryName, ct);
        }
        catch (DirectoryUnavailableException ex)
        {
            // Not the user's fault: the lockout counter is untouched. The exception message never holds a password.
            _logger.LogWarning("Company directory unavailable during sign-in ({ExceptionType}).", ex.GetType().Name);
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.DirectoryUnavailable,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: "The company directory could not be reached or answered."));
            await auth.RecordLoginAsync(user.Id, user.Username, false, "Directory unavailable", ipAddress);
            return new LoginOutcome(false, null, null, UnavailableMessage);
        }

        if (!bound)
        {
            await auth.ApplyFailedLoginAttemptAndLockoutAsync(user, SecurityEventCodes.LoginFailedBadCredentials, ct);
            await _db.SaveChangesAsync(ct);
            await auth.RecordLoginAsync(user.Id, user.Username, false, "Wrong password", ipAddress);
            return Refused();
        }

        if (entry is null)
        {
            // Bound a moment ago but cannot be read back: refuse rather than guess an identity.
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.SsoLoginFailedNotLinked,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: "The directory entry could not be read after sign-in."));
            await auth.RecordLoginAsync(user.Id, user.Username, false, "Directory entry not found", ipAddress);
            return Refused();
        }

        if (entry.Disabled)
        {
            await DeactivateAsync(auth, user, nowUtc, ipAddress, ct);
            return Refused();
        }

        var bySubject = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.DirectorySubjectId == entry.SubjectId, ct);

        var match = DirectoryUserMatcher.Match(
            new DirectoryIdentity(entry.SubjectId, entry.Username, MfaSatisfied: false), bySubject, user);

        // The permanent id wins in the matcher; it must still be the person who typed the name.
        if (match.Failure == MatchFailure.None && match.User?.Id != user.Id)
            match = new MatchResult(match.User, MatchFailure.SubjectMismatch, BindSubjectNow: false);

        if (match.Failure != MatchFailure.None)
        {
            var eventCode = match.Failure switch
            {
                MatchFailure.SubjectMismatch => SecurityEventCodes.SsoLoginFailedSubjectMismatch,
                MatchFailure.UserInactive => SecurityEventCodes.LoginFailedAccountDisabled,
                _ => SecurityEventCodes.SsoLoginFailedNotLinked
            };
            _securityAudit.Record(new SecurityEventRequest(
                eventCode,
                SecurityEventOutcome.Failure,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: match.Failure == MatchFailure.SubjectMismatch
                    ? "Directory subject ID does not match the bound user."
                    : "Directory identity could not be matched."));
            await auth.RecordLoginAsync(user.Id, user.Username, false, $"Directory match failed ({match.Failure})", ipAddress);
            return Refused();
        }

        if (match.BindSubjectNow)
        {
            _db.CurrentUserId = user.Id;
            _db.CurrentReason = "User directory linked";
            user.DirectorySubjectId = entry.SubjectId;
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.UserDirectoryLinked,
                SecurityEventOutcome.Success,
                ActorUserId: user.Id,
                TargetUserId: user.Id,
                TargetUsername: user.Username));
            await _db.SaveChangesAsync(ct);
        }

        return await auth.ContinueAfterPrimaryAuthenticationAsync(
            user, mode, ipAddress, userAgent, deviceToken, AuthenticationService.LdapLoginMethod);
    }

    // The directory says the account is disabled: the person has left or been suspended, so
    // MicroLIMS closes the account and every open session. The refusal gives nothing away.
    private async Task DeactivateAsync(AuthenticationService auth, User user, DateTime nowUtc, string? ipAddress, CancellationToken ct)
    {
        _db.CurrentUserId = null;
        _db.CurrentSystemProcessName = "LdapSignIn";
        _db.CurrentReason = "Account disabled in the company directory";
        try
        {
            user.IsActive = false;
            user.DirectorySignInRefusedAt = nowUtc;
            var revoked = await auth.RevokeAllRefreshTokensAsync(user.Id);

            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.UserDeactivatedByDirectory,
                SecurityEventOutcome.Success,
                ActorIsSystem: true,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: "Account disabled in the company directory.",
                Metadata: $"{{\"revokedSessionCount\":{revoked}}}"));
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _db.CurrentSystemProcessName = null;
            _db.CurrentReason = null;
        }

        await auth.RecordLoginAsync(user.Id, user.Username, false, "Disabled in the company directory", ipAddress);
    }

    private static LoginOutcome Refused() =>
        new(false, null, null, AuthenticationService.LoginFailedMessage);
}
