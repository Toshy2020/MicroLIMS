using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class MaterialReceivingMediaProductTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        return db;
    }

    private static SaveMaterialRequest CreateRequest(
        MaterialType type, string name, string? code = null, int? mediaProductId = null) => new(
        MaterialType: type,
        MaterialName: name,
        ManufacturerName: "Test Manufacturer",
        BatchNumber: "BATCH-001",
        ReceivingDate: DateTime.UtcNow.AddDays(-1),
        ExpiryDate: DateTime.UtcNow.AddYears(1),
        Code: code,
        Location: "Warehouse",
        QuantityReceived: 500,
        Unit: MaterialUnit.Gram,
        MinimumStockLevel: 50,
        AtccNumber: null,
        OrganismId: null,
        MediaProductId: mediaProductId);

    [Fact]
    public async Task Create_DehydratedWithoutProduct_Throws()
    {
        await using var db = NewDb();
        var service = new MaterialService(db, new UserSectionScopeService(db));
        var req = CreateRequest(MaterialType.DehydratedMedia, "TSA", "TSA", mediaProductId: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, 1));
        Assert.Contains("Choose the configured media product for this dehydrated media", ex.Message);
    }

    [Fact]
    public async Task Create_DehydratedWithProduct_CopiesNameAndCodeIgnoringRequestValues()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var service = new MaterialService(db, new UserSectionScopeService(db));

        var req = CreateRequest(
            MaterialType.DehydratedMedia, "Ignored Custom Name", "IGNORED", mediaProductId: product.Id);

        var material = await service.CreateAsync(req, 1);

        Assert.Equal(product.Id, material.MediaProductId);
        Assert.Equal("Tryptic Soy Agar", material.MaterialName);
        Assert.Equal("TSA", material.Code);
    }

    [Fact]
    public async Task Create_NonDehydrated_ForcesMediaProductIdNull()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var service = new MaterialService(db, new UserSectionScopeService(db));

        var req = CreateRequest(
            MaterialType.Chemical, "Sodium Chloride", "NACL", mediaProductId: product.Id);

        var material = await service.CreateAsync(req, 1);

        Assert.Null(material.MediaProductId);
        Assert.Equal("Sodium Chloride", material.MaterialName);
        Assert.Equal("NACL", material.Code);
    }

    [Fact]
    public async Task Update_UnchangedProduct_KeepsStoredSnapshot()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MediaProductId = product.Id,
            MaterialName = "Tryptic Soy Agar Stored Snapshot",
            Code = "TSA-SNAP",
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var service = new MaterialService(db, new UserSectionScopeService(db));
        var updateReq = CreateRequest(
            MaterialType.DehydratedMedia, "Different Requested Name", "DIFF", mediaProductId: product.Id);

        await service.UpdateAsync(material.Id, updateReq, 1);

        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(product.Id, reloaded.MediaProductId);
        Assert.Equal("Tryptic Soy Agar Stored Snapshot", reloaded.MaterialName);
        Assert.Equal("TSA-SNAP", reloaded.Code);
    }

    [Fact]
    public async Task Update_ChangingLinkedBatchProduct_WithExistingLots_Throws()
    {
        await using var db = NewDb();
        var product1 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 1", "TSA1");
        var product2 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 2", "TSA2");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MediaProductId = product1.Id,
            MaterialName = product1.Name,
            Code = product1.Code,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        db.Media.Add(new Media
        {
            MaterialId = material.Id,
            LotNumber = "TSA1/01/26",
            ExpiryDate = DateTime.UtcNow.AddMonths(1)
        });
        await db.SaveChangesAsync();

        var service = new MaterialService(db, new UserSectionScopeService(db));
        var updateReq = CreateRequest(
            MaterialType.DehydratedMedia, "TSA 2", "TSA2", mediaProductId: product2.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(material.Id, updateReq, 1));
        Assert.Contains("its media product can't be changed", ex.Message);
    }

    [Fact]
    public async Task Update_LinkingPreviouslyUnlinkedBatch_WithExistingLots_AllowedAndResnapshots()
    {
        await using var db = NewDb();
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MediaProductId = null,
            MaterialName = "Legacy Unlinked Dehydrated Media",
            Code = "LEGACY",
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-LEGACY",
            ReceivingDate = DateTime.UtcNow.AddDays(-50),
            Location = "Lab",
            QuantityReceived = 200,
            QuantityRemaining = 200,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        db.Media.Add(new Media
        {
            MaterialId = material.Id,
            LotNumber = "LEGACY/01/26",
            ExpiryDate = DateTime.UtcNow.AddMonths(1)
        });
        await db.SaveChangesAsync();

        var service = new MaterialService(db, new UserSectionScopeService(db));
        var updateReq = CreateRequest(
            MaterialType.DehydratedMedia, "Tryptic Soy Agar", "TSA", mediaProductId: product.Id);

        await service.UpdateAsync(material.Id, updateReq, 1);

        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(product.Id, reloaded.MediaProductId);
        Assert.Equal("Tryptic Soy Agar", reloaded.MaterialName);
        Assert.Equal("TSA", reloaded.Code);
    }

    [Fact]
    public async Task Update_ChangingProduct_OnBatchWithoutLots_AllowedAndResnapshots()
    {
        await using var db = NewDb();
        var product1 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 1", "TSA1");
        var product2 = await MediaProductTestData.CreateOrGetAsync(db, "TSA 2", "TSA2");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MediaProductId = product1.Id,
            MaterialName = product1.Name,
            Code = product1.Code,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var service = new MaterialService(db, new UserSectionScopeService(db));
        var updateReq = CreateRequest(
            MaterialType.DehydratedMedia, "TSA 2", "TSA2", mediaProductId: product2.Id);

        await service.UpdateAsync(material.Id, updateReq, 1);

        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(product2.Id, reloaded.MediaProductId);
        Assert.Equal(product2.Name, reloaded.MaterialName);
        Assert.Equal(product2.Code, reloaded.Code);
    }
}
