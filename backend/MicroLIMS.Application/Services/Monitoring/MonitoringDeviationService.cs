using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

public record DeviationResponse(
    int Id, uint Version, string Number, DeviationKind Kind, DeviationStatus Status,
    int OccurrenceId, MonitoringCategory Category, string Target, string? Group, string PeriodKey,
    DateOnly? TargetDate, DateOnly WindowStart, DateOnly WindowEnd,
    string Description, DateTime OpenedAt, DateOnly DueDate, bool PastDue,
    int? FollowUpOccurrenceId, DeviationClosure? Closure, string? ClosureReason, DateTime? ClosedAt);

public record DeviationListResponse(int OpenCount, int PastDueCount, List<DeviationResponse> Items);

public record DeviationScanResponse(int MissedOpened, int ExcursionsOpened, int FollowUpsCreated);

public record CloseDeviationsRequest(
    List<int> DeviationIds, DeviationClosure Closure, string Reason, string Password,
    string SecondSignerUsername, string SecondSignerPassword);

// Deviations of the monitoring plan (a missed occurrence, an approved result
// over its action limit). The plan opens them itself; a person closes them
// with a reason and two signatures. An excursion also opens follow-up
// sampling for the same target.
public class MonitoringDeviationService
{
    private const int MaxPerRequest = 500;
    private const int MaxListed = 1000;

    // One scan at a time, so two scans can never open the same deviation twice.
    private static readonly SemaphoreSlim ScanLock = new(1, 1);

    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly OccurrenceStatusService _status;
    private readonly IElectronicSignatureService _signatures;

    public MonitoringDeviationService(
        IMicroLimsDbContext db, ILabClock clock, OccurrenceStatusService status, IElectronicSignatureService signatures)
    {
        _db = db;
        _clock = clock;
        _status = status;
        _signatures = signatures;
    }

    // ---- opening ----

    // Looks for missed occurrences and excursions that have no deviation yet.
    // Safe to run as often as wanted.
    public async Task<DeviationScanResponse> DetectAsync()
    {
        await ScanLock.WaitAsync();
        try
        {
            var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
            var missed = await OpenMissedAsync(settings);
            var (excursions, followUps) = await OpenExcursionsAsync(settings);
            return new DeviationScanResponse(missed, excursions, followUps);
        }
        finally
        {
            ScanLock.Release();
        }
    }

    private async Task<int> OpenMissedAsync(MonitoringSettings settings)
    {
        var today = _clock.LabToday;
        // Missed once today is past the window end plus the grace days.
        var cutoff = today.AddDays(-settings.GraceDays);

        var candidates = await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => o.CreatedByRevision!.Status != PlanRevisionStatus.Draft
                        && o.RetiredByRevisionId == null
                        && o.ExceptionState == OccurrenceExceptionState.None
                        && o.WindowEnd < cutoff
                        && !_db.MonitoringDeviations.Any(d => d.MonitoringOccurrenceId == o.Id && d.Kind == DeviationKind.MissedOccurrence))
            .Include(o => o.Items)
            .ToListAsync();
        if (candidates.Count == 0) return 0;

        var details = await _status.ComputeAsync(candidates);
        var missed = candidates.Where(o => details[o.Id].Result.Status == OccurrenceStatus.Missed).ToList();
        if (missed.Count == 0) return 0;

        var names = await OccurrenceNameResolver.TargetNamesAsync(_db, missed);
        var now = _clock.UtcNow.UtcDateTime;
        var rows = missed.Select(o =>
        {
            var r = details[o.Id].Result;
            var target = names.GetValueOrDefault(OccurrenceNameResolver.TargetKey(o), "?");
            return new MonitoringDeviation
            {
                Kind = DeviationKind.MissedOccurrence,
                MonitoringOccurrenceId = o.Id,
                OpenedAt = now,
                DueDate = today.AddDays(settings.DeviationDueDays),
                Description = r.ItemsLinked == 0
                    ? $"{target}: nothing was sampled in the window {o.WindowStart:yyyy-MM-dd} to {o.WindowEnd:yyyy-MM-dd}."
                    : $"{target}: {r.ItemsLinked} of {r.ItemsTotal} expected tests were started by {o.WindowEnd:yyyy-MM-dd}."
            };
        }).ToList();

        await UnitOfWork.RunAsync(_db, async () =>
        {
            _db.MonitoringDeviations.AddRange(rows);
            await _db.SaveChangesAsync();
            Number(rows);
            await _db.SaveChangesAsync();
        });
        return rows.Count;
    }

    private async Task<(int Excursions, int FollowUps)> OpenExcursionsAsync(MonitoringSettings settings)
    {
        var hits = await _db.SampleLocations.AsNoTracking()
            .Where(l => l.MonitoringOccurrenceId != null
                        && (l.Status == ResultStatus.ActionLimitExceeded || l.Status == ResultStatus.OutOfSpecification || l.Status == ResultStatus.Detected)
                        && l.TestOrder!.Status == ApprovalStatus.Approved
                        && !l.TestOrder.IsSuperseded
                        && !_db.MonitoringDeviations.Any(d => d.MonitoringOccurrenceId == l.MonitoringOccurrenceId && d.Kind == DeviationKind.ActionLimitExceeded))
            .Select(l => new { OccurrenceId = l.MonitoringOccurrenceId!.Value, Reference = l.Sample!.ReferenceNumber, Result = l.Status!.Value, l.TestOrder!.TestCode })
            .ToListAsync();
        if (hits.Count == 0) return (0, 0);

        var ids = hits.Select(h => h.OccurrenceId).Distinct().ToList();
        var occurrences = await _db.MonitoringOccurrences.AsNoTracking()
            .Where(o => ids.Contains(o.Id) && o.RetiredByRevisionId == null && o.CreatedByRevision!.Status != PlanRevisionStatus.Draft)
            .Include(o => o.Items)
            .ToDictionaryAsync(o => o.Id);
        if (occurrences.Count == 0) return (0, 0);

        var names = await OccurrenceNameResolver.TargetNamesAsync(_db, occurrences.Values.ToList());
        var today = _clock.LabToday;
        var now = _clock.UtcNow.UtcDateTime;

        var rows = hits.Where(h => occurrences.ContainsKey(h.OccurrenceId)).GroupBy(h => h.OccurrenceId).Select(g =>
        {
            var o = occurrences[g.Key];
            var target = names.GetValueOrDefault(OccurrenceNameResolver.TargetKey(o), "?");
            var what = string.Join("; ", g.Select(x => $"{x.TestCode} {Describe(x.Result)} (sample {x.Reference})").Distinct());
            return new MonitoringDeviation
            {
                Kind = DeviationKind.ActionLimitExceeded,
                MonitoringOccurrenceId = o.Id,
                OpenedAt = now,
                DueDate = today.AddDays(settings.DeviationDueDays),
                Description = $"{target}: {what}."
            };
        }).ToList();

        var nonWorking = (await _db.LabCalendarDates.AsNoTracking()
                .Where(d => d.Date >= today.AddDays(-1) && d.Date <= today.AddDays(90)).Select(d => d.Date).ToListAsync())
            .ToHashSet();
        var start = PlanWindowCalculator.NextWorkingDay(today, settings.WorkingWeekdays, nonWorking);
        var end = PlanWindowCalculator.AddWorkingDays(start, settings.FollowUpWindowDays, settings.WorkingWeekdays, nonWorking);

        await UnitOfWork.RunAsync(_db, async () =>
        {
            _db.MonitoringDeviations.AddRange(rows);
            await _db.SaveChangesAsync();
            Number(rows);
            await _db.SaveChangesAsync();

            // Extra sampling of the same tests at the same target, due in a few working days.
            foreach (var d in rows)
            {
                var source = occurrences[d.MonitoringOccurrenceId];
                var followUp = new MonitoringOccurrence
                {
                    CreatedByRevisionId = source.CreatedByRevisionId,
                    Category = source.Category,
                    TargetType = source.TargetType,
                    WaterSamplingPointId = source.WaterSamplingPointId,
                    RoomId = source.RoomId,
                    MachineId = source.MachineId,
                    PeriodKey = $"resp-{d.Id}",
                    Sequence = 1,
                    FrequencySnapshot = $"Follow-up to {d.Number}",
                    WindowStart = start,
                    WindowEnd = end,
                    OriginalWindowEnd = end,
                    Items = source.Items.Select(i => new MonitoringOccurrenceItem
                    {
                        RoomTestConfigurationId = i.RoomTestConfigurationId,
                        MachinePartConfigurationId = i.MachinePartConfigurationId,
                        TestCode = i.TestCode
                    }).ToList()
                };
                _db.MonitoringOccurrenceEvents.Add(new MonitoringOccurrenceEvent
                {
                    MonitoringOccurrence = followUp,
                    EventType = OccurrenceEventType.Created,
                    NewValue = $"Follow-up sampling for {d.Number}",
                    PerformedAt = now
                });
                _db.MonitoringOccurrences.Add(followUp);
                d.FollowUpOccurrence = followUp;
            }
            await _db.SaveChangesAsync();
        });
        return (rows.Count, rows.Count);
    }

    private static void Number(IEnumerable<MonitoringDeviation> rows)
    {
        foreach (var d in rows) d.Number = $"MD-{d.OpenedAt.Year}-{d.Id:D5}";
    }

    private static string Describe(ResultStatus result) => result switch
    {
        ResultStatus.ActionLimitExceeded => "over the action limit",
        ResultStatus.OutOfSpecification => "out of specification",
        ResultStatus.Detected => "organism detected",
        _ => result.ToString()
    };

    // ---- reading ----

    public async Task<DeviationListResponse> ListAsync(DeviationStatus? status, DeviationKind? kind)
    {
        var today = _clock.LabToday;
        var open = await _db.MonitoringDeviations.AsNoTracking().CountAsync(d => d.Status == DeviationStatus.Open);
        var pastDue = await _db.MonitoringDeviations.AsNoTracking().CountAsync(d => d.Status == DeviationStatus.Open && d.DueDate < today);

        var query = _db.MonitoringDeviations.AsNoTracking().Include(d => d.MonitoringOccurrence).AsQueryable();
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (kind.HasValue) query = query.Where(d => d.Kind == kind.Value);
        var rows = await query.OrderBy(d => d.Status).ThenBy(d => d.DueDate).ThenBy(d => d.Id).Take(MaxListed).ToListAsync();

        var occurrences = rows.Select(d => d.MonitoringOccurrence!).ToList();
        var targets = await OccurrenceNameResolver.TargetNamesAsync(_db, occurrences);
        var groups = await OccurrenceNameResolver.GroupNamesAsync(_db, occurrences);

        var items = rows.Select(d =>
        {
            var o = d.MonitoringOccurrence!;
            var key = OccurrenceNameResolver.TargetKey(o);
            return new DeviationResponse(
                d.Id, d.Version, d.Number, d.Kind, d.Status, o.Id, o.Category,
                targets.GetValueOrDefault(key, "?"), groups.GetValueOrDefault(key), o.PeriodKey,
                o.TargetDate, o.WindowStart, o.WindowEnd, d.Description, d.OpenedAt, d.DueDate,
                d.Status == DeviationStatus.Open && d.DueDate < today,
                d.FollowUpOccurrenceId, d.Closure, d.ClosureReason, d.ClosedAt);
        }).ToList();
        return new DeviationListResponse(open, pastDue, items);
    }

    // ---- closing ----

    // Closes one or more deviations with one reason and two signatures. Missed
    // work that fell before go-live can be closed as not applicable, which also
    // skips those occurrences so they stop counting against the plan.
    public async Task<int> CloseAsync(CloseDeviationsRequest request, int userId, string? ipAddress)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 5)
            throw new BusinessRuleException("Give a reason of at least five characters.");
        var ids = (request.DeviationIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0) throw new BusinessRuleException("Choose at least one deviation to close.");
        if (ids.Count > MaxPerRequest) throw new BusinessRuleException($"Close at most {MaxPerRequest} deviations at a time.");

        var rows = await _db.MonitoringDeviations
            .Include(d => d.MonitoringOccurrence).ThenInclude(o => o!.Items)
            .Where(d => ids.Contains(d.Id)).ToListAsync();
        var missing = ids.Except(rows.Select(d => d.Id)).ToList();
        if (missing.Count > 0) throw new NotFoundException($"Deviation(s) not found: {string.Join(", ", missing)}.");
        var closed = rows.FirstOrDefault(d => d.Status != DeviationStatus.Open);
        if (closed is not null) throw new BusinessRuleException($"{closed.Number} is already closed.");

        foreach (var d in rows)
        {
            var allowed = d.Kind == DeviationKind.MissedOccurrence
                ? request.Closure is DeviationClosure.NotApplicableBeforeGoLive or DeviationClosure.Justified
                : request.Closure == DeviationClosure.Investigated;
            if (!allowed)
                throw new BusinessRuleException($"{d.Number} cannot be closed as '{request.Closure}'.");
        }

        var skipping = request.Closure == DeviationClosure.NotApplicableBeforeGoLive;
        if (skipping)
        {
            var occurrences = rows.Select(d => d.MonitoringOccurrence!).ToList();
            var details = await _status.ComputeAsync(occurrences);
            var started = rows.FirstOrDefault(d => details[d.MonitoringOccurrenceId].Items.Any(i => i.Progress != ItemProgress.None));
            if (started is not null)
                throw new BusinessRuleException($"{started.Number}: a test was already started for this occurrence, so it cannot be closed as not applicable.");
            var skipped = rows.FirstOrDefault(d => d.MonitoringOccurrence!.ExceptionState != OccurrenceExceptionState.None);
            if (skipped is not null)
                throw new BusinessRuleException($"{skipped.Number}: this occurrence has already been skipped or retired.");
        }

        await UnitOfWork.RunAsync(_db, async () =>
        {
            var (first, second, _) = await DualSignature.SignAsync(
                _db, _signatures, SignatureMeaning.MonitoringDeviationClosed, nameof(MonitoringDeviation), rows[0].Id,
                reason, userId, request.Password, request.SecondSignerUsername, request.SecondSignerPassword, ipAddress);

            var now = _clock.UtcNow.UtcDateTime;
            foreach (var d in rows)
            {
                d.Status = DeviationStatus.Closed;
                d.Closure = request.Closure;
                d.ClosureReason = reason;
                d.ClosedAt = now;
                d.ClosedByUserId = userId;
                d.ClosureSignature = first;
                d.ClosureSecondSignature = second;

                if (!skipping) continue;
                var o = d.MonitoringOccurrence!;
                o.ExceptionState = OccurrenceExceptionState.Skipped;
                _db.MonitoringOccurrenceEvents.Add(new MonitoringOccurrenceEvent
                {
                    MonitoringOccurrenceId = o.Id,
                    EventType = OccurrenceEventType.Skipped,
                    OldValue = $"window {o.WindowStart:yyyy-MM-dd} to {o.WindowEnd:yyyy-MM-dd}",
                    NewValue = $"Skipped: closed as not applicable ({d.Number})",
                    Reason = reason,
                    PerformedByUserId = userId,
                    PerformedAt = now,
                    ElectronicSignature = first,
                    SecondElectronicSignature = second
                });
            }
            await _db.SaveChangesAsync();
        });
        return rows.Count;
    }
}
