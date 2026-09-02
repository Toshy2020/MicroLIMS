using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
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
    DateTime? PasswordChangedAt);

public class UserService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuthenticationService _authService;

    public UserService(MicroLimsDbContext db, IAuthenticationService authService)
    {
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

    public async Task<List<UserDto>> GetEligibleAnalystsAsync()
    {
        var now = DateTime.UtcNow;
        var users = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive && (u.LockedUntil == null || u.LockedUntil <= now) && u.Role != null && u.Role.Type == RoleType.Analyst)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new InvalidOperationException($"User {id} not found.");
        return ToDto(user);
    }

    public async Task<UserDto> CreateAsync(User user, string plainPassword)
    {
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
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var reloaded = await _db.Users.Include(u => u.Role).FirstAsync(u => u.Id == user.Id);
        return ToDto(reloaded);
    }

    public async Task<UserDto> UpdateProfileAsync(int targetUserId, string fullName, string username, string? email, string? jobTitle = null, int actingUserId = 0)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("Full Name is required.");
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");

        if (user.Username != username && await _db.Users.AnyAsync(u => u.Username == username && u.Id != targetUserId))
            throw new InvalidOperationException($"Username '{username}' is already taken.");

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
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<UserDto> ChangeRoleAsync(int targetUserId, int newRoleId, string reason, int actingUserId)
    {
        if (actingUserId == targetUserId)
            throw new InvalidOperationException("System Administrators cannot modify their own role.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to change a user's role.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        var newRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == newRoleId)
            ?? throw new InvalidOperationException("Selected role does not exist.");

        var prevRoleName = user.Role?.Name ?? "Unknown";
        user.RoleId = newRoleId;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_ROLE_CHANGED",
            PreviousValue = JsonSerializer.Serialize(new { RoleId = user.RoleId, RoleName = prevRoleName }),
            NewValue = JsonSerializer.Serialize(new { RoleId = newRoleId, RoleName = newRole.Name, Reason = reason }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        var updatedUser = await _db.Users.Include(u => u.Role).FirstAsync(u => u.Id == targetUserId);
        return ToDto(updatedUser);
    }

    public async Task<UserDto> SetStatusAsync(int targetUserId, bool isActive, string? reason, int actingUserId)
    {
        if (!isActive && actingUserId == targetUserId)
            throw new InvalidOperationException("System Administrators cannot disable their own account.");

        if (!isActive && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to disable a user account.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        var prevStatus = user.IsActive;
        user.IsActive = isActive;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = isActive ? "USER_ENABLED" : "USER_DISABLED",
            PreviousValue = JsonSerializer.Serialize(new { IsActive = prevStatus }),
            NewValue = JsonSerializer.Serialize(new { IsActive = isActive, Reason = reason }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task DeactivateAsync(int userId)
    {
        await SetStatusAsync(userId, false, "Deactivated via legacy service call", userId);
    }

    public async Task UpdateEmailAsync(int userId, string? email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        user.Email = email;
        await _db.SaveChangesAsync();
    }

    public async Task<UserDto> UnlockUserAsync(int targetUserId, string? reason, int actingUserId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        var prevFailed = user.FailedLoginAttempts;
        var prevLocked = user.LockedUntil;

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_UNLOCKED",
            PreviousValue = JsonSerializer.Serialize(new { FailedLoginAttempts = prevFailed, LockedUntil = prevLocked }),
            NewValue = JsonSerializer.Serialize(new { FailedLoginAttempts = 0, LockedUntil = (DateTime?)null, Reason = reason }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task InitiatePasswordResetAsync(int targetUserId, string? reason, int actingUserId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        await _authService.RequestPasswordResetAsync(user.Username);

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "ADMIN_PASSWORD_RESET_REQUESTED",
            PreviousValue = null,
            NewValue = JsonSerializer.Serialize(new { TargetUsername = user.Username, HasEmail = !string.IsNullOrWhiteSpace(user.Email), Reason = reason }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task<UserDto> ForcePasswordChangeAsync(int targetUserId, int actingUserId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        var prevMust = user.MustChangePassword;
        user.MustChangePassword = true;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "FORCE_PASSWORD_CHANGE_SET",
            PreviousValue = JsonSerializer.Serialize(new { MustChangePassword = prevMust }),
            NewValue = JsonSerializer.Serialize(new { MustChangePassword = true }),
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
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
        u.PasswordChangedAt);
}
