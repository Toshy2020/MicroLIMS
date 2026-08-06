using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// EvaluationStatus is Conform/NonConform/Pending from the lot's
// MediaEvaluation - the MediaEvaluation-based replacement for the old
// flat "GPT status" concept (see ReplaceGptWithMediaEvaluation migration).
// No remaining-quantity field exists on Media yet, so it's intentionally
// not part of this DTO - deferred until that's added to the domain model.
public record MediaExpiryDto(int MediaId, string LotNumber, string MediaTypeName, DateTime ExpiryDate, int DaysRemaining, string EvaluationStatus);

public class MediaExpiryService
{
    private readonly MicroLimsDbContext _db;

    public MediaExpiryService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<MediaExpiryDto>> GetExpiringAsync(int withinDays = 7)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(withinDays);

        var lots = await _db.Media
            .Include(m => m.Material)
            .Where(m => m.Status == MediaStatus.Active && m.ExpiryDate <= horizon)
            .OrderBy(m => m.ExpiryDate)
            .ToListAsync();
        if (lots.Count == 0) return new List<MediaExpiryDto>();

        var lotIds = lots.Select(m => m.Id).ToList();
        var evalByMedia = (await _db.MediaEvaluations
                .Where(e => lotIds.Contains(e.MediaId))
                .ToListAsync())
            .GroupBy(e => e.MediaId)
            .ToDictionary(g => g.Key, g => g.First());

        return lots.Select(m =>
        {
            var status = "Pending";
            if (evalByMedia.TryGetValue(m.Id, out var eval) && eval.Outcome is { } outcome)
                status = outcome == EvaluationOutcome.Conform ? "Passed" : "Failed";

            return new MediaExpiryDto(
                m.Id, m.LotNumber, m.Material?.MaterialName ?? string.Empty, m.ExpiryDate,
                (int)Math.Ceiling((m.ExpiryDate - now).TotalDays), status);
        }).ToList();
    }
}
