using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class IcpMethodTestMasterTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<int> AddMethodAsync(MicroLimsDbContext db, int sectionId, int userId, bool active = true)
    {
        var std = new MaterialMasterEntry
        {
            SectionId = sectionId, Code = "RS-ICP-" + Guid.NewGuid().ToString("N")[..4], Name = "ICP standard",
            Category = MaterialMasterCategory.ReferenceStandard, BaseUnit = MaterialUnit.Gram, IsActive = true,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(std);
        await db.SaveChangesAsync();

        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(new SaveIcpMethodRequest(
            Name: "Minerals by ICP-OES", Abbreviation: "min-icp", EffectiveDate: new DateTime(2026, 10, 1),
            Mode: IcpMethodMode.MineralAssay, StandardLevelsMgPerL: "0.1, 0.5, 1",
            CalibrationStandardEntryId: std.Id, MinCorrelation: 0.999m, SampleVolumeMl: 50m,
            Elements: new() { new IcpElementInput(null, "zn", 213.857m, AnalyteView.Axial) }), userId);
        if (!active)
            await svc.SetActiveAsync(m.Id, false, "Test setup", userId);
        return m.Id;
    }

    private static CreateTestDefinitionRequest Req(int sectionId, int? icpMethodId, bool requiresSst = false) =>
        new(
            Code: "ICP-T1", DisplayName: "ICP Assay Test", SectionId: sectionId,
            WorkflowType: WorkflowType.IcpMethodAssay, EquationType: EquationType.IcpMethodAssay,
            RequiresSystemSuitability: requiresSst, MethodAbbreviation: "ICP-T1",
            IcpMethodId: icpMethodId);

    [Fact]
    public async Task TestDef_IcpAssay_LinksMethod()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var methodId = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var created = await service.CreateTestDefinitionAsync(userId, Req(section.Id, methodId));

        Assert.Equal(methodId, created.IcpMethodId);
        Assert.Equal(methodId, (await db.TestDefinitions.FirstAsync(t => t.Id == created.Id)).IcpMethodId);
    }

    [Fact]
    public async Task TestDef_IcpAssay_WithoutMethod_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, Req(section.Id, null)));
        Assert.Equal("ICP method is required for ICP method assay tests.", ex.Message);
    }

    [Fact]
    public async Task TestDef_IcpAssay_InactiveMethod_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var methodId = await AddMethodAsync(db, section.Id, userId, active: false);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, Req(section.Id, methodId)));
        Assert.Equal("The ICP method is inactive.", ex.Message);
    }

    [Fact]
    public async Task TestDef_Titration_WithIcpMethod_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var methodId = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var req = new CreateTestDefinitionRequest(
            Code: "OTH-1", DisplayName: "Other test", SectionId: section.Id,
            WorkflowType: WorkflowType.Measurement, EquationType: EquationType.Measurement,
            ReplicateCount: 3, EvaluationBasis: MeasurementEvaluationBasis.Mean,
            IcpMethodId: methodId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTestDefinitionAsync(userId, req));
        Assert.Equal("ICP method is only allowed for ICP method assay tests.", ex.Message);
    }

    [Fact]
    public async Task TestDef_IcpAssay_RequiresSst_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var methodId = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateTestDefinitionAsync(userId, Req(section.Id, methodId, requiresSst: true)));
        Assert.Equal("ICP method assay tests use the run calibration, not system suitability.", ex.Message);
    }
}
