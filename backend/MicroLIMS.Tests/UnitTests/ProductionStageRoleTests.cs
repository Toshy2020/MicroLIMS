using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Slice 1 of the FP Standard-Comparison Assay rework: ProductionStage.Role
// (code-meaningful classification), Sample.ProductionStageId (FK alongside
// the historical string), and ProductWorkflowEngine.ReceiveAsync resolving
// it at receiving time.
public class ProductionStageRoleTests
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

    // ---- Entity default ----

    [Fact]
    public void ProductionStage_DefaultRole_IsOther()
    {
        var stage = new ProductionStage { Name = "Unclassified" };
        Assert.Equal(ProductionStageRole.Other, stage.Role);
        Assert.Equal(0, (int)ProductionStageRole.Other);
    }

    // ---- MasterDataController CRUD carries Role ----

    [Fact]
    public async Task CreateProductionStage_PersistsRole()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);

        var result = await controller.CreateProductionStage(new CreateProductionStageRequest("Stability-Test", ProductionStageRole.Stability));
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        var entity = Assert.IsType<ProductionStage>(response.Data);

        Assert.Equal(ProductionStageRole.Stability, entity.Role);

        var reloaded = await db.ProductionStages.FirstAsync(s => s.Id == entity.Id);
        Assert.Equal(ProductionStageRole.Stability, reloaded.Role);
    }

    [Fact]
    public async Task UpdateProductionStage_ChangesRole()
    {
        using var db = NewDb();
        var (controller, _) = SetupAdminController(db);

        var stage = new ProductionStage { Name = "Custom", Role = ProductionStageRole.Other };
        db.ProductionStages.Add(stage);
        await db.SaveChangesAsync();

        await controller.UpdateProductionStage(stage.Id, new UpdateProductionStageRequest("Custom", ProductionStageRole.Bulk));

        var reloaded = await db.ProductionStages.FirstAsync(s => s.Id == stage.Id);
        Assert.Equal(ProductionStageRole.Bulk, reloaded.Role);
    }

    // ---- ProductWorkflowEngine.ReceiveAsync sets ProductionStageId ----

    private static async Task<Item> SeedFpItemAsync(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        db.TestDefinitions.Add(new TestDefinition { Code = "FP-TEST", DisplayName = "FP Test", SectionId = micro.Id });
        var item = new Item { Name = "Tablet", Code = "TAB-01", Category = SampleCategory.FinishedProduct, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = "FP-TEST" });
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static async Task<Item> SeedRawMaterialItemAsync(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        db.TestDefinitions.Add(new TestDefinition { Code = "RM-TEST", DisplayName = "RM Test", SectionId = micro.Id });
        var item = new Item { Name = "Raw Material", Code = "RM-01", Category = SampleCategory.RawMaterial, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = "RM-TEST" });
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task ReceiveAsync_FinishedProduct_RecognisedStageName_SetsStringAndFK()
    {
        using var db = NewDb();
        var stage = new ProductionStage { Name = "F.P", Role = ProductionStageRole.Finished };
        db.ProductionStages.Add(stage);
        var item = await SeedFpItemAsync(db);

        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, 1, "10 units", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "F.P", 1));

        Assert.Equal("F.P", sample.ProductionStage);
        Assert.Equal(stage.Id, sample.ProductionStageId);
    }

    [Fact]
    public async Task ReceiveAsync_FinishedProduct_UnknownOrBlankStage_IsRefused()
    {
        using var db = NewDb();
        db.ProductionStages.Add(new ProductionStage { Name = "F.P", Role = ProductionStageRole.Finished });
        var item = await SeedFpItemAsync(db);

        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, 1, "10 units", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Some Renamed Stage", 1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, 1, "10 units", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "  ", 1)));
        Assert.Empty(db.Samples);
    }

    [Fact]
    public async Task ReceiveAsync_NonFinishedProduct_SetsNeitherStringNorFK()
    {
        using var db = NewDb();
        db.ProductionStages.Add(new ProductionStage { Name = "F.P", Role = ProductionStageRole.Finished });
        var item = await SeedRawMaterialItemAsync(db);

        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, 1, "10 units", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "F.P", 1));

        Assert.Null(sample.ProductionStage);
        Assert.Null(sample.ProductionStageId);
    }

    // ---- Deleting a stage leaves historical samples intact ----

    [Fact]
    public async Task DeleteProductionStage_LeavesHistoricalSampleIntact_FKIsSetNull()
    {
        using var db = NewDb();
        var stage = new ProductionStage { Name = "F.P", Role = ProductionStageRole.Finished };
        db.ProductionStages.Add(stage);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "FP-DEL-1",
            Category = SampleCategory.FinishedProduct,
            ControlNumber = "C1",
            SampledBy = "Analyst",
            ReceivedByUserId = 1,
            ProductionStage = "F.P",
            ProductionStageId = stage.Id
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.ProductionStages.Remove(stage);
        await db.SaveChangesAsync();

        var reloaded = await db.Samples.AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Null(reloaded.ProductionStageId);
        // The historical string snapshot is untouched by the stage's deletion.
        Assert.Equal("F.P", reloaded.ProductionStage);
    }
}
