using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class IncubatorEligibilityTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<int> SeedStepMediaAsync(MicroLimsDbContext db, decimal tempMin, decimal tempMax)
    {
        var stepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = 1, MaterialId = 1, TempMin = tempMin, TempMax = tempMax, IsRequired = true, DisplayOrder = 1 };
        db.TestWorkflowStepMedias.Add(stepMedia);
        await db.SaveChangesAsync();
        return stepMedia.Id;
    }

    private static Equipment Incubator(string code, decimal? setPoint, DateTime? calibrationDue = null) =>
        new() { Name = code, Code = code, Type = EquipmentType.Incubator, SetPointTemperature = setPoint, CalibrationDueDate = calibrationDue };

    [Fact]
    public async Task InRangeIncubator_IsReturned()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-03", 35));
        await db.SaveChangesAsync();

        var result = await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId);

        Assert.Single(result);
        Assert.Equal("INC-03", result[0].Code);
        Assert.Equal("Current", result[0].CalibrationStatus);
    }

    [Fact]
    public async Task OutOfRangeIncubator_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-09", 43));
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task NonIncubatorEquipment_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(new Equipment { Name = "AUT-01", Code = "AUT-01", Type = EquipmentType.Autoclave, SetPointTemperature = 36 });
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task IncubatorWithNoSetPoint_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-11", null));
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task OverdueCalibration_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-04", 36, calibrationDue: DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task BoundaryTemperatures_AreInclusive()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.AddRange(Incubator("INC-LOW", 35), Incubator("INC-HIGH", 37));
        await db.SaveChangesAsync();

        Assert.Equal(2, (await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId)).Count);
    }

    [Fact]
    public async Task IsWithinRangeAsync_MatchesListMembership()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        var good = Incubator("INC-03", 36);
        var bad = Incubator("INC-09", 43);
        db.Equipment.AddRange(good, bad);
        await db.SaveChangesAsync();

        var service = new IncubatorEligibilityService(db);
        Assert.True(await service.IsWithinRangeAsync(stepMediaId, good.Id));
        Assert.False(await service.IsWithinRangeAsync(stepMediaId, bad.Id));
    }

    [Fact]
    public async Task UnknownStepMedia_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(999));
    }
}
