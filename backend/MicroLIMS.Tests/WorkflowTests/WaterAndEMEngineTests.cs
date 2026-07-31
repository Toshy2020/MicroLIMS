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

    [Fact]
    public async Task EM_CombinedCountExceedsActionLimit_FlagsOutOfTrend()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var room = new Room { Name = "Grade A Filling", Department = dept, GradeClassification = "A" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        db.RoomTestConfigurations.Add(new RoomTestConfiguration
        {
            RoomId = room.Id, TestType = "PassiveAirSample", TestCode = "EM_TAMC",
            AlertLimit = "1", ActionLimit = "3", SpecLimit = "5"
        });
        await db.SaveChangesAsync();

        var engine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "Position-1", 1));
        var prepared = await engine.PrepareAsync(sample.Id,
            new List<EMPreparationSelection> { new(room.Id, new List<string> { "PassiveAirSample" }) }, 1);
        var order = prepared.TestOrders.First();

        await engine.StartStep1Async(order.Id, 1);
        await engine.StartStep2Async(order.Id, 1);
        var monitoring = await engine.CompleteAsync(order.Id, 4, 1, actionLimit: 3);

        Assert.True(monitoring.IsOutOfTrend); // final count 4 > action limit of 3
    }
}
