using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class LocationPathogenObservationService
{
    private readonly MicroLimsDbContext _db;

    public LocationPathogenObservationService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<LocationPathogenObservation> RecordPrimaryObservationAsync(
        int sampleLocationId,
        int testOrderId,
        GrowthObservation observation,
        string? selectiveMediaSnapshot,
        int observedByUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.LocationPathogenObservations
            .FirstOrDefaultAsync(x => x.SampleLocationId == sampleLocationId && x.TestOrderId == testOrderId, cancellationToken);

        if (existing is not null)
        {
            existing.GrowthObservation = observation;
            existing.SelectiveMediaSnapshot = selectiveMediaSnapshot ?? existing.SelectiveMediaSnapshot;
            existing.ObservedAt = DateTime.UtcNow;
            existing.ObservedByUserId = observedByUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var entity = new LocationPathogenObservation
        {
            SampleLocationId = sampleLocationId,
            TestOrderId = testOrderId,
            GrowthObservation = observation,
            SelectiveMediaSnapshot = selectiveMediaSnapshot,
            ObservedAt = DateTime.UtcNow,
            ObservedByUserId = observedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.LocationPathogenObservations.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<LocationPathogenObservation?> GetByLocationAndTestOrderAsync(
        int sampleLocationId,
        int testOrderId,
        CancellationToken cancellationToken = default)
    {
        return await _db.LocationPathogenObservations
            .Include(x => x.SampleLocation)
            .Include(x => x.TestOrder)
            .Include(x => x.ObservedByUser)
            .Include(x => x.ConfirmatoryPlateObservations)
            .FirstOrDefaultAsync(x => x.SampleLocationId == sampleLocationId && x.TestOrderId == testOrderId, cancellationToken);
    }

    public IQueryable<LocationPathogenObservation> QueryByTestOrder(int testOrderId)
    {
        return _db.LocationPathogenObservations
            .Where(x => x.TestOrderId == testOrderId)
            .Include(x => x.SampleLocation)
            .Include(x => x.ObservedByUser)
            .Include(x => x.ConfirmatoryPlateObservations);
    }

    public async Task<List<LocationPathogenObservation>> GetBySampleIdAsync(
        int sampleId,
        CancellationToken cancellationToken = default)
    {
        return await _db.LocationPathogenObservations
            .Include(x => x.SampleLocation)
            .Include(x => x.TestOrder)
            .Include(x => x.ObservedByUser)
            .Include(x => x.ConfirmatoryPlateObservations)
            .Where(x => x.SampleLocation != null && x.SampleLocation.SampleId == sampleId)
            .ToListAsync(cancellationToken);
    }
}
