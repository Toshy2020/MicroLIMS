using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Validation;

namespace MicroLIMS.Application.Services;

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
    DateTime? PasswordChangedAt)
{
    // The record's row version, sent back as If-Match with an edit.
    public uint Version { get; init; }
}

public class UserService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IAuthenticationService _authService;
    private readonly ISecurityAuditService _securityAudit;
    private readonly AdministrationContinuityGuard _continuityGuard;
    private readonly IElectronicSignatureService _signatureService;
    private readonly TimeProvider _time;

    public UserService(IMicroLimsDbContext db, IAuthenticationService authService, ISecurityAuditService securityAudit, TimeProvider? timeProvider = null, AdministrationContinuityGuard? continuityGuard = null, IElectronicSignatureService? signatureService = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _signatureService = signatureService ?? new ElectronicSignatureService(db, _time);
        _continuityGuard = continuityGuard ?? new AdministrationContinuityGuard(db);
        _securityAudit = securityAudit;
        _db = db;
        _authService = authService;
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _db.Users.Include(u => u.Role).OrderBy(u => u.Id).ToListAsync();
        return users.Select(ToDto).ToList();
    }

    public async Task<List<UserDirectoryDto>> GetDirectoryAsync()
    {
        var users = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive)
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

        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new NotFoundException($"User {id} not found.");
        return ToDto(user);
    }

    public async Task<UserDto> CreateAsync(User user, string plainPassword, string? reason)
    {
        var trimmedReason = AdminActionReason.Require(reason, "create a user");
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(user.FullName))
            throw new InvalidOperationException("Full Name is required.");

        var passwordFailures = PasswordPolicy.Validate(plainPassword);
        if (passwordFailures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", passwordFailures));

        if (await _db.Users.AnyAsync(u => u.Username == user.Username))
            throw new InvalidOperationException($"Username '{user.Username}' is already taken.");
        if (!await _db.Roles.AnyAsync(r => r.Id == user.RoleId))
            throw new InvalidOperationException("Selected role does not exist.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        user.MustChangePassword = true;
        _db.CurrentReason = trimmedReason;
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var reloaded = await _db.Users.Include(u => u.Role).FirstAsync(u => u.Id == user.Id);
        return ToDto(reloaded);
    }

    public async Task<UserDto> UpdateProfileAsync(int targetUserId, string fullName, string username, string? email, string? jobTitle = null, int actingUserId = 0, string? reason = null)
    {
        var trimmedReason = AdminActionReason.Require(reason, "change a user's profile");
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException($"User {targetUserId} not found.");
        RecordVersion.EnsureCurrent(_db, user);

        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("Full Name is required.");
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");

        if (user.Username != username && await _db.Users.AnyAsync(u => u.Username == username && u.Id != targetUserId))
            throw new InvalidOperationException($"Username '{username}' is already taken.");

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

        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    // Signed (D7): the administrator re-enters their password and the signature
    // is linked to the affected user. A refused password changes nothing.
    public Task<UserDto> ChangeRoleAsync(int targetUserId, int newRoleId, string? reason, int actingUserId, string? password, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => ChangeRoleCoreAsync(targetUserId, newRoleId, reason, actingUserId, password, ipAddress));

    private async Task<UserDto> ChangeRoleCoreAsync(int targetUserId, int newRoleId, string? rawReason, int actingUserId, string? password, string? ipAddress)
    {
        var reason = AdminActionReason.Require(rawReason, "change a user's role");

        if (actingUserId == targetUserId)
            throw new InvalidOperationException("System Administrators cannot modify their own role.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException($"User {targetUserId} not found.");

        var newRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == newRoleId)
            ?? throw new InvalidOperationException("Selected role does not exist.");

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
        return ToDto(updatedUser);
    }

    public async Task<UserDto> SetStatusAsync(int targetUserId, bool isActive, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, isActive ? "enable a user account" : "disable a user account");

        if (!isActive && actingUserId == targetUserId)
            throw new InvalidOperationException("System Administrators cannot disable their own account.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException($"User {targetUserId} not found.");

        if (!isActive)
            await _continuityGuard.EnsureAdministrableAfterAsync(AdministrationChange.UserDisabledOrDeleted(targetUserId));

        _db.CurrentReason = reason;
        var prevStatus = user.IsActive;
        user.IsActive = isActive;

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
        return ToDto(user);
    }

    public async Task DeactivateAsync(int userId)
    {
        await SetStatusAsync(userId, false, "Deactivated via legacy service call", userId);
    }

    public async Task UpdateEmailAsync(int userId, string? email, string? reason)
    {
        var trimmedReason = AdminActionReason.Require(reason, "change a user's email address");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException($"User {userId} not found.");
        _db.CurrentReason = trimmedReason;
        user.Email = email;
        await _db.SaveChangesAsync();
    }

    public async Task<UserDto> UnlockUserAsync(int targetUserId, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, "unlock a user account");
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException($"User {targetUserId} not found.");

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
        return ToDto(user);
    }

    public Task InitiatePasswordResetAsync(int targetUserId, string? reason, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => InitiatePasswordResetCoreAsync(targetUserId, reason, actingUserId));

    private async Task InitiatePasswordResetCoreAsync(int targetUserId, string? reason, int actingUserId)
    {
        reason = AdminActionReason.Require(reason, "reset a user's password");
        _db.CurrentReason = reason;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new NotFoundException($"User {targetUserId} not found.");

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
            ?? throw new NotFoundException($"User {targetUserId} not found.");

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
        return ToDto(user);
    }

    private static UserDto ToDto(User u) => new(
        u.Id,
        u.FullName,
        u.Username,
        u.Email,
        u.JobTitle,
        u.RoleId,
        u.Role is null ? null : new RoleDto(u.Role.Id, u.Role.Name, u.Role.Type.ToString()),
        u.IsActive,
        u.IsLocked,
        u.LockedUntil,
        u.MustChangePassword,
        u.CreatedAt,
        u.LastLoginAt,
        u.PasswordChangedAt)
    {
        Version = u.Version
    };
}
