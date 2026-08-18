using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterAndEMEngineTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Legacy per-point sample shape - created directly, the way pre-batch
    // water samples exist in the database today (no SampleLocation rows,
    // WaterSamplingPointId set directly). ReceiveAsync no longer produces
    // this shape (see Water_ReceiveAsync_StartsAsNeedsPreparation below),
    // but CalculateAndCompareAsync must keep serving old rows exactly as
    // before.
    [Fact]
    public async Task Water_AverageExceedsSpecLimit_FlagsOutOfSpecification()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100"
        });
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817001", Category = SampleCategory.Water, WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-1", SampledBy = "Analyst", Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = sample.TestOrders.First();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var result = await engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 90, 110, 120 });

        Assert.Equal("OutOfSpecification", result.Status);
    }

    [Theory]
    [InlineData(new double[] { 12, 14 }, "AlertLimitExceeded")]   // avg 13 > alert 10, < action 50
    [InlineData(new double[] { 60, 60 }, "ActionLimitExceeded")]  // avg 60 > action 50, < spec 100
    public async Task Water_ConfiguredLimits_ProduceExpectedStatus(double[] readings, string expected)
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-10", Location = "WTU", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100"
        });
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817010", Category = SampleCategory.Water, WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-10", SampledBy = "Analyst", Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = sample.TestOrders.First();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var result = await engine.CalculateAndCompareAsync(order.Id, readings.Select(r => (decimal)r).ToList());

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Water_NoConfiguredLimits_StaysWithinLimits()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-11", Location = "WTU", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817011", Category = SampleCategory.Water, WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-11", SampledBy = "Analyst", Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = sample.TestOrders.First();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var result = await engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 9999 });

        Assert.Equal("WithinLimits", result.Status);
    }

    [Fact]
    public async Task Water_CalculateAndCompareAsync_RejectsBatchPreparedTestOrder()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();
        var point = new WaterSamplingPoint { Code = "SP-1", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-20", 1));
        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { point.Id }, 1);
        var order = prepared.TestOrders.Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 5 }));
    }

    // EM/After Cleaning batch preparation (multiple rooms/parts -> one
    // TestOrder per TestCode + SampleLocation rows) is covered by
    // EMBatchLocationTests.cs, replacing the old per-location model this
    // file used to exercise here.

    [Fact]
    public async Task Water_ReceiveAsync_StartsAsNeedsPreparation()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-2", 1));

        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
        Assert.Empty(sample.TestOrders);
    }

    [Fact]
    public async Task SamplePreparationService_PrepareAsync_FlipsSampleToReady()
    {
        await using var db = NewDb();
        var diluent = new DiluentType { Name = "Buffer", RequiresBatchTracking = false };
        var neutralizer = new Neutralizer { Name = "Tween" };
        db.DiluentTypes.Add(diluent);
        db.Neutralizers.Add(neutralizer);
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-3", Status = SampleStatus.Received, PreparationStatus = SamplePreparationStatus.NeedsPreparation };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new SamplePreparationService(db);
        await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 5, null, null));

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SamplePreparationStatus.Ready, reloaded.PreparationStatus);
    }
}
