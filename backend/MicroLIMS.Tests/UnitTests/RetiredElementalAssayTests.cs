using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Slice R: the old calibration-run / elemental assay path is retired; ICP method assay replaces it.
public class RetiredElementalAssayTests
{
    private const string Retired = "Elemental assay tests are retired; use an ICP method assay test.";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };

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

    private static async Task AssertRetiredAsync(Func<Task> act) =>
        Assert.Equal(Retired, (await Assert.ThrowsAsync<InvalidOperationException>(act)).Message);

    [Fact]
    public async Task CreateTest_ElementalAssay_Refused()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        await AssertRetiredAsync(() => TestServiceFactory.TestDefinitionMaster(db)
            .CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(
                Code: "EA-1", DisplayName: "EA", SectionId: section.Id, WorkflowType: WorkflowType.ElementalAssay)));
    }

    [Fact]
    public async Task CreateTest_CalibrationCurveEquation_Refused()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        await AssertRetiredAsync(() => TestServiceFactory.TestDefinitionMaster(db)
            .CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(
                Code: "CC-1", DisplayName: "CC", SectionId: section.Id, EquationType: EquationType.CalibrationCurve)));
    }

    [Fact]
    public async Task Spec_OnRetiredElementalTest_Refused()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        // A legacy row: WorkflowType 3 (ElementalAssay) seeded directly.
        db.TestDefinitions.Add(new TestDefinition { Code = "EA-OLD", DisplayName = "EA", SectionId = section.Id, WorkflowType = WorkflowType.ElementalAssay });
        var item = new Item { Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1", AssignedTests = { new SampleTest { TestCode = "EA-OLD", DisplayName = "EA" } } };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        await AssertRetiredAsync(() => new SpecificationMasterDataService(db, new UserSectionScopeService(db))
            .CreateSpecificationAsync(userId, new CreateSpecificationRequest(
                ItemId: item.Id, TestCode: "EA-OLD", ParameterName: "Zn", LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m)));
    }
}
