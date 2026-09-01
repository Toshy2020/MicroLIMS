using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// The preparation parameters an analyst confirms and a Section Head
// configures - identical field set on both sides, so the rules live here
// once rather than being repeated in each service.
public record PreparationParameters(
    decimal Amount,
    string Unit,
    string Technique,
    decimal? FiltrationVolume,
    decimal? WashingVolume,
    int DiluentTypeId,
    int? DiluentMediaId,
    int NeutralizerId);

public class PreparationParameterValidator
{
    private readonly MicroLimsDbContext _db;

    public PreparationParameterValidator(MicroLimsDbContext db)
    {
        _db = db;
    }

    // Returns the resolved DiluentType so callers can decide whether to
    // persist DiluentMediaId (only meaningful when batch tracked).
    public async Task<DiluentType> ValidateAsync(PreparationParameters p)
    {
        var diluentType = await _db.DiluentTypes.FirstOrDefaultAsync(d => d.Id == p.DiluentTypeId)
            ?? throw new InvalidOperationException($"Diluent type {p.DiluentTypeId} not found.");

        if (diluentType.RequiresBatchTracking)
        {
            if (p.DiluentMediaId is null)
                throw new InvalidOperationException($"{diluentType.Name} requires a specific released media lot to be selected.");

            var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == p.DiluentMediaId)
                ?? throw new InvalidOperationException("Selected diluent media lot not found.");
            if (!media.IsReleasedForUse || media.Status == MediaStatus.OutOfStock || media.Status == MediaStatus.QuarantineFailed || media.ExpiryDate <= DateTime.UtcNow)
                throw new InvalidOperationException($"Media lot {media.LotNumber} is not GPT-released, out of stock, rejected, or expired - cannot be used as diluent.");
        }

        if (!await _db.Neutralizers.AnyAsync(n => n.Id == p.NeutralizerId))
            throw new InvalidOperationException($"Neutralizer {p.NeutralizerId} not found.");

        if (p.Technique.Equals("Filtration", StringComparison.OrdinalIgnoreCase))
        {
            if (p.FiltrationVolume is null || p.WashingVolume is null)
                throw new InvalidOperationException("Filtration technique requires both Filtration Volume and Washing Volume.");
        }

        return diluentType;
    }
}
