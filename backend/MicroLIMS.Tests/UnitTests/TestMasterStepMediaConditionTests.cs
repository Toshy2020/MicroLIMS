using MicroLIMS.Application.DTOs;
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

    private static MasterDataControllers CreateController(MicroLimsDbContext db) =>
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

    private static CreateTestWorkflowStepRequest CreateRequest(int productId, int? conditionId) =>
        new(
            "CountIncubation",
            0,
            0,
            0m,
            0m,
            true,
            StepType.PlateCount,
            null,
            new List<StepMediaRequest> { new(productId, true, 1, conditionId) },
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

        var request = CreateRequest(product.Id, condition.Id);
        var result = await controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, request);

        Assert.IsType<OkObjectResult>(result);
        var stepMedia = await db.TestWorkflowStepMedias.SingleAsync();
        Assert.Equal(product.Id, stepMedia.MediaProductId);
        Assert.Null(stepMedia.MaterialId);
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

        var request = CreateRequest(product.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Contains("Choose an incubation condition for medium 'Tryptic Soy Agar'", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_InactiveProduct_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35m, 37m);
        product.IsActive = false;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, CreateRequest(product.Id, condition.Id)));

        Assert.Contains("'Tryptic Soy Agar' is inactive", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_UnknownCondition_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");

        var request = CreateRequest(product.Id, 9999);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, request));

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

        var otherProduct = await MediaProductTestData.CreateOrGetAsync(db, "R2A Agar", "R2A");
        var condition = await MediaProductTestData.AddConditionAsync(db, otherProduct, 18, 24, 35m, 37m);

        var request = CreateRequest(product.Id, condition.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Contains("belongs to a different media product", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    [Fact]
    public async Task CreateStep_UnknownProduct_Throws()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var request = CreateRequest(9999, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Equal("Media product 9999 not found.", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }

    private static UpdateTestWorkflowStepRequest CreateUpdateRequest(int productId, int? conditionId) =>
        new(
            "CountIncubation",
            0,
            0,
            0m,
            0m,
            true,
            StepType.PlateCount,
            null,
            new List<StepMediaRequest> { new(productId, true, 1, conditionId) },
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
        var conditionA = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        var conditionB = await MediaProductTestData.AddConditionAsync(db, product, 48, 72, 20m, 25m);

        var createRequest = CreateRequest(product.Id, conditionA.Id);
        await controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, createRequest);
        var stepId = (await db.TestWorkflowSteps.SingleAsync()).Id;

        var updateRequest = CreateUpdateRequest(product.Id, conditionB.Id);
        var result = await controller.TestWorkflowStep.UpdateTestWorkflowStep(stepId, updateRequest);

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
        var conditionA = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);

        var createRequest = CreateRequest(product.Id, conditionA.Id);
        await controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, createRequest);
        var stepId = (await db.TestWorkflowSteps.SingleAsync()).Id;

        var updateRequest = CreateUpdateRequest(product.Id, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.UpdateTestWorkflowStep(stepId, updateRequest));

        Assert.Contains("Choose an incubation condition", ex.Message);

        db.ChangeTracker.Clear();
        var stepMedia = await db.TestWorkflowStepMedias.SingleAsync();
        Assert.Equal(conditionA.Id, stepMedia.MediaIncubationConditionId);
        Assert.Equal(30m, stepMedia.TempMin);
        Assert.Equal(35m, stepMedia.TempMax);
        Assert.Equal(24, stepMedia.IncubationMinHours);
        Assert.Equal(48, stepMedia.IncubationMaxHours);
    }

    [Fact]
    public async Task UpdateStep_InactiveProductAlreadyOnTheStep_KeepsWorking_ButAnInactiveNewOneIsRefused()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        await controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, CreateRequest(product.Id, condition.Id));
        var stepId = (await db.TestWorkflowSteps.SingleAsync()).Id;

        var other = await MediaProductTestData.CreateOrGetAsync(db, "R2A Agar", "R2A");
        var otherCondition = await MediaProductTestData.AddConditionAsync(db, other, 24, 48, 30m, 35m);
        product.IsActive = false;
        other.IsActive = false;
        await db.SaveChangesAsync();

        // Saving the step again with the product it already has is fine.
        Assert.IsType<OkObjectResult>(await controller.TestWorkflowStep.UpdateTestWorkflowStep(stepId, CreateUpdateRequest(product.Id, condition.Id)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.UpdateTestWorkflowStep(stepId, CreateUpdateRequest(other.Id, otherCondition.Id)));
        Assert.Contains("'R2A Agar' is inactive", ex.Message);

        db.ChangeTracker.Clear();
        Assert.Equal(product.Id, (await db.TestWorkflowStepMedias.SingleAsync()).MediaProductId);
    }

    [Fact]
    public async Task CreateStep_SameProductTwice_FailsTemplateValidation()
    {
        using var db = NewDb();
        var controller = CreateController(db);
        var testDefinition = await SeedTestDefinitionAsync(db);

        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 18, 24, 35m, 37m);
        var request = CreateRequest(product.Id, condition.Id) with
        {
            StepMedia = new List<StepMediaRequest> { new(product.Id, false, 1, condition.Id), new(product.Id, false, 2, condition.Id) }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.TestWorkflowStep.CreateTestWorkflowStep(testDefinition.Id, request));

        Assert.Contains("more than once", ex.Message);
        Assert.Empty(db.TestWorkflowSteps);
    }
}
