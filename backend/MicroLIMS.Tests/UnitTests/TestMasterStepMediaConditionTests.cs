using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TestMasterStepMediaConditionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(
            new DbContextOptionsBuilder<MicroLimsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.CurrentUserId = 1;
        return db;
    }

    private static MasterDataController CreateController(MicroLimsDbContext db) =>
        new(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            new UserSectionScopeService(db),
            new ChromatographyColumnService(db, new UserSectionScopeService(db)));

    private static async Task<TestDefinition> SeedTestDefinitionAsync(MicroLimsDbContext db)
    {
        var testDefinition = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "TAMC",
            WorkflowType = WorkflowType.CountTest,
            SectionId = TestServiceFactory.EnsureMicroSection(db).Id
        };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();
        return testDefinition;
    }

    private static async Task<Material> SeedMaterialAsync(
        MicroLimsDbContext db,
        MediaProduct? productToLink = null,
        string materialName = "Tryptic Soy Agar")
    {
        var material = new Material
        {
            SectionId = TestServiceFactory.EnsureMicroSection(db).Id,
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = materialName,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };

        if (productToLink != null)
        {
            MediaProductTestData.Link(material, productToLink);
        }

        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return material;
    }

    private static CreateTestWorkflowStepRequest CreateRequest(int materialId, int? conditionId) =>
        new(
            "CountIncubation",
            0,
            0,
            0m,
            0m,
            true,
            StepType.PlateCount,
            null,
            new List<StepMediaRequest> { new(materialId, true, 1, conditionId) },
            false,
            null,
            null,
            null);

    [Fact]
    public async Task CreateStep_MediumWithCondition_CopiesConditionSnapshot()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35m, 37m);
        var material = await SeedMaterialAsync(db, product);

        var request = CreateRequest(material.Id, condition.Id);
        var result = await controller.CreateTestWorkflowStep(testDefinition.Id, request);

        Assert.IsType<OkObjectResult>(result);
        var stepMedia = await db.TestWorkflowStepMedias.SingleAsync();
        Assert.Equal(condition.Id, stepMedia.MediaIncubationConditionId);
        Assert.Equal(35m, stepMedia.TempMin);
        Assert.Equal(37m, stepMedia.TempMax);
        Assert.Equal(18, stepMedia.IncubationMinHours);
        Assert.Equal(24, stepMedia.IncubationMaxHours);
    }

    [Fact]
    public async Task CreateStep_MediumWithoutCondition_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var material = await SeedMaterialAsync(db, product);

        var request = CreateRequest(material.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Contains("Choose an incubation condition for medium 'Tryptic Soy Agar'", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_MaterialNotLinkedToProduct_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35m, 37m);
        var material = await SeedMaterialAsync(db, productToLink: null, materialName: "Tryptic Soy Agar");

        var request = CreateRequest(material.Id, condition.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Contains("isn't linked to a media product", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_UnknownCondition_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var material = await SeedMaterialAsync(db, product);

        var request = CreateRequest(material.Id, 9999);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Equal("Incubation condition 9999 not found.", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_ConditionOfDifferentProduct_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var material = await SeedMaterialAsync(db, product);

        var otherProduct = await MediaProductTestData.CreateOrGetAsync(db, "R2A Agar", "R2A");
        var condition = await MediaProductTestData.AddConditionAsync(db, otherProduct, 18, 24, 35m, 37m);

        var request = CreateRequest(material.Id, condition.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Contains("belongs to a different media product", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_UnknownMaterial_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var request = CreateRequest(9999, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Equal("Material 9999 not found.", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    private static UpdateTestWorkflowStepRequest CreateUpdateRequest(int materialId, int? conditionId) =>
        new(
            "CountIncubation",
            0,
            0,
            0m,
            0m,
            true,
            StepType.PlateCount,
            null,
            new List<StepMediaRequest> { new(materialId, true, 1, conditionId) },
            false,
            null,
            null,
            null);

    [Fact]
    public async Task UpdateStep_SwitchesCondition_RecopiesSnapshot()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var material = await SeedMaterialAsync(db, product);
        var conditionA = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        var conditionB = await MediaProductTestData.AddConditionAsync(db, product, 48, 72, 20m, 25m);

        var createRequest = CreateRequest(material.Id, conditionA.Id);
        await controller.CreateTestWorkflowStep(testDefinition.Id, createRequest);
        var stepId = (await db.TestWorkflowSteps.SingleAsync()).Id;

        var updateRequest = CreateUpdateRequest(material.Id, conditionB.Id);
        var result = await controller.UpdateTestWorkflowStep(stepId, updateRequest);

        Assert.IsType<OkObjectResult>(result);
        var stepMedia = await db.TestWorkflowStepMedias.SingleAsync();
        Assert.Equal(conditionB.Id, stepMedia.MediaIncubationConditionId);
        Assert.Equal(20m, stepMedia.TempMin);
        Assert.Equal(25m, stepMedia.TempMax);
        Assert.Equal(48, stepMedia.IncubationMinHours);
        Assert.Equal(72, stepMedia.IncubationMaxHours);
    }

    [Fact]
    public async Task UpdateStep_WithoutCondition_ThrowsAndKeepsExistingStepMedia()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var material = await SeedMaterialAsync(db, product);
        var conditionA = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);

        var createRequest = CreateRequest(material.Id, conditionA.Id);
        await controller.CreateTestWorkflowStep(testDefinition.Id, createRequest);
        var stepId = (await db.TestWorkflowSteps.SingleAsync()).Id;

        var updateRequest = CreateUpdateRequest(material.Id, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.UpdateTestWorkflowStep(stepId, updateRequest));

        Assert.Contains("Choose an incubation condition", ex.Message);

        db.ChangeTracker.Clear();
        var stepMedia = await db.TestWorkflowStepMedias.SingleAsync();
        Assert.Equal(conditionA.Id, stepMedia.MediaIncubationConditionId);
        Assert.Equal(30m, stepMedia.TempMin);
        Assert.Equal(35m, stepMedia.TempMax);
        Assert.Equal(24, stepMedia.IncubationMinHours);
        Assert.Equal(48, stepMedia.IncubationMaxHours);
    }
}
