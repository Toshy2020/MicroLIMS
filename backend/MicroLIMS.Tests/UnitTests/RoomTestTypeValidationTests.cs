using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// EM room test types: passive/surface samples carry a fixed unit, while
// active air, compressed air and drains report the unit entered on the
// configuration - so it must be there, and unknown types are refused.
public class RoomTestTypeValidationTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateRoomTestConfigRequest Create(string testType, string? unit) =>
        new(RoomId: 1, TestType: testType, TestCode: "TAMC", AlertLimit: "1", ActionLimit: "3", SpecLimit: "5", Unit: unit);

    [Theory]
    [InlineData(RoomTestTypes.ActiveAirSample)]
    [InlineData(RoomTestTypes.CompressedAir)]
    [InlineData(RoomTestTypes.Drains)]
    public async Task NewTestTypes_SaveWithTheirEnteredUnit(string testType)
    {
        await using var db = NewDb();
        var saved = await new EnvironmentalMonitoringMasterDataService(db).CreateRoomTestConfigurationAsync(Create(testType, " CFU/m3 "));

        Assert.Equal(testType, saved.TestType);
        Assert.Equal("CFU/m3", (await db.RoomTestConfigurations.SingleAsync()).Unit);
    }

    [Theory]
    [InlineData(RoomTestTypes.ActiveAirSample, null)]
    [InlineData(RoomTestTypes.CompressedAir, "")]
    [InlineData(RoomTestTypes.Drains, "   ")]
    public async Task NewTestTypes_RequireAUnit(string testType, string? unit)
    {
        await using var db = NewDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EnvironmentalMonitoringMasterDataService(db).CreateRoomTestConfigurationAsync(Create(testType, unit)));

        Assert.Contains("Unit is required", ex.Message);
        Assert.Empty(db.RoomTestConfigurations);
    }

    [Theory]
    [InlineData(RoomTestTypes.PassiveAirSample)]
    [InlineData(RoomTestTypes.SurfaceAirSample)]
    public async Task FixedUnitTestTypes_StillSaveWithoutAUnit(string testType)
    {
        await using var db = NewDb();
        await new EnvironmentalMonitoringMasterDataService(db).CreateRoomTestConfigurationAsync(Create(testType, null));

        Assert.Single(db.RoomTestConfigurations);
    }

    [Fact]
    public async Task UnknownTestType_IsRefused()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EnvironmentalMonitoringMasterDataService(db).CreateRoomTestConfigurationAsync(Create("Swab", "CFU/swab")));
    }

    [Fact]
    public async Task Update_ToANewTestTypeWithoutAUnit_IsRefused()
    {
        await using var db = NewDb();
        var service = new EnvironmentalMonitoringMasterDataService(db);
        var saved = await service.CreateRoomTestConfigurationAsync(Create(RoomTestTypes.PassiveAirSample, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRoomTestConfigurationAsync(saved.Id,
                new UpdateRoomTestConfigRequest(RoomTestTypes.Drains, "TAMC", "1", "3", "5", Unit: null)));
    }
}
