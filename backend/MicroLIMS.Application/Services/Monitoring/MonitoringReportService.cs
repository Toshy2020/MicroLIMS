using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

// One row per target: how much of the plan was planned, done, done late and missed.
public record CoverageRowResponse(
    MonitoringCategory Category, string Target, string? Group, int Planned, int Performed, int PerformedLate,
    int InProgress, int Overdue, int Upcoming, int Skipped, decimal? CoveragePercent);

public record CoverageReportResponse(int Year, DateOnly AsOf, List<CoverageRowResponse> Rows, CoverageRowResponse Total);

// Two consecutive fulfilments of one target that sit too close or too far apart.
public record SpacingRowResponse(
    MonitoringCategory Category, string Target, string? Group, string Frequency, DateOnly PreviousOn, DateOnly NextOn,
    int GapDays, int MinDays, int MaxDays, bool Flagged);

public record SpacingReportResponse(int Year, List<SpacingRowResponse> Rows);

// Reports over the plan (spec 13). Everything comes from the same derived
// status the calendar shows, so a report can never disagree with the screen.
public class MonitoringReportService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly OccurrenceStatusService _status;

    public MonitoringReportService(IMicroLimsDbContext db, ILabClock clock, OccurrenceStatusService status)
    {
        _db = db;
        _clock = clock;
        _status = status;
    }

    public async Task<CoverageReportResponse> GetCoverageAsync(int year, MonitoringCategory? category)
    {
        var (occurrences, details) = await LoadYearAsync(year, category);
        var targets = await OccurrenceNameResolver.TargetNamesAsync(_db, occurrences);
        var groups = await OccurrenceNameResolver.GroupNamesAsync(_db, occurrences);

        var rows = occurrences
            .GroupBy(o => (o.Category, Key: OccurrenceNameResolver.TargetKey(o)))
            .Select(g =>
            {
                var first = g.First();
                return Row(first.Category, targets.GetValueOrDefault(g.Key.Key, "?"),
                    groups.GetValueOrDefault(g.Key.Key), g.Select(o => details[o.Id].Result).ToList());
            })
            .OrderBy(r => r.Category).ThenBy(r => r.Group).ThenBy(r => r.Target)
            .ToList();

        var total = Row(category ?? MonitoringCategory.EnvironmentalMonitoring, "All targets", null,
            occurrences.Select(o => details[o.Id].Result).ToList());
        return new CoverageReportResponse(year, _clock.LabToday, rows, total);
    }

    private static CoverageRowResponse Row(MonitoringCategory category, string target, string? group, List<OccurrenceStatusResult> results)
    {
        var skipped = results.Count(r => r.Status == OccurrenceStatus.Skipped);
        var performed = results.Count(r => r.Status is OccurrenceStatus.Performed or OccurrenceStatus.PerformedExcursion);
        var late = results.Count(r => r.Status is OccurrenceStatus.Performed or OccurrenceStatus.PerformedExcursion && r.Late);
        var overdue = results.Count(r => r.Status is OccurrenceStatus.Overdue or OccurrenceStatus.Missed);
        var upcoming = results.Count(r => r.Status == OccurrenceStatus.Upcoming);
        var planned = results.Count - skipped;
        var inProgress = planned - performed - overdue - upcoming;

        // Coverage looks only at what has fallen due: performed over performed + overdue + under way.
        var due = performed + overdue + inProgress;
        decimal? percent = due == 0 ? null : Math.Round(100m * performed / due, 1);
        return new CoverageRowResponse(category, target, group, planned, performed, late, inProgress, overdue, upcoming, skipped, percent);
    }

    public async Task<SpacingReportResponse> GetSpacingAsync(int year, MonitoringCategory? category, bool flaggedOnly)
    {
        var (occurrences, details) = await LoadYearAsync(year, category);
        var targets = await OccurrenceNameResolver.TargetNamesAsync(_db, occurrences);
        var groups = await OccurrenceNameResolver.GroupNamesAsync(_db, occurrences);

        // Allowed days between two fulfilments of one target come from the plan settings (N8).
        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();

        var rows = new List<SpacingRowResponse>();
        // Weekly and monthly series are checked separately; manual and daily entries have no cadence.
        foreach (var series in occurrences
                     .Select(o => (Occurrence: o, Kind: SeriesOf(o)))
                     .Where(x => x.Kind != null)
                     .GroupBy(x => (Key: OccurrenceNameResolver.TargetKey(x.Occurrence), x.Kind)))
        {
            var (min, max) = series.Key.Kind == "Weekly"
                ? (settings.WeeklyMinGapDays, settings.WeeklyMaxGapDays)
                : (settings.MonthlyMinGapDays, settings.MonthlyMaxGapDays);
            var dates = series
                .Select(x => details[x.Occurrence.Id].Result.FulfilledOn)
                .Where(d => d.HasValue).Select(d => d!.Value)
                .Where(d => d.Year == year)
                .Distinct().OrderBy(d => d).ToList();
            var first = series.First().Occurrence;

            for (var i = 1; i < dates.Count; i++)
            {
                var gap = dates[i].DayNumber - dates[i - 1].DayNumber;
                var flagged = gap < min || gap > max;
                if (flaggedOnly && !flagged) continue;
                rows.Add(new SpacingRowResponse(
                    first.Category, targets.GetValueOrDefault(series.Key.Key, "?"), groups.GetValueOrDefault(series.Key.Key),
                    series.Key.Kind!, dates[i - 1], dates[i], gap, min, max, flagged));
            }
        }

        return new SpacingReportResponse(year, rows
            .OrderByDescending(r => r.Flagged).ThenBy(r => r.Category).ThenBy(r => r.Target).ThenBy(r => r.PreviousOn).ToList());
    }

    // "2026-W43" is weekly, "2026-11" monthly; anything else (manual, daily) has no cadence.
    private static string? SeriesOf(MonitoringOccurrence o)
    {
        if (o.PeriodKey.Contains("-W", StringComparison.Ordinal)) return "Weekly";
        if (o.PeriodKey.Length == 7 && o.PeriodKey[4] == '-' && char.IsDigit(o.PeriodKey[5])) return "Monthly";
        return null;
    }

    private async Task<(List<MonitoringOccurrence> Occurrences, Dictionary<int, OccurrenceStatusDetail> Details)> LoadYearAsync(
        int year, MonitoringCategory? category)
    {
        if (year is < 2000 or > 2100) throw new BusinessRuleException("Choose a plan year between 2000 and 2100.");
        var first = new DateOnly(year, 1, 1);
        var last = new DateOnly(year, 12, 31);

        var query = _db.MonitoringOccurrences
            .Where(o => o.CreatedByRevision!.Status != PlanRevisionStatus.Draft
                        && o.RetiredByRevisionId == null
                        && o.WindowEnd >= first && o.WindowStart <= last);
        if (category.HasValue) query = query.Where(o => o.Category == category.Value);

        var occurrences = await query.Include(o => o.Items).AsNoTracking().ToListAsync();
        // An occurrence counts in the year of its target date, or of its window end.
        occurrences = occurrences.Where(o => (o.TargetDate ?? o.WindowEnd).Year == year).ToList();
        return (occurrences, await _status.ComputeAsync(occurrences));
    }
}
