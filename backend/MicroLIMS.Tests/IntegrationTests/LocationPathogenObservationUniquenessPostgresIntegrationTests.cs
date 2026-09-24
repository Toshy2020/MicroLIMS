using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: the in-memory provider enforces no unique
// index. Every writer upserts the primary observation for a (location,
// test order), but two saves racing each other could both insert - the
// unique index is what makes "one observation per pair" a guarantee.
[Collection("PostgresDatabaseCollection")]
public class LocationPathogenObservationUniquenessPostgresIntegrationTests
{
    private const string UniqueViolation = "23505";
    private readonly PostgresTestFixture _fixture;

    public LocationPathogenObservationUniquenessPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task SecondObservationForTheSameLocationAndTestOrder_IsRefusedByTheDatabase()
    {
        var (locationId, testOrderId) = await SeedLocationAndTestOrderAsync();

        await using (var first = _fixture.CreateDbContext())
        {
            first.LocationPathogenObservations.Add(Observation(locationId, testOrderId, GrowthObservation.NoGrowth));
            await first.SaveChangesAsync();
        }

        // A second writer that did not see the first row - e.g. a concurrent save.
        await using var second = _fixture.CreateDbContext();
        second.LocationPathogenObservations.Add(Observation(locationId, testOrderId, GrowthObservation.GrowthConforming));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(UniqueViolation, pg.SqlState);

        await using var check = _fixture.CreateDbContext();
        var stored = Assert.Single(await check.LocationPathogenObservations
            .Where(o => o.SampleLocationId == locationId && o.TestOrderId == testOrderId)
            .ToListAsync());
        Assert.Equal(GrowthObservation.NoGrowth, stored.GrowthObservation);
    }

    [PostgresFact]
    public async Task SameLocation_DifferentTestOrders_EachGetAnObservation()
    {
        var (locationId, testOrderId) = await SeedLocationAndTestOrderAsync();

        await using var db = _fixture.CreateDbContext();
        var sampleId = await db.SampleLocations.Where(l => l.Id == locationId).Select(l => l.SampleId).SingleAsync();
        var otherOrder = new TestOrder { SampleId = sampleId, TestCode = "SA", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating };
        db.TestOrders.Add(otherOrder);
        await db.SaveChangesAsync();

        db.LocationPathogenObservations.AddRange(
            Observation(locationId, testOrderId, GrowthObservation.NoGrowth),
            Observation(locationId, otherOrder.Id, GrowthObservation.NoGrowth));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.LocationPathogenObservations.CountAsync(o => o.SampleLocationId == locationId));
    }

    private LocationPathogenObservation Observation(int locationId, int testOrderId, GrowthObservation observation) => new()
    {
        SampleLocationId = locationId,
        TestOrderId = testOrderId,
        GrowthObservation = observation,
        ObservedByUserId = _fixture.SeededUserId
    };

    private async Task<(int LocationId, int TestOrderId)> SeedLocationAndTestOrderAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Release" };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var sample = new Sample
        {
            ReferenceNumber = $"UQ-{tag}",
            ControlNumber = $"UQ-CTRL-{tag}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = _fixture.SeededUserId,
            SampledBy = "Uniqueness test"
        };
        var order = new TestOrder { Sample = sample, TestCode = "EC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating };
        var location = new SampleLocation { Sample = sample, TestOrder = order, LocationType = LocationType.Room, DilutionFactor = 1.0m };
        sample.TestOrders.Add(order);
        sample.Locations.Add(location);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (location.Id, order.Id);
    }
}
