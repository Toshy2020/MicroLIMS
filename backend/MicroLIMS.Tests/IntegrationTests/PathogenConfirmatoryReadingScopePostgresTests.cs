using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class PathogenConfirmatoryReadingScopePostgresTests(PostgresTestFixture fixture)
{
    [PostgresFact]
    public async Task ForeignAndUnknownObservationIds_RejectTheWholeRequestWithoutWrites()
    {
        var (own, foreign, materialId, auditCount) = await SeedAsync();
        const string message = "One or more primary observations were not found for this sample.";
        foreach (var invalidId in new[] { foreign.Id, int.MaxValue })
        {
            await using var actDb = fixture.CreateDbContext();
            actDb.CurrentUserId = fixture.SeededUserId;
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                new PathogenSessionService(actDb).SaveBatchConfirmatoryPlateReadingsAsync(
                    own.SampleLocation!.SampleId, Request(materialId, own.Id, invalidId), fixture.SeededUserId));
            Assert.Equal("Pathogen.ConfirmatoryPrimaryObservationNotFound", ex.Code);
            Assert.Equal(message, ex.Message);
        }

        await using var assertDb = fixture.CreateDbContext();
        var plates = await assertDb.ConfirmatoryPlateObservations.AsNoTracking()
            .Where(p => p.LocationPathogenObservationId == own.Id || p.LocationPathogenObservationId == foreign.Id).ToListAsync();
        Assert.Equal(2, plates.Count);
        Assert.All(plates, p => Assert.Equal(GrowthObservation.GrowthConforming, p.Observation));
        Assert.False(await assertDb.WorkflowHistories.AsNoTracking()
            .AnyAsync(h => h.TestOrderId == own.TestOrderId || h.TestOrderId == foreign.TestOrderId));
        var locations = await assertDb.SampleLocations.AsNoTracking()
            .Where(l => l.Id == own.SampleLocationId || l.Id == foreign.SampleLocationId).ToListAsync();
        Assert.Equal(2, locations.Count);
        Assert.All(locations, l => Assert.Null(l.EnteredByUserId));
        Assert.Equal(auditCount, await assertDb.AuditLogs.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task OwnSampleObservation_UpdatesTheReadingAndAudit()
    {
        var (own, _, materialId, _) = await SeedAsync();
        await using (var actDb = fixture.CreateDbContext())
        {
            actDb.CurrentUserId = fixture.SeededUserId;
            await new PathogenSessionService(actDb).SaveBatchConfirmatoryPlateReadingsAsync(
                own.SampleLocation!.SampleId, Request(materialId, own.Id), fixture.SeededUserId);
        }

        await using var assertDb = fixture.CreateDbContext();
        var plate = await assertDb.ConfirmatoryPlateObservations.AsNoTracking()
            .SingleAsync(p => p.LocationPathogenObservationId == own.Id);
        Assert.Equal(own.SampleLocationId, plate.SampleLocationId);
        Assert.Equal(materialId, plate.MaterialId);
        Assert.Equal(0, plate.MediumIndex);
        Assert.Equal(GrowthObservation.NoGrowth, plate.Observation);
        Assert.Equal(fixture.SeededUserId, plate.RecordedByUserId);
        var location = await assertDb.SampleLocations.AsNoTracking().SingleAsync(l => l.Id == own.SampleLocationId);
        Assert.Equal(ResultStatus.Absent, location.Status);
        Assert.Equal(fixture.SeededUserId, location.EnteredByUserId);
        Assert.Equal(DateTimeKind.Utc, plate.RecordedAtUtc.Kind);

        var entityId = plate.Id.ToString();
        var audit = await assertDb.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(ConfirmatoryPlateObservation) && a.EntityId == entityId && a.Action == "Update");
        Assert.Equal(fixture.SeededUserId, audit.UserId);
        Assert.Contains(nameof(ConfirmatoryPlateObservation.Observation), audit.PreviousValue);
        Assert.Contains(nameof(ConfirmatoryPlateObservation.Observation), audit.NewValue);
    }

    private static SaveBatchConfirmatoryPlateReadingsRequest Request(int materialId, params int[] observationIds) =>
        new(observationIds.Select(id =>
            new BatchConfirmatoryPlateReadingInput(id, MediumIndex: 0, MaterialId: materialId, Observation: GrowthObservation.NoGrowth)).ToList(),
            "Supporting observation");

    private async Task<(LocationPathogenObservation Own, LocationPathogenObservation Foreign, int MaterialId, int AuditCount)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var sectionId = await db.DocumentSections.Where(s => s.Code == "MICRO").Select(s => s.Id).SingleAsync();
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync() ?? new CauseOfTesting { Name = "Routine testing" };
        var material = new Material
        {
            SectionId = sectionId,
            MaterialName = "Confirmatory plate",
            BatchNumber = Guid.NewGuid().ToString("N"),
        };
        var own = Observation(db, sectionId, cause);
        var foreign = Observation(db, sectionId, cause);
        db.ConfirmatoryPlateObservations.AddRange(ExistingPlate(own, material), ExistingPlate(foreign, material));
        await db.SaveChangesAsync();
        return (own, foreign, material.Id, await db.AuditLogs.AsNoTracking().CountAsync());
    }

    private LocationPathogenObservation Observation(MicroLimsDbContext db, int sectionId, CauseOfTesting cause)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var sample = new Sample
        {
            ReferenceNumber = $"SMP-SCOPE-{tag}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTesting = cause,
            ReceivedByUserId = fixture.SeededUserId,
        };
        var order = new TestOrder { Sample = sample, TestCode = $"PATH-{tag}", SectionId = sectionId };
        var location = new SampleLocation { Sample = sample, TestOrder = order, LocationType = LocationType.Room };
        var observation = new LocationPathogenObservation
        {
            SampleLocation = location,
            TestOrder = order,
            GrowthObservation = GrowthObservation.GrowthConforming,
            ObservedByUserId = fixture.SeededUserId,
        };
        db.LocationPathogenObservations.Add(observation);
        return observation;
    }

    // The migrated database requires a workflow step result even for a
    // location-linked plate. Updating an existing plate exercises the scope
    // rule without relying on the session's first-insert path (CONF-214).
    private ConfirmatoryPlateObservation ExistingPlate(LocationPathogenObservation observation, Material material) =>
        new()
        {
            LocationPathogenObservation = observation,
            SampleLocation = observation.SampleLocation,
            Material = material,
            RecordedByUserId = fixture.SeededUserId,
            Observation = GrowthObservation.GrowthConforming,
            WorkflowStepResult = new WorkflowStepResult
            {
                TestOrder = observation.TestOrder,
                Incubation = new Incubation
                {
                    TestOrder = observation.TestOrder,
                    StepNumber = 1,
                    StepName = "Confirmatory Plating",
                },
                StepName = "Confirmatory Plating",
                StepType = StepType.ConfirmatoryPlating,
                SubmittedByUserId = fixture.SeededUserId,
            },
        };
}
