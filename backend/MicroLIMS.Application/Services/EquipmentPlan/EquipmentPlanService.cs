using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public class EquipmentPlanService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly IElectronicSignatureService _signatures;

    public EquipmentPlanService(IMicroLimsDbContext db, ILabClock clock, IElectronicSignatureService signatures)
    {
        _db = db;
        _clock = clock;
        _signatures = signatures;
    }

    public async Task<List<EquipmentPlanRevisionResponse>> GetRevisionsAsync(int year)
    {
        var revisions = await _db.EquipmentPlanRevisions.AsNoTracking()
            .Where(r => r.Year == year)
            .OrderBy(r => r.RevisionNumber)
            .ToListAsync();
        var counts = await TaskCountsAsync(revisions.Select(r => r.Id).ToList());
        return revisions.Select(r => EquipmentPlanRevisionResponse.From(r, counts.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<EquipmentPlanPreviewResponse> GenerateDraftAsync(int year, int userId, DateOnly? goLiveDate = null)
    {
        ValidateYear(year);
        var today = _clock.LabToday;
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var earliest = today > yearStart ? today : yearStart;
        if (earliest > yearEnd)
            throw new BusinessRuleException($"The {year} plan year has already ended. Generate a plan for the current or a later year.");
        var effectiveFrom = goLiveDate ?? earliest;
        if (effectiveFrom < earliest)
            throw new BusinessRuleException($"The go-live date cannot be before {earliest:yyyy-MM-dd}. Earlier dates would plan work that can no longer be done.");
        if (effectiveFrom > yearEnd)
            throw new BusinessRuleException($"The go-live date must fall inside {year}.");

        var approved = await _db.EquipmentPlanRevisions.AsNoTracking()
            .AnyAsync(r => r.Year == year && r.Status == PlanRevisionStatus.Approved);

        var plan = await BuildPlanAsync(year, effectiveFrom);
        var existing = await LiveTasksAsync(year);
        var existingKeys = await AllKeysAsync(year);

        var toAdd = plan.Tasks.Where(t => !existingKeys.Contains(KeyOf(t))).ToList();
        var toRetire = RetirementCandidates(existing, plan);
        var toReschedule = RescheduleCandidates(existing, plan);
        if (approved && toAdd.Count == 0 && toRetire.Count == 0 && toReschedule.Count == 0)
            throw new BusinessRuleException($"The approved plan for {year} already matches the master data. There is nothing to revise.");

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var draft = await _db.EquipmentPlanRevisions.Include(r => r.CreatedTasks)
                .FirstOrDefaultAsync(r => r.Year == year && r.Status == PlanRevisionStatus.Draft);

            if (draft is null)
            {
                var last = await _db.EquipmentPlanRevisions.Where(r => r.Year == year)
                    .Select(r => (int?)r.RevisionNumber).MaxAsync() ?? 0;
                draft = new EquipmentPlanRevision { Year = year, RevisionNumber = last + 1 };
                _db.EquipmentPlanRevisions.Add(draft);
            }
            else
            {
                // A draft has no history yet, so rebuilding it replaces its rows.
                _db.EquipmentTasks.RemoveRange(draft.CreatedTasks);
                draft.CreatedTasks = new List<EquipmentTask>();
                await _db.SaveChangesAsync();
            }

            draft.EffectiveFrom = effectiveFrom;
            draft.GeneratedAt = _clock.UtcNow.UtcDateTime;
            draft.GeneratedByUserId = userId;
            draft.Reason = null;

            foreach (var g in toAdd)
                draft.CreatedTasks.Add(ToEntity(g));

            await _db.SaveChangesAsync();
            return await PreviewAsync(draft, plan, toRetire, toReschedule);
        });
    }

    public async Task<EquipmentPlanPreviewResponse> GetPreviewAsync(int id)
    {
        var revision = await _db.EquipmentPlanRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException($"Plan revision {id} not found.");

        if (revision.Status != PlanRevisionStatus.Draft)
            return await PreviewAsync(revision, null, null, null);

        var plan = await BuildPlanAsync(revision.Year, revision.EffectiveFrom);
        var live = await LiveTasksAsync(revision.Year);
        var toRetire = RetirementCandidates(live, plan);
        var toReschedule = RescheduleCandidates(live, plan);
        return await PreviewAsync(revision, plan, toRetire, toReschedule);
    }

    public async Task<EquipmentPlanRevisionResponse> ApproveAsync(
        int revisionId, ApprovePlanRequest request, int userId, string? ipAddress)
    {
        var revision = await _db.EquipmentPlanRevisions.Include(r => r.CreatedTasks)
            .FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new NotFoundException($"Plan revision {revisionId} not found.");
        RecordVersion.EnsureCurrent(_db, revision);

        if (revision.Status != PlanRevisionStatus.Draft)
            throw new BusinessRuleException("Only a draft revision can be approved.");

        if (revision.GeneratedByUserId == userId)
            throw new BusinessRuleException("The plan must be approved by someone other than the person who generated it.");

        var previous = await _db.EquipmentPlanRevisions
            .FirstOrDefaultAsync(r => r.Year == revision.Year && r.Status == PlanRevisionStatus.Approved);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (previous is not null && reason is null)
            throw new BusinessRuleException("A reason is required to approve a revision of a year that already has an approved plan.");

        var plan = await BuildPlanAsync(revision.Year, revision.EffectiveFrom);
        var existingKeys = await AllKeysAsync(revision.Year);
        var expectedAdds = plan.Tasks.Where(t => !existingKeys.Contains(KeyOf(t))).Select(KeyOf).ToHashSet();
        var draftKeys = revision.CreatedTasks.Select(KeyOf).ToHashSet();
        if (!expectedAdds.SetEquals(draftKeys))
            throw new BusinessRuleException("The master data has changed since this draft was generated. Generate the draft again before approving it.");

        var live = await LiveTasksAsync(revision.Year);
        var toRetire = RetirementCandidates(live, plan);
        var toReschedule = RescheduleCandidates(live, plan);

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var signature = await _signatures.SignAsync(
                userId,
                request.Password,
                SignatureMeaning.EquipmentPlanApproved,
                nameof(EquipmentPlanRevision),
                revision.Id,
                reason,
                ipAddress);

            var now = _clock.UtcNow.UtcDateTime;
            revision.Status = PlanRevisionStatus.Approved;
            revision.Reason = reason;
            revision.ApprovalSignature = signature;
            revision.ApprovedAt = now;
            if (previous is not null) previous.Status = PlanRevisionStatus.Superseded;

            if (toRetire.Count > 0)
            {
                var ids = toRetire.Select(o => o.Id).ToList();
                var rows = await _db.EquipmentTasks.Where(o => ids.Contains(o.Id)).ToListAsync();
                foreach (var row in rows)
                {
                    row.RetiredByRevisionId = revision.Id;
                    _db.EquipmentTaskEvents.Add(new EquipmentTaskEvent
                    {
                        EquipmentTaskId = row.Id,
                        EventType = EquipmentTaskEventType.Retired,
                        OldValue = $"{row.DueDate:yyyy-MM-dd}",
                        Reason = reason ?? $"Plan revision {revision.RevisionNumber}",
                        PerformedByUserId = userId,
                        PerformedAt = now,
                        ElectronicSignature = signature
                    });
                }
            }

            if (toReschedule.Count > 0)
            {
                var ids = toReschedule.Select(c => c.Existing.Id).ToList();
                var rows = await _db.EquipmentTasks.Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
                foreach (var (existing, desired) in toReschedule)
                {
                    var row = rows[existing.Id];
                    _db.EquipmentTaskEvents.Add(new EquipmentTaskEvent
                    {
                        EquipmentTaskId = row.Id,
                        EventType = EquipmentTaskEventType.Rescheduled,
                        OldValue = $"{row.DueDate:yyyy-MM-dd}",
                        NewValue = $"{desired.DueDate:yyyy-MM-dd}",
                        Reason = reason ?? $"Plan revision {revision.RevisionNumber}",
                        PerformedByUserId = userId,
                        PerformedAt = now,
                        ElectronicSignature = signature
                    });
                    row.DueDate = desired.DueDate;
                }
            }

            await _db.SaveChangesAsync();
            signature.EntityId = revision.Id;
            await _db.SaveChangesAsync();
            return EquipmentPlanRevisionResponse.From(revision, revision.CreatedTasks.Count);
        });
    }

    private static void ValidateYear(int year)
    {
        if (year is < 2000 or > 2100)
            throw new BusinessRuleException("Choose a plan year between 2000 and 2100.");
    }

    private async Task<GeneratedEquipmentPlan> BuildPlanAsync(int year, DateOnly effectiveFrom)
    {
        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
        var from = new DateOnly(year - 1, 12, 1);
        var to = new DateOnly(year + 1, 1, 31);
        var nonWorking = (await _db.LabCalendarDates.AsNoTracking()
                .Where(d => d.Date >= from && d.Date <= to).Select(d => d.Date).ToListAsync())
            .ToHashSet();

        var today = _clock.LabToday;
        var instruments = await _db.EquipmentInventories.AsNoTracking()
            .OrderBy(e => e.Code)
            .ToListAsync();

        var completedTasks = await _db.EquipmentTasks.AsNoTracking()
            .Where(t => t.CompletedOn != null)
            .ToListAsync();

        var completedByInstrumentAndType = completedTasks
            .GroupBy(t => (t.EquipmentInventoryId, t.TaskType))
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.CompletedOn).ToList());

        var completedThisYearByInstrumentAndType = completedTasks
            .Where(t => t.Year == year)
            .GroupBy(t => (t.EquipmentInventoryId, t.TaskType))
            .ToDictionary(g => g.Key, g => g.Count());

        var tasks = new List<GeneratedEquipmentTask>();
        var exceptions = new List<EquipmentPlanExceptionResponse>();

        foreach (var inst in instruments)
        {
            if (inst.Status == EquipmentOperationalStatus.Retired)
            {
                exceptions.Add(new EquipmentPlanExceptionResponse(
                    EquipmentPlanExceptionKind.RetiredInstrument,
                    null,
                    inst.Id,
                    inst.Code,
                    $"Instrument {inst.Code} is retired."));
                continue;
            }

            var hasCalInterval = inst.CalibrationIntervalValue.HasValue && inst.CalibrationIntervalUnit.HasValue;
            var hasPmInterval = inst.PmIntervalValue.HasValue && inst.PmIntervalUnit.HasValue;

            if (!hasCalInterval && !hasPmInterval && !inst.LastCalibratedOn.HasValue && !inst.LastPmOn.HasValue)
            {
                exceptions.Add(new EquipmentPlanExceptionResponse(
                    EquipmentPlanExceptionKind.NoInterval,
                    null,
                    inst.Id,
                    inst.Code,
                    "No calibration or maintenance interval configured."));
                continue;
            }

            // Calibration
            if (hasCalInterval)
            {
                var history = completedByInstrumentAndType.GetValueOrDefault((inst.Id, EquipmentTaskType.Calibration));
                var latestCompleted = history?.LastOrDefault()?.CompletedOn;
                var baseline = latestCompleted ?? inst.LastCalibratedOn;

                if (baseline is null)
                {
                    exceptions.Add(new EquipmentPlanExceptionResponse(
                        EquipmentPlanExceptionKind.NoBaseline,
                        EquipmentTaskType.Calibration,
                        inst.Id,
                        inst.Code,
                        "No last calibration date"));
                }
                else
                {
                    var completedCountThisYear = completedThisYearByInstrumentAndType.GetValueOrDefault((inst.Id, EquipmentTaskType.Calibration), 0);
                    var snapshot = EquipmentScheduleCalculator.Describe(inst.CalibrationIntervalValue!.Value, inst.CalibrationIntervalUnit!.Value);
                    var dates = EquipmentScheduleCalculator.ProjectYear(
                        baseline.Value,
                        inst.CalibrationIntervalValue.Value,
                        inst.CalibrationIntervalUnit.Value,
                        null,
                        year,
                        effectiveFrom,
                        today,
                        settings.WorkingWeekdays,
                        nonWorking);

                    var seq = completedCountThisYear + 1;
                    foreach (var d in dates)
                    {
                        tasks.Add(new GeneratedEquipmentTask(
                            inst.Id,
                            inst.Code,
                            EquipmentTaskType.Calibration,
                            year,
                            seq++,
                            d,
                            snapshot));
                    }
                }
            }
            else if (inst.LastCalibratedOn.HasValue)
            {
                exceptions.Add(new EquipmentPlanExceptionResponse(
                    EquipmentPlanExceptionKind.NoInterval,
                    EquipmentTaskType.Calibration,
                    inst.Id,
                    inst.Code,
                    "No calibration interval configured."));
            }

            // Preventive Maintenance
            if (hasPmInterval)
            {
                var history = completedByInstrumentAndType.GetValueOrDefault((inst.Id, EquipmentTaskType.PreventiveMaintenance));
                var latestCompleted = history?.LastOrDefault()?.CompletedOn;
                var baseline = latestCompleted ?? inst.LastPmOn;

                if (baseline is null)
                {
                    exceptions.Add(new EquipmentPlanExceptionResponse(
                        EquipmentPlanExceptionKind.NoBaseline,
                        EquipmentTaskType.PreventiveMaintenance,
                        inst.Id,
                        inst.Code,
                        "No last maintenance date"));
                }
                else
                {
                    var completedCountThisYear = completedThisYearByInstrumentAndType.GetValueOrDefault((inst.Id, EquipmentTaskType.PreventiveMaintenance), 0);
                    var snapshot = EquipmentScheduleCalculator.Describe(inst.PmIntervalValue!.Value, inst.PmIntervalUnit!.Value);
                    var dates = EquipmentScheduleCalculator.ProjectYear(
                        baseline.Value,
                        inst.PmIntervalValue.Value,
                        inst.PmIntervalUnit.Value,
                        null,
                        year,
                        effectiveFrom,
                        today,
                        settings.WorkingWeekdays,
                        nonWorking);

                    var seq = completedCountThisYear + 1;
                    foreach (var d in dates)
                    {
                        tasks.Add(new GeneratedEquipmentTask(
                            inst.Id,
                            inst.Code,
                            EquipmentTaskType.PreventiveMaintenance,
                            year,
                            seq++,
                            d,
                            snapshot));
                    }
                }
            }
            else if (inst.LastPmOn.HasValue)
            {
                exceptions.Add(new EquipmentPlanExceptionResponse(
                    EquipmentPlanExceptionKind.NoInterval,
                    EquipmentTaskType.PreventiveMaintenance,
                    inst.Id,
                    inst.Code,
                    "No maintenance interval configured."));
            }
        }

        return new GeneratedEquipmentPlan(tasks, exceptions);
    }

    private async Task<List<EquipmentTask>> LiveTasksAsync(int year)
    {
        return await _db.EquipmentTasks.AsNoTracking()
            .Include(t => t.EquipmentInventory)
            .Where(t => t.Year == year
                        && t.RetiredByRevisionId == null
                        && t.CompletedOn == null
                        && t.CreatedByRevision!.Status != PlanRevisionStatus.Draft)
            .ToListAsync();
    }

    private async Task<HashSet<(int, EquipmentTaskType, int, int)>> AllKeysAsync(int year)
    {
        var rows = await _db.EquipmentTasks.AsNoTracking()
            .Where(t => t.Year == year && t.CreatedByRevision!.Status != PlanRevisionStatus.Draft)
            .Select(t => new { t.EquipmentInventoryId, t.TaskType, t.Year, t.Sequence })
            .ToListAsync();
        return rows.Select(t => (t.EquipmentInventoryId, t.TaskType, t.Year, t.Sequence)).ToHashSet();
    }

    private static List<EquipmentTask> RetirementCandidates(List<EquipmentTask> live, GeneratedEquipmentPlan plan)
    {
        var desiredKeys = plan.Tasks.Select(KeyOf).ToHashSet();
        return live.Where(t => !desiredKeys.Contains(KeyOf(t))).ToList();
    }

    private static List<(EquipmentTask Existing, GeneratedEquipmentTask Desired)> RescheduleCandidates(
        List<EquipmentTask> live, GeneratedEquipmentPlan plan)
    {
        var desired = plan.Tasks.ToDictionary(KeyOf);
        return live
            .Where(t => desired.TryGetValue(KeyOf(t), out var d) && t.DueDate != d.DueDate)
            .Select(t => (t, desired[KeyOf(t)]))
            .ToList();
    }

    private static (int EquipmentInventoryId, EquipmentTaskType TaskType, int Year, int Sequence) KeyOf(GeneratedEquipmentTask g) =>
        (g.EquipmentInventoryId, g.TaskType, g.Year, g.Sequence);

    private static (int EquipmentInventoryId, EquipmentTaskType TaskType, int Year, int Sequence) KeyOf(EquipmentTask t) =>
        (t.EquipmentInventoryId, t.TaskType, t.Year, t.Sequence);

    private static EquipmentTask ToEntity(GeneratedEquipmentTask g) => new()
    {
        EquipmentInventoryId = g.EquipmentInventoryId,
        TaskType = g.TaskType,
        Year = g.Year,
        Sequence = g.Sequence,
        IsUnplanned = false,
        OriginalDueDate = g.DueDate,
        DueDate = g.DueDate,
        IntervalSnapshot = g.IntervalSnapshot
    };

    private async Task<Dictionary<int, int>> TaskCountsAsync(List<int> revisionIds) =>
        revisionIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.EquipmentTasks.AsNoTracking()
                .Where(t => revisionIds.Contains(t.CreatedByRevisionId ?? 0))
                .GroupBy(t => t.CreatedByRevisionId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

    private async Task<EquipmentPlanPreviewResponse> PreviewAsync(
        EquipmentPlanRevision revision,
        GeneratedEquipmentPlan? plan,
        List<EquipmentTask>? toRetire,
        List<(EquipmentTask Existing, GeneratedEquipmentTask Desired)>? toReschedule)
    {
        var tasksInDb = await _db.EquipmentTasks.AsNoTracking()
            .Include(t => t.EquipmentInventory)
            .Where(t => t.CreatedByRevisionId == revision.Id)
            .ToListAsync();

        var byType = tasksInDb.GroupBy(t => t.TaskType)
            .Select(g => new EquipmentPlanTypeTotal(g.Key, g.Select(t => t.EquipmentInventoryId).Distinct().Count(), g.Count()))
            .OrderBy(t => t.TaskType).ToList();

        var retired = toRetire ?? await _db.EquipmentTasks.AsNoTracking()
            .Include(t => t.EquipmentInventory)
            .Where(t => t.RetiredByRevisionId == revision.Id).ToListAsync();
        var moved = toReschedule ?? new();

        var instIds = retired.Select(t => t.EquipmentInventoryId)
            .Concat(moved.Select(m => m.Existing.EquipmentInventoryId))
            .Concat(tasksInDb.Select(t => t.EquipmentInventoryId))
            .Distinct().ToList();
        var codeMap = instIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.EquipmentInventories.AsNoTracking()
                .Where(e => instIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Code);

        var retirements = retired
            .OrderBy(t => t.DueDate)
            .Select(t => new EquipmentTaskRetirementResponse(
                t.Id,
                t.EquipmentInventoryId,
                codeMap.GetValueOrDefault(t.EquipmentInventoryId, t.EquipmentInventory?.Code ?? string.Empty),
                t.TaskType,
                t.Sequence,
                t.DueDate))
            .ToList();

        var exceptions = (plan?.Exceptions ?? new List<EquipmentPlanExceptionResponse>())
            .ToList();

        var reschedules = moved
            .OrderBy(m => m.Existing.DueDate)
            .Select(m => new EquipmentTaskRescheduleResponse(
                m.Existing.Id,
                m.Existing.EquipmentInventoryId,
                codeMap.GetValueOrDefault(m.Existing.EquipmentInventoryId, m.Existing.EquipmentInventory?.Code ?? string.Empty),
                m.Existing.TaskType,
                m.Existing.Sequence,
                m.Existing.DueDate,
                m.Desired.DueDate))
            .ToList();

        var taskResponses = tasksInDb
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Sequence)
            .Select(t => EquipmentTaskResponse.From(t))
            .ToList();

        return new EquipmentPlanPreviewResponse(
            EquipmentPlanRevisionResponse.From(revision, tasksInDb.Count),
            byType,
            retirements,
            reschedules,
            exceptions,
            taskResponses);
    }
}

internal record GeneratedEquipmentTask(
    int EquipmentInventoryId,
    string EquipmentCode,
    EquipmentTaskType TaskType,
    int Year,
    int Sequence,
    DateOnly DueDate,
    string IntervalSnapshot);

internal record GeneratedEquipmentPlan(
    List<GeneratedEquipmentTask> Tasks,
    List<EquipmentPlanExceptionResponse> Exceptions);
