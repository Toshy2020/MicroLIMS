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

    [Fact]
    public async Task Water_AverageExceedsSpecLimit_FlagsOutOfSpecification()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100"
        });
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(point.Id, 0, "500ml", "Analyst", "CTRL-1", 1));
        var order = sample.TestOrders.First();

        var result = await engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 90, 110, 120 });

        Assert.Equal("OutOfSpecification", result.Status);
    }

    // EM/After Cleaning batch preparation (multiple rooms/parts -> one
    // TestOrder per TestCode + SampleLocation rows) is covered by
    // EMBatchLocationTests.cs, replacing the old per-location model this
    // file used to exercise here.

    [Fact]
    public async Task Water_ReceiveAsync_StartsAsNeedsPreparation()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-02", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(point.Id, 0, "500ml", "Analyst", "CTRL-2", 1));

        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
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
