using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MaterialServiceWorkingStandardTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        return db;
    }

    private static Material Lot(int sectionId, MaterialType type, string batch) => new()
    {
        SectionId = sectionId, MaterialType = type, MaterialName = "Paracetamol", ManufacturerName = "x",
        BatchNumber = batch, ReceivingDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddMonths(6),
        Location = "S1", QuantityReceived = 5m, QuantityRemaining = 5m, Unit = MaterialUnit.Gram,
        Purity = 99.5m, MoisturePercent = 0.2m, CreatedByUserId = 1, LastModifiedByUserId = 1,
    };

    [Fact]
    public async Task Create_WorkingStandard_Refused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var service = TestServiceFactory.Material(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveMaterialRequest(
            MaterialType.WorkingStandard, "P", "M", "B1", DateTime.UtcNow, null, null, "S1", 1m, MaterialUnit.Gram,
            null, null, null, SectionId: section.Id), 1));
        Assert.Equal("Working standards are created by approving a qualification, not received here.", ex.Message);
    }

    [Fact]
    public async Task Update_WorkingStandardLot_Refused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var lot = Lot(section.Id, MaterialType.WorkingStandard, "WS1");
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.Material(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(lot.Id, new SaveMaterialRequest(
            MaterialType.WorkingStandard, "P", "M", "WS1", DateTime.UtcNow, null, null, "S1", 5m, MaterialUnit.Gram,
            null, null, null), 1));
        Assert.Equal("Working standards change only through their qualifications.", ex.Message);
    }

    [Fact]
    public async Task UsableReferenceStandards_IncludesWorkingStandards()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        db.Materials.AddRange(Lot(section.Id, MaterialType.ReferenceStandard, "RS1"), Lot(section.Id, MaterialType.WorkingStandard, "WS1"),
            Lot(section.Id, MaterialType.Chemical, "CH1"));
        await db.SaveChangesAsync();

        var lots = await TestServiceFactory.Material(db).GetUsableReferenceStandardsAsync(1);

        Assert.Equal(new[] { "RS1", "WS1" }, lots.Select(l => l.BatchNumber).OrderBy(b => b));
    }
}
