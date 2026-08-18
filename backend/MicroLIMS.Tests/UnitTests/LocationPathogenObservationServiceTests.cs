using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LocationPathogenObservationServiceTests
{
    private static (MicroLimsDbContext db, int sampleLocationId, int testOrderId, int userId) SetupEnvironment()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        var user = new User { Id = 1, FullName = "Test Analyst", Username = "analyst", PasswordHash = "hash" };
        db.Users.Add(user);

        var sample = new Sample { Id = 10, ReferenceNumber = "S-001", ReceivedAt = DateTime.UtcNow };
        db.Samples.Add(sample);

        var testOrder = new TestOrder { Id = 5, SampleId = 10, TestCode = "Salmonella" };
        db.TestOrders.Add(testOrder);

        var loc = new SampleLocation { Id = 101, SampleId = 10, TestOrderId = 5, LocationType = LocationType.Room };
        db.SampleLocations.Add(loc);

        db.SaveChanges();
        return (db, 101, 5, 1);
    }

    [Fact]
    public async Task RecordPrimaryObservation_CreatesNewRecord()
    {
        var (db, locId, orderId, userId) = SetupEnvironment();
        var service = new LocationPathogenObservationService(db);

        var obs = await service.RecordPrimaryObservationAsync(
            sampleLocationId: locId,
            testOrderId: orderId,
            observation: GrowthObservation.GrowthConforming,
            selectiveMediaSnapshot: "{\"Media\":\"XLD\"}",
            observedByUserId: userId);

        Assert.NotNull(obs);
        Assert.Equal(locId, obs.SampleLocationId);
        Assert.Equal(orderId, obs.TestOrderId);
        Assert.Equal(GrowthObservation.GrowthConforming, obs.GrowthObservation);
        Assert.Equal("{\"Media\":\"XLD\"}", obs.SelectiveMediaSnapshot);

        var retrieved = await service.GetByLocationAndTestOrderAsync(locId, orderId);
        Assert.NotNull(retrieved);
        Assert.Equal(GrowthObservation.GrowthConforming, retrieved.GrowthObservation);
    }

    [Fact]
    public async Task RecordPrimaryObservation_ExistingRecord_UpdatesObservation()
    {
        var (db, locId, orderId, userId) = SetupEnvironment();
        var service = new LocationPathogenObservationService(db);

        await service.RecordPrimaryObservationAsync(locId, orderId, GrowthObservation.NoGrowth, null, userId);
        var updated = await service.RecordPrimaryObservationAsync(locId, orderId, GrowthObservation.GrowthNonConforming, "{\"Media\":\"BCA\"}", userId);

        Assert.Equal(GrowthObservation.GrowthNonConforming, updated.GrowthObservation);

        var count = await db.LocationPathogenObservations.CountAsync(o => o.SampleLocationId == locId && o.TestOrderId == orderId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task QueryByTestOrder_ReturnsObservationsForSpecificTest()
    {
        var (db, locId, orderId, userId) = SetupEnvironment();
        var service = new LocationPathogenObservationService(db);

        var loc2 = new SampleLocation { Id = 102, SampleId = 10, TestOrderId = orderId, LocationType = LocationType.Room };
        db.SampleLocations.Add(loc2);
        await db.SaveChangesAsync();

        await service.RecordPrimaryObservationAsync(locId, orderId, GrowthObservation.NoGrowth, null, userId);
        await service.RecordPrimaryObservationAsync(102, orderId, GrowthObservation.GrowthConforming, null, userId);

        var results = await service.QueryByTestOrder(orderId).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(orderId, r.TestOrderId));
    }
}
