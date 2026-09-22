using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Slice 1 of the FP Standard-Comparison Assay rework: per-(TestDefinition,
// ProductionStageRole) replicate counts (TestDefinitionStageReplicate) and
// the StageReplicateResolver read helper.
public class TestDefinitionStageReplicateTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (MasterDataController controller, User admin) SetupAdminController(MicroLimsDbContext db)
    {
        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.Add(adminRole);
        db.SaveChanges();

        var admin = new User
        {
            Username = "sysadmin_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "System Admin",
            RoleId = adminRole.Id,
            Role = adminRole,
            IsActive = true
        };
        db.Users.Add(admin);
        db.SaveChanges();

        var scope = new UserSectionScopeService(db);
        var colService = new ChromatographyColumnService(db, scope);
        var controller = new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            scope,
            colService);

        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, admin.Id.ToString()) },
                    "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, admin);
    }

    private static async Task<TestDefinition> SeedTestDefinitionAsync(MicroLimsDbContext db, string code = "SCA-TEST")
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var testDef = new TestDefinition { Code = code, DisplayName = code, SectionId = micro.Id, IsActive = true };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();
        return testDef;
    }

    // ---- Controller CRUD validation ----

    [Fact]
    public async Task CreateTestDefinitionStageReplicate_Valid_Persists()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var result = await controller.CreateTestDefinitionStageReplicate(
            testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Finished, 6, 2));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        var dto = Assert.IsType<TestDefinitionStageReplicateDto>(response.Data);

        Assert.Equal(ProductionStageRole.Finished, dto.Role);
        Assert.Equal(6, dto.StandardReplicates);
        Assert.Equal(2, dto.SampleReplicates);

        var persisted = await db.TestDefinitionStageReplicates.SingleAsync(r => r.TestDefinitionId == testDef.Id);
        Assert.Equal(6, persisted.StandardReplicates);
        Assert.Equal(2, persisted.SampleReplicates);
    }

    [Fact]
    public async Task CreateTestDefinitionStageReplicate_StandardReplicatesBelowOne_Rejected()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 0, 1)));
        Assert.Contains("Standard replicates must be at least 1", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinitionStageReplicate_SampleReplicatesBelowOne_Rejected()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 1, 0)));
        Assert.Contains("Sample replicates must be at least 1", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinitionStageReplicate_DuplicateRoleForSameTest_Rejected()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 6, 1));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 3, 2)));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateTestDefinitionStageReplicate_SameTestDifferentRoles_BothAllowed()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 6, 1));
        await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Finished, 6, 2));
        await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Stability, 3, 3));

        var count = await db.TestDefinitionStageReplicates.CountAsync(r => r.TestDefinitionId == testDef.Id);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task UpdateTestDefinitionStageReplicate_BelowOne_Rejected()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var created = await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 6, 1));
        var dto = Assert.IsType<TestDefinitionStageReplicateDto>(Assert.IsType<ApiResponse<object>>(Assert.IsType<OkObjectResult>(created).Value).Data);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.UpdateTestDefinitionStageReplicate(testDef.Id, dto.Id, new UpdateTestDefinitionStageReplicateRequest(StandardReplicates: 0)));
        Assert.Contains("Standard replicates must be at least 1", ex.Message);
    }

    [Fact]
    public async Task UpdateTestDefinitionStageReplicate_Valid_Persists()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var created = await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 6, 1));
        var dto = Assert.IsType<TestDefinitionStageReplicateDto>(Assert.IsType<ApiResponse<object>>(Assert.IsType<OkObjectResult>(created).Value).Data);

        await controller.UpdateTestDefinitionStageReplicate(testDef.Id, dto.Id, new UpdateTestDefinitionStageReplicateRequest(StandardReplicates: 3, SampleReplicates: 2));

        var reloaded = await db.TestDefinitionStageReplicates.FirstAsync(r => r.Id == dto.Id);
        Assert.Equal(3, reloaded.StandardReplicates);
        Assert.Equal(2, reloaded.SampleReplicates);
    }

    [Fact]
    public async Task DeleteTestDefinitionStageReplicate_RemovesRow()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var created = await controller.CreateTestDefinitionStageReplicate(testDef.Id, new CreateTestDefinitionStageReplicateRequest(ProductionStageRole.Bulk, 6, 1));
        var dto = Assert.IsType<TestDefinitionStageReplicateDto>(Assert.IsType<ApiResponse<object>>(Assert.IsType<OkObjectResult>(created).Value).Data);

        await controller.DeleteTestDefinitionStageReplicate(testDef.Id, dto.Id);

        Assert.False(await db.TestDefinitionStageReplicates.AnyAsync(r => r.Id == dto.Id));
    }

    [Fact]
    public async Task GetTestDefinitionStageReplicates_TestWithNoRows_ReturnsEmpty_AndIsStillValid()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);
        var testDef = await SeedTestDefinitionAsync(db);

        var result = await controller.GetTestDefinitionStageReplicates(testDef.Id);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        var list = Assert.IsAssignableFrom<IEnumerable<TestDefinitionStageReplicateDto>>(response.Data);
        Assert.Empty(list);
    }

    // ---- StageReplicateResolver ----

    private static async Task<(TestDefinition testDef, ProductionStage stage, Sample sample)> SeedResolverScenarioAsync(
        MicroLimsDbContext db, ProductionStageRole role, bool sampleHasStage = true)
    {
        var testDef = await SeedTestDefinitionAsync(db, "RESOLVER-TEST");
        var stage = new ProductionStage { Name = "Resolver Stage", Role = role };
        db.ProductionStages.Add(stage);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "FP-RESOLVER-1",
            Category = SampleCategory.FinishedProduct,
            ControlNumber = "C1",
            SampledBy = "Analyst",
            ReceivedByUserId = 1,
            ProductionStage = sampleHasStage ? stage.Name : null,
            ProductionStageId = sampleHasStage ? stage.Id : null
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder { SampleId = sample.Id, TestCode = testDef.Code, SectionId = testDef.SectionId };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        return (testDef, stage, sample);
    }

    [Fact]
    public async Task Resolver_SampleWithNoStage_ReturnsSampleStageNotReconciled()
    {
        using var db = NewDb();
        var (testDef, _, sample) = await SeedResolverScenarioAsync(db, ProductionStageRole.Bulk, sampleHasStage: false);

        var resolution = await StageReplicateResolver.ResolveAsync(db, sample.Id, testDef.Id);

        Assert.Equal(StageReplicateResolutionStatus.SampleStageNotReconciled, resolution.Status);
        Assert.False(resolution.IsConfigured);
        Assert.Null(resolution.StandardReplicates);
        Assert.Null(resolution.SampleReplicates);
        Assert.NotNull(resolution.Message);
    }

    [Fact]
    public async Task Resolver_StageResolvesButNoReplicateRow_ReturnsNotConfigured()
    {
        using var db = NewDb();
        var (testDef, _, sample) = await SeedResolverScenarioAsync(db, ProductionStageRole.Finished);

        var resolution = await StageReplicateResolver.ResolveAsync(db, sample.Id, testDef.Id);

        Assert.Equal(StageReplicateResolutionStatus.NotConfiguredForStage, resolution.Status);
        Assert.False(resolution.IsConfigured);
        Assert.Equal(ProductionStageRole.Finished, resolution.StageRole);
        Assert.Null(resolution.StandardReplicates);
        Assert.Null(resolution.SampleReplicates);
    }

    [Fact]
    public async Task Resolver_ConfiguredStage_ReturnsCounts()
    {
        using var db = NewDb();
        var (testDef, _, sample) = await SeedResolverScenarioAsync(db, ProductionStageRole.Finished);

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Finished,
            StandardReplicates = 6,
            SampleReplicates = 2
        });
        await db.SaveChangesAsync();

        var resolution = await StageReplicateResolver.ResolveAsync(db, sample.Id, testDef.Id);

        Assert.Equal(StageReplicateResolutionStatus.Resolved, resolution.Status);
        Assert.True(resolution.IsConfigured);
        Assert.Equal(ProductionStageRole.Finished, resolution.StageRole);
        Assert.Equal(6, resolution.StandardReplicates);
        Assert.Equal(2, resolution.SampleReplicates);
        Assert.Null(resolution.Message);
    }

    [Fact]
    public async Task Resolver_DifferentRoleConfigured_StillNotConfiguredForSamplesRole()
    {
        using var db = NewDb();
        var (testDef, _, sample) = await SeedResolverScenarioAsync(db, ProductionStageRole.Stability);

        // Only Bulk is configured - the sample resolves to Stability.
        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Bulk,
            StandardReplicates = 6,
            SampleReplicates = 1
        });
        await db.SaveChangesAsync();

        var resolution = await StageReplicateResolver.ResolveAsync(db, sample.Id, testDef.Id);

        Assert.Equal(StageReplicateResolutionStatus.NotConfiguredForStage, resolution.Status);
        Assert.Equal(ProductionStageRole.Stability, resolution.StageRole);
    }

    [Fact]
    public async Task Resolver_ResolveForTestOrderAsync_ResolvesViaOrder()
    {
        using var db = NewDb();
        var (testDef, _, sample) = await SeedResolverScenarioAsync(db, ProductionStageRole.Bulk);
        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Bulk,
            StandardReplicates = 6,
            SampleReplicates = 1
        });
        await db.SaveChangesAsync();

        var order = await db.TestOrders.FirstAsync(o => o.SampleId == sample.Id);
        var resolution = await StageReplicateResolver.ResolveForTestOrderAsync(db, order.Id);

        Assert.Equal(StageReplicateResolutionStatus.Resolved, resolution.Status);
        Assert.Equal(6, resolution.StandardReplicates);
        Assert.Equal(1, resolution.SampleReplicates);
    }
}
