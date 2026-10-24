using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

public record OccurrenceSummaryResponse(
    int Id, uint Version, MonitoringCategory Category, string Target, string? Group, string PeriodKey,
    DateOnly? TargetDate, DateOnly WindowStart, DateOnly WindowEnd, DateOnly OriginalWindowEnd,
    OccurrenceStatus Status, bool Late, int ItemsTotal, int ItemsPerformed, bool Manual);

public record OccurrenceItemResponse(int ItemId, string Label, ItemProgress Progress, string? SampleReference);

public record OccurrenceEventResponse(
    int Id, OccurrenceEventType EventType, string? OldValue, string? NewValue, string? Reason,
    string PerformedBy, DateTime PerformedAt, bool Signed, string? SecondSigner);

public record OccurrenceDetailResponse(
    OccurrenceSummaryResponse Summary, string FrequencySnapshot, DateOnly? OriginalTargetDate,
    List<OccurrenceItemResponse> Items, List<OccurrenceEventResponse> Events);

public record MonthSummaryResponse(int Month, int Total, int Performed, int InProgress, int Overdue, int Upcoming, int Skipped);

public record YearSummaryResponse(int Year, List<MonthSummaryResponse> Months);

public record DueResponse(DateOnly Today, int WarningDayOfMonth, bool ShowWarning, List<OccurrenceSummaryResponse> Items);

// Day: due on TargetDate (with the After Cleaning tolerances). Week: due any
// time in the lab week holding TargetDate. Month: due any time in TargetDate's month.
public enum ManualWindowKind
{
    Day,
    Week,
    Month
}

public record CreateManualOccurrenceRequest(
    int MachineId, DateOnly TargetDate, List<int>? MachinePartConfigurationIds, string? Reason,
    ManualWindowKind Kind = ManualWindowKind.Day);

// A skip is signed once, by a Section Head. A window change is signed by the
// person making it and by a second person who holds the approve permission.
public record SkipOccurrenceRequest(string Reason, string Password);

public record ChangeWindowRequest(
    DateOnly? NewTargetDate, DateOnly NewWindowEnd, string Reason, string Password,
    string SecondSignerUsername, string SecondSignerPassword);

// Reading the plan (calendar, due list, detail) and the changes a person may
// make to one occurrence: a manual After Cleaning entry, a skip, a new
// window. Every change is a signed, append-only event (spec 7, 12).
public class MonitoringOccurrenceService
{
    private const int MaxCalendarDays = 400;

    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly OccurrenceStatusService _status;
    private readonly IElectronicSignatureService _signatures;

    public MonitoringOccurrenceService(
        IMicroLimsDbContext db, ILabClock clock, OccurrenceStatusService status, IElectronicSignatureService signatures)
    {
        _db = db;
        _clock = clock;
        _status = status;
        _signatures = signatures;
    }

    // ---- reading ----

    // Occurrences whose window touches the range, for the calendar grids.
    public async Task<List<OccurrenceSummaryResponse>> GetCalendarAsync(
        DateOnly from, DateOnly to, MonitoringCategory? category, bool includeRetired)
    {
        if (to < from) throw new BusinessRuleException("The end of the range is before its start.");
        if (to.DayNumber - from.DayNumber > MaxCalendarDays) throw new BusinessRuleException($"Choose a range of at most {MaxCalendarDays} days.");

        var query = LiveQuery().Where(o => o.WindowEnd >= from && o.WindowStart <= to);
        if (category.HasValue) query = query.Where(o => o.Category == category.Value);
        if (!includeRetired) query = query.Where(o => o.RetiredByRevisionId == null);

        var occurrences = await query.Include(o => o.Items).AsNoTracking()
            .OrderBy(o => o.WindowStart).ThenBy(o => o.Id).ToListAsync();
        return await SummariesAsync(occurrences);
    }

    // Month totals for the year view. An occurrence belongs to the month of
    // its target date, or of the end of its window when it has none.
    public async Task<YearSummaryResponse> GetYearSummaryAsync(int year, MonitoringCategory? category)
    {
        if (year is < 2000 or > 2100) throw new BusinessRuleException("Choose a plan year between 2000 and 2100.");
        var first = new DateOnly(year, 1, 1);
        var last = new DateOnly(year, 12, 31);

        var query = LiveQuery().Where(o => o.RetiredByRevisionId == null && o.WindowEnd >= first && o.WindowStart <= last);
        if (category.HasValue) query = query.Where(o => o.Category == category.Value);
        var occurrences = await query.Include(o => o.Items).AsNoTracking().ToListAsync();
        var details = await _status.ComputeAsync(occurrences);

        var months = Enumerable.Range(1, 12).Select(m => new int[5]).ToArray();
        var totals = new int[12];
        foreach (var o in occurrences)
        {
            var on = o.TargetDate ?? o.WindowEnd;
            if (on.Year != year) continue;
            var m = on.Month - 1;
            totals[m]++;
            switch (details[o.Id].Result.Status)
            {
                case OccurrenceStatus.Performed:
                case OccurrenceStatus.PerformedExcursion: months[m][0]++; break;
                case OccurrenceStatus.Overdue:
                case OccurrenceStatus.Missed: months[m][2]++; break;
                case OccurrenceStatus.Upcoming: months[m][3]++; break;
                case OccurrenceStatus.Skipped: months[m][4]++; break;
                default: months[m][1]++; break;
            }
        }

        return new YearSummaryResponse(year, Enumerable.Range(0, 12)
            .Select(m => new MonthSummaryResponse(m + 1, totals[m], months[m][0], months[m][1], months[m][2], months[m][3], months[m][4]))
            .ToList());
    }

    // What is still to do this lab month, plus anything overdue.
    public async Task<DueResponse> GetDueAsync(MonitoringCategory? category)
    {
        var today = _clock.LabToday;
        var monthEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();

        var query = LiveQuery().Where(o => o.RetiredByRevisionId == null
            && o.ExceptionState == OccurrenceExceptionState.None
            && o.WindowEnd >= new DateOnly(today.Year - 1, 1, 1)
            && o.WindowStart <= monthEnd);
        if (category.HasValue) query = query.Where(o => o.Category == category.Value);
        var occurrences = await query.Include(o => o.Items).AsNoTracking().ToListAsync();

        var details = await _status.ComputeAsync(occurrences);
        // A missed occurrence whose deviation has been closed has been dealt with; it is not work to do.
        var dealtWith = (await _db.MonitoringDeviations.AsNoTracking()
                .Where(d => d.Kind == DeviationKind.MissedOccurrence && d.Status == DeviationStatus.Closed)
                .Select(d => d.MonitoringOccurrenceId).ToListAsync())
            .ToHashSet();
        var open = occurrences
            .Where(o => !dealtWith.Contains(o.Id) && details[o.Id].Items.Any(i => i.Progress == ItemProgress.None))
            .OrderBy(o => o.WindowEnd).ThenBy(o => o.Id)
            .ToList();

        return new DueResponse(today, settings.WarningDayOfMonth, today.Day >= settings.WarningDayOfMonth,
            await SummariesAsync(open, details));
    }

    public async Task<OccurrenceDetailResponse> GetDetailAsync(int id)
    {
        var occurrence = await _db.MonitoringOccurrences.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Monitoring occurrence {id} not found.");

        var details = await _status.ComputeAsync(new[] { occurrence });
        var summary = (await SummariesAsync(new List<MonitoringOccurrence> { occurrence }, details)).Single();
        var labels = await OccurrenceNameResolver.ItemLabelsAsync(_db, new[] { occurrence });
        var items = details[id].Items
            .Select(i => new OccurrenceItemResponse(i.ItemId, labels.GetValueOrDefault(i.ItemId, "?"), i.Progress, i.SampleReference))
            .ToList();

        var events = await _db.MonitoringOccurrenceEvents.AsNoTracking()
            .Where(e => e.MonitoringOccurrenceId == id)
            .OrderBy(e => e.PerformedAt).ThenBy(e => e.Id)
            .ToListAsync();
        var userIds = events.Where(e => e.PerformedByUserId.HasValue).Select(e => e.PerformedByUserId!.Value).Distinct().ToList();
        var secondIds = events.Where(e => e.SecondElectronicSignatureId.HasValue).Select(e => e.SecondElectronicSignatureId!.Value).ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        var secondSigners = secondIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.ElectronicSignatures.AsNoTracking().Where(s => secondIds.Contains(s.Id))
                .Select(s => new { s.Id, s.User!.Username }).ToDictionaryAsync(s => s.Id, s => s.Username);

        var history = events.Select(e => new OccurrenceEventResponse(
            e.Id, e.EventType, e.OldValue, e.NewValue, e.Reason, e.PerformedByUserId is int by ? users.GetValueOrDefault(by, "?") : "System",
            e.PerformedAt, e.ElectronicSignatureId.HasValue,
            e.SecondElectronicSignatureId is int sid ? secondSigners.GetValueOrDefault(sid) : null)).ToList();

        return new OccurrenceDetailResponse(summary, occurrence.FrequencySnapshot, occurrence.OriginalTargetDate, items, history);
    }

    // ---- manual After Cleaning entries ----

    // After Cleaning has no frequency: a section head adds an occurrence when a
    // machine is to be sampled after cleaning (spec 6.5). It is due on a day,
    // or any time in a chosen week or month.
    public async Task<OccurrenceSummaryResponse> CreateManualAsync(CreateManualOccurrenceRequest request, int userId)
    {
        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
        var machine = await _db.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MachineId)
            ?? throw new NotFoundException($"Machine {request.MachineId} not found.");
        if (!machine.IsActive) throw new BusinessRuleException($"Machine '{machine.Name}' is inactive.");

        // The occurrence is for the machine itself: whichever parts are cleaned and sampled at the
        // session fulfil it. Naming parts is optional and only for entries that must cover exactly those.
        var partIds = (request.MachinePartConfigurationIds ?? new List<int>()).Distinct().ToList();
        var configs = new List<MachinePartConfiguration>();
        if (partIds.Count > 0)
        {
            configs = await _db.MachinePartConfigurations.AsNoTracking().Include(c => c.MachinePart)
                .Where(c => partIds.Contains(c.Id)).ToListAsync();
            var missing = partIds.Except(configs.Select(c => c.Id)).ToList();
            if (missing.Count > 0) throw new NotFoundException($"Machine part configuration(s) not found: {string.Join(", ", missing)}.");
            var wrong = configs.Where(c => c.MachinePart?.MachineId != machine.Id).ToList();
            if (wrong.Count > 0) throw new BusinessRuleException($"Part(s) {string.Join(", ", wrong.Select(c => c.MachinePart?.Name))} do not belong to machine '{machine.Name}'.");
            var inactive = configs.Where(c => !c.IsActive || c.MachinePart?.IsActive == false).ToList();
            if (inactive.Count > 0) throw new BusinessRuleException($"Part(s) {string.Join(", ", inactive.Select(c => c.MachinePart?.Name))} are inactive.");
        }

        var today = _clock.LabToday;
        var date = request.TargetDate;
        var nonWorking = await NonWorkingDatesAsync(date.AddDays(-45), date.AddDays(75));
        DateOnly? targetDate;
        DateOnly windowStart, windowEnd;
        string period, snapshot, described;
        switch (request.Kind)
        {
            case ManualWindowKind.Week:
                var labWeekStart = PlanWindowCalculator.WeekStart(date, settings.LabWeekStartDay);
                (windowStart, windowEnd) = PlanWindowCalculator.TrimToWorkingDays(
                    labWeekStart, labWeekStart.AddDays(6), settings.WorkingWeekdays, nonWorking);
                targetDate = null;
                period = $"manual-W{labWeekStart:yyyyMMdd}";
                snapshot = "Manual entry, any day in the week";
                described = $"the week {windowStart:yyyy-MM-dd} to {windowEnd:yyyy-MM-dd}";
                break;
            case ManualWindowKind.Month:
                (windowStart, windowEnd) = PlanWindowCalculator.TrimToWorkingDays(
                    new DateOnly(date.Year, date.Month, 1),
                    new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                    settings.WorkingWeekdays, nonWorking);
                targetDate = null;
                period = $"manual-M{date:yyyyMM}";
                snapshot = "Manual entry, any day in the month";
                described = date.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
                break;
            default:
                if (!PlanWindowCalculator.IsWorkingDay(date, settings.WorkingWeekdays, nonWorking))
                    throw new BusinessRuleException($"{date:yyyy-MM-dd} is not a working day. Choose a working day.");
                windowStart = PlanWindowCalculator.AddWorkingDays(date, -settings.AcEarlyToleranceDays, settings.WorkingWeekdays, nonWorking);
                windowEnd = PlanWindowCalculator.AddWorkingDays(date, settings.AcLateToleranceDays, settings.WorkingWeekdays, nonWorking);
                targetDate = date;
                period = $"manual-{date:yyyyMMdd}";
                snapshot = "Manual entry";
                described = date.ToString("yyyy-MM-dd");
                break;
        }
        if ((targetDate ?? windowEnd) < today) throw new BusinessRuleException(
            request.Kind == ManualWindowKind.Day ? "The target date cannot be in the past." : "The week or month has already ended.");

        var planYear = (targetDate ?? windowEnd).Year;
        var revision = await _db.MonitoringPlanRevisions
            .FirstOrDefaultAsync(r => r.Year == planYear && r.Status == PlanRevisionStatus.Approved)
            ?? throw new BusinessRuleException($"There is no approved monitoring plan for {planYear}. Approve the plan before adding entries.");

        var sequence = (await _db.MonitoringOccurrences.Where(o => o.TargetType == MonitoringTargetType.Machine
                && o.MachineId == machine.Id && o.PeriodKey == period)
            .Select(o => (int?)o.Sequence).MaxAsync() ?? 0) + 1;

        var occurrence = new MonitoringOccurrence
        {
            CreatedByRevisionId = revision.Id,
            Category = MonitoringCategory.AfterCleaning,
            TargetType = MonitoringTargetType.Machine,
            MachineId = machine.Id,
            PeriodKey = period,
            Sequence = sequence,
            FrequencySnapshot = snapshot,
            TargetDate = targetDate,
            OriginalTargetDate = targetDate,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            OriginalWindowEnd = windowEnd,
            // No parts named: one item for the machine itself.
            Items = configs.Count == 0
                ? new List<MonitoringOccurrenceItem> { new() }
                : configs.Select(c => new MonitoringOccurrenceItem { MachinePartConfigurationId = c.Id }).ToList()
        };
        _db.MonitoringOccurrenceEvents.Add(new MonitoringOccurrenceEvent
        {
            MonitoringOccurrence = occurrence,
            EventType = OccurrenceEventType.Created,
            NewValue = $"Manual entry for {described}",
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            PerformedByUserId = userId,
            PerformedAt = _clock.UtcNow.UtcDateTime
        });
        _db.MonitoringOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        var created = await _db.MonitoringOccurrences.AsNoTracking().Include(o => o.Items).FirstAsync(o => o.Id == occurrence.Id);
        return (await SummariesAsync(new List<MonitoringOccurrence> { created })).Single();
    }

    // ---- skip and window change ----

    public async Task<OccurrenceSummaryResponse> SkipAsync(int id, SkipOccurrenceRequest request, int userId, string? ipAddress)
    {
        var reason = RequireReason(request.Reason);
        var occurrence = await LoadChangeableAsync(id);
        if (await HasActiveLinksAsync(id))
            throw new BusinessRuleException("A test has already been started for this occurrence, so it cannot be skipped.");

        var role = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (RoleType?)u.Role!.Type).FirstOrDefaultAsync();
        if (role != RoleType.SectionHead)
            throw new BusinessRuleException("Only a Section Head can skip an occurrence of the monitoring plan.");

        await UnitOfWork.RunAsync(_db, async () =>
        {
            var signature = await _signatures.SignAsync(userId, request.Password,
                SignatureMeaning.MonitoringOccurrenceSkipped, nameof(MonitoringOccurrence), id, reason, ipAddress);

            occurrence.ExceptionState = OccurrenceExceptionState.Skipped;
            _db.MonitoringOccurrenceEvents.Add(NewEvent(id, OccurrenceEventType.Skipped,
                Describe(occurrence.TargetDate, occurrence.WindowStart, occurrence.WindowEnd), "Skipped", reason, userId, signature, null));
            await _db.SaveChangesAsync();
        });

        return await SummaryOfAsync(id);
    }

    public async Task<OccurrenceSummaryResponse> ChangeWindowAsync(int id, ChangeWindowRequest request, int userId, string? ipAddress)
    {
        var reason = RequireReason(request.Reason);
        var occurrence = await LoadChangeableAsync(id);

        var newTarget = request.NewTargetDate ?? occurrence.TargetDate;
        if (request.NewWindowEnd < occurrence.WindowStart)
            throw new BusinessRuleException("The new end of the window cannot be before its start.");
        if (newTarget.HasValue && (newTarget < occurrence.WindowStart || newTarget > request.NewWindowEnd))
            throw new BusinessRuleException("The target date must fall inside the window.");
        if (newTarget == occurrence.TargetDate && request.NewWindowEnd == occurrence.WindowEnd)
            throw new BusinessRuleException("Nothing is changing: the target date and the end of the window are the same.");

        // Work is never planned on a day the lab does not work.
        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
        var lowest = newTarget.HasValue && newTarget < request.NewWindowEnd ? newTarget.Value : request.NewWindowEnd;
        var nonWorking = await NonWorkingDatesAsync(lowest.AddDays(-45), request.NewWindowEnd.AddDays(45));
        if (request.NewWindowEnd != occurrence.WindowEnd
            && !PlanWindowCalculator.IsWorkingDay(request.NewWindowEnd, settings.WorkingWeekdays, nonWorking))
            throw new BusinessRuleException($"{request.NewWindowEnd:yyyy-MM-dd} is not a working day. Choose a working day for the end of the window.");
        if (newTarget.HasValue && newTarget != occurrence.TargetDate
            && !PlanWindowCalculator.IsWorkingDay(newTarget.Value, settings.WorkingWeekdays, nonWorking))
            throw new BusinessRuleException($"{newTarget:yyyy-MM-dd} is not a working day. Choose a working day for the target date.");

        await UnitOfWork.RunAsync(_db, async () =>
        {
            var (first, second, secondUserId) = await SignBothAsync(
                SignatureMeaning.MonitoringOccurrenceWindowChanged, id, reason, userId, request.Password,
                request.SecondSignerUsername, request.SecondSignerPassword, ipAddress);

            if (newTarget != occurrence.TargetDate)
            {
                _db.MonitoringOccurrenceEvents.Add(NewEvent(id, OccurrenceEventType.TargetDateChanged,
                    occurrence.TargetDate?.ToString("yyyy-MM-dd"), newTarget?.ToString("yyyy-MM-dd"), reason, userId, first, second));
                occurrence.TargetDate = newTarget;
            }
            if (request.NewWindowEnd != occurrence.WindowEnd)
            {
                _db.MonitoringOccurrenceEvents.Add(NewEvent(id, OccurrenceEventType.WindowExtended,
                    occurrence.WindowEnd.ToString("yyyy-MM-dd"), request.NewWindowEnd.ToString("yyyy-MM-dd"), reason, userId, first, second));
                occurrence.WindowEnd = request.NewWindowEnd;
            }
            await _db.SaveChangesAsync();
            return secondUserId;
        });

        return await SummaryOfAsync(id);
    }

    // ---- helpers ----

    private async Task<IReadOnlySet<DateOnly>> NonWorkingDatesAsync(DateOnly from, DateOnly to) =>
        (await _db.LabCalendarDates.AsNoTracking().Where(d => d.Date >= from && d.Date <= to).Select(d => d.Date).ToListAsync())
            .ToHashSet();

    private IQueryable<MonitoringOccurrence> LiveQuery() =>
        _db.MonitoringOccurrences.Where(o => o.CreatedByRevision!.Status != PlanRevisionStatus.Draft);

    private static string RequireReason(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 5)
            throw new BusinessRuleException("Give a reason of at least five characters.");
        return trimmed;
    }

    private async Task<MonitoringOccurrence> LoadChangeableAsync(int id)
    {
        var occurrence = await _db.MonitoringOccurrences.Include(o => o.CreatedByRevision).FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Monitoring occurrence {id} not found.");
        RecordVersion.EnsureCurrent(_db, occurrence);
        if (occurrence.CreatedByRevision!.Status == PlanRevisionStatus.Draft)
            throw new BusinessRuleException("This occurrence belongs to a plan revision that is not approved yet.");
        if (occurrence.RetiredByRevisionId != null)
            throw new BusinessRuleException("This occurrence is no longer in the plan.");
        if (occurrence.ExceptionState != OccurrenceExceptionState.None)
            throw new BusinessRuleException("This occurrence has already been skipped.");
        return occurrence;
    }

    private async Task<bool> HasActiveLinksAsync(int occurrenceId)
    {
        var occurrence = await _db.MonitoringOccurrences.AsNoTracking().Include(o => o.Items).FirstAsync(o => o.Id == occurrenceId);
        var detail = (await _status.ComputeAsync(new[] { occurrence }))[occurrenceId];
        return detail.Items.Any(i => i.Progress != ItemProgress.None);
    }

    private Task<(ElectronicSignature First, ElectronicSignature Second, int SecondUserId)> SignBothAsync(
        SignatureMeaning meaning, int occurrenceId, string reason, int userId, string password,
        string secondUsername, string secondPassword, string? ipAddress) =>
        DualSignature.SignAsync(_db, _signatures, meaning, nameof(MonitoringOccurrence), occurrenceId, reason, userId, password,
            secondUsername, secondPassword, ipAddress);

    private MonitoringOccurrenceEvent NewEvent(
        int occurrenceId, OccurrenceEventType type, string? oldValue, string? newValue, string reason, int userId,
        ElectronicSignature first, ElectronicSignature? second) => new()
    {
        MonitoringOccurrenceId = occurrenceId,
        EventType = type,
        OldValue = oldValue,
        NewValue = newValue,
        Reason = reason,
        PerformedByUserId = userId,
        PerformedAt = _clock.UtcNow.UtcDateTime,
        ElectronicSignature = first,
        SecondElectronicSignature = second
    };

    private static string Describe(DateOnly? target, DateOnly start, DateOnly end) =>
        $"{(target.HasValue ? $"target {target:yyyy-MM-dd}, " : string.Empty)}window {start:yyyy-MM-dd} to {end:yyyy-MM-dd}";

    private async Task<OccurrenceSummaryResponse> SummaryOfAsync(int id)
    {
        var occurrence = await _db.MonitoringOccurrences.AsNoTracking().Include(o => o.Items).FirstAsync(o => o.Id == id);
        return (await SummariesAsync(new List<MonitoringOccurrence> { occurrence })).Single();
    }

    private async Task<List<OccurrenceSummaryResponse>> SummariesAsync(
        List<MonitoringOccurrence> occurrences, Dictionary<int, OccurrenceStatusDetail>? details = null)
    {
        if (occurrences.Count == 0) return new List<OccurrenceSummaryResponse>();
        details ??= await _status.ComputeAsync(occurrences);
        var targets = await OccurrenceNameResolver.TargetNamesAsync(_db, occurrences);
        var groups = await OccurrenceNameResolver.GroupNamesAsync(_db, occurrences);

        return occurrences.Select(o =>
        {
            var key = OccurrenceNameResolver.TargetKey(o);
            var r = details[o.Id].Result;
            return new OccurrenceSummaryResponse(
                o.Id, o.Version, o.Category, targets.GetValueOrDefault(key, "?"), groups.GetValueOrDefault(key),
                o.PeriodKey, o.TargetDate, o.WindowStart, o.WindowEnd, o.OriginalWindowEnd,
                r.Status, r.Late, r.ItemsTotal, r.ItemsPerformed, o.PeriodKey.StartsWith("manual-", StringComparison.Ordinal));
        }).ToList();
    }
}
