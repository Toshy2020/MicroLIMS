using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record EligibleIncubatorDto(int Id, string Name, string Code, decimal? SetTemperature, string CalibrationStatus);

// Incubator selection was previously filtered in the browser only. This
// makes the temperature window an enforced, server-side fact so a
// hand-crafted request cannot assign an out-of-range incubator.
public class IncubatorEligibilityService
{
    private readonly MicroLimsDbContext _db;

    public IncubatorEligibilityService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EligibleIncubatorDto>> GetEligibleIncubatorsAsync(
        int stepMediaId, CancellationToken cancellationToken = default)
    {
        var stepMedia = await _db.TestWorkflowStepMedias
            .FirstOrDefaultAsync(m => m.Id == stepMediaId, cancellationToken)
            ?? throw new InvalidOperationException($"Step media {stepMediaId} not found.");

        var now = DateTime.UtcNow;

        return await _db.Equipment
            .Where(e => e.Type == EquipmentType.Incubator
                     && e.SetPointTemperature != null
                     && e.SetPointTemperature >= stepMedia.TempMin
                     && e.SetPointTemperature <= stepMedia.TempMax
                     && (e.CalibrationDueDate == null || e.CalibrationDueDate >= now))
            .OrderBy(e => e.Code)
            .Select(e => new EligibleIncubatorDto(e.Id, e.Name, e.Code, e.SetPointTemperature, "Current"))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsWithinRangeAsync(int stepMediaId, int equipmentId, CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleIncubatorsAsync(stepMediaId, cancellationToken);
        return eligible.Any(e => e.Id == equipmentId);
    }
}
