using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LocationChemistryValidationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public void Range_RequiresLowerOrUpperLimit()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(LimitType.Range, null, null, null));
        Assert.Contains("Range limit requires a lower limit, an upper limit, or both", ex.Message);

        // Valid cases:
        LocationChemistryLimitRules.Validate(LimitType.Range, 5m, 10m, null);
        LocationChemistryLimitRules.Validate(LimitType.Range, 5m, null, null);
        LocationChemistryLimitRules.Validate(LimitType.Range, null, 10m, null);
    }

    [Fact]
    public void Range_RejectsLowerGreaterThanUpper()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(LimitType.Range, 10m, 5m, null));
        Assert.Contains("Lower limit cannot be greater than upper limit", ex.Message);
    }

    [Fact]
    public void NotMoreThan_RequiresUpperLimit()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(LimitType.NotMoreThan, null, null, null));
        Assert.Contains("Upper limit is required", ex.Message);

        LocationChemistryLimitRules.Validate(LimitType.NotMoreThan, null, 1.3m, null);
    }

    [Fact]
    public void NotLessThan_RequiresLowerLimit()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(LimitType.NotLessThan, null, null, null));
        Assert.Contains("Lower limit is required", ex.Message);

        LocationChemistryLimitRules.Validate(LimitType.NotLessThan, 5m, null, null);
    }

    [Fact]
    public void Qualitative_RequiresExpectedResultText()
    {
        var ex1 = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(LimitType.Qualitative, null, null, null));
        Assert.Contains("Expected result text is required", ex1.Message);

        var ex2 = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(LimitType.Qualitative, null, null, "   "));
        Assert.Contains("Expected result text is required", ex2.Message);

        LocationChemistryLimitRules.Validate(LimitType.Qualitative, null, null, "Conforms");
    }

    [Theory]
    [InlineData(LimitType.TargetWithTolerance)]
    [InlineData(LimitType.CountTiered)]
    [InlineData(LimitType.PresenceAbsence)]
    [InlineData(LimitType.MultiStage)]
    [InlineData(LimitType.DissolutionQ)]
    [InlineData(LimitType.DisintegrationTime)]
    [InlineData(LimitType.WeightVariation)]
    public void UnsupportedLimitTypes_Throw(LimitType unsupported)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            LocationChemistryLimitRules.Validate(unsupported, 1m, 2m, "text"));
        Assert.Contains("Only Range, NotMoreThan, NotLessThan, and Qualitative", ex.Message);
    }

    [Fact]
    public void NullLimitType_IsAcceptedForMicroLegacy()
    {
        // Should not throw even with nulls everywhere
        LocationChemistryLimitRules.Validate(null, null, null, null);
    }

    [Fact]
    public async Task WaterMasterDataService_CreateAndUpdate_ValidatesLimits()
    {
        await using var db = NewDb();
        var scope = new StubSectionScope(_ => null);
        var service = new WaterMasterDataService(db, scope);

        var point = new WaterSamplingPoint { Code = "W-01", Location = "Loop", TestingFrequency = "Daily" };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        // Range with lower > upper fails
        var invalidReq = new CreateWaterSamplingConfigRequest(
            point.Id, "PH", LimitType: LimitType.Range, LowerLimit: 9m, UpperLimit: 5m);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWaterSamplingConfigurationAsync(1, invalidReq));

        // Valid create
        var validReq = new CreateWaterSamplingConfigRequest(
            point.Id, "PH", LimitType: LimitType.Range, LowerLimit: 6.5m, UpperLimit: 8.5m, Unit: "pH");
        var created = await service.CreateWaterSamplingConfigurationAsync(1, validReq);
        Assert.Equal(LimitType.Range, created.LimitType);
        Assert.Equal(6.5m, created.LowerLimit);
        Assert.Equal(8.5m, created.UpperLimit);
        Assert.Equal("pH", created.Unit);

        // Invalid update fails
        var invalidUpdate = new UpdateWaterSamplingConfigRequest(
            "PH", LimitType: LimitType.NotMoreThan, UpperLimit: null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWaterSamplingConfigurationAsync(1, created.Id, invalidUpdate));

        // Valid update succeeds
        var validUpdate = new UpdateWaterSamplingConfigRequest(
            "PH", LimitType: LimitType.Range, LowerLimit: 6.0m, UpperLimit: 8.0m, Unit: "pH");
        var updated = await service.UpdateWaterSamplingConfigurationAsync(1, created.Id, validUpdate);
        Assert.Equal(6.0m, updated.LowerLimit);
        Assert.Equal(8.0m, updated.UpperLimit);
    }

    [Fact]
    public async Task AfterCleaningMasterDataService_CreateAndUpdate_ValidatesLimits()
    {
        await using var db = NewDb();
        var scope = new StubSectionScope(_ => null);
        var service = new AfterCleaningMasterDataService(db, scope);

        var machine = new Machine { Name = "Filler" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var part = new MachinePart { Name = "Nozzle", MachineId = machine.Id };
        db.MachineParts.Add(part);
        await db.SaveChangesAsync();

        // Qualitative without expected text fails
        var invalidReq = new CreateMachinePartConfigRequest(
            part.Id, "Swab", "TOC", LimitType: LimitType.Qualitative, ExpectedResultText: "");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateMachinePartConfigurationAsync(1, invalidReq));

        // Valid create
        var validReq = new CreateMachinePartConfigRequest(
            part.Id, "Swab", "TOC", LimitType: LimitType.Qualitative, ExpectedResultText: "Pass");
        var created = await service.CreateMachinePartConfigurationAsync(1, validReq);
        Assert.Equal(LimitType.Qualitative, created.LimitType);
        Assert.Equal("Pass", created.ExpectedResultText);

        // Valid update
        var validUpdate = new UpdateMachinePartConfigRequest(
            "Swab", "TOC", LimitType: LimitType.Qualitative, ExpectedResultText: "Conforms");
        var updated = await service.UpdateMachinePartConfigurationAsync(1, created.Id, validUpdate);
        Assert.Equal("Conforms", updated.ExpectedResultText);
    }
}
