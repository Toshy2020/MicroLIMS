using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Validation;

namespace MicroLIMS.Application.Services;

// Never carries the token hash.
public record UserSessionDto(int Id, DateTime CreatedAt, string? CreatedIp, string? UserAgent, DateTime? LastUsedAt, DateTime ExpiresAt, bool MfaVerified = false);

public record RoleDto(int Id, string Name, string Type);

public record UserDirectoryDto(
    int Id,
    string FullName,
    string Username,
    string? JobTitle,
    string RoleName,
    bool IsActive);

public record UserDto(
    int Id,
    string FullName,
    string Username,
    string? Email,
    string? JobTitle,
    int RoleId,
    RoleDto? Role,
    bool IsActive,
    bool IsLocked,
    DateTime? LockedUntil,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    DateTime? PasswordChangedAt,
    string MfaState = nameof(MfaUserState.NotRequired),
    string SignInMethod = nameof(Domain.Enums.SignInMethod.Local),
    string? DirectoryUsername = null,
    bool IsEmergencyAccount = false,
    string LinkState = nameof(DirectoryLinkState.NotLinked))
{
    // The record's row version, sent back as If-Match with an edit.
    public uint Version { get; init; }
}

// The sign-in fields of a user edit. A null field means "leave as it is";
// an empty DirectoryUsername clears the company username.
public record UserSignInChange(SignInMethod? SignInMethod, string? DirectoryUsername, bool? IsEmergencyAccount);

public class UserService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IAuthenticationService _authService;
    private readonly ISecurityAuditService _securityAudit;
    private readonly AdministrationContinuityGuard _continuityGuard;
    private readonly IElectronicSignatureService _signatureService;
    private readonly TimeProvider _time;

    private readonly ISystemSettingsReader? _settings;
    private readonly MfaPolicyService _mfaPolicy;

    public UserService(IMicroLimsDbContext db, IAuthenticationService authService, ISecurityAuditService securityAudit, TimeProvider? timeProvider = null, AdministrationContinuityGuard? continuityGuard = null, IElectronicSignatureService? signatureService = null, ISystemSettingsReader? settings = null, MfaPolicyService? mfaPolicy = null)
    {
        _settings = settings;
        _mfaPolicy = mfaPolicy ?? new MfaPolicyService(_settings);
        _time = timeProvider ?? TimeProvider.System;
        _signatureService = signatureService ?? new ElectronicSignatureService(db, _time);
        _continuityGuard = continuityGuard ?? new AdministrationContinuityGuard(db);
        _securityAudit = securityAudit;
        _db = db;
        _authService = authService;
    }

    // Admin list. "active" (default) and "inactive" are kept apart: inactive
    // users are only surfaced on the admin Inactive list (for reactivation).
    // "all" exists solely for screens that resolve historical attribution
    // (a name for a stored user id) and must never feed a picker or roster.
    public async Task<List<UserDto>> GetAllAsync(string? status = null)
    {
        bool? wantActive = (status?.Trim().ToLowerInvariant()) switch
        {
            null or "" or "active" => true,
            "inactive" => false,
            "all" => null,
            _ => throw new BusinessRuleException(
                MessageCodes.UserStatusFilterInvalid,
                "Status must be 'active', 'inactive' or 'all'.")
        };
        var query = _db.Users.AsNoTracking().Include(u => u.Role).AsQueryable();
        if (wantActive.HasValue) query = query.Where(u => u.IsActive == wantActive.Value);
        var users = await query.OrderBy(u => u.Id).ToListAsync();
        return await ToDtosAsync(users);
    }

    // Document Control assignment pickers ask for only the users whose role
    // holds the code the assigned function needs (permission = that code).
    public static readonly IReadOnlySet<string> DirectoryPermissionFilters = new HashSet<string>
    {
        PermissionConstants.DocumentsDraftEdit, PermissionConstants.DocumentsRevisionCreate, PermissionConstants.DocumentsReview,
        PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsPeriodicReview
    };

    public async Task<List<UserDirectoryDto>> GetDirectoryAsync(string? requiredPermission = null)
    {
        if (!string.IsNullOrWhiteSpace(requiredPermission) && !DirectoryPermissionFilters.Contains(requiredPermission))
            throw new BusinessRuleException(
                MessageCodes.UserDirectoryPermissionFilterNotAllowed,
                $"Directory cannot be filtered by permission '{requiredPermission}'.",
                new Dictionary<string, string> { ["requiredPermission"] = requiredPermission });

        var query = _db.Users.Include(u => u.Role).Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(requiredPermission))
            query = query.Where(u => _db.RolePermissions.Any(rp => rp.RoleId == u.RoleId && rp.Permission!.Code == requiredPermission));
        var users = await query
            .OrderBy(u => u.FullName)
            .AsNoTracking()
            .ToListAsync();

        return users.Select(u => new UserDirectoryDto(
            u.Id,
            u.FullName,
            u.Username,
            u.JobTitle,
            u.Role?.Name ?? "Staff",
            u.IsActive
        )).ToList();
    }

    // Everyone the acting user may assign to tests (AnalystAssignmentRules):
    // Analysts, Reviewers, and the acting user themselves if a Section Head.
    public async Task<List<UserDto>> GetEligibleAnalystsAsync(int actingUserId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var users = await AnalystAssignmentRules.Candidates(_db, actingUserId, now)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return await ToDtosAsync(users);
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {id} not found.",
                new Dictionary<string, string> { ["userId"] = id.ToString(CultureInfo.InvariantCulture) });
        return await ToDtoAsync(user);
    }

    // requestEmergencyAccount goes through the same rules as SetEmergencyAccountAsync.
    // A brand-new account has no second factor yet, so the request is refused with
    // that rule's message rather than dropped: mark the account once it has enrolled.
    public Task<UserDto> CreateAsync(User user, string? plainPassword, string? reason, bool requestEmergencyAccount = false, int actingUserId = 0, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => CreateCoreAsync(user, plainPassword, reason, requestEmergencyAccount, actingUserId, ct));

    private async Task<UserDto> CreateCoreAsync(User user, string? plainPassword, string? reason, bool requestEmergencyAccount, int actingUserId, CancellationToken ct)
    {
        var trimmedReason = AdminActionReason.Require(reason, "create a user");
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new BusinessRuleException(MessageCodes.UserUsernameRequired, "Username is required.");
        if (string.IsNullOrWhiteSpace(user.FullName))
            throw new BusinessRuleException(MessageCodes.UserFullNameRequired, "Full Name is required.");

        if (user.SignInMethod != SignInMethod.Directory)
        {
            var passwordFailures = PasswordPolicy.Validate(plainPassword ?? string.Empty, await _settings.GetIntOrDefaultAsync(SystemSettingKeys.PasswordMinimumLength));
            if (passwordFailures.Count > 0)
                throw new BusinessRuleException(
                    MessageCodes.UserPasswordPolicyFailed,
                    string.Join(" ", passwordFailures),
                    new Dictionary<string, string> { ["message"] = string.Join(" ", passwordFailures) });
        }

        if (await _db.Users.AnyAsync(u => u.Username == user.Username, ct))
            throw new BusinessRuleException(
                MessageCodes.UserUsernameAlreadyTaken,
                $"Username '{user.Username}' is already taken.",
                new Dictionary<string, string> { ["username"] = user.Username });
        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == user.RoleId, ct)
            ?? throw new BusinessRuleException(MessageCodes.UserSelectedRoleNotFound, "Selected role does not exist.");

        string? directoryUsername = null;
        if (user.SignInMethod == SignInMethod.Directory || !string.IsNullOrWhiteSpace(user.DirectoryUsername))
        {
            if (user.SignInMethod == SignInMethod.Directory && string.IsNullOrWhiteSpace(user.DirectoryUsername))
                throw new BusinessRuleException(
                    MessageCodes.UserCompanyUsernameRequired,
                    "A company username is required for company sign-in.");
            directoryUsername = DirectoryUsernameRules.NormalizeOrThrow(user.DirectoryUsername);
            await EnsureDirectoryUsernameFreeAsync(directoryUsername, exceptUserId: null, ct);
        }
        user.DirectoryUsername = directoryUsername;

        user.IsEmergencyAccount = false;
        if (requestEmergencyAccount)
            await EnsureEmergencyEligibleAsync(user.Id, role.Type, user.SignInMethod, ct);

        if (user.SignInMethod == SignInMethod.Directory)
        {
            var unusableSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(unusableSecret);
            user.MustChangePassword = false;
            user.SigningPasswordSetAt = null;
        }
        else
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword!);
            user.MustChangePassword = true;
        }

        _db.CurrentUserId = actingUserId > 0 ? actingUserId : _db.CurrentUserId;
        _db.CurrentReason = trimmedReason;
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        if (directoryUsername is not null)
        {
            _securityAudit.Record(new SecurityEventRequest(
                SecurityEventCodes.UserDirectoryLinked,
                SecurityEventOutcome.Success,
                ActorUserId: actingUserId > 0 ? actingUserId : null,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: trimmedReason));
            await _db.SaveChangesAsync(ct);
        }

        var reloaded = await _db.Users.Include(u => u.Role).FirstAsync(u => u.Id == user.Id, ct);
        return await ToDtoAsync(reloaded);
    }

    public Task<UserDto> UpdateProfileAsync(int targetUserId, string fullName, string username, string? email, string? jobTitle = null, int actingUserId = 0, string? reason = null, UserSignInChange? signIn = null, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => UpdateProfileCoreAsync(targetUserId, fullName, username, email, jobTitle, actingUserId, reason, signIn, ct));

    private async Task<UserDto> UpdateProfileCoreAsync(int targetUserId, string fullName, string username, string? email, string? jobTitle, int actingUserId, string? reason, UserSignInChange? signIn, CancellationToken ct)
    {
        var trimmedReason = AdminActionReason.Require(reason, "change a user's profile");
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, user);

        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleException(MessageCodes.UserFullNameRequired, "Full Name is required.");
        if (string.IsNullOrWhiteSpace(username))
            throw new BusinessRuleException(MessageCodes.UserUsernameRequired, "Username is required.");

        if (user.Username != username && await _db.Users.AnyAsync(u => u.Username == username && u.Id != targetUserId))
            throw new BusinessRuleException(
                MessageCodes.UserUsernameAlreadyTaken,
                $"Username '{username}' is already taken.",
                new Dictionary<string, string> { ["username"] = username });

        _db.CurrentUserId = actingUserId > 0 ? actingUserId : _db.CurrentUserId;
        _db.CurrentReason = trimmedReason;
        var prevData = new { user.FullName, user.Username, user.Email, user.JobTitle };
        user.FullName = fullName;
        user.Username = username;
        user.Email = email;
        user.JobTitle = jobTitle;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_DATA_UPDATED",
            PreviousValue = JsonSerializer.Serialize(prevData),
            NewValue = JsonSerializer.Serialize(new { FullName = fullName, Username = username, Email = email, JobTitle = jobTitle }),
            Reason = trimmedReason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        if (signIn is not null)
            await ApplySignInChangeAsync(user, signIn, trimmedReason, actingUserId, ct);

        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(user);
    }

    // The form sends every sign-in field on every edit; only what differs is
    // changed and audited. Order matters for combined edits: emergency status
    // is removed before the method changes, and granted after it changes.
    private async Task ApplySignInChangeAsync(User user, UserSignInChange signIn, string reason, int actingUserId, CancellationToken ct)
    {
        var targetMethod = signIn.SignInMethod ?? user.SignInMethod;
        var targetDirectoryUsername = signIn.DirectoryUsername is null
            ? user.DirectoryUsername
            : string.IsNullOrWhiteSpace(signIn.DirectoryUsername)
                ? null
                : DirectoryUsernameRules.NormalizeOrThrow(signIn.DirectoryUsername);
        var targetEmergency = signIn.IsEmergencyAccount ?? user.IsEmergencyAccount;

        if (targetMethod == SignInMethod.Directory && targetDirectoryUsername is null)
            throw new BusinessRuleException(
                MessageCodes.UserCompanyUsernameRequired,
                "A company username is required for company sign-in.");

        if (user.IsEmergencyAccount && !targetEmergency)
            ApplyEmergencyChange(user, false, reason, actingUserId);

        if (targetMethod != user.SignInMethod
            || !string.Equals(targetDirectoryUsername, user.DirectoryUsername, StringComparison.Ordinal))
        {
            if (user.IsEmergencyAccount && targetMethod != SignInMethod.Local)
                throw new BusinessRuleException(
                    MessageCodes.UserEmergencyMustUseLocalSignIn,
                    "Emergency accounts must use Local sign-in.");
            if (targetDirectoryUsername is not null)
                await EnsureDirectoryUsernameFreeAsync(targetDirectoryUsername, user.Id, ct);
            ApplySsoLinkChange(user, targetMethod, targetDirectoryUsername, reason, actingUserId);
        }

        if (!user.IsEmergencyAccount && targetEmergency)
        {
            await EnsureEmergencyEligibleAsync(user.Id, user.Role?.Type, user.SignInMethod, ct);
            ApplyEmergencyChange(user, true, reason, actingUserId);
        }
    }

    // Signed (D7): the administrator re-enters their password and the signature
    // is linked to the affected user. A refused password changes nothing.
    public Task<UserDto> ChangeRoleAsync(int targetUserId, int newRoleId, string? reason, int actingUserId, string? password, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => ChangeRoleCoreAsync(targetUserId, newRoleId, reason, actingUserId, password, ipAddress));

    private async Task<UserDto> ChangeRoleCoreAsync(int targetUserId, int newRoleId, string? rawReason, int actingUserId, string? password, string? ipAddress)
    {
        var reason = AdminActionReason.Require(rawReason, "change a user's role");

        if (actingUserId == targetUserId)
            throw new BusinessRuleException(
                MessageCodes.UserCannotModifyOwnRole,
                "System Administrators cannot modify their own role.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        var newRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == newRoleId)
            ?? throw new BusinessRuleException(MessageCodes.UserSelectedRoleNotFound, "Selected role does not exist.");

        // Self-changes are blocked above, but two administrators could still
        // demote each other until nobody is left.
        await _continuityGuard.EnsureAdministrableAfterAsync(AdministrationChange.UserRoleChanged(targetUserId, newRoleId));

        var prevRoleId = user.RoleId;
        var prevRoleName = user.Role?.Name ?? "Unknown";

        // Signs before anything changes: a wrong password throws, and the
        // refused attempt is recorded by the signature service.
        _db.CurrentUserId = actingUserId;
        _db.CurrentReason = reason;
        await _signatureService.SignAsync(actingUserId, password ?? string.Empty, SignatureMeaning.UserRoleChanged,
            nameof(User), targetUserId, $"{prevRoleName} -> {newRole.Name}: {reason}", ipAddress);

        user.RoleId = newRoleId;

        // A role change alters what GMP-controlled actions the account may
        // perform, so it is security-relevant regardless of whether any
        // session is revoked (it deliberately is not - see the session
        // revocation work: refresh already re-reads the current role).
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.RoleChanged, SecurityEventOutcome.Success,
            ActorUserId: actingUserId, TargetUserId: targetUserId, TargetUsername: user.Username,
            Reason: reason,
            Metadata: $"{{\"fromRole\":\"{prevRoleName}\",\"toRole\":\"{newRole.Name}\"}}"));

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_ROLE_CHANGED",
            PreviousValue = JsonSerializer.Serialize(new { RoleId = prevRoleId, RoleName = prevRoleName }),
            NewValue = JsonSerializer.Serialize(new { RoleId = newRoleId, RoleName = newRole.Name, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync();
        var updatedUser = await _db.Users.Include(u => u.Role).FirstAsync(u => u.Id == targetUserId);
        return await ToDtoAsync(updatedUser);
    }

    public async Task<UserDto> SetStatusAsync(int targetUserId, bool isActive, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, isActive ? "enable a user account" : "disable a user account");

        if (!isActive && actingUserId == targetUserId)
            throw new BusinessRuleException(
                MessageCodes.UserCannotDisableOwnAccount,
                "System Administrators cannot disable their own account.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        if (!isActive)
            await _continuityGuard.EnsureAdministrableAfterAsync(AdministrationChange.UserDisabledOrDeleted(targetUserId));

        _db.CurrentReason = reason;
        var prevStatus = user.IsActive;
        user.IsActive = isActive;
        if (prevStatus != isActive)
            user.DeactivatedAt = isActive ? null : _time.GetUtcNow().UtcDateTime;

        // Disabling has to end the account's existing sessions in the same
        // transaction as the status change - otherwise the user stays
        // blocked from the login form while their refresh token keeps
        // minting new access tokens. Re-enabling revokes nothing: it
        // restores access rather than withdrawing it.
        var revokedSessions = 0;
        if (!isActive)
            revokedSessions = await _authService.RevokeAllRefreshTokensAsync(targetUserId);

        // Two identities: the administrator who acted, and the account
        // acted upon. The GxP AuditLog entry below is unchanged - this
        // records the same action as security evidence, not instead of it.
        _securityAudit.Record(new SecurityEventRequest(
            isActive ? SecurityEventCodes.AccountEnabled : SecurityEventCodes.AccountDisabled,
            SecurityEventOutcome.Success,
            ActorUserId: actingUserId, TargetUserId: targetUserId, TargetUsername: user.Username,
            Reason: reason,
            Metadata: isActive ? null : $"{{\"revokedSessionCount\":{revokedSessions}}}"));

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = isActive ? "USER_ENABLED" : "USER_DISABLED",
            PreviousValue = JsonSerializer.Serialize(new { IsActive = prevStatus }),
            NewValue = JsonSerializer.Serialize(new { IsActive = isActive, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync();
        return await ToDtoAsync(user);
    }

    public async Task DeactivateAsync(int userId)
    {
        await SetStatusAsync(userId, false, "Deactivated via legacy service call", userId);
    }

    public async Task UpdateEmailAsync(int userId, string? email, string? reason)
    {
        var trimmedReason = AdminActionReason.Require(reason, "change a user's email address");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {userId} not found.",
                new Dictionary<string, string> { ["userId"] = userId.ToString(CultureInfo.InvariantCulture) });
        _db.CurrentReason = trimmedReason;
        user.Email = email;
        await _db.SaveChangesAsync();
    }

    public async Task<List<UserSessionDto>> GetActiveSessionsAsync(int targetUserId)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == targetUserId))
            throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        var now = _time.GetUtcNow().UtcDateTime;
        return await _db.RefreshTokens
            .AsNoTracking()
            .Where(r => r.UserId == targetUserId && r.RevokedAt == null && r.ExpiresAt > now)
            .OrderByDescending(r => r.LastUsedAt ?? r.CreatedAt)
            .Select(r => new UserSessionDto(r.Id, r.CreatedAt, r.CreatedIp, r.UserAgent, r.LastUsedAt, r.ExpiresAt, r.MfaVerified))
            .ToListAsync();
    }

    // Returns how many sessions were revoked. Access tokens already issued
    // stay valid until they expire (Jwt:AccessTokenMinutes); only refreshing
    // is cut off immediately.
    public async Task<int> RevokeSessionsAsync(int targetUserId, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, "revoke a user's sessions");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        _db.CurrentReason = reason;
        var revoked = await _authService.RevokeAllRefreshTokensAsync(targetUserId);

        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.SessionRevoked, SecurityEventOutcome.Success,
            ActorUserId: actingUserId, TargetUserId: targetUserId, TargetUsername: user.Username,
            Reason: reason,
            Metadata: $"{{\"revokedSessionCount\":{revoked}}}"));

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_SESSIONS_REVOKED",
            PreviousValue = null,
            NewValue = JsonSerializer.Serialize(new { RevokedSessionCount = revoked, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync();
        return revoked;
    }

    public async Task<UserDto> UnlockUserAsync(int targetUserId, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, "unlock a user account");
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        _db.CurrentReason = reason;
        var prevFailed = user.FailedLoginAttempts;
        var prevLocked = user.LockedUntil;

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.AccountUnlocked, SecurityEventOutcome.Success,
            ActorUserId: actingUserId, TargetUserId: targetUserId, TargetUsername: user.Username,
            Reason: reason));

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_UNLOCKED",
            PreviousValue = JsonSerializer.Serialize(new { FailedLoginAttempts = prevFailed, LockedUntil = prevLocked }),
            NewValue = JsonSerializer.Serialize(new { FailedLoginAttempts = 0, LockedUntil = (DateTime?)null, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync();
        return await ToDtoAsync(user);
    }

    public Task InitiatePasswordResetAsync(int targetUserId, string? reason, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => InitiatePasswordResetCoreAsync(targetUserId, reason, actingUserId));

    private async Task InitiatePasswordResetCoreAsync(int targetUserId, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, "reset a user's password");
        _db.CurrentReason = reason;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        await _authService.RequestPasswordResetAsync(user.Username);

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "ADMIN_PASSWORD_RESET_REQUESTED",
            PreviousValue = null,
            NewValue = JsonSerializer.Serialize(new { TargetUsername = user.Username, HasEmail = !string.IsNullOrWhiteSpace(user.Email), Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync();
    }

    public async Task<UserDto> ForcePasswordChangeAsync(int targetUserId, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, "force a password change");
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        _db.CurrentReason = reason;
        var prevMust = user.MustChangePassword;
        user.MustChangePassword = true;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "FORCE_PASSWORD_CHANGE_SET",
            PreviousValue = JsonSerializer.Serialize(new { MustChangePassword = prevMust }),
            NewValue = JsonSerializer.Serialize(new { MustChangePassword = true }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync();
        return await ToDtoAsync(user);
    }

    public Task<UserDto> SetEmergencyAccountAsync(int targetUserId, bool isEmergencyAccount, string? reason, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => SetEmergencyAccountCoreAsync(targetUserId, isEmergencyAccount, reason, actingUserId));

    private async Task<UserDto> SetEmergencyAccountCoreAsync(int targetUserId, bool isEmergencyAccount, string? reason, int actingUserId)
    {
        var trimmedReason = AdminActionReason.Require(
            reason,
            isEmergencyAccount ? "mark an account as an emergency administrator" : "remove emergency administrator status");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        if (isEmergencyAccount)
            await EnsureEmergencyEligibleAsync(targetUserId, user.Role?.Type, user.SignInMethod, CancellationToken.None);

        ApplyEmergencyChange(user, isEmergencyAccount, trimmedReason, actingUserId);

        await _db.SaveChangesAsync();
        return await ToDtoAsync(user);
    }

    // The emergency-account rules, in the order their messages are given.
    // A user not yet saved (id 0) has no second factor and is refused.
    private async Task EnsureEmergencyEligibleAsync(int userId, RoleType? roleType, SignInMethod signInMethod, CancellationToken ct)
    {
        if (roleType != RoleType.SystemAdministrator)
            throw new BusinessRuleException(
                MessageCodes.UserEmergencyAdminOnly,
                "Only a System Administrator account can be marked as an emergency account.");

        if (signInMethod != SignInMethod.Local)
            throw new BusinessRuleException(
                MessageCodes.UserEmergencyMustUseLocalSignIn,
                "Emergency accounts must use Local sign-in.");

        var hasActiveMfa = userId > 0 && await _db.UserMfaCredentials
            .AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Active, ct);
        if (!hasActiveMfa)
            throw new BusinessRuleException(
                MessageCodes.UserEmergencyRequiresActiveMfa,
                "An emergency account must have an active second factor.");

        var otherEmergencyCount = await _db.Users
            .AsNoTracking()
            .CountAsync(u => u.Id != userId && u.IsEmergencyAccount, ct);
        if (otherEmergencyCount >= 3)
            throw new BusinessRuleException(
                MessageCodes.UserEmergencyMaxCountReached,
                "At most 3 emergency administrator accounts may be configured.");
    }

    private void ApplyEmergencyChange(User user, bool isEmergencyAccount, string reason, int actingUserId)
    {
        _db.CurrentUserId = actingUserId > 0 ? actingUserId : null;
        _db.CurrentReason = reason;
        var prevEmergency = user.IsEmergencyAccount;
        user.IsEmergencyAccount = isEmergencyAccount;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "USER_EMERGENCY_ACCOUNT_CHANGED",
            PreviousValue = JsonSerializer.Serialize(new { IsEmergencyAccount = prevEmergency }),
            NewValue = JsonSerializer.Serialize(new { IsEmergencyAccount = isEmergencyAccount, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });
    }

    public Task<UserDto> UpdateSsoLinkAsync(int targetUserId, SignInMethod signInMethod, string? directoryUsername, string? reason, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => UpdateSsoLinkCoreAsync(targetUserId, signInMethod, directoryUsername, reason, actingUserId));

    private async Task<UserDto> UpdateSsoLinkCoreAsync(int targetUserId, SignInMethod signInMethod, string? directoryUsername, string? reason, int actingUserId)
    {
        var trimmedReason = AdminActionReason.Require(reason, "update a user's sign-in method");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        var normalizedDirectoryUsername = User.NormalizeDirectoryUsername(directoryUsername);
        if (user.IsEmergencyAccount && signInMethod != SignInMethod.Local)
            throw new BusinessRuleException(
                MessageCodes.UserEmergencyMustUseLocalSignIn,
                "Emergency accounts must use Local sign-in.");

        if (normalizedDirectoryUsername is not null)
            await EnsureDirectoryUsernameFreeAsync(normalizedDirectoryUsername, targetUserId, CancellationToken.None);

        ApplySsoLinkChange(user, signInMethod, normalizedDirectoryUsername, trimmedReason, actingUserId);

        await _db.SaveChangesAsync();
        return await ToDtoAsync(user);
    }

    private async Task EnsureDirectoryUsernameFreeAsync(string normalizedDirectoryUsername, int? exceptUserId, CancellationToken ct)
    {
        if (await _db.Users.AsNoTracking().AnyAsync(
                u => u.DirectoryUsername == normalizedDirectoryUsername && (exceptUserId == null || u.Id != exceptUserId),
                ct))
        {
            throw new BusinessRuleException(
                MessageCodes.UserDirectoryUsernameAlreadyLinked,
                $"Directory username '{normalizedDirectoryUsername}' is already linked to another user.",
                new Dictionary<string, string> { ["directoryUsername"] = normalizedDirectoryUsername });
        }
    }

    // Changes the method and/or company username of a tracked user and writes
    // the audit row and security event. Saving is the caller's. Used by the
    // user form, the link endpoint and the link import (which passes the
    // user's current method, so only the company username changes).
    internal void ApplySsoLinkChange(User user, SignInMethod signInMethod, string? normalizedDirectoryUsername, string reason, int actingUserId)
    {
        _db.CurrentUserId = actingUserId > 0 ? actingUserId : null;
        _db.CurrentReason = reason;

        var prevMethod = user.SignInMethod;
        var prevDirectoryUsername = user.DirectoryUsername;
        var prevSubjectId = user.DirectorySubjectId;
        user.SignInMethod = signInMethod;
        user.DirectoryUsername = normalizedDirectoryUsername;

        // The permanent company id belongs to the company account the previous
        // username named. The sign-in matcher lets a stored id win over the
        // username, so keeping it would let that previous account go on signing
        // in as this user. A first-time link (no previous username) has nothing
        // to forget.
        var usernameChanged = prevDirectoryUsername is not null
            && !string.Equals(prevDirectoryUsername, normalizedDirectoryUsername, StringComparison.Ordinal);
        if (usernameChanged)
        {
            user.DirectorySubjectId = null;
            user.DirectorySignInRefusedAt = null;
        }

        if (prevMethod == SignInMethod.Local
            && signInMethod == SignInMethod.Directory
            && user.SigningPasswordSetAt is null
            && !string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            user.SigningPasswordSetAt = _time.GetUtcNow().UtcDateTime;
        }

        if (normalizedDirectoryUsername is not null
            && !string.Equals(prevDirectoryUsername, normalizedDirectoryUsername, StringComparison.Ordinal))
        {
            var eventCode = prevDirectoryUsername is null
                ? SecurityEventCodes.UserDirectoryLinked
                : SecurityEventCodes.UserDirectoryRelinked;
            _securityAudit.Record(new SecurityEventRequest(
                eventCode,
                SecurityEventOutcome.Success,
                ActorUserId: actingUserId > 0 ? actingUserId : null,
                TargetUserId: user.Id,
                TargetUsername: user.Username,
                Reason: reason));
        }

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "USER_SSO_LINK_UPDATED",
            PreviousValue = JsonSerializer.Serialize(new { SignInMethod = prevMethod.ToString(), DirectoryUsername = prevDirectoryUsername, DirectorySubjectId = prevSubjectId }),
            NewValue = JsonSerializer.Serialize(new { SignInMethod = signInMethod.ToString(), DirectoryUsername = normalizedDirectoryUsername, DirectorySubjectId = user.DirectorySubjectId, Reason = reason }),
            Reason = reason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });
    }

    // Forgets the permanent company id bound at the user's first company
    // sign-in, so the next sign-in binds again (e.g. the company re-created
    // the account). The company username stays. An administrator cannot do
    // this to their own account.
    public Task RelinkDirectoryAsync(int targetUserId, string? reason, int actingUserId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => RelinkDirectoryCoreAsync(targetUserId, reason, actingUserId, ct));

    private async Task RelinkDirectoryCoreAsync(int targetUserId, string? reason, int actingUserId, CancellationToken ct)
    {
        var trimmedReason = AdminActionReason.Require(reason, "re-link a user's company account");

        if (actingUserId == targetUserId)
            throw new BusinessRuleException(
                MessageCodes.UserCannotRelinkOwnCompanyAccount,
                "You cannot re-link your own company account. Ask another administrator.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
            ?? throw new NotFoundException(
                MessageCodes.UserNotFound,
                $"User {targetUserId} not found.",
                new Dictionary<string, string> { ["userId"] = targetUserId.ToString(CultureInfo.InvariantCulture) });

        if (user.DirectoryUsername is null)
            throw new BusinessRuleException(
                MessageCodes.UserNoCompanyUsernameToRelink,
                "This user has no company username to re-link.");

        _db.CurrentUserId = actingUserId > 0 ? actingUserId : null;
        _db.CurrentReason = trimmedReason;

        var prevSubject = user.DirectorySubjectId;
        var prevRefusedAt = user.DirectorySignInRefusedAt;
        user.DirectorySubjectId = null;
        user.DirectorySignInRefusedAt = null;

        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.UserDirectoryRelinked,
            SecurityEventOutcome.Success,
            ActorUserId: actingUserId > 0 ? actingUserId : null,
            TargetUserId: targetUserId,
            TargetUsername: user.Username,
            Reason: trimmedReason,
            Metadata: "{\"action\":\"subjectCleared\"}"));

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_DIRECTORY_RELINKED",
            PreviousValue = JsonSerializer.Serialize(new { DirectorySubjectId = prevSubject, DirectorySignInRefusedAt = prevRefusedAt }),
            NewValue = JsonSerializer.Serialize(new { DirectorySubjectId = (string?)null, DirectorySignInRefusedAt = (DateTime?)null, Reason = trimmedReason }),
            Reason = trimmedReason,
            UserId = actingUserId,
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<UserDto>> ToDtosAsync(IReadOnlyList<User> users)
    {
        if (users.Count == 0)
            return new List<UserDto>();

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var states = await _mfaPolicy.GetUserStatesAsync(
            _db,
            users.Select(u => (u.Id, u.Role?.Type.ToString() ?? string.Empty, u.MfaRequiredSince)),
            nowUtc);

        return users
            .Select(u => ToDto(u, states.GetValueOrDefault(u.Id, MfaUserState.NotRequired), nowUtc))
            .ToList();
    }

    private async Task<UserDto> ToDtoAsync(User user)
    {
        var dtos = await ToDtosAsync(new[] { user });
        return dtos[0];
    }

    private static UserDto ToDto(User u, MfaUserState mfaState, DateTime nowUtc) => new(
        u.Id,
        u.FullName,
        u.Username,
        u.Email,
        u.JobTitle,
        u.RoleId,
        u.Role is null ? null : new RoleDto(u.Role.Id, u.Role.Name, u.Role.Type.ToString()),
        u.IsActive,
        u.IsLockedAt(nowUtc),
        u.LockedUntil,
        u.MustChangePassword,
        u.CreatedAt,
        u.LastLoginAt,
        u.PasswordChangedAt,
        mfaState.ToString(),
        u.SignInMethod.ToString(),
        u.DirectoryUsername,
        u.IsEmergencyAccount,
        DirectoryLinkStates.Of(u).ToString())
    {
        Version = u.Version
    };
}
