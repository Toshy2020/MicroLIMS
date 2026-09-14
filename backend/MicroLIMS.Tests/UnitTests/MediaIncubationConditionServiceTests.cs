using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MediaIncubationConditionServiceTests
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

    private static async Task<MediaConfiguration> SeedConfigurationUsageAsync(
        MicroLimsDbContext db, MediaProduct product, MediaIncubationCondition condition)
    {
        var config = new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            MediaIncubationConditionId = condition.Id
        };
        db.MediaConfigurations.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    private static async Task<TestWorkflowStepMedia> SeedStepMediaUsageAsync(
        MicroLimsDbContext db, MediaIncubationCondition condition, string batchNumber = "BATCH-1")
    {
        var testDefinition = new TestDefinition
        {
            Code = $"TD_{Guid.NewGuid():N}"[..10],
            DisplayName = "Count Test",
            WorkflowType = WorkflowType.CountTest
        };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id,
            StepOrder = 1,
            StepName = "CountIncubation",
            IsFinalStep = true,
            StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = "Legacy TSA",
            ManufacturerName = "Himedia",
            BatchNumber = batchNumber,
            ReceivingDate = DateTime.UtcNow,
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var stepMedia = new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id,
            MaterialId = material.Id,
            MediaIncubationConditionId = condition.Id,
            TempMin = condition.TemperatureMin,
            TempMax = condition.TemperatureMax,
            IncubationMinHours = condition.IncubationMinHours,
            IncubationMaxHours = condition.IncubationMaxHours
        };
        db.TestWorkflowStepMedias.Add(stepMedia);
        await db.SaveChangesAsync();
        return stepMedia;
    }

    // --- Create ---

    [Fact]
    public async Task CreateAsync_ValidValues_SavesCondition()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");

        var condition = await service.CreateAsync(product.Id, 24, 48, 30m, 35m);

        Assert.NotNull(condition);
        Assert.Equal(product.Id, condition.MediaProductId);
        Assert.Equal(24, condition.IncubationMinHours);
        Assert.Equal(48, condition.IncubationMaxHours);
        Assert.Equal(30m, condition.TemperatureMin);
        Assert.Equal(35m, condition.TemperatureMax);

        var reloaded = await db.MediaIncubationConditions.AsNoTracking().SingleAsync(c => c.Id == condition.Id);
        Assert.Equal(product.Id, reloaded.MediaProductId);
        Assert.Equal(24, reloaded.IncubationMinHours);
        Assert.Equal(48, reloaded.IncubationMaxHours);
        Assert.Equal(30m, reloaded.TemperatureMin);
        Assert.Equal(35m, reloaded.TemperatureMax);
    }

    [Fact]
    public async Task CreateAsync_UnknownProduct_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(9999, 24, 48, 30m, 35m));
        Assert.Contains("Media product with ID 9999 not found", ex.Message);
    }

    [Theory]
    [InlineData(0, 24, 30, 35, "Incubation min hours must be greater than zero.")]
    [InlineData(48, 24, 30, 35, "Incubation max hours cannot be less than min hours.")]
    [InlineData(24, 48, 40, 35, "Temperature min cannot exceed max.")]
    public async Task CreateAsync_InvalidRanges_ThrowsAndNothingSaved(
        int minH, int maxH, decimal tMin, decimal tMax, string expectedMessage)
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(product.Id, minH, maxH, tMin, tMax));
        Assert.Equal(expectedMessage, ex.Message);
        Assert.False(await db.MediaIncubationConditions.AnyAsync());
    }

    [Fact]
    public async Task CreateAsync_SameValuesTwiceOnSameProduct_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");

        await service.CreateAsync(product.Id, 24, 48, 30m, 35m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(product.Id, 24, 48, 30m, 35m));
        Assert.Contains("already has an incubation condition", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_SameValuesOnDifferentProduct_Succeeds()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var productA = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var productB = await MediaProductTestData.CreateOrGetAsync(db, "SDA", "SDA");

        var conditionA = await service.CreateAsync(productA.Id, 24, 48, 30m, 35m);
        var conditionB = await service.CreateAsync(productB.Id, 24, 48, 30m, 35m);

        Assert.NotNull(conditionA);
        Assert.NotNull(conditionB);
        Assert.NotEqual(conditionA.Id, conditionB.Id);
        Assert.Equal(productA.Id, conditionA.MediaProductId);
        Assert.Equal(productB.Id, conditionB.MediaProductId);
        Assert.Equal(2, await db.MediaIncubationConditions.CountAsync());
    }

    // --- Update ---

    [Fact]
    public async Task UpdateAsync_UnusedCondition_UpdatesSuccessfully()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);

        var updated = await service.UpdateAsync(condition.Id, 48, 72, 20m, 25m);

        Assert.Equal(48, updated.IncubationMinHours);
        Assert.Equal(72, updated.IncubationMaxHours);
        Assert.Equal(20m, updated.TemperatureMin);
        Assert.Equal(25m, updated.TemperatureMax);

        var reloaded = await db.MediaIncubationConditions.AsNoTracking().SingleAsync(c => c.Id == condition.Id);
        Assert.Equal(48, reloaded.IncubationMinHours);
        Assert.Equal(72, reloaded.IncubationMaxHours);
        Assert.Equal(20m, reloaded.TemperatureMin);
        Assert.Equal(25m, reloaded.TemperatureMax);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(9999, 24, 48, 30m, 35m));
        Assert.Contains("Incubation condition with ID 9999 not found", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_UsedByConfiguration_ThrowsAndValuesUnchanged()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        await SeedConfigurationUsageAsync(db, product, condition);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(condition.Id, 48, 72, 20m, 25m));
        Assert.Contains("used by 1 evaluation configuration(s) and 0 Test Master step medium/media", ex.Message);

        var reloaded = await db.MediaIncubationConditions.AsNoTracking().SingleAsync(c => c.Id == condition.Id);
        Assert.Equal(24, reloaded.IncubationMinHours);
        Assert.Equal(48, reloaded.IncubationMaxHours);
        Assert.Equal(30m, reloaded.TemperatureMin);
        Assert.Equal(35m, reloaded.TemperatureMax);
    }

    [Fact]
    public async Task UpdateAsync_UsedByStepMedia_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        await SeedStepMediaUsageAsync(db, condition);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(condition.Id, 48, 72, 20m, 25m));
        Assert.Contains("used by 0 evaluation configuration(s) and 1 Test Master", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_ClashesWithAnotherConditionOfSameProduct_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition1 = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        var condition2 = await MediaProductTestData.AddConditionAsync(db, product, 48, 72, 20m, 25m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(condition2.Id, 24, 48, 30m, 35m));
        Assert.Contains("already has an incubation condition", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_OwnCurrentValues_Succeeds()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);

        var updated = await service.UpdateAsync(condition.Id, 24, 48, 30m, 35m);

        Assert.NotNull(updated);
        Assert.Equal(24, updated.IncubationMinHours);
        Assert.Equal(48, updated.IncubationMaxHours);
        Assert.Equal(30m, updated.TemperatureMin);
        Assert.Equal(35m, updated.TemperatureMax);
    }

    [Fact]
    public async Task UpdateAsync_InvalidRange_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(condition.Id, 48, 24, 30m, 35m));
        Assert.Equal("Incubation max hours cannot be less than min hours.", ex.Message);

        var reloaded = await db.MediaIncubationConditions.AsNoTracking().SingleAsync(c => c.Id == condition.Id);
        Assert.Equal(24, reloaded.IncubationMinHours);
        Assert.Equal(48, reloaded.IncubationMaxHours);
        Assert.Equal(30m, reloaded.TemperatureMin);
        Assert.Equal(35m, reloaded.TemperatureMax);
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteAsync_UnusedCondition_RemovesCondition()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);

        await service.DeleteAsync(condition.Id);

        Assert.False(await db.MediaIncubationConditions.AnyAsync(c => c.Id == condition.Id));
    }

    [Fact]
    public async Task DeleteAsync_UsedByConfiguration_ThrowsAndConditionStillExists()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        await SeedConfigurationUsageAsync(db, product, condition);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(condition.Id));
        Assert.Contains("used by 1 evaluation configuration(s) and 0 Test Master step medium/media", ex.Message);
        Assert.True(await db.MediaIncubationConditions.AnyAsync(c => c.Id == condition.Id));
    }

    [Fact]
    public async Task DeleteAsync_UsedByStepMedia_ThrowsAndConditionStillExists()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA", "TSA");
        var condition = await MediaProductTestData.AddConditionAsync(db, product, 24, 48, 30m, 35m);
        await SeedStepMediaUsageAsync(db, condition);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(condition.Id));
        Assert.Contains("used by 0 evaluation configuration(s) and 1 Test Master", ex.Message);
        Assert.True(await db.MediaIncubationConditions.AnyAsync(c => c.Id == condition.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_Throws()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(9999));
        Assert.Contains("Incubation condition with ID 9999 not found", ex.Message);
    }

    // --- GetAllAsync ---

    [Fact]
    public async Task GetAllAsync_FiltersByProductAndCalculatesCounts()
    {
        await using var db = NewDb();
        var service = TestServiceFactory.MediaIncubationCondition(db);

        var productA = await MediaProductTestData.CreateOrGetAsync(db, "Product A", "PROD-A");
        var productB = await MediaProductTestData.CreateOrGetAsync(db, "Product B", "PROD-B");

        var conditionA1 = await MediaProductTestData.AddConditionAsync(db, productA, 24, 48, 30m, 35m);
        var conditionA2 = await MediaProductTestData.AddConditionAsync(db, productA, 48, 72, 20m, 25m);
        var conditionB1 = await MediaProductTestData.AddConditionAsync(db, productB, 24, 48, 30m, 35m);

        await SeedConfigurationUsageAsync(db, productA, conditionA1);

        await SeedStepMediaUsageAsync(db, conditionA2, batchNumber: "BATCH-M1");
        await SeedStepMediaUsageAsync(db, conditionA2, batchNumber: "BATCH-M2");

        var rowsA = await service.GetAllAsync(productA.Id);
        Assert.Equal(2, rowsA.Count);
        Assert.All(rowsA, r => Assert.Equal(productA.Id, r.MediaProductId));

        var rowA1 = rowsA.Single(r => r.Id == conditionA1.Id);
        Assert.Equal(1, rowA1.ConfigurationCount);
        Assert.Equal(0, rowA1.StepMediaCount);

        var rowA2 = rowsA.Single(r => r.Id == conditionA2.Id);
        Assert.Equal(0, rowA2.ConfigurationCount);
        Assert.Equal(2, rowA2.StepMediaCount);

        var rowsAll = await service.GetAllAsync(null);
        Assert.Equal(3, rowsAll.Count);
        Assert.Contains(rowsAll, r => r.Id == conditionA1.Id);
        Assert.Contains(rowsAll, r => r.Id == conditionA2.Id);
        Assert.Contains(rowsAll, r => r.Id == conditionB1.Id);
    }
}
