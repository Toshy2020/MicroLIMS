using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

public record ChecklistItemResponse(int ItemId, string Label);

public record ChecklistOccurrenceResponse(
    int OccurrenceId, string Target, string PeriodKey, DateOnly? TargetDate, DateOnly WindowStart, DateOnly WindowEnd,
    OccurrenceStatus Status, bool PreTicked, List<ChecklistItemResponse> RemainingItems);

public record ChecklistSectionResponse(string Key, string Title, List<ChecklistOccurrenceResponse> Occurrences);

public record MonitoringChecklistResponse(int SampleId, MonitoringCategory Category, List<ChecklistSectionResponse> Sections);

// Connects a monitoring session (an EM, Water or After Cleaning sample) to
// the plan occurrences it fulfils (spec 9). The checklist tells the analyst
// what is due for the sample's department, water department or machine; the
// engines call LinkAsync while preparing the sample so the links are saved
// in the same transaction as the locations.
public class MonitoringLinkService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly OccurrenceStatusService _status;

    public MonitoringLinkService(IMicroLimsDbContext db, ILabClock clock, OccurrenceStatusService status)
    {
        _db = db;
        _clock = clock;
        _status = status;
    }

    public static MonitoringCategory? CategoryOf(SampleCategory category) => category switch
    {
        SampleCategory.Water => MonitoringCategory.Water,
        SampleCategory.EnvironmentalMonitoring => MonitoringCategory.EnvironmentalMonitoring,
        SampleCategory.AfterCleaning => MonitoringCategory.AfterCleaning,
        _ => null
    };

    // ---- the checklist ----

    public async Task<MonitoringChecklistResponse> GetChecklistAsync(int sampleId)
    {
        var sample = await _db.Samples.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");
        var category = CategoryOf(sample.Category)
            ?? throw new BusinessRuleException("Only Water, Environmental Monitoring and After Cleaning samples belong to the monitoring plan.");

        var today = _clock.LabToday;
        var monthEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        var earliest = new DateOnly(today.Year - 1, 1, 1);

        var candidates = await GroupOccurrencesQuery(sample, category)
            .Where(o => o.RetiredByRevisionId == null
                        && o.ExceptionState == OccurrenceExceptionState.None
                        && o.CreatedByRevision!.Status != PlanRevisionStatus.Draft
                        && o.WindowEnd >= earliest
                        && o.WindowStart <= monthEnd)
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync();

        var details = await _status.ComputeAsync(candidates);
        var open = candidates
            .Where(o => details[o.Id].Items.Any(i => i.Progress == ItemProgress.None))
            .OrderBy(o => o.WindowEnd).ThenBy(o => o.WindowStart).ThenBy(o => o.Id)
            .ToList();

        var targets = await OccurrenceNameResolver.TargetNamesAsync(_db, open);
        var labels = await OccurrenceNameResolver.ItemLabelsAsync(_db, open);

        // One occurrence per target is ticked: a sample carries one location per
        // room test, so it can fulfil only the oldest due occurrence of each.
        var ticked = new HashSet<(MonitoringTargetType, int)>();
        ChecklistOccurrenceResponse Row(MonitoringOccurrence o, bool dueNow)
        {
            var key = OccurrenceNameResolver.TargetKey(o);
            var tick = dueNow && ticked.Add(key);
            var remaining = o.Items
                .Where(i => details[o.Id].Items.First(s => s.ItemId == i.Id).Progress == ItemProgress.None)
                .Select(i => new ChecklistItemResponse(i.Id, labels.GetValueOrDefault(i.Id, "?")))
                .ToList();
            return new ChecklistOccurrenceResponse(
                o.Id, targets.GetValueOrDefault(key, "?"), o.PeriodKey, o.TargetDate, o.WindowStart, o.WindowEnd,
                details[o.Id].Result.Status, tick, remaining);
        }

        var overdue = open.Where(o => o.WindowEnd < today).Select(o => Row(o, true)).ToList();
        var dueNowRows = open.Where(o => o.WindowStart <= today && o.WindowEnd >= today).Select(o => Row(o, true)).ToList();
        var later = open.Where(o => o.WindowStart > today).Select(o => Row(o, false)).ToList();

        return new MonitoringChecklistResponse(sampleId, category, new List<ChecklistSectionResponse>
        {
            new("overdue", "Overdue", overdue),
            new("dueNow", "Due now", dueNowRows),
            new("laterThisMonth", "Also due this month", later)
        });
    }

    // Occurrences of the sample's group: its department, water department or machine.
    private IQueryable<MonitoringOccurrence> GroupOccurrencesQuery(Sample sample, MonitoringCategory category)
    {
        var all = _db.MonitoringOccurrences.Where(o => o.Category == category);
        switch (category)
        {
            case MonitoringCategory.EnvironmentalMonitoring:
                var rooms = _db.Rooms.Where(r => r.DepartmentId == sample.DepartmentId).Select(r => r.Id);
                return all.Where(o => o.RoomId != null && rooms.Contains(o.RoomId.Value));
            case MonitoringCategory.Water:
                var points = _db.WaterSamplingPoints.Where(p => p.WaterDepartmentId == sample.WaterDepartmentId).Select(p => p.Id);
                return all.Where(o => o.WaterSamplingPointId != null && points.Contains(o.WaterSamplingPointId.Value));
            default:
                return all.Where(o => o.MachineId == sample.MachineId);
        }
    }

    // ---- linking while preparing a sample ----

    // Called by an engine after it added the sample's locations and before
    // it saves. Each ticked occurrence takes the new locations that match its
    // expected items, oldest window first. Nothing is saved here.
    public async Task LinkAsync(Sample sample, IReadOnlyCollection<int> occurrenceIds, int userId)
    {
        var ids = occurrenceIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var category = CategoryOf(sample.Category)
            ?? throw new BusinessRuleException("Only Water, Environmental Monitoring and After Cleaning samples belong to the monitoring plan.");

        // Two sessions ticking the same occurrence wait for each other, so an
        // item is fulfilled once.
        if (_db.Database.IsRelational())
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM \"MonitoringOccurrences\" WHERE \"Id\" = ANY({0}) FOR UPDATE", new object[] { ids.ToArray() });

        var occurrences = await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Include(o => o.Items)
            .Include(o => o.CreatedByRevision)
            .ToListAsync();

        var missing = ids.Except(occurrences.Select(o => o.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Monitoring occurrence(s) not found: {string.Join(", ", missing)}.");

        var names = await OccurrenceNameResolver.TargetNamesAsync(_db, occurrences);
        var inGroup = await InSampleGroupAsync(sample, category, occurrences);
        foreach (var o in occurrences)
        {
            var label = $"{names.GetValueOrDefault(OccurrenceNameResolver.TargetKey(o), "?")} ({o.PeriodKey})";
            if (o.Category != category || !inGroup.Contains(o.Id))
                throw new BusinessRuleException($"{label} does not belong to this sample's {GroupName(category)}.");
            if (o.ExceptionState != OccurrenceExceptionState.None || o.RetiredByRevisionId != null)
                throw new BusinessRuleException($"{label} is {(o.ExceptionState == OccurrenceExceptionState.Skipped ? "skipped" : "no longer in the plan")} and cannot be fulfilled.");
            if (o.CreatedByRevision!.Status == PlanRevisionStatus.Draft)
                throw new BusinessRuleException($"{label} is in a plan revision that is not approved yet.");
        }

        var details = await _status.ComputeAsync(occurrences);
        var fresh = sample.Locations.Where(l => l.Id == 0 && l.MonitoringOccurrenceId == null).ToList();
        var now = _clock.UtcNow.UtcDateTime;

        foreach (var occurrence in occurrences.OrderBy(o => o.WindowEnd).ThenBy(o => o.WindowStart).ThenBy(o => o.Id))
        {
            var label = $"{names.GetValueOrDefault(OccurrenceNameResolver.TargetKey(occurrence), "?")} ({occurrence.PeriodKey})";
            var linked = 0;
            foreach (var item in occurrence.Items)
            {
                var state = details[occurrence.Id].Items.First(s => s.ItemId == item.Id);
                var location = fresh.FirstOrDefault(l => l.MonitoringOccurrenceId == null
                    && OccurrenceItemMatcher.Matches(occurrence, item,
                        new LocationFacts(l.RoomTestConfigurationId, l.MachinePartConfigurationId, l.WaterSamplingPointId, l.TestOrder?.TestCode ?? string.Empty)));
                if (location is null) continue;

                if (state.Progress != ItemProgress.None)
                    throw new BusinessRuleException(
                        $"{label} already has this test fulfilled by sample {state.SampleReference}. Untick it, or void that sample first.");

                location.MonitoringOccurrenceId = occurrence.Id;
                linked++;
            }

            if (linked == 0)
                throw new BusinessRuleException(
                    $"None of the locations in this sample fulfil {label}. Include its tests in the session, or untick it.");

            _db.MonitoringOccurrenceEvents.Add(new MonitoringOccurrenceEvent
            {
                MonitoringOccurrenceId = occurrence.Id,
                EventType = OccurrenceEventType.Linked,
                NewValue = $"{sample.ReferenceNumber}: {linked} of {occurrence.Items.Count} test(s)",
                PerformedByUserId = userId,
                PerformedAt = now
            });
        }
    }

    private async Task<HashSet<int>> InSampleGroupAsync(Sample sample, MonitoringCategory category, List<MonitoringOccurrence> occurrences)
    {
        var ids = occurrences.Select(o => o.Id).ToList();
        var matching = await GroupOccurrencesQuery(sample, category).Where(o => ids.Contains(o.Id)).Select(o => o.Id).ToListAsync();
        return matching.ToHashSet();
    }

    private static string GroupName(MonitoringCategory category) => category switch
    {
        MonitoringCategory.EnvironmentalMonitoring => "department",
        MonitoringCategory.Water => "water department",
        _ => "machine"
    };
}
