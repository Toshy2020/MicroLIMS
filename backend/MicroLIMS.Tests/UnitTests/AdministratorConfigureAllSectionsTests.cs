using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Sections.ConfigureAll: the System Administrator configures master data and settings in ANY section,
// but never writes GxP records (those follow membership). A Section Head holding MasterData.Manage
// without Sections.ConfigureAll stays limited to its own sections.
public class AdministratorConfigureAllSectionsTests
{
    private sealed record World(
        MicroLimsDbContext Db, User Admin, User Head, DocumentSection Micro, DocumentSection Fp,
        TestDefinition MicroTest, TestDefinition FpTest);

    private static World Seed()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var type in Enum.GetValues<RoleType>())
            db.Roles.Add(new Role { Type = type, Name = type.ToString(), IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);

        int RoleOf(RoleType kind) => db.Roles.First(r => r.Type == kind).Id;
        User NewUser(string name, RoleType kind) => new() { Username = name, FullName = name, PasswordHash = "x", RoleId = RoleOf(kind), IsActive = true };
        var admin = NewUser("admin", RoleType.SystemAdministrator);
        var head = NewUser("head", RoleType.SectionHead);
        db.Users.AddRange(admin, head);
        db.SaveChanges();

        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Chemistry", Code = "CHEM", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        db.SaveChanges();
        TestServiceFactory.AssignUserToMicroSection(db, head.Id); // the head belongs to Micro only; the admin to nothing

        var microTest = new TestDefinition { Code = "MCFG", DisplayName = "Micro cfg", SectionId = micro.Id, WorkflowType = WorkflowType.CountTest };
        var fpTest = new TestDefinition { Code = "FCFG", DisplayName = "FP cfg", SectionId = fp.Id, WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.AddRange(microTest, fpTest);
        db.SaveChanges();
        return new World(db, admin, head, micro, fp, microTest, fpTest);
    }

    private static TestDefinitionMasterDataService TestMaster(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new AuditEventService(db, new DatabaseSequenceHelper(db)));

    [Fact]
    public async Task Scope_AdministratorIsUnrestricted_HeadFollowsMembership()
    {
        var w = Seed();
        var scope = new UserSectionScopeService(w.Db);

        Assert.Null(await scope.GetConfigurableSectionIdsAsync(w.Admin.Id));
        Assert.Equal(new[] { w.Micro.Id }, await scope.GetConfigurableSectionIdsAsync(w.Head.Id));
        // Writing GxP records is still membership for the administrator: none.
        Assert.Empty(await scope.GetMemberSectionIdsAsync(w.Admin.Id));
    }

    [Fact]
    public async Task Administrator_WithoutMembership_CanCreateTestsInAnySection_AndEditSpecificationOwnership()
    {
        var w = Seed();
        var scope = new UserSectionScopeService(w.Db);

        Assert.Equal(w.Fp.Id, await scope.ResolveSectionForCreateAsync(w.Admin.Id, w.Fp.Id, access: SectionAccess.Configure));
        Assert.Equal(w.Micro.Id, await scope.ResolveSectionForCreateAsync(w.Admin.Id, w.Micro.Id, access: SectionAccess.Configure));
        // Without a requested section the administrator must choose one.
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ResolveSectionForCreateAsync(w.Admin.Id, null, access: SectionAccess.Configure));
        // The same call with Write access (a GxP create) is still membership.
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ResolveSectionForCreateAsync(w.Admin.Id, w.Fp.Id));

        // Specifications belong to the section of their test: allowed in either section.
        await SpecificationOwnership.EnsureCanEditAsync(w.Db, scope, w.Admin.Id, w.FpTest.Code);
        await SpecificationOwnership.EnsureCanEditAsync(w.Db, scope, w.Admin.Id, w.MicroTest.Code);
    }

    [Fact]
    public async Task Administrator_CanUpdateATestDefinition_InAnySection()
    {
        var w = Seed();
        var svc = TestMaster(w.Db);

        var updated = await svc.UpdateTestDefinitionAsync(w.Admin.Id, w.FpTest.Id, new UpdateTestDefinitionRequest(w.FpTest.Code, "FP renamed"));
        Assert.Equal("FP renamed", updated.DisplayName);
        updated = await svc.UpdateTestDefinitionAsync(w.Admin.Id, w.MicroTest.Id, new UpdateTestDefinitionRequest(w.MicroTest.Code, "Micro renamed"));
        Assert.Equal("Micro renamed", updated.DisplayName);
    }

    [Fact]
    public async Task SectionHead_WithoutConfigureAll_StaysLimitedToMemberSections()
    {
        var w = Seed();
        var scope = new UserSectionScopeService(w.Db);
        var headCodes = await new PermissionService(w.Db).GetPermissionCodesForRoleAsync(w.Head.RoleId);
        Assert.Contains(PermissionConstants.MasterDataManage, headCodes);
        Assert.DoesNotContain(PermissionConstants.SectionsConfigureAll, headCodes);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestMaster(w.Db).UpdateTestDefinitionAsync(w.Head.Id, w.FpTest.Id, new UpdateTestDefinitionRequest(w.FpTest.Code, "nope")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SpecificationOwnership.EnsureCanEditAsync(w.Db, scope, w.Head.Id, w.FpTest.Code));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ResolveSectionForCreateAsync(w.Head.Id, w.Fp.Id, access: SectionAccess.Configure));

        // ...and still works inside its own section.
        await SpecificationOwnership.EnsureCanEditAsync(w.Db, scope, w.Head.Id, w.MicroTest.Code);
        var updated = await TestMaster(w.Db).UpdateTestDefinitionAsync(w.Head.Id, w.MicroTest.Id, new UpdateTestDefinitionRequest(w.MicroTest.Code, "Micro by head"));
        Assert.Equal("Micro by head", updated.DisplayName);
    }

    [Fact]
    public async Task Administrator_StillCannotWriteGxpRecords_InAnySection()
    {
        var w = Seed();
        var fpMaterial = new Material { SectionId = w.Fp.Id, MaterialName = "FP reagent", MaterialType = MaterialType.Chemical, QuantityReceived = 10, QuantityRemaining = 10, BatchNumber = "B1" };
        var microMaterial = new Material { SectionId = w.Micro.Id, MaterialName = "Micro reagent", MaterialType = MaterialType.Chemical, QuantityReceived = 10, QuantityRemaining = 10, BatchNumber = "B2" };
        w.Db.Materials.AddRange(fpMaterial, microMaterial);
        await w.Db.SaveChangesAsync();
        var service = new MaterialService(w.Db, new UserSectionScopeService(w.Db));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConsumeAsync(fpMaterial.Id, MaterialType.Chemical, 1m, w.Admin.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConsumeAsync(microMaterial.Id, MaterialType.Chemical, 1m, w.Admin.Id));

        var adminCodes = await new PermissionService(w.Db).GetPermissionCodesForRoleAsync(w.Admin.RoleId);
        Assert.Contains(PermissionConstants.SectionsConfigureAll, adminCodes);
        Assert.DoesNotContain(PermissionConstants.SamplesApprove, adminCodes);
        Assert.DoesNotContain(PermissionConstants.SamplesReview, adminCodes);
    }

    [Fact]
    public void SectionsConfigureAll_IsEnforced_GrantedToAdministrator_AndInNoSegregationConflict()
    {
        Assert.Contains(PermissionConstants.SectionsConfigureAll, PermissionConstants.All);
        Assert.Contains(PermissionConstants.SectionsConfigureAll, PermissionConstants.Enforced);
        Assert.Contains(PermissionConstants.SectionsConfigureAll, PermissionConstants.SystemAdministratorGrants);
        Assert.DoesNotContain(PermissionConstants.SectionsConfigureAll, SegregationOfDutiesRules.AdministrationCodes);
        Assert.DoesNotContain(PermissionConstants.SectionsConfigureAll, SegregationOfDutiesRules.GxpExecutionAndApprovalCodes);
    }
}
