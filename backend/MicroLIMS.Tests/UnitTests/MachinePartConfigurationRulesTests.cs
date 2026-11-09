using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// After Cleaning parts: several tests per part (including several
// pathogens), each test type + code once, and no deleting a
// configuration samples have already used.
public class MachinePartConfigurationRulesTests
{
    private const int UserId = 1;

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AfterCleaningMasterDataService NewService(MicroLimsDbContext db) =>
        new(db, new StubSectionScope(_ => null));

    private static CreateMachinePartConfigRequest Pathogen(string code) =>
        new(MachinePartId: 1, TestType: "Pathogen", TestCode: code, AlertLimit: "", ActionLimit: "", SpecLimit: "", IsPathogenTest: true);

    [Fact]
    public async Task APart_CanHaveSeveralPathogenTests()
    {
        await using var db = NewDb();
        var service = NewService(db);

        await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI"));
        await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("SALM"));
        await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("PSEUDO"));

        Assert.Equal(3, await db.MachinePartConfigurations.CountAsync(c => c.MachinePartId == 1 && c.IsPathogenTest));
    }

    [Fact]
    public async Task TheSameTestTwiceOnOnePart_IsRefused()
    {
        await using var db = NewDb();
        var service = NewService(db);
        await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI"));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI")));
        Assert.Equal(1, await db.MachinePartConfigurations.CountAsync());
    }

    [Fact]
    public async Task TheSameTestCode_UnderAnotherTestType_IsAllowed()
    {
        await using var db = NewDb();
        var service = NewService(db);
        await service.CreateMachinePartConfigurationAsync(UserId, new(1, "Swab", "TAMC", "1", "3", "5", false));
        await service.CreateMachinePartConfigurationAsync(UserId, new(1, "Rinse", "TAMC", "1", "3", "5", false));

        Assert.Equal(2, await db.MachinePartConfigurations.CountAsync());
    }

    [Fact]
    public async Task Update_OntoAnotherRowsTest_IsRefused()
    {
        await using var db = NewDb();
        var service = NewService(db);
        await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI"));
        var salm = await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("SALM"));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateMachinePartConfigurationAsync(UserId, salm.Id, new UpdateMachinePartConfigRequest("Pathogen", "ECOLI", "", "", "", true)));
    }

    [Fact]
    public async Task Update_KeepingItsOwnTest_IsAllowed()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var ecoli = await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI"));

        var saved = await service.UpdateMachinePartConfigurationAsync(UserId, ecoli.Id, new UpdateMachinePartConfigRequest("Pathogen", "ECOLI", "", "", "", true, "per swab"));

        Assert.Equal("per swab", saved.Unit);
    }

    [Fact]
    public async Task Delete_OfAConfigurationSamplesUsed_IsRefused()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var ecoli = await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI"));
        db.SampleLocations.Add(new SampleLocation { SampleId = 1, TestOrderId = 1, LocationType = LocationType.MachinePart, MachinePartConfigurationId = ecoli.Id });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteMachinePartConfigurationAsync(UserId, ecoli.Id));
        Assert.Equal(1, await db.MachinePartConfigurations.CountAsync());
    }

    [Fact]
    public async Task Delete_OfAnUnusedConfiguration_Succeeds()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var ecoli = await service.CreateMachinePartConfigurationAsync(UserId, Pathogen("ECOLI"));

        await service.DeleteMachinePartConfigurationAsync(UserId, ecoli.Id);

        Assert.Equal(0, await db.MachinePartConfigurations.CountAsync());
    }
}
