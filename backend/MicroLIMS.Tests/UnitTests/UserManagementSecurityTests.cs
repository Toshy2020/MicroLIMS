using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Validation;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UserManagementSecurityTests
{
    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" },
            new Role { Id = 2, Type = RoleType.SectionHead, Name = "Section Head" },
            new Role { Id = 3, Type = RoleType.Reviewer, Name = "Reviewer" },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" }
        );
        db.SaveChanges();
        return db;
    }

    private static (UserService UserService, AuthenticationService AuthService, MicroLimsDbContext Db) CreateServices(MicroLimsDbContext db)
    {
        var emailSender = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        var authLogger = NullLogger<AuthenticationService>.Instance;
        Func<string, string, IEnumerable<string>, string> tokenIssuer = (id, role, permissionCodes) => "fake-jwt-token";
        var authService = new AuthenticationService(db, tokenIssuer, new PermissionService(db), emailSender, authLogger);
        var userService = new UserService(db, authService);
        return (userService, authService, db);
    }

    [Fact]
    public async Task Scenario01_SysAdminCanViewUsers()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        db.Users.Add(new User { Id = 2, FullName = "Test Analyst", Username = "analyst1", RoleId = 4 });
        await db.SaveChangesAsync();

        var users = await userService.GetAllAsync();
        Assert.NotEmpty(users);
        Assert.Contains(users, u => u.Username == "analyst1");
    }

    [Fact]
    public async Task Scenario02_SysAdminCanEditProfileInformation()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Old Name", Username = "olduser", Email = "old@test.com", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updated = await userService.UpdateProfileAsync(user.Id, "New Name", "newuser", "new@test.com", actingUserId: 1);

        Assert.Equal("New Name", updated.FullName);
        Assert.Equal("newuser", updated.Username);
        Assert.Equal("new@test.com", updated.Email);
    }

    [Fact]
    public async Task Scenario03_SysAdminCanChangeUserRole()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Target User", Username = "target1", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updated = await userService.ChangeRoleAsync(user.Id, 3, "Promoted to Reviewer", actingUserId: 1);

        Assert.Equal(3, updated.RoleId);
        Assert.Equal("Reviewer", updated.Role?.Name);
    }

    [Fact]
    public async Task Scenario04_SysAdminCannotChangeOwnRole()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var admin = new User { Id = 1, FullName = "Admin User", Username = "admin", RoleId = 1 };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.ChangeRoleAsync(admin.Id, 4, "Self demotion attempt", actingUserId: admin.Id));

        Assert.Contains("cannot modify their own role", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario05_SysAdminCannotDisableOwnAccount()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var admin = new User { Id = 1, FullName = "Admin User", Username = "admin", RoleId = 1, IsActive = true };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.SetStatusAsync(admin.Id, false, "Self disable attempt", actingUserId: admin.Id));

        Assert.Contains("cannot disable their own account", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario06_SysAdminCanDisableUserWithReason()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Analyst User", Username = "analyst2", RoleId = 4, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updated = await userService.SetStatusAsync(user.Id, false, "Left the company", actingUserId: 1);

        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Scenario07_SysAdminCanEnableDisabledUser()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Analyst User", Username = "analyst2", RoleId = 4, IsActive = false };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updated = await userService.SetStatusAsync(user.Id, true, "Returned to company", actingUserId: 1);

        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Scenario08_SysAdminCanUnlockLockedUser()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Locked User", Username = "locked1", RoleId = 4, FailedLoginAttempts = 5, LockedUntil = DateTime.UtcNow.AddMinutes(15) };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.True(user.IsLocked);

        var updated = await userService.UnlockUserAsync(user.Id, "Unlocked by admin request", actingUserId: 1);

        Assert.False(updated.IsLocked);
        Assert.Null(updated.LockedUntil);
    }

    [Fact]
    public async Task Scenario09_SysAdminCanInitiatePasswordReset()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Reset User", Username = "reset1", Email = "reset@test.com", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await userService.InitiatePasswordResetAsync(user.Id, "User forgot password", actingUserId: 1);

        var resetToken = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        Assert.NotNull(resetToken);
        Assert.False(string.IsNullOrWhiteSpace(resetToken.TokenHash));
    }

    [Fact]
    public async Task Scenario10_PasswordResetDoesNotExposePlaintextPassword()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Reset User 2", Username = "reset2", Email = "reset2@test.com", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await userService.InitiatePasswordResetAsync(user.Id, "Admin triggered", actingUserId: 1);

        var userDto = await userService.GetByIdAsync(user.Id);
        var json = System.Text.Json.JsonSerializer.Serialize(userDto);
        Assert.DoesNotContain("PasswordHash", json);
        Assert.DoesNotContain("secretPassword", json);
    }

    [Fact]
    public async Task Scenario11_PasswordResetEnforcesPasswordPolicy()
    {
        var failures = PasswordPolicy.Validate("weak");
        Assert.NotEmpty(failures);
        Assert.Contains(failures, f => f.Contains("at least 8 characters"));

        var validFailures = PasswordPolicy.Validate("StrongP@ss123");
        Assert.Empty(validFailures);
    }

    [Fact]
    public async Task Scenario12_PasswordHistoryEnforcedOnReset()
    {
        var db = CreateDbContext();
        var (userService, authService, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "User 2", Username = "historyuser", Email = "history@test.com", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var initialHash = BCrypt.Net.BCrypt.HashPassword("InitialP@ss123");
        user.PasswordHash = initialHash;
        db.PasswordHistories.Add(new PasswordHistory { UserId = user.Id, PasswordHash = initialHash });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var tokenHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
            var tokenHashHex = Convert.ToHexString(tokenHash);

            db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = tokenHashHex, ExpiresAt = DateTime.UtcNow.AddHours(1) });
            await db.SaveChangesAsync();

            await authService.ConfirmPasswordResetAsync(rawToken, "InitialP@ss123");
        });

        Assert.Contains("must not match any of your last", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario13_ForcePasswordChangeSetsFlagCorrectly()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "User 2", Username = "forceuser", RoleId = 4, MustChangePassword = false };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var updated = await userService.ForcePasswordChangeAsync(user.Id, actingUserId: 1);

        Assert.True(updated.MustChangePassword);
    }

    [Fact]
    public async Task Scenario14_UnauthorizedRolesBlockedOnAdminEndpoints()
    {
        var controllerType = typeof(MicroLIMS.API.Controllers.UserController);
        var authorizeAttrs = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(authorizeAttrs);
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorizeAttrs[0];
        Assert.Equal(MicroLIMS.Shared.Constants.RoleConstants.SystemAdministrator, attr.Roles);
    }

    [Fact]
    public async Task Scenario15_AuditRecordsGeneratedForSecurityActions()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Audit Target", Username = "audittarget", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await userService.ChangeRoleAsync(user.Id, 3, "Promotion", actingUserId: 1);
        await userService.SetStatusAsync(user.Id, false, "Disabled target", actingUserId: 1);
        await userService.UnlockUserAsync(user.Id, "Unlocked target", actingUserId: 1);
        await userService.InitiatePasswordResetAsync(user.Id, "Reset target", actingUserId: 1);
        await userService.ForcePasswordChangeAsync(user.Id, actingUserId: 1);

        var auditLogs = await db.AuditLogs.Where(a => a.EntityId == "2").ToListAsync();
        Assert.Contains(auditLogs, a => a.Action == "USER_ROLE_CHANGED");
        Assert.Contains(auditLogs, a => a.Action == "USER_DISABLED");
        Assert.Contains(auditLogs, a => a.Action == "USER_UNLOCKED");
        Assert.Contains(auditLogs, a => a.Action == "ADMIN_PASSWORD_RESET_REQUESTED");
        Assert.Contains(auditLogs, a => a.Action == "FORCE_PASSWORD_CHANGE_SET");
    }

    [Fact]
    public async Task Scenario16_PasswordsAndTokensNeverWrittenToAuditLogs()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Audit Security Target", Username = "secuser", Email = "sec@test.com", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await userService.CreateAsync(new User { FullName = "New User", Username = "newsecuser", RoleId = 4 }, "SecuredP@ss123!");
        await userService.InitiatePasswordResetAsync(user.Id, "Reset sec", actingUserId: 1);

        var auditLogs = await db.AuditLogs.ToListAsync();
        foreach (var log in auditLogs)
        {
            var text = (log.PreviousValue ?? "") + (log.NewValue ?? "");
            Assert.DoesNotContain("SecuredP@ss123!", text);
        }
    }

    [Fact]
    public async Task Scenario17_GetEligibleAnalysts_ReturnsOnlyActiveUnlockedAnalysts()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);

        db.Users.AddRange(
            new User { Id = 10, FullName = "Active Analyst 1", Username = "analyst1", RoleId = 4, IsActive = true, FailedLoginAttempts = 0 },
            new User { Id = 11, FullName = "Active Analyst 2", Username = "analyst2", RoleId = 4, IsActive = true, FailedLoginAttempts = 0 },
            new User { Id = 12, FullName = "Disabled Analyst", Username = "analyst_dis", RoleId = 4, IsActive = false },
            new User { Id = 13, FullName = "Locked Analyst", Username = "analyst_lock", RoleId = 4, IsActive = true, FailedLoginAttempts = 5, LockedUntil = DateTime.UtcNow.AddHours(1) },
            new User { Id = 14, FullName = "Section Head", Username = "head1", RoleId = 2, IsActive = true },
            new User { Id = 15, FullName = "Reviewer", Username = "rev1", RoleId = 3, IsActive = true },
            new User { Id = 16, FullName = "Sys Admin", Username = "admin1", RoleId = 1, IsActive = true }
        );
        await db.SaveChangesAsync();

        var eligible = await userService.GetEligibleAnalystsAsync();

        Assert.Equal(2, eligible.Count);
        Assert.Contains(eligible, u => u.Username == "analyst1");
        Assert.Contains(eligible, u => u.Username == "analyst2");
        Assert.DoesNotContain(eligible, u => u.Username == "analyst_dis");
        Assert.DoesNotContain(eligible, u => u.Username == "analyst_lock");
        Assert.DoesNotContain(eligible, u => u.Username == "head1");
        Assert.DoesNotContain(eligible, u => u.Username == "rev1");
        Assert.DoesNotContain(eligible, u => u.Username == "admin1");
    }
}
