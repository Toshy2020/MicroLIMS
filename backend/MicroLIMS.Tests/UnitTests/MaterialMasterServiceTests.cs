using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MaterialMasterServiceTests
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

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static SaveMaterialMasterEntryRequest Req(
        string code = "NAOH-01",
        string name = "Sodium Hydroxide",
        MaterialMasterCategory category = MaterialMasterCategory.Reagent,
        string? workingConcentration = null,
        string? solvent = null,
        decimal? transitionRangeFrom = null,
        decimal? transitionRangeTo = null,
        string? colourChange = null,
        string? indicatorUse = null,
        int? sectionId = null) =>
        new(code, name, category, "AR Grade", "Merck", MaterialUnit.Gram, sectionId,
            workingConcentration, solvent, transitionRangeFrom, transitionRangeTo, colourChange, indicatorUse);

    [Fact]
    public async Task Create_TrimsAndUppercasesCode_AndIsActive()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var result = await service.CreateAsync(Req(code: " naoh-01 ", name: "  Sodium Hydroxide  "), userId);

        Assert.Equal("NAOH-01", result.Code);
        Assert.Equal("Sodium Hydroxide", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateCodeInSameSection_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        await service.CreateAsync(Req(code: "NAOH-01"), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(code: "naoh-01", name: "Another"), userId));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Create_IndicatorFieldsOnNonIndicator_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(category: MaterialMasterCategory.Reagent, solvent: "Ethanol"), userId));
        Assert.Contains("Indicator fields", ex.Message);
    }

    [Fact]
    public async Task Create_TransitionRangeFromAboveTo_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(category: MaterialMasterCategory.Indicator, transitionRangeFrom: 10, transitionRangeTo: 8), userId));
        Assert.Contains("Transition range", ex.Message);
    }

    [Fact]
    public async Task Update_ChangesFields_AndKeepsCategoryRuleOnExistingLots()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var entry = await service.CreateAsync(Req(category: MaterialMasterCategory.Reagent), userId);

        db.Materials.Add(new Material
        {
            SectionId = section.Id,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Merck",
            BatchNumber = "B1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Shelf",
            QuantityReceived = 10,
            QuantityRemaining = 10,
            Unit = MaterialUnit.Gram
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(entry.Id, Req(category: MaterialMasterCategory.Indicator, solvent: "Water"), userId));
        Assert.Contains("linked stock lots", ex.Message);

        var updated = await service.UpdateAsync(entry.Id, Req(name: "Sodium Hydroxide Pellets"), userId);
        Assert.Equal("Sodium Hydroxide Pellets", updated.Name);
    }

    [Fact]
    public async Task SetActive_False_HidesFromActiveOnlyList()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var entry = await service.CreateAsync(Req(), userId);
        await service.SetActiveAsync(entry.Id, false, userId);

        var activeOnly = await service.GetAllAsync(userId, activeOnly: true);
        Assert.DoesNotContain(activeOnly, e => e.Id == entry.Id);

        var all = await service.GetAllAsync(userId);
        Assert.Contains(all, e => e.Id == entry.Id);
    }
}
