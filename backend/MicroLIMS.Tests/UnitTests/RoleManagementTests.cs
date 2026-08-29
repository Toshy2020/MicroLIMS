using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class RoleManagementTests
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

    private static RoleService CreateService(MicroLimsDbContext db) => new(db, new PermissionService(db));

    [Fact]
    public async Task CreateAsync_CreatesANonSystemRole()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync("QC Trainee", "Trainee reviewer with restricted access", RoleType.Analyst);

        Assert.False(created.IsSystemRole);
        Assert.True(created.IsActive);
        Assert.Equal("QC Trainee", created.Name);
        Assert.Equal("Analyst", created.Type);
        Assert.Empty(created.PermissionCodes);
    }

    [Fact]
    public async Task CreateAsync_RejectsBlankName()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("  ", null, RoleType.Analyst));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsGrantedPermissionCodes()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var reviewer = await service.GetByIdAsync(3); // Reviewer

        Assert.NotNull(reviewer);
        Assert.Equal(4, reviewer!.PermissionCodes.Count);
        Assert.Contains(PermissionConstants.SamplesReview, reviewer.PermissionCodes);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameAndDescription()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Old Name", "Old description", RoleType.Analyst);

        var updated = await service.UpdateAsync(created.Id, "New Name", "New description");

        Assert.Equal("New Name", updated.Name);
        Assert.Equal("New description", updated.Description);
    }

    [Fact]
    public async Task UpdateAsync_LeavesSystemRoleFlagUntouched()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var updated = await service.UpdateAsync(2, "Section Head", "Renamed description only");

        Assert.True(updated.IsSystemRole); // still a system role - Update never changes this flag
    }

    [Fact]
    public async Task DeleteAsync_RejectsSystemRole()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(2)); // Section Head
        Assert.Contains("system role", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_RejectsRoleCurrentlyAssignedToAUser()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("In-Use Role", null, RoleType.Analyst);
        db.Users.Add(new User { FullName = "Holder", Username = "holder1", RoleId = created.Id });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(created.Id));
        Assert.Contains("assigned to", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAnUnusedNonSystemRoleAndItsGrants()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Disposable Role", null, RoleType.Analyst);
        await service.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, actingUserId: 1);

        await service.DeleteAsync(created.Id);

        Assert.Null(await db.Roles.FirstOrDefaultAsync(r => r.Id == created.Id));
        Assert.Empty(await db.RolePermissions.Where(rp => rp.RoleId == created.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdatePermissionsAsync_GrantsAndRevokesToMatchTheRequestedSet()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Custom Role", null, RoleType.Analyst);
        await service.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView, PermissionConstants.SamplesReview }, actingUserId: 1);

        var updated = await service.UpdatePermissionsAsync(
            created.Id,
            new List<string> { PermissionConstants.SamplesReview, PermissionConstants.MasterDataManage }, // drop AuditView, add MasterDataManage, keep SamplesReview
            actingUserId: 1);

        Assert.Equal(2, updated.PermissionCodes.Count);
        Assert.Contains(PermissionConstants.SamplesReview, updated.PermissionCodes);
        Assert.Contains(PermissionConstants.MasterDataManage, updated.PermissionCodes);
        Assert.DoesNotContain(PermissionConstants.AuditView, updated.PermissionCodes);
    }

    [Fact]
    public async Task UpdatePermissionsAsync_RejectsAnUnknownPermissionCode()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Custom Role", null, RoleType.Analyst);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePermissionsAsync(created.Id, new List<string> { "NotARealCode" }, actingUserId: 1));
        Assert.Contains("Unknown permission code", ex.Message);
    }

    [Fact]
    public async Task UpdatePermissionsAsync_WritesAConsolidatedAuditEntry()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Audited Role", null, RoleType.Analyst);

        await service.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, actingUserId: 7);

        var entry = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == nameof(Role) && a.Action == "ROLE_PERMISSIONS_UPDATED" && a.EntityId == created.Id.ToString());
        Assert.NotNull(entry);
        Assert.Equal(7, entry!.UserId);
        Assert.Contains(PermissionConstants.AuditView, entry.NewValue);
    }

    [Fact]
    public async Task CreateAndUpdate_ProduceAutomaticGenericAuditEntries()
    {
        // Plain CRUD relies on MicroLimsDbContext's automatic SaveChanges
        // capture (same convention as ItemService) rather than a custom
        // AuditLog write - this confirms that capture actually fires for
        // Role the same way it does for every other tracked entity.
        var db = CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync("Auto-Audited Role", null, RoleType.Analyst);
        await service.UpdateAsync(created.Id, "Renamed", "New description");

        var entries = await db.AuditLogs.Where(a => a.EntityName == nameof(Role) && a.EntityId == created.Id.ToString()).ToListAsync();
        Assert.Contains(entries, a => a.Action == "Create");
        Assert.Contains(entries, a => a.Action == "Update");
    }

    // Mirrors UserManagementSecurityTests.Scenario14 - confirms the new
    // CRUD actions all still live under RoleController's untouched
    // class-level [Authorize(Roles=SystemAdministrator)], per "role
    // management itself isn't migrated to the new policy system in this
    // phase" - the actions themselves have no per-method override.
    [Fact]
    public void RoleController_StaysSystemAdministratorOnlyAtTheClassLevel()
    {
        var controllerType = typeof(MicroLIMS.API.Controllers.RoleController);
        var authorizeAttrs = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.Single(authorizeAttrs);
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorizeAttrs[0];
        Assert.Equal(RoleConstants.SystemAdministrator, attr.Roles);

        foreach (var methodName in new[] { nameof(MicroLIMS.API.Controllers.RoleController.GetById), nameof(MicroLIMS.API.Controllers.RoleController.Create), nameof(MicroLIMS.API.Controllers.RoleController.Update), nameof(MicroLIMS.API.Controllers.RoleController.Delete), nameof(MicroLIMS.API.Controllers.RoleController.UpdatePermissions), nameof(MicroLIMS.API.Controllers.RoleController.GetAllPermissions) })
        {
            var method = controllerType.GetMethod(methodName)!;
            var methodAttrs = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
            Assert.Empty(methodAttrs); // no method-level override - inherits the class-level SystemAdministrator-only restriction
        }
    }
}
