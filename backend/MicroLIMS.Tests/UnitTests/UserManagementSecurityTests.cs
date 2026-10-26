using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
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
        DbSeeder.SeedPermissionsAndGrants(db);
        return db;
    }

    // Somebody must still be able to administer users and roles after the change under test.
    private static void AddStandingAdmin(MicroLimsDbContext db)
    {
        db.Users.Add(new User { Id = 900, FullName = "Standing Admin", Username = "standingadmin", RoleId = 1, IsActive = true });
        db.SaveChanges();
    }

    private static (UserService UserService, AuthenticationService AuthService, MicroLimsDbContext Db) CreateServices(MicroLimsDbContext db)
    {
        var emailSender = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        var authLogger = NullLogger<AuthenticationService>.Instance;
        AccessTokenIssuer tokenIssuer = _ => "fake-jwt-token";
        var securityAudit = new SecurityAuditService(db, new SystemSecurityRequestContext());
        var authService = new AuthenticationService(db, tokenIssuer, new PermissionService(db), emailSender, authLogger, securityAudit);
        var userService = new UserService(db, authService, securityAudit);
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
        AddStandingAdmin(db);
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
        AddStandingAdmin(db);
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

        var initialHash = TestPasswords.Hash("InitialP@ss123");
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
        foreach (var action in new[] { nameof(MicroLIMS.API.Controllers.UserController.Create), nameof(MicroLIMS.API.Controllers.UserController.GetAll) })
            Assert.Equal(new[] { MicroLIMS.Domain.Enums.RoleType.SystemAdministrator },
                MicroLIMS.Tests.ArchitectureTests.AuthorizationMatrixTests.AllowedRoles<MicroLIMS.API.Controllers.UserController>(action));
    }

    [Fact]
    public async Task Scenario15_AuditRecordsGeneratedForSecurityActions()
    {
        var db = CreateDbContext();
        AddStandingAdmin(db);
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

    // Analysts and Reviewers can be assigned tests; a Section Head appears
    // only in their own list, since only they may assign themselves.
    [Fact]
    public async Task Scenario17_GetEligibleAnalysts_ReturnsActiveUnlockedAnalystsReviewersAndOnlyTheCallingSectionHead()
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
            new User { Id = 16, FullName = "Sys Admin", Username = "admin1", RoleId = 1, IsActive = true },
            new User { Id = 17, FullName = "Other Section Head", Username = "head2", RoleId = 2, IsActive = true }
        );
        await db.SaveChangesAsync();

        var eligible = await userService.GetEligibleAnalystsAsync(actingUserId: 14);

        Assert.Equal(new[] { "analyst1", "analyst2", "head1", "rev1" }, eligible.Select(u => u.Username).OrderBy(n => n));

        var asAdmin = await userService.GetEligibleAnalystsAsync(actingUserId: 16);
        Assert.Equal(new[] { "analyst1", "analyst2", "rev1" }, asAdmin.Select(u => u.Username).OrderBy(n => n));
    }

    [Fact]
    public async Task Scenario18_RoleChangeAuditRecordsPreviousRoleIdAndNewRoleId()
    {
        var db = CreateDbContext();
        AddStandingAdmin(db);
        var (userService, _, _) = CreateServices(db);
        var user = new User { Id = 2, FullName = "Role Target", Username = "roletarget", RoleId = 4 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await userService.ChangeRoleAsync(user.Id, 3, "Promoted to Reviewer", actingUserId: 1);

        var log = await db.AuditLogs.SingleAsync(a => a.Action == "USER_ROLE_CHANGED" && a.EntityId == "2");
        using var previous = System.Text.Json.JsonDocument.Parse(log.PreviousValue!);
        using var next = System.Text.Json.JsonDocument.Parse(log.NewValue!);

        Assert.Equal(4, previous.RootElement.GetProperty("RoleId").GetInt32());
        Assert.Equal("Analyst", previous.RootElement.GetProperty("RoleName").GetString());
        Assert.Equal(3, next.RootElement.GetProperty("RoleId").GetInt32());
        Assert.Equal("Reviewer", next.RootElement.GetProperty("RoleName").GetString());
    }

    // ---- Administration continuity ----
    // Roles 1 holds Users.Manage and Roles.Manage (seeded grants); nobody
    // else does unless a test adds a custom role that does.

    private static async Task<User> AddAdminAsync(MicroLimsDbContext db, int id, bool isActive = true, DateTime? lockedUntil = null)
    {
        var admin = new User { Id = id, FullName = $"Admin {id}", Username = $"admin{id}", RoleId = 1, IsActive = isActive, LockedUntil = lockedUntil };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    private static async Task<User> AddCustomRoleAdminAsync(MicroLimsDbContext db, int id)
    {
        var role = new Role { Id = 10, Type = RoleType.Analyst, Name = "Operations Admin", IsSystemRole = false, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var permissionIds = await db.Permissions
            .Where(p => p.Code == MicroLIMS.Shared.Constants.PermissionConstants.UsersManage || p.Code == MicroLIMS.Shared.Constants.PermissionConstants.RolesManage)
            .Select(p => p.Id).ToListAsync();
        foreach (var permissionId in permissionIds)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        var user = new User { Id = id, FullName = "Ops Admin", Username = "opsadmin", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task ChangeRole_IsRejected_WhenDemotingTheLastActiveHolderOfUsersManage()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var onlyAdmin = await AddAdminAsync(db, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.ChangeRoleAsync(onlyAdmin.Id, 4, "Demotion", actingUserId: 2));

        Assert.Contains("Users.Manage", ex.Message);
        Assert.Equal(1, (await db.Users.AsNoTracking().FirstAsync(u => u.Id == onlyAdmin.Id)).RoleId);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "USER_ROLE_CHANGED");
    }

    [Fact]
    public async Task ChangeRole_Succeeds_WhenAnotherActiveUserStillHoldsTheAdministrationPermissions()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var first = await AddAdminAsync(db, 1);
        await AddAdminAsync(db, 2);

        var updated = await userService.ChangeRoleAsync(first.Id, 4, "Demotion", actingUserId: 2);

        Assert.Equal(4, updated.RoleId);
    }

    [Fact]
    public async Task ChangeRole_IsRejected_WhenTheOtherAdministratorIsDisabled()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var first = await AddAdminAsync(db, 1);
        await AddAdminAsync(db, 2, isActive: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.ChangeRoleAsync(first.Id, 4, "Demotion", actingUserId: 3));

        Assert.Equal(1, (await db.Users.AsNoTracking().FirstAsync(u => u.Id == first.Id)).RoleId);
    }

    [Fact]
    public async Task SetStatus_IsRejected_WhenDisablingTheLastActiveHolder()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var onlyAdmin = await AddAdminAsync(db, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.SetStatusAsync(onlyAdmin.Id, false, "Leaving", actingUserId: 2));

        Assert.Contains("Users.Manage", ex.Message);
        Assert.True((await db.Users.AsNoTracking().FirstAsync(u => u.Id == onlyAdmin.Id)).IsActive);
        Assert.DoesNotContain(db.AuditLogs, a => a.Action == "USER_DISABLED");
    }

    [Fact]
    public async Task SetStatus_IsRejected_WhenTheOnlyOtherAdministratorIsAlreadyDisabled()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var first = await AddAdminAsync(db, 1);
        await AddAdminAsync(db, 2, isActive: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.SetStatusAsync(first.Id, false, "Leaving", actingUserId: 3));
    }

    [Fact]
    public async Task SetStatus_Succeeds_WhenACustomRoleHoldingBothPermissionsHasAnActiveUser()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var systemAdmin = await AddAdminAsync(db, 1);
        await AddCustomRoleAdminAsync(db, 2);

        var updated = await userService.SetStatusAsync(systemAdmin.Id, false, "Leaving", actingUserId: 2);

        Assert.False(updated.IsActive);
    }

    // A lockout expires by itself after 15 minutes, so a locked administrator
    // still counts as one.
    [Fact]
    public async Task SetStatus_CountsTemporarilyLockedAdministrators()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var first = await AddAdminAsync(db, 1);
        await AddAdminAsync(db, 2, lockedUntil: DateTime.UtcNow.AddMinutes(10));

        var updated = await userService.SetStatusAsync(first.Id, false, "Leaving", actingUserId: 3);

        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task SetStatus_EnablingIsNeverBlocked()
    {
        var db = CreateDbContext();
        var (userService, _, _) = CreateServices(db);
        var disabledAdmin = await AddAdminAsync(db, 1, isActive: false);

        var updated = await userService.SetStatusAsync(disabledAdmin.Id, true, null, actingUserId: 2);

        Assert.True(updated.IsActive);
    }
}
