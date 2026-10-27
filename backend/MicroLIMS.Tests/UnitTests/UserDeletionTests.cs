using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UserDeletionTests
{
    private static MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true },
            new Role { Id = 2, Type = RoleType.SectionHead, Name = "Section Head", IsSystemRole = true, IsActive = true },
            new Role { Id = 3, Type = RoleType.Reviewer, Name = "Reviewer", IsSystemRole = true, IsActive = true },
            new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true }
        );
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);
        return db;
    }

    private static UserDeletionService CreateService(MicroLimsDbContext db) => new(db);

    // ---- UserHasAnyHistoryAsync ----

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsTrue_ForReferenceInGroupAEntity()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Signer", Username = "signer1", RoleId = 4 };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        // ElectronicSignature.UserId - real DB Restrict FK (Group A).
        db.ElectronicSignatures.Add(new ElectronicSignature
        {
            Id = 1,
            UserId = target.Id,
            UserFullNameSnapshot = target.FullName,
            UsernameSnapshot = target.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.Approved,
            EntityType = "Sample",
            EntityId = 1
        });
        await db.SaveChangesAsync();

        Assert.True(await service.UserHasAnyHistoryAsync(target.Id));
    }

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsTrue_ForReferenceInGroupBEntity()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Notified", Username = "notified1", RoleId = 4 };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        // NotificationLog.UserId - no DB FK at all (Group B).
        db.NotificationLogs.Add(new NotificationLog { Id = 1, UserId = target.Id, Type = "MediaExpiry", Message = "test" });
        await db.SaveChangesAsync();

        Assert.True(await service.UserHasAnyHistoryAsync(target.Id));
    }

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsTrue_ForReferenceInMaterialDocumentAccessLog()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Viewer", Username = "viewer1", RoleId = 4 };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        db.MaterialDocumentAccessLogs.Add(new MaterialDocumentAccessLog
        {
            Id = 1,
            DocumentId = 1,
            MaterialId = 1,
            UserId = target.Id,
            Action = MaterialDocumentAccessAction.View
        });
        await db.SaveChangesAsync();

        Assert.True(await service.UserHasAnyHistoryAsync(target.Id));
    }

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsTrue_ForReferenceInEquipmentDocumentAccessLog()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Viewer2", Username = "viewer2", RoleId = 4 };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        db.EquipmentDocumentAccessLogs.Add(new EquipmentDocumentAccessLog
        {
            Id = 1,
            DocumentId = 1,
            EquipmentInventoryId = 1,
            UserId = target.Id,
            Action = EquipmentDocumentAccessAction.View
        });
        await db.SaveChangesAsync();

        Assert.True(await service.UserHasAnyHistoryAsync(target.Id));
    }

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsFalse_WhenOnlyReferencesAreInExcludedAuthTables()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Auth Only", Username = "authonly1", RoleId = 4 };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        db.PasswordHistories.Add(new PasswordHistory { UserId = target.Id, PasswordHash = "hash1" });
        db.PasswordResetTokens.Add(new PasswordResetToken { UserId = target.Id, TokenHash = "hash2", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        db.RefreshTokens.Add(new RefreshToken { UserId = target.Id, TokenHash = "hash3", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        Assert.False(await service.UserHasAnyHistoryAsync(target.Id));
    }

    [Fact]
    public async Task HardDeleteAsync_IsBlocked_ForUserReferencedBySecurityAuditEvent()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Security Subject", Username = "secsubject1", RoleId = 4, IsActive = false };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        db.SecurityAuditEvents.Add(new SecurityAuditEvent { EventCode = "AUTH_LOGIN_FAILED", Source = "Api", TargetUserId = target.Id });
        await db.SaveChangesAsync();

        Assert.True(await service.UserHasAnyHistoryAsync(target.Id));
        var ex = await Assert.ThrowsAsync<UserHasHistoryException>(() => service.HardDeleteAsync(target.Id, actingUserId: 1));
        Assert.Contains("activity history", ex.Message);
        Assert.NotNull(await db.Users.FindAsync(target.Id));
    }

    [Fact]
    public async Task HardDeleteAsync_IsBlocked_ForUserWithLoginHistory()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Login Subject", Username = "loginsubject1", RoleId = 4, IsActive = false };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        db.LoginHistories.Add(new LoginHistory { UserId = target.Id, Username = target.Username, Success = false });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UserHasHistoryException>(() => service.HardDeleteAsync(target.Id, actingUserId: 1));
    }

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsFalse_ForAGenuinelyCleanUser()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Clean User", Username = "cleanuser1", RoleId = 4 };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        Assert.False(await service.UserHasAnyHistoryAsync(target.Id));
    }

    [Fact]
    public async Task UserHasAnyHistoryAsync_ReturnsTrue_WhenLastLoginAtIsSet()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var target = new User { Id = 2, FullName = "Logged In", Username = "loggedin1", RoleId = 4, LastLoginAt = DateTime.UtcNow };
        db.Users.Add(target);
        await db.SaveChangesAsync();

        Assert.True(await service.UserHasAnyHistoryAsync(target.Id));
    }

    // ---- HardDeleteAsync (exercises the same path the DELETE /api/users/{id} endpoint calls) ----

    [Fact]
    public async Task HardDeleteAsync_Succeeds_ForACleanUser()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var admin = new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, IsActive = true };
        var target = new User { Id = 2, FullName = "Fresh User", Username = "freshuser1", RoleId = 4 };
        db.Users.AddRange(admin, target);
        await db.SaveChangesAsync();

        await service.HardDeleteAsync(target.Id, admin.Id);

        Assert.Null(await db.Users.FirstOrDefaultAsync(u => u.Id == target.Id));
        var auditEntry = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == target.Id.ToString() && a.Action == "USER_HARD_DELETED");
        Assert.NotNull(auditEntry);
        Assert.Equal(admin.Id, auditEntry!.UserId);
        Assert.Contains("freshuser1", auditEntry.PreviousValue);
    }

    [Fact]
    public async Task HardDeleteAsync_IsRejected_ForAUserWithHistory_WithExpectedMessage()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var admin = new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, IsActive = true };
        var target = new User { Id = 2, FullName = "Used User", Username = "useduser1", RoleId = 4 };
        db.Users.AddRange(admin, target);
        await db.SaveChangesAsync();

        db.NotificationLogs.Add(new NotificationLog { Id = 1, UserId = target.Id, Type = "MediaExpiry", Message = "test" });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UserHasHistoryException>(() => service.HardDeleteAsync(target.Id, admin.Id));

        Assert.Contains("cannot be permanently deleted", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deactivate", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Id == target.Id));
    }

    [Fact]
    public async Task HardDeleteAsync_IsRejected_ForTheLastActiveSystemAdministrator()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var onlyAdmin = new User { Id = 1, FullName = "Only Admin", Username = "onlyadmin", RoleId = 1, IsActive = true };
        var otherUser = new User { Id = 2, FullName = "Other", Username = "otheruser1", RoleId = 4, IsActive = true };
        db.Users.AddRange(onlyAdmin, otherUser);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HardDeleteAsync(onlyAdmin.Id, otherUser.Id));
        Assert.Contains("last active System Administrator", ex.Message);
        Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Id == onlyAdmin.Id));

        // With a second active admin present, deleting the first is fine (assuming no history).
        var secondAdmin = new User { Id = 3, FullName = "Second Admin", Username = "secondadmin1", RoleId = 1, IsActive = true };
        db.Users.Add(secondAdmin);
        await db.SaveChangesAsync();

        await service.HardDeleteAsync(onlyAdmin.Id, secondAdmin.Id);
        Assert.Null(await db.Users.FirstOrDefaultAsync(u => u.Id == onlyAdmin.Id));
    }

    [Fact]
    public async Task HardDeleteAsync_IsRejected_WhenActingUserTargetsSelf()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var admin1 = new User { Id = 1, FullName = "Only Admin", Username = "onlyadmin", RoleId = 1, IsActive = true };
        var admin2 = new User { Id = 2, FullName = "Second Admin", Username = "seconadmin", RoleId = 1, IsActive = true };
        db.Users.AddRange(admin1, admin2);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HardDeleteAsync(admin1.Id, admin1.Id));
        Assert.Contains("own account", ex.Message);
    }

    // ---- Drift protection ----

    // Guards against a new User-referencing column being added later and
    // nobody deciding whether it should block a hard delete. Only catches
    // the "*UserId" naming convention actually used throughout this
    // codebase - TestOrder.AssignedAnalystId is a known exception found
    // only by manual review during the original recon; it is already in
    // the registry but cannot be auto-discovered by name here.
    [Fact]
    public void UserReferenceRegistry_AccountsForEveryUserIdNamedPropertyInTheModel()
    {
        using var db = CreateDbContext();

        var discovered = db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Distinct()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?))
                        && p.Name.EndsWith("UserId", StringComparison.Ordinal))
            .Select(p => (EntityType: p.DeclaringType!, PropertyName: p.Name))
            .Distinct()
            .ToList();

        var missing = discovered
            .Where(d => !UserReferenceRegistry.All.Any(r => r.EntityType == d.EntityType && r.PropertyName == d.PropertyName))
            .ToList();

        Assert.True(missing.Count == 0,
            "Found User-referencing column(s) not accounted for in UserReferenceRegistry: " +
            string.Join(", ", missing.Select(m => $"{m.EntityType.Name}.{m.PropertyName}")));
    }

    [Fact]
    public void UserReferenceRegistry_EveryEntryMatchesARealIntProperty()
    {
        foreach (var entry in UserReferenceRegistry.All)
        {
            var property = entry.EntityType.GetProperty(entry.PropertyName);
            Assert.True(property is not null, $"{entry.EntityType.Name}.{entry.PropertyName} does not exist.");
            Assert.True(property!.PropertyType == typeof(int) || property.PropertyType == typeof(int?),
                $"{entry.EntityType.Name}.{entry.PropertyName} is not an int/int? property.");
        }
    }

    // ---- Administration continuity (hard delete counts by permission, not RoleType) ----

    private static async Task<User> AddCustomRoleAdminAsync(MicroLimsDbContext db, int id, params string[] permissionCodes)
    {
        var role = new Role { Id = 10, Type = RoleType.Analyst, Name = "Operations Admin", IsSystemRole = false, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var permissionIds = await db.Permissions.Where(p => permissionCodes.Contains(p.Code)).Select(p => p.Id).ToListAsync();
        foreach (var permissionId in permissionIds)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        var user = new User { Id = id, FullName = "Ops Admin", Username = "opsadmin", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task HardDeleteAsync_Succeeds_WhenACustomRoleHoldingBothAdministrationPermissionsRemains()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var systemAdmin = new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, IsActive = true };
        db.Users.Add(systemAdmin);
        await db.SaveChangesAsync();
        var customAdmin = await AddCustomRoleAdminAsync(db, 2, MicroLIMS.Shared.Constants.PermissionConstants.UsersManage, MicroLIMS.Shared.Constants.PermissionConstants.RolesManage);

        // The custom role is not typed SystemAdministrator - a RoleType count would call this the last administrator.
        await service.HardDeleteAsync(systemAdmin.Id, customAdmin.Id);

        Assert.Null(await db.Users.FirstOrDefaultAsync(u => u.Id == systemAdmin.Id));
    }

    [Fact]
    public async Task HardDeleteAsync_IsRejected_WhenTheOnlyOtherUserLacksRolesManage()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var systemAdmin = new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, IsActive = true };
        db.Users.Add(systemAdmin);
        await db.SaveChangesAsync();
        var usersOnly = await AddCustomRoleAdminAsync(db, 2, MicroLIMS.Shared.Constants.PermissionConstants.UsersManage);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HardDeleteAsync(systemAdmin.Id, usersOnly.Id));

        Assert.Contains("Roles.Manage", ex.Message);
        Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Id == systemAdmin.Id));
    }

    [Fact]
    public async Task HardDeleteAsync_IsRejected_WhenTheOtherAdministratorIsDisabled()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var systemAdmin = new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, IsActive = true };
        var disabledAdmin = new User { Id = 2, FullName = "Disabled Admin", Username = "disabledadmin", RoleId = 1, IsActive = false };
        var analyst = new User { Id = 3, FullName = "Analyst", Username = "analyst3", RoleId = 4, IsActive = true };
        db.Users.AddRange(systemAdmin, disabledAdmin, analyst);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HardDeleteAsync(systemAdmin.Id, analyst.Id));

        Assert.Contains("last active System Administrator", ex.Message);
        Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Id == systemAdmin.Id));
    }
}
