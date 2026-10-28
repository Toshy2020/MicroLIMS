using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// A batch-tracked diluent points at a Culture Media product; any released, active, non-expired lot of that product fits.
public class DiluentTypeBatchTrackingTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(
            new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<Media> AddLotAsync(
        MicroLimsDbContext db, MediaProduct product, string lotNumber, bool released = true,
        MediaStatus status = MediaStatus.Active, int expiresInDays = 30)
    {
        var batch = new Material
        {
            SectionId = TestServiceFactory.EnsureMicroSection(db).Id,
            MaterialType = MaterialType.DehydratedMedia, MaterialName = product.Name, ManufacturerName = "Himedia",
            BatchNumber = "B-" + lotNumber, ReceivingDate = DateTime.UtcNow, Location = "Lab",
            QuantityReceived = 100, QuantityRemaining = 100, Unit = MaterialUnit.Gram,
            Code = product.Code, MediaProductId = product.Id
        };
        db.Materials.Add(batch);
        await db.SaveChangesAsync();
        var lot = new Media
        {
            MaterialId = batch.Id, LotNumber = lotNumber, IsReleasedForUse = released, Status = status,
            ExpiryDate = DateTime.UtcNow.AddDays(expiresInDays)
        };
        db.Media.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    [Fact]
    public async Task Create_BatchTracked_StoresTheMediaProduct()
    {
        await using var db = NewDb();
        var service = new ReferenceListMasterDataService(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptone Soya Broth", "TSB");

        var created = await service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("TSB", true, product.Id));

        Assert.Equal(product.Id, created.MediaProductId);
        Assert.Equal("Tryptone Soya Broth", created.MediaProductName);
        Assert.Equal(product.Id, (await service.GetDiluentTypesAsync()).Single().MediaProductId);
    }

    [Fact]
    public async Task Create_BatchTrackedWithoutProduct_Throws()
    {
        await using var db = NewDb();
        var service = new ReferenceListMasterDataService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("TSB", true, null)));
        Assert.Contains("Choose the Culture Media product", ex.Message);
        Assert.Empty(db.DiluentTypes);
    }

    [Fact]
    public async Task Create_InactiveOrUnknownProduct_Throws()
    {
        await using var db = NewDb();
        var service = new ReferenceListMasterDataService(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptone Soya Broth", "TSB");
        product.IsActive = false;
        await db.SaveChangesAsync();

        var inactive = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("TSB", true, product.Id)));
        Assert.Contains("is inactive", inactive.Message);

        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("TSB", true, 9999)));
        Assert.Contains("not found", unknown.Message);
        Assert.Empty(db.DiluentTypes);
    }

    [Fact]
    public async Task Create_NotBatchTracked_TakesNoProduct()
    {
        await using var db = NewDb();
        var service = new ReferenceListMasterDataService(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptone Soya Broth", "TSB");

        var buffer = await service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("Buffer PhB 7.2", false, null));
        Assert.Null(buffer.MediaProductId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("Buffer", false, product.Id)));
    }

    [Fact]
    public async Task SelectableLots_AreEveryReleasedActiveNonExpiredLotOfTheProduct_FromAnyBatch()
    {
        await using var db = NewDb();
        var service = new ReferenceListMasterDataService(db);
        var tsb = await MediaProductTestData.CreateOrGetAsync(db, "Tryptone Soya Broth", "TSB");
        var other = await MediaProductTestData.CreateOrGetAsync(db, "XLD Agar", "XLD");
        var diluent = await service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("TSB", true, tsb.Id));

        var batchA = await AddLotAsync(db, tsb, "TSB/01/26");
        var batchB = await AddLotAsync(db, tsb, "TSB/02/26");
        await AddLotAsync(db, tsb, "TSB/03/26", released: false);
        await AddLotAsync(db, tsb, "TSB/04/26", status: MediaStatus.QuarantineFailed);
        await AddLotAsync(db, tsb, "TSB/05/26", status: MediaStatus.OutOfStock);
        await AddLotAsync(db, tsb, "TSB/06/26", expiresInDays: -1);
        var wrongProduct = await AddLotAsync(db, other, "XLD/01/26");

        var lots = await service.GetSelectableDiluentLotsAsync(diluent.Id);

        Assert.Equal(new[] { batchA.Id, batchB.Id }.OrderBy(i => i), lots.Select(l => l.Id).OrderBy(i => i));
        Assert.Equal(batchB.Id, (await service.RequireSelectableDiluentLotAsync(diluent.Id, batchB.Id)).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequireSelectableDiluentLotAsync(diluent.Id, wrongProduct.Id));
    }

    [Fact]
    public async Task SelectableLots_OfADiluentWithoutBatchTracking_AreEmpty()
    {
        await using var db = NewDb();
        var service = new ReferenceListMasterDataService(db);
        var tsb = await MediaProductTestData.CreateOrGetAsync(db, "Tryptone Soya Broth", "TSB");
        await AddLotAsync(db, tsb, "TSB/01/26");
        var buffer = await service.CreateDiluentTypeAsync(new CreateDiluentTypeRequest("Buffer PhB 7.2", false, null));

        Assert.Empty(await service.GetSelectableDiluentLotsAsync(buffer.Id));
    }
}
