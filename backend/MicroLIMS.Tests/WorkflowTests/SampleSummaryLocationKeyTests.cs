using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// There is one RoomTestConfiguration (and MachinePartConfiguration) per
// room/part per TestCode, so an EM sample testing the same room for TAMC
// and TYMC links its two SampleLocations to two different configuration
// rows. The Certificate of Analysis must still show that room once, with
// both results on the same row.
public class SampleSummaryLocationKeyTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<int> SeedSampleAsync(MicroLimsDbContext db, SampleCategory category,
        Func<string, (LocationType Type, int? RoomConfigId, int? PartConfigId)[]> locationsFor)
    {
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest });
        db.TestDefinitions.Add(new TestDefinition { Code = "TYMC", DisplayName = "TYMC", WorkflowType = WorkflowType.CountTest });
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = category, CauseOfTestingId = cause.Id, ControlNumber = "EM-1", Status = SampleStatus.Approved };
        var tamc = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Waiting };
        var tymc = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(tamc);
        sample.TestOrders.Add(tymc);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        foreach (var order in new[] { tamc, tymc })
            foreach (var (type, roomConfigId, partConfigId) in locationsFor(order.TestCode))
                db.SampleLocations.Add(new SampleLocation
                {
                    SampleId = sample.Id, TestOrderId = order.Id, LocationType = type,
                    RoomTestConfigurationId = roomConfigId, MachinePartConfigurationId = partConfigId,
                    DilutionFactor = 1, CFUResult = 1, CalculatedResult = 1, ReportedResult = "1",
                    SpecLimit = "10", Status = ResultStatus.WithinLimits, Unit = "CFU/plate"
                });
        await db.SaveChangesAsync();
        return sample.Id;
    }

    [Fact]
    public async Task EmSample_SameRoomUnderTwoTests_OneCoaRowPerRoom()
    {
        await using var db = NewDb();
        var department = new Department { Name = "Production" };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        var roomA = new Room { Name = "Room A", DepartmentId = department.Id };
        var roomB = new Room { Name = "Room B", DepartmentId = department.Id };
        db.Rooms.AddRange(roomA, roomB);
        await db.SaveChangesAsync();
        var configs = new Dictionary<(int, string), RoomTestConfiguration>();
        foreach (var room in new[] { roomA, roomB })
            foreach (var code in new[] { "TAMC", "TYMC" })
                configs[(room.Id, code)] = new RoomTestConfiguration { RoomId = room.Id, TestCode = code, TestType = code, SpecLimit = "10", Unit = "CFU/plate" };
        db.RoomTestConfigurations.AddRange(configs.Values);
        await db.SaveChangesAsync();

        var sampleId = await SeedSampleAsync(db, SampleCategory.EnvironmentalMonitoring, code => new[]
        {
            (LocationType.Room, (int?)configs[(roomA.Id, code)].Id, (int?)null),
            (LocationType.Room, (int?)configs[(roomB.Id, code)].Id, (int?)null)
        });

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        var matrix = summary!.Certificate.Sample.Matrix!;
        Assert.Equal(new[] { "Room A", "Room B" }, matrix.Rows.Select(r => r.LocationName).Order());
        Assert.All(matrix.Rows, r => Assert.All(r.Cells, c => Assert.NotNull(c)));
        Assert.Equal(2, matrix.TotalLocations);
    }

    [Fact]
    public async Task AfterCleaningSample_SamePartUnderTwoTests_OneCoaRowPerPart()
    {
        await using var db = NewDb();
        var machine = new Machine { Name = "Mixer" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var part = new MachinePart { Name = "Blade", MachineId = machine.Id };
        db.MachineParts.Add(part);
        await db.SaveChangesAsync();
        var tamcConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "TAMC", TestType = "Swab", SpecLimit = "10", Unit = "CFU/swab" };
        var tymcConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestCode = "TYMC", TestType = "Swab", SpecLimit = "10", Unit = "CFU/swab" };
        db.MachinePartConfigurations.AddRange(tamcConfig, tymcConfig);
        await db.SaveChangesAsync();

        var sampleId = await SeedSampleAsync(db, SampleCategory.AfterCleaning, code => new[]
        {
            (LocationType.MachinePart, (int?)null, (int?)(code == "TAMC" ? tamcConfig.Id : tymcConfig.Id))
        });

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        var row = Assert.Single(summary!.Certificate.Sample.Matrix!.Rows);
        Assert.Equal("Blade", row.LocationName);
        Assert.All(row.Cells, c => Assert.NotNull(c));
    }
}
