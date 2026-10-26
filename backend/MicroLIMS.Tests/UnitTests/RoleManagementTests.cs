using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
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
        // Somebody must still be able to administer users and roles after the change under test.
        db.Users.Add(new User { Id = 1, FullName = "Standing Admin", Username = "admin", RoleId = 1, IsActive = true });
        db.SaveChanges();
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
    }

    // Endpoints authorize by permission, so a new custom role starts with
    // what its base system role holds - before, it passed every role check
    // for its base type and a role with no permissions would lose all of it.
    [Fact]
    public async Task CreateAsync_StartsWithTheBaseSystemRolesPermissions()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var analyst = await service.GetByIdAsync(4);

        var created = await service.CreateAsync("QC Trainee", null, RoleType.Analyst);

        Assert.NotEmpty(created.PermissionCodes);
        Assert.Equal(analyst!.PermissionCodes.OrderBy(c => c), created.PermissionCodes.OrderBy(c => c));
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
        // 7 laboratory codes, the 4 Document Control codes the Reviewer role
        // carries as technical reviewer / QA auditor, and the 4 codes that
        // replaced role-only gates (review dashboard, media preparation,
        // receiving for their own lab, correcting a sample), and
        // MonitoringPlan.View.
        Assert.Equal(18, reviewer!.PermissionCodes.Count);
        Assert.Contains(PermissionConstants.SamplesReview, reviewer.PermissionCodes);
        Assert.Contains(PermissionConstants.DocumentsReview, reviewer.PermissionCodes);
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
        Assert.Equal(PermissionConstants.RolesManage, attr.Policy);
        Assert.Equal(new[] { MicroLIMS.Domain.Enums.RoleType.SystemAdministrator },
            MicroLIMS.Tests.ArchitectureTests.AuthorizationMatrixTests.AllowedRoles<MicroLIMS.API.Controllers.RoleController>(
                nameof(MicroLIMS.API.Controllers.RoleController.GetById)));

        foreach (var methodName in new[] { nameof(MicroLIMS.API.Controllers.RoleController.GetById), nameof(MicroLIMS.API.Controllers.RoleController.Create), nameof(MicroLIMS.API.Controllers.RoleController.Update), nameof(MicroLIMS.API.Controllers.RoleController.Delete), nameof(MicroLIMS.API.Controllers.RoleController.UpdatePermissions), nameof(MicroLIMS.API.Controllers.RoleController.GetAllPermissions) })
        {
            var method = controllerType.GetMethod(methodName)!;
            var methodAttrs = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
            Assert.Empty(methodAttrs); // no method-level override - inherits the class-level SystemAdministrator-only restriction
        }
    }

    // ---- PermissionVersion ----
    // Every access token carries the version of the role's grants it was
    // issued from; a change in grants must therefore move the version.

    private static async Task<int> PermissionVersionOf(MicroLimsDbContext db, int roleId) =>
        (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == roleId)).PermissionVersion;

    [Fact]
    public async Task NewRole_StartsAtPermissionVersionOne()
    {
        var db = CreateDbContext();
        var created = await CreateService(db).CreateAsync("QC Trainee", null, RoleType.Analyst);

        Assert.Equal(1, await PermissionVersionOf(db, created.Id));
        Assert.Equal(1, await PermissionVersionOf(db, 4));
    }

    [Fact]
    public async Task UpdatePermissionsAsync_IncrementsThePermissionVersionOnce_WhateverTheNumberOfChanges()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db, roleId: 3);
        var analyst = await service.GetByIdAsync(4);
        // Revokes one code and grants another in a single call. The granted
        // code must not be an administration code: the Analyst role holds
        // GxP execution codes, and mixing the two is a segregation-of-duties
        // violation that UpdatePermissionsAsync rejects.
        var desired = analyst!.PermissionCodes.Skip(1).Append(PermissionConstants.KpiView).ToList();

        await service.UpdatePermissionsAsync(4, desired, actor.Id);

        Assert.Equal(2, await PermissionVersionOf(db, 4));
        Assert.Equal(1, await PermissionVersionOf(db, 3)); // other roles untouched
        Assert.Equal(1, await PermissionVersionOf(db, 1));
    }

    [Fact]
    public async Task UpdatePermissionsAsync_IncrementsOnEachChangingCall()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db, roleId: 3);
        var analyst = await service.GetByIdAsync(4);

        await service.UpdatePermissionsAsync(4, analyst!.PermissionCodes.Skip(1).ToList(), actor.Id);
        await service.UpdatePermissionsAsync(4, analyst.PermissionCodes, actor.Id);

        Assert.Equal(3, await PermissionVersionOf(db, 4));
    }

    [Fact]
    public async Task UpdatePermissionsAsync_LeavesThePermissionVersionAlone_WhenNothingChanges()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db, roleId: 3);
        var analyst = await service.GetByIdAsync(4);

        await service.UpdatePermissionsAsync(4, analyst!.PermissionCodes.ToList(), actor.Id);

        Assert.Equal(1, await PermissionVersionOf(db, 4));
    }

    [Fact]
    public async Task UpdatePermissionsAsync_DoesNotIncrement_WhenTheChangeIsRefused()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db, roleId: 3);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePermissionsAsync(1, AllCodesExcept(PermissionConstants.RolesManage), actor.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePermissionsAsync(4, new List<string> { "No.Such.Code" }, actor.Id));

        Assert.Equal(1, await PermissionVersionOf(db, 1));
        Assert.Equal(1, await PermissionVersionOf(db, 4));
    }

    [Fact]
    public async Task RenamingARole_DoesNotChangeThePermissionVersion()
    {
        var db = CreateDbContext();
        await CreateService(db).UpdateAsync(4, "Lab Technician", null);

        Assert.Equal(1, await PermissionVersionOf(db, 4));
    }

    // ---- Administration continuity ----
    // CreateDbContext seeds one active administrator (user 1, role 1), the
    // only user whose role holds Users.Manage and Roles.Manage.

    // The seeded System Administrator holds every code, which segregation of
    // duties no longer lets a save keep; these tests are about continuity, so
    // they save the administration side of it without the GxP codes.
    private static List<string> AllCodesExcept(string code) =>
        PermissionConstants.All
            .Where(c => c != code && !SegregationOfDutiesRules.GxpExecutionAndApprovalCodes.Contains(c))
            .ToList();

    // A non-system role holding both administration permissions, with one
    // user in it. Not typed SystemAdministrator - holding is by permission.
    private static async Task<User> AddCustomAdministratorAsync(MicroLimsDbContext db, bool isActive = true, DateTime? lockedUntil = null)
    {
        var role = new Role { Name = "Operations Admin", Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var permissionIds = await db.Permissions
            .Where(p => p.Code == PermissionConstants.UsersManage || p.Code == PermissionConstants.RolesManage)
            .Select(p => p.Id).ToListAsync();
        foreach (var permissionId in permissionIds)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        var user = new User { FullName = "Ops Admin", Username = "opsadmin", RoleId = role.Id, IsActive = isActive, LockedUntil = lockedUntil };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // The seeded System Administrator no longer holds a GxP code (WP1.2), so
    // it no longer violates segregation of duties. Tests that need the
    // administrator to start in violation add the GxP grant explicitly.
    private static async Task SeedAdministratorGxpGrantAsync(MicroLimsDbContext db)
    {
        var samplesApprove = await db.Permissions.SingleAsync(p => p.Code == PermissionConstants.SamplesApprove);
        db.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = samplesApprove.Id });
        await db.SaveChangesAsync();
    }

    private static async Task<User> AddActingUserAsync(MicroLimsDbContext db, int roleId = 4)
    {
        var user = new User { FullName = "Acting User", Username = "actinguser", RoleId = roleId, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Theory]
    [InlineData(PermissionConstants.UsersManage)]
    [InlineData(PermissionConstants.RolesManage)]
    public async Task UpdatePermissionsAsync_RejectsRemovingAnAdministrationPermission_FromTheOnlyHoldingRole(string permissionCode)
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db);
        var auditCountBefore = await db.AuditLogs.CountAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePermissionsAsync(1, AllCodesExcept(permissionCode), actor.Id));

        Assert.Contains(permissionCode, ex.Message);
        // Nothing was saved: the role keeps the permission and no audit entry was written.
        Assert.Contains(permissionCode, await new PermissionService(db).GetPermissionCodesForRoleAsync(1));
        Assert.Equal(auditCountBefore, await db.AuditLogs.CountAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED"));
    }

    [Theory]
    [InlineData(PermissionConstants.UsersManage)]
    [InlineData(PermissionConstants.RolesManage)]
    public async Task UpdatePermissionsAsync_AllowsRemovingAnAdministrationPermission_WhenAnotherActiveUsersRoleHoldsIt(string permissionCode)
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db);
        await AddCustomAdministratorAsync(db);

        var updated = await service.UpdatePermissionsAsync(1, AllCodesExcept(permissionCode), actor.Id);

        Assert.DoesNotContain(permissionCode, updated.PermissionCodes);
    }

    [Fact]
    public async Task UpdatePermissionsAsync_DoesNotCountDisabledUsers()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db);
        await AddCustomAdministratorAsync(db, isActive: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePermissionsAsync(1, AllCodesExcept(PermissionConstants.UsersManage), actor.Id));

        Assert.Contains(PermissionConstants.UsersManage, ex.Message);
    }

    // A lockout expires by itself after 15 minutes, so it must not be what
    // blocks administration.
    [Fact]
    public async Task UpdatePermissionsAsync_CountsTemporarilyLockedUsers()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db);
        await AddCustomAdministratorAsync(db, lockedUntil: DateTime.UtcNow.AddMinutes(10));

        var updated = await service.UpdatePermissionsAsync(1, AllCodesExcept(PermissionConstants.RolesManage), actor.Id);

        Assert.DoesNotContain(PermissionConstants.RolesManage, updated.PermissionCodes);
    }

    [Fact]
    public async Task UpdatePermissionsAsync_RejectsChangingThePermissionsOfTheActingUsersOwnRole()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Own Role", null, RoleType.Analyst);
        var actor = await AddActingUserAsync(db, roleId: created.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePermissionsAsync(created.Id, new List<string> { PermissionConstants.AuditView }, actor.Id));

        Assert.Equal("You cannot change the permissions of your own role.", ex.Message);
        Assert.DoesNotContain(PermissionConstants.AuditView, await new PermissionService(db).GetPermissionCodesForRoleAsync(created.Id));
    }

    // ---- Segregation of duties ----

    private static async Task<int> CountRolePermissionsAsync(MicroLimsDbContext db, int roleId) =>
        await db.RolePermissions.CountAsync(rp => rp.RoleId == roleId);

    [Fact]
    public async Task UpdatePermissionsAsync_RejectsAdministrationTogetherWithGxpExecution_AndSavesNothing()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Mixed Role", null, RoleType.Analyst);
        var actor = await AddActingUserAsync(db);
        var grantsBefore = await CountRolePermissionsAsync(db, created.Id);
        var auditCountBefore = await db.AuditLogs.CountAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED");

        var ex = await Assert.ThrowsAsync<SegregationOfDutiesViolationException>(() =>
            service.UpdatePermissionsAsync(created.Id,
                new List<string> { PermissionConstants.AuditView, PermissionConstants.UsersManage, PermissionConstants.SamplesApprove }, actor.Id));

        Assert.Contains("Users.Manage + Samples.Approve", ex.Message);
        Assert.Equal(grantsBefore, await CountRolePermissionsAsync(db, created.Id));
        Assert.Equal(auditCountBefore, await db.AuditLogs.CountAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED"));
    }

    [Fact]
    public async Task UpdatePermissionsAsync_AllowsAdministrationWithoutGxp_AndGxpWithoutAdministration()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync("Split Role", null, RoleType.Analyst);
        var actor = await AddActingUserAsync(db);

        var administration = await service.UpdatePermissionsAsync(created.Id,
            new List<string> { PermissionConstants.UsersManage, PermissionConstants.RolesManage, PermissionConstants.AuditView }, actor.Id);
        var gxp = await service.UpdatePermissionsAsync(created.Id,
            new List<string> { PermissionConstants.SamplesApprove, PermissionConstants.TestWorkflowExecute }, actor.Id);

        Assert.Empty(administration.SodConflicts);
        Assert.Empty(gxp.SodConflicts);
    }

    // The seeded System Administrator still holds every code on a database
    // not yet migrated; saving it down to administration only must work.
    [Fact]
    public async Task UpdatePermissionsAsync_AcceptsRemovingTheConflictsOfAnAlreadyViolatingRole()
    {
        var db = CreateDbContext();
        await SeedAdministratorGxpGrantAsync(db);
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db);
        Assert.NotEmpty((await service.GetByIdAsync(1))!.SodConflicts);

        var updated = await service.UpdatePermissionsAsync(1, AllCodesExcept("none"), actor.Id);

        Assert.Empty(updated.SodConflicts);
        Assert.Empty((await service.GetByIdAsync(1))!.SodConflicts);
        Assert.Contains(PermissionConstants.UsersManage, updated.PermissionCodes);
    }

    [Fact]
    public async Task UpdatePermissionsAsync_RejectsASaveThatKeepsSomeOfAnExistingViolation()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var actor = await AddActingUserAsync(db);
        var codes = AllCodesExcept("none");
        codes.Add(PermissionConstants.OosManage);

        await Assert.ThrowsAsync<SegregationOfDutiesViolationException>(() => service.UpdatePermissionsAsync(1, codes, actor.Id));
    }

    [Fact]
    public async Task CreateAsync_RejectsABaseRoleWhoseGrantsWouldConflict_AndSavesNothing()
    {
        var db = CreateDbContext();
        await SeedAdministratorGxpGrantAsync(db);
        var service = CreateService(db);
        var rolesBefore = await db.Roles.CountAsync();

        var ex = await Assert.ThrowsAsync<SegregationOfDutiesViolationException>(() =>
            service.CreateAsync("Admin Copy", null, RoleType.SystemAdministrator));

        Assert.Contains("Roles.Manage + ", ex.Message);
        Assert.Equal(rolesBefore, await db.Roles.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_AcceptsABaseRoleWithoutConflicts()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync("Section Copy", null, RoleType.SectionHead);

        Assert.Empty(created.SodConflicts);
    }

    [Fact]
    public async Task GetAllAsync_ListsTheConflictsOfExistingRoles()
    {
        var db = CreateDbContext();
        await SeedAdministratorGxpGrantAsync(db);
        var service = CreateService(db);

        var roles = await service.GetAllAsync();

        Assert.Contains("Users.Manage + Samples.Approve", roles.Single(r => r.Id == 1).SodConflicts);
        Assert.Empty(roles.Single(r => r.Id == 4).SodConflicts);
    }

    private static RoleController CreateController(MicroLimsDbContext db, int actingUserId) => new(CreateService(db))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, actingUserId.ToString())
                }, "TestAuth"))
            }
        }
    };

    [Fact]
    public async Task Controller_MapsASegregationOfDutiesViolationToConflict_OnUpdatePermissionsAndCreate()
    {
        var db = CreateDbContext();
        await SeedAdministratorGxpGrantAsync(db);
        var actor = await AddActingUserAsync(db);
        var controller = CreateController(db, actor.Id);
        var created = await CreateService(db).CreateAsync("Mixed Role", null, RoleType.Analyst);

        var update = await controller.UpdatePermissions(created.Id,
            new UpdateRolePermissionsRequest(new List<string> { PermissionConstants.RolesManage, PermissionConstants.MediaRelease }));
        var create = await controller.Create(new CreateRoleRequest("Admin Copy", null, RoleType.SystemAdministrator));

        Assert.IsType<ConflictObjectResult>(update);
        Assert.IsType<ConflictObjectResult>(create);
    }

    [Fact]
    public void Controller_GetSodRules_ReturnsBothSets()
    {
        var controller = CreateController(CreateDbContext(), 1);

        var ok = Assert.IsType<OkObjectResult>(controller.GetSodRules());

        var body = Assert.IsType<MicroLIMS.Shared.Responses.ApiResponse<SodRulesDto>>(ok.Value);
        Assert.Contains(PermissionConstants.UsersManage, body.Data!.AdministrationCodes);
        Assert.Contains(PermissionConstants.DocumentsApprove, body.Data.GxpExecutionAndApprovalCodes);
    }
}
