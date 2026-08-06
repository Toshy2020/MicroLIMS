using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record PrepareSampleRequest(
    int SampleId, decimal Amount, string Unit, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    int DiluentTypeId, int? DiluentMediaId, int NeutralizerId, int UserId,
    string? StorageCondition, int? StorageTimeHours); // Water only

// Test Preparation - Product/RM/PM/Water only, once per Sample. Must
// complete before any result can be entered for any of that sample's TestOrders.
public class SamplePreparationService
{
    private readonly MicroLimsDbContext _db;

    public SamplePreparationService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<SamplePreparation> PrepareAsync(PrepareSampleRequest request)
    {
        var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == request.SampleId)
            ?? throw new InvalidOperationException($"Sample {request.SampleId} not found.");

        if (await _db.SamplePreparations.AnyAsync(p => p.SampleId == request.SampleId))
            throw new InvalidOperationException("This sample has already been prepared.");

        var diluentType = await _db.DiluentTypes.FirstOrDefaultAsync(d => d.Id == request.DiluentTypeId)
            ?? throw new InvalidOperationException($"Diluent type {request.DiluentTypeId} not found.");

        if (diluentType.RequiresBatchTracking)
        {
            if (request.DiluentMediaId is null)
                throw new InvalidOperationException($"{diluentType.Name} requires a specific released media lot to be selected.");

            var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == request.DiluentMediaId)
                ?? throw new InvalidOperationException("Selected diluent media lot not found.");
            if (!media.IsReleasedForUse || media.ExpiryDate <= DateTime.UtcNow)
                throw new InvalidOperationException($"Media lot {media.LotNumber} is not GPT-released, inactive, or expired - cannot be used as diluent.");
        }

        if (request.Technique.Equals("Filtration", StringComparison.OrdinalIgnoreCase))
        {
            if (request.FiltrationVolume is null || request.WashingVolume is null)
                throw new InvalidOperationException("Filtration technique requires both Filtration Volume and Washing Volume.");
        }

        // Water storage condition rule: refrigerated samples must record a storage time.
        if (sample.Category == Domain.Enums.SampleCategory.Water)
        {
            if (string.IsNullOrWhiteSpace(request.StorageCondition))
                throw new InvalidOperationException("Storage condition is required for water samples.");
            if (request.StorageCondition == "Refrigerator" && request.StorageTimeHours is null)
                throw new InvalidOperationException("Storage time is required when a water sample was refrigerated.");

            sample.StorageCondition = request.StorageCondition;
            sample.StorageTimeHours = request.StorageTimeHours;
        }

        var prep = new SamplePreparation
        {
            SampleId = request.SampleId,
            Amount = request.Amount,
            Unit = request.Unit,
            Technique = request.Technique,
            FiltrationVolume = request.FiltrationVolume,
            WashingVolume = request.WashingVolume,
            DiluentTypeId = request.DiluentTypeId,
            DiluentMediaId = diluentType.RequiresBatchTracking ? request.DiluentMediaId : null,
            NeutralizerId = request.NeutralizerId,
            PreparedByUserId = request.UserId
        };

        sample.PreparationStatus = SamplePreparationStatus.Ready;

        _db.SamplePreparations.Add(prep);
        await _db.SaveChangesAsync();

        // "Start Testing" - the person who completes preparation is
        // assigned as the analyst for every test on this sample that
        // hasn't started yet. Tests already past Waiting keep whoever's
        // already on them.
        var waitingOrders = await _db.TestOrders
            .Where(t => t.SampleId == request.SampleId && t.CurrentStep == WorkflowStep.Waiting)
            .ToListAsync();
        foreach (var order in waitingOrders)
            order.AssignedAnalystId = request.UserId;
        if (waitingOrders.Count > 0)
            await _db.SaveChangesAsync();

        return prep;
    }

    public async Task<bool> IsPreparedAsync(int sampleId) =>
        await _db.SamplePreparations.AnyAsync(p => p.SampleId == sampleId);
}
