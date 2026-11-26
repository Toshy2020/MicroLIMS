using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Quality;

public sealed class QualityDueDateService(IMicroLimsDbContext db)
{
    public async Task<DateOnly?> ComputeDueAsync(QualityRecordType type, int? classificationId, DateOnly start,
        CancellationToken ct = default)
    {
        var days = await db.QualityDueRules.AsNoTracking()
            .Where(r => r.Type == type && r.WorkingDays != null
                && (r.ClassificationId == classificationId || r.ClassificationId == null))
            .OrderByDescending(r => r.ClassificationId == classificationId)
            .Select(r => r.WorkingDays)
            .FirstOrDefaultAsync(ct);
        if (days is null) return null;

        var weekdays = await db.MonitoringSettings.AsNoTracking().Select(r => (int?)r.WorkingWeekdays).FirstOrDefaultAsync(ct)
            ?? new MonitoringSettings().WorkingWeekdays;
        var holidays = (await db.LabCalendarDates.AsNoTracking().Where(d => d.Date >= start)
            .Select(d => d.Date).ToListAsync(ct)).ToHashSet();
        return PlanWindowCalculator.AddWorkingDays(start, days.Value, weekdays, holidays);
    }
}
