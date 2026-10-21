using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

// Annual Monitoring Plan: generating a year's draft revision from master
// data, previewing it, and signing it into force (spec 6.3, 6.4). The window
// maths lives in PlanWindowCalculator and PlanGenerator; this service loads
// the targets and writes the result.
public class MonitoringPlanService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly IElectronicSignatureService _signatures;

    public MonitoringPlanService(IMicroLimsDbContext db, ILabClock clock, IElectronicSignatureService signatures)
    {
        _db = db;
        _clock = clock;
        _signatures = signatures;
    }

    public async Task<List<PlanRevisionResponse>> GetRevisionsAsync(int year)
    {
        var revisions = await _db.MonitoringPlanRevisions.AsNoTracking()
            .Where(r => r.Year == year)
            .OrderBy(r => r.RevisionNumber)
            .ToListAsync();
        var counts = await OccurrenceCountsAsync(revisions.Select(r => r.Id).ToList());
        return revisions.Select(r => PlanRevisionResponse.From(r, counts.GetValueOrDefault(r.Id))).ToList();
    }

    // Builds (or rebuilds) the year's Draft. Occurrences already in force
    // are not touched: a revision of an approved year only adds what is
    // missing from now on.
    public async Task<PlanPreviewResponse> GenerateDraftAsync(int year, int userId)
    {
        ValidateYear(year);
        var today = _clock.LabToday;

        var approved = await _db.MonitoringPlanRevisions.AsNoTracking()
            .AnyAsync(r => r.Year == year && r.Status == PlanRevisionStatus.Approved);
        var effectiveFrom = approved && today > new DateOnly(year, 1, 1) ? today : new DateOnly(year, 1, 1);

        var plan = await BuildPlanAsync(year, effectiveFrom);
        var existing = await LiveOccurrencesAsync(year);
        var existingKeys = await AllKeysAsync(year);

        var toAdd = plan.Occurrences.Where(o => !existingKeys.Contains(KeyOf(o))).ToList();
        var toRetire = RetirementCandidates(existing, plan, today);
        if (approved && toAdd.Count == 0 && toRetire.Count == 0)
            throw new BusinessRuleException($"The approved plan for {year} already matches the master data. There is nothing to revise.");

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var draft = await _db.MonitoringPlanRevisions.Include(r => r.CreatedOccurrences)
                .FirstOrDefaultAsync(r => r.Year == year && r.Status == PlanRevisionStatus.Draft);

            if (draft is null)
            {
                var last = await _db.MonitoringPlanRevisions.Where(r => r.Year == year)
                    .Select(r => (int?)r.RevisionNumber).MaxAsync() ?? 0;
                draft = new MonitoringPlanRevision { Year = year, RevisionNumber = last + 1 };
                _db.MonitoringPlanRevisions.Add(draft);
            }
            else
            {
                // A draft has no history yet, so rebuilding it replaces its rows.
                _db.MonitoringOccurrences.RemoveRange(draft.CreatedOccurrences);
                draft.CreatedOccurrences = new List<MonitoringOccurrence>();
                // Saved before the new rows go in, so a reused period key never meets its old row.
                await _db.SaveChangesAsync();
            }

            draft.EffectiveFrom = effectiveFrom;
            draft.GeneratedAt = _clock.UtcNow.UtcDateTime;
            draft.GeneratedByUserId = userId;
            draft.Reason = null;

            foreach (var g in toAdd)
                draft.CreatedOccurrences.Add(ToEntity(g));

            await _db.SaveChangesAsync();
            return await PreviewAsync(draft, plan, toRetire);
        });
    }

    public async Task<PlanPreviewResponse> GetPreviewAsync(int revisionId)
    {
        var revision = await _db.MonitoringPlanRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new NotFoundException($"Plan revision {revisionId} not found.");

        if (revision.Status != PlanRevisionStatus.Draft)
            return await PreviewAsync(revision, null, null);

        var plan = await BuildPlanAsync(revision.Year, revision.EffectiveFrom);
        var toRetire = RetirementCandidates(await LiveOccurrencesAsync(revision.Year), plan, _clock.LabToday);
        return await PreviewAsync(revision, plan, toRetire);
    }

    public async Task<PlanRevisionResponse> ApproveAsync(int revisionId, ApprovePlanRequest request, int userId, string? ipAddress)
    {
        var revision = await _db.MonitoringPlanRevisions.Include(r => r.CreatedOccurrences)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new NotFoundException($"Plan revision {revisionId} not found.");
        RecordVersion.EnsureCurrent(_db, revision);

        if (revision.Status != PlanRevisionStatus.Draft)
            throw new BusinessRuleException("Only a draft revision can be approved.");

        var previous = await _db.MonitoringPlanRevisions
            .FirstOrDefaultAsync(r => r.Year == revision.Year && r.Status == PlanRevisionStatus.Approved);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (previous is not null && reason is null)
            throw new BusinessRuleException("A reason is required to approve a revision of a year that already has an approved plan.");

        var today = _clock.LabToday;
        var plan = await BuildPlanAsync(revision.Year, revision.EffectiveFrom);
        var existingKeys = await AllKeysAsync(revision.Year);
        var expectedAdds = plan.Occurrences.Where(o => !existingKeys.Contains(KeyOf(o))).Select(KeyOf).ToHashSet();
        var draftKeys = revision.CreatedOccurrences.Select(KeyOf).ToHashSet();
        if (!expectedAdds.SetEquals(draftKeys))
            throw new BusinessRuleException("The master data has changed since this draft was generated. Generate the draft again before approving it.");

        var toRetire = RetirementCandidates(await LiveOccurrencesAsync(revision.Year), plan, today);

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var signature = await _signatures.SignAsync(userId, request.Password, SignatureMeaning.MonitoringPlanApproved,
                nameof(MonitoringPlanRevision), revision.Id, reason, ipAddress);

            var now = _clock.UtcNow.UtcDateTime;
            revision.Status = PlanRevisionStatus.Approved;
            revision.Reason = reason;
            revision.ApprovalSignature = signature;
            revision.ApprovedAt = now;
            if (previous is not null) previous.Status = PlanRevisionStatus.Superseded;

            if (toRetire.Count > 0)
            {
                var ids = toRetire.Select(o => o.Id).ToList();
                var rows = await _db.MonitoringOccurrences.Where(o => ids.Contains(o.Id)).ToListAsync();
                foreach (var row in rows)
                {
                    row.RetiredByRevisionId = revision.Id;
                    row.ExceptionState = OccurrenceExceptionState.Retired;
                    _db.MonitoringOccurrenceEvents.Add(new MonitoringOccurrenceEvent
                    {
                        MonitoringOccurrenceId = row.Id,
                        EventType = OccurrenceEventType.Retired,
                        OldValue = $"{row.WindowStart:yyyy-MM-dd} to {row.WindowEnd:yyyy-MM-dd}",
                        Reason = reason,
                        PerformedByUserId = userId,
                        PerformedAt = now,
                        ElectronicSignature = signature
                    });
                }
            }

            await _db.SaveChangesAsync();
            signature.EntityId = revision.Id;
            await _db.SaveChangesAsync();
            return PlanRevisionResponse.From(revision, revision.CreatedOccurrences.Count);
        });
    }

    // ---- building the plan ----

    private static void ValidateYear(int year)
    {
        if (year is < 2000 or > 2100) throw new BusinessRuleException("Choose a plan year between 2000 and 2100.");
    }

    private async Task<GeneratedPlan> BuildPlanAsync(int year, DateOnly effectiveFrom)
    {
        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
        var from = new DateOnly(year - 1, 12, 1);
        var to = new DateOnly(year + 1, 1, 31);
        var nonWorking = (await _db.LabCalendarDates.AsNoTracking()
                .Where(d => d.Date >= from && d.Date <= to).Select(d => d.Date).ToListAsync())
            .ToHashSet();

        var targets = await WaterTargetsAsync();
        targets.AddRange(await RoomTargetsAsync());
        return PlanGenerator.Generate(targets, year, effectiveFrom, settings, nonWorking);
    }

    private async Task<List<PlanTarget>> WaterTargetsAsync()
    {
        var points = await _db.WaterSamplingPoints.AsNoTracking()
            .Where(p => p.IsActive && (p.WaterDepartment == null || p.WaterDepartment.IsActive))
            .OrderBy(p => p.Code)
            .ToListAsync();

        return points.Select(p => new PlanTarget(
            MonitoringCategory.Water, MonitoringTargetType.WaterSamplingPoint, p.Id, p.Code,
            FrequencyRules.From(p), "own",
            p.AssignedTestCodes.Distinct().Select(c => new PlanItem(null, null, c)).ToList())).ToList();
    }

    private async Task<List<PlanTarget>> RoomTargetsAsync()
    {
        var rooms = await _db.Rooms.AsNoTracking()
            .Where(r => r.IsActive && r.Department!.IsActive)
            .Include(r => r.Department)
            .OrderBy(r => r.Name)
            .ToListAsync();
        var byId = rooms.ToDictionary(r => r.Id);
        var parentIds = rooms.Where(r => r.ParentRoomId.HasValue && !byId.ContainsKey(r.ParentRoomId.Value))
            .Select(r => r.ParentRoomId!.Value).Distinct().ToList();
        // A parent can be inactive and so missing from the list above; its rule still applies.
        var extraParents = parentIds.Count == 0
            ? new Dictionary<int, Room>()
            : await _db.Rooms.AsNoTracking().Where(r => parentIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        var configs = await _db.RoomTestConfigurations.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.RoomId })
            .ToListAsync();
        var configsByRoom = configs.GroupBy(c => c.RoomId).ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        return rooms.Select(r =>
        {
            Room? parent = null;
            if (r.ParentRoomId is int pid)
                parent = byId.TryGetValue(pid, out var p) ? p : extraParents.GetValueOrDefault(pid);

            var effective = FrequencyRules.Resolve(r, r.Department, parent);
            var source = effective.Source switch
            {
                FrequencySource.Own => "own",
                FrequencySource.ParentRoom => $"from room {parent?.Name}",
                FrequencySource.Department => $"from department {r.Department?.Name}",
                _ => "none"
            };
            var name = r.Kind == RoomKind.LAF ? $"{r.Name} (LAF)" : r.Name;
            var items = configsByRoom.GetValueOrDefault(r.Id, new List<int>())
                .Select(id => new PlanItem(id, null, null)).ToList();
            return new PlanTarget(MonitoringCategory.EnvironmentalMonitoring, MonitoringTargetType.Room, r.Id, name, effective.Rule, source, items);
        }).ToList();
    }

    // ---- existing occurrences ----

    private async Task<List<MonitoringOccurrence>> LiveOccurrencesAsync(int year)
    {
        var prefix = $"{year}-";
        return await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => o.PeriodKey.StartsWith(prefix)
                        && o.TargetType != MonitoringTargetType.Machine
                        && o.RetiredByRevisionId == null
                        && o.ExceptionState == OccurrenceExceptionState.None
                        && o.CreatedByRevision!.Status != PlanRevisionStatus.Draft
                        // Once a session has been started from it, an occurrence stays.
                        && !_db.SampleLocations.Any(l => l.MonitoringOccurrenceId == o.Id))
            .ToListAsync();
    }

    // Every key already taken in the year, retired or not. Drafts are left
    // out: rebuilding a draft replaces its rows.
    private async Task<HashSet<(MonitoringTargetType, int, string, int)>> AllKeysAsync(int year)
    {
        var prefix = $"{year}-";
        var rows = await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => o.PeriodKey.StartsWith(prefix) && o.TargetType != MonitoringTargetType.Machine
                        && o.CreatedByRevision!.Status != PlanRevisionStatus.Draft)
            .Select(o => new { o.TargetType, o.WaterSamplingPointId, o.RoomId, o.PeriodKey, o.Sequence })
            .ToListAsync();
        return rows.Select(o => (o.TargetType, (o.WaterSamplingPointId ?? o.RoomId)!.Value, o.PeriodKey, o.Sequence)).ToHashSet();
    }

    // Occurrences a revision may retire: in force, no longer in the plan,
    // window not yet open, no exception, no session started from it.
    private static List<MonitoringOccurrence> RetirementCandidates(List<MonitoringOccurrence> live, GeneratedPlan plan, DateOnly today)
    {
        var desired = plan.Occurrences.Select(KeyOf).ToHashSet();
        return live.Where(o => o.WindowStart > today && !desired.Contains(KeyOf(o))).ToList();
    }

    private static (MonitoringTargetType, int, string, int) KeyOf(GeneratedOccurrence g) => g.Key;

    private static (MonitoringTargetType, int, string, int) KeyOf(MonitoringOccurrence o) =>
        (o.TargetType, (o.WaterSamplingPointId ?? o.RoomId ?? o.MachineId)!.Value, o.PeriodKey, o.Sequence);

    private static MonitoringOccurrence ToEntity(GeneratedOccurrence g) => new()
    {
        Category = g.Category,
        TargetType = g.TargetType,
        WaterSamplingPointId = g.TargetType == MonitoringTargetType.WaterSamplingPoint ? g.TargetId : null,
        RoomId = g.TargetType == MonitoringTargetType.Room ? g.TargetId : null,
        MachineId = g.TargetType == MonitoringTargetType.Machine ? g.TargetId : null,
        PeriodKey = g.Period.PeriodKey,
        Sequence = 1,
        FrequencySnapshot = g.FrequencySnapshot,
        TargetDate = g.Period.TargetDate,
        OriginalTargetDate = g.Period.OriginalTargetDate ?? g.Period.TargetDate,
        WindowStart = g.Period.WindowStart,
        WindowEnd = g.Period.WindowEnd,
        OriginalWindowEnd = g.Period.WindowEnd,
        Items = g.Items.Select(i => new MonitoringOccurrenceItem
        {
            RoomTestConfigurationId = i.RoomTestConfigurationId,
            MachinePartConfigurationId = i.MachinePartConfigurationId,
            TestCode = i.TestCode
        }).ToList()
    };

    // ---- responses ----

    private async Task<Dictionary<int, int>> OccurrenceCountsAsync(List<int> revisionIds) =>
        revisionIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.MonitoringOccurrences.AsNoTracking()
                .Where(o => revisionIds.Contains(o.CreatedByRevisionId))
                .GroupBy(o => o.CreatedByRevisionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

    private async Task<PlanPreviewResponse> PreviewAsync(
        MonitoringPlanRevision revision, GeneratedPlan? plan, List<MonitoringOccurrence>? toRetire)
    {
        var totals = await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => o.CreatedByRevisionId == revision.Id)
            .Select(o => new { o.Category, Target = o.WaterSamplingPointId ?? o.RoomId ?? o.MachineId })
            .ToListAsync();
        var byCategory = totals.GroupBy(t => t.Category)
            .Select(g => new PlanCategoryTotal(g.Key, g.Select(t => t.Target).Distinct().Count(), g.Count()))
            .OrderBy(t => t.Category).ToList();

        var retired = toRetire ?? await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => o.RetiredByRevisionId == revision.Id).ToListAsync();
        var names = await TargetNamesAsync(retired);
        var retirements = retired
            .OrderBy(o => o.WindowStart)
            .Select(o => new PlanRetirementResponse(o.Id, o.Category, names.GetValueOrDefault(KeyOf(o).Item2 + ":" + o.TargetType, "?"), o.PeriodKey, o.WindowStart, o.WindowEnd))
            .ToList();

        var exceptions = (plan?.Exceptions ?? new List<PlanException>())
            .Select(e => new PlanExceptionResponse(e.Kind, e.Category, e.Target, e.Message)).ToList();

        return new PlanPreviewResponse(PlanRevisionResponse.From(revision, totals.Count), byCategory, retirements, exceptions);
    }

    private async Task<Dictionary<string, string>> TargetNamesAsync(List<MonitoringOccurrence> occurrences)
    {
        var names = new Dictionary<string, string>();
        var pointIds = occurrences.Where(o => o.WaterSamplingPointId.HasValue).Select(o => o.WaterSamplingPointId!.Value).Distinct().ToList();
        var roomIds = occurrences.Where(o => o.RoomId.HasValue).Select(o => o.RoomId!.Value).Distinct().ToList();
        if (pointIds.Count > 0)
            foreach (var p in await _db.WaterSamplingPoints.AsNoTracking().Where(p => pointIds.Contains(p.Id)).Select(p => new { p.Id, p.Code }).ToListAsync())
                names[$"{p.Id}:{MonitoringTargetType.WaterSamplingPoint}"] = p.Code;
        if (roomIds.Count > 0)
            foreach (var r in await _db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id)).Select(r => new { r.Id, r.Name }).ToListAsync())
                names[$"{r.Id}:{MonitoringTargetType.Room}"] = r.Name;
        return names;
    }
}
