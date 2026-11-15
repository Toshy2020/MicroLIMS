using System.Text.Json;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Notifications;

public sealed record EscalationRuleDto(
    int Id,
    int SectionId,
    string SectionName,
    EscalationTrigger Trigger,
    decimal ThresholdWorkingHours,
    RoleType NotifyRoleType,
    bool IsActive,
    int CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime? RetiredAt,
    int? RetiredByUserId,
    string? RetiredByName,
    int? ReplacesRuleId);

public sealed class EscalationService
{
    public const decimal MinThresholdWorkingHours = 1m;
    public const decimal MaxThresholdWorkingHours = 999m;
    private const string EscalationDedupePrefix = "Escalation:rule:";

    private const string EscalationNotificationType = "Escalation";

    private readonly IMicroLimsDbContext _db;
    private readonly INotificationPublisher _publisher;
    private readonly NotificationRecipients _recipients;
    private readonly PermissionService _permissions;
    private readonly IUserSectionScopeService _scope;
    private readonly ILabClock _clock;
    private readonly TimeProvider? _time;

    public EscalationService(
        IMicroLimsDbContext db,
        INotificationPublisher publisher,
        NotificationRecipients recipients,
        PermissionService permissions,
        IUserSectionScopeService scope,
        ILabClock? clock = null,
        TimeProvider? time = null)
    {
        _db = db;
        _publisher = publisher;
        _recipients = recipients;
        _permissions = permissions;
        _scope = scope;
        _clock = clock ?? LabClock.Default;
        _time = time;
    }

    private DateTime UtcNow() => _time is not null ? _time.GetUtcNow().UtcDateTime : _clock.UtcNow.UtcDateTime;

    public async Task<IReadOnlyList<EscalationRuleDto>> ListAsync(
        int userId,
        int? sectionId,
        bool includeRetired,
        CancellationToken ct = default)
    {
        await EnsureCanConfigureAsync(userId, ct);

        var memberSections = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (sectionId.HasValue && !memberSections.Contains(sectionId.Value))
            throw new UnauthorizedAccessException("You can only view or configure escalation rules for a laboratory section you belong to.");

        if (!sectionId.HasValue && memberSections.Count == 0)
            return Array.Empty<EscalationRuleDto>();

        var query = _db.EscalationRules.AsNoTracking();
        query = sectionId.HasValue
            ? query.Where(r => r.SectionId == sectionId.Value)
            : query.Where(r => memberSections.Contains(r.SectionId));

        if (!includeRetired)
            query = query.Where(r => r.IsActive && r.RetiredAt == null);

        return await query
            .OrderBy(r => r.Section != null ? r.Section.Name : string.Empty)
            .ThenByDescending(r => r.IsActive)
            .ThenBy(r => r.Trigger)
            .ThenBy(r => r.NotifyRoleType)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new EscalationRuleDto(
                r.Id,
                r.SectionId,
                r.Section != null ? r.Section.Name : string.Empty,
                r.Trigger,
                r.ThresholdWorkingHours,
                r.NotifyRoleType,
                r.IsActive,
                r.CreatedByUserId,
                r.CreatedByUser != null ? r.CreatedByUser.FullName : null,
                r.CreatedAt,
                r.RetiredAt,
                r.RetiredByUserId,
                r.RetiredByUser != null ? r.RetiredByUser.FullName : null,
                r.ReplacesRuleId))
            .ToListAsync(ct);
    }

    public async Task<EscalationRuleDto> CreateAsync(
        int userId,
        int sectionId,
        EscalationTrigger trigger,
        decimal thresholdWorkingHours,
        RoleType notifyRole,
        CancellationToken ct = default)
    {
        await EnsureCanConfigureAsync(userId, ct);
        await EnsureWorkingHoursConfiguredAsync(ct);
        await EnsureSectionMemberAsync(userId, sectionId, ct);
        ValidateRuleInputs(trigger, thresholdWorkingHours, notifyRole);

        var sectionExists = await _db.DocumentSections
            .AsNoTracking()
            .AnyAsync(d => d.Id == sectionId && d.IsActive, ct);
        if (!sectionExists)
            throw new NotFoundException($"Section {sectionId} was not found.");

        var duplicateExists = await _db.EscalationRules
            .AsNoTracking()
            .AnyAsync(r => r.IsActive
                        && r.RetiredAt == null
                        && r.SectionId == sectionId
                        && r.Trigger == trigger
                        && r.NotifyRoleType == notifyRole, ct);
        if (duplicateExists)
            throw new BusinessRuleException("An active escalation rule already exists for this section, trigger, and role.");

        var now = UtcNow();
        var rule = new EscalationRule
        {
            SectionId = sectionId,
            Trigger = trigger,
            ThresholdWorkingHours = thresholdWorkingHours,
            NotifyRoleType = notifyRole,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = now
        };

        _db.CurrentUserId = userId;
        _db.EscalationRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return await LoadRuleDtoAsync(rule.Id, ct);
    }

    public async Task<EscalationRuleDto> ReplaceAsync(
        int userId,
        int ruleId,
        decimal thresholdWorkingHours,
        RoleType notifyRole,
        CancellationToken ct = default)
    {
        await EnsureCanConfigureAsync(userId, ct);
        await EnsureWorkingHoursConfiguredAsync(ct);

        var existing = await _db.EscalationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, ct)
            ?? throw new NotFoundException($"Escalation rule {ruleId} was not found.");

        await EnsureSectionMemberAsync(userId, existing.SectionId, ct);

        if (!existing.IsActive || existing.RetiredAt is not null)
            throw new BusinessRuleException("A retired escalation rule cannot be replaced.");

        ValidateRuleInputs(existing.Trigger, thresholdWorkingHours, notifyRole);

        var duplicateExists = await _db.EscalationRules
            .AsNoTracking()
            .AnyAsync(r => r.Id != existing.Id
                        && r.IsActive
                        && r.RetiredAt == null
                        && r.SectionId == existing.SectionId
                        && r.Trigger == existing.Trigger
                        && r.NotifyRoleType == notifyRole, ct);
        if (duplicateExists)
            throw new BusinessRuleException("An active escalation rule already exists for this section, trigger, and role.");

        var now = UtcNow();
        existing.IsActive = false;
        existing.RetiredAt = now;
        existing.RetiredByUserId = userId;

        var replacement = new EscalationRule
        {
            SectionId = existing.SectionId,
            Trigger = existing.Trigger,
            ThresholdWorkingHours = thresholdWorkingHours,
            NotifyRoleType = notifyRole,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = now,
            ReplacesRuleId = existing.Id
        };

        _db.CurrentUserId = userId;
        _db.EscalationRules.Add(replacement);
        await _db.SaveChangesAsync(ct);

        return await LoadRuleDtoAsync(replacement.Id, ct);
    }

    public async Task RetireAsync(int userId, int ruleId, CancellationToken ct = default)
    {
        await EnsureCanConfigureAsync(userId, ct);

        var existing = await _db.EscalationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, ct)
            ?? throw new NotFoundException($"Escalation rule {ruleId} was not found.");

        await EnsureSectionMemberAsync(userId, existing.SectionId, ct);

        if (!existing.IsActive || existing.RetiredAt is not null)
            return;

        existing.IsActive = false;
        existing.RetiredAt = UtcNow();
        existing.RetiredByUserId = userId;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> EvaluateAsync(CancellationToken ct = default)
    {
        var openEscalationKeys = await _db.NotificationLogs
            .AsNoTracking()
            .Where(n => n.ResolvedAt == null
                     && n.DedupeKey != null
                     && n.DedupeKey.StartsWith(EscalationDedupePrefix))
            .Select(n => n.DedupeKey!)
            .Distinct()
            .ToListAsync(ct);

        var activeRules = await _db.EscalationRules
            .AsNoTracking()
            .Where(r => r.IsActive && r.RetiredAt == null)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        if (activeRules.Count == 0 && openEscalationKeys.Count == 0)
            return 0;

        var openParsed = new List<(string DedupeKey, int RuleId, string RecordType, int RecordId)>();
        var openRuleIds = new HashSet<int>();
        foreach (var key in openEscalationKeys)
        {
            if (TryParseEscalationDedupeKey(key, out var parsedRuleId, out var parsedType, out var parsedId))
            {
                openParsed.Add((key, parsedRuleId, parsedType, parsedId));
                openRuleIds.Add(parsedRuleId);
            }
        }

        var openRuleMetadata = openRuleIds.Count == 0
            ? new Dictionary<int, (int SectionId, EscalationTrigger Trigger)>()
            : await _db.EscalationRules
                .AsNoTracking()
                .Where(r => openRuleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => (r.SectionId, r.Trigger), ct);

        var neededSectionsByTrigger = new Dictionary<EscalationTrigger, HashSet<int>>();
        foreach (var rule in activeRules)
        {
            if (!neededSectionsByTrigger.TryGetValue(rule.Trigger, out var set))
                neededSectionsByTrigger[rule.Trigger] = set = new HashSet<int>();
            set.Add(rule.SectionId);
        }
        foreach (var meta in openRuleMetadata.Values)
        {
            if (!neededSectionsByTrigger.TryGetValue(meta.Trigger, out var set))
                neededSectionsByTrigger[meta.Trigger] = set = new HashSet<int>();
            set.Add(meta.SectionId);
        }

        var now = UtcNow();
        var waitingByTrigger = new Dictionary<EscalationTrigger, List<WaitingRecord>>();
        foreach (var (trigger, sectionIds) in neededSectionsByTrigger)
        {
            waitingByTrigger[trigger] = await LoadWaitingRecordsAsync(trigger, sectionIds, now, ct);
        }

        // Resolve any open escalation notifications whose underlying cause has ended
        // (checked against current active waiting records for that rule's section and trigger).
        var activeRecordSets = waitingByTrigger.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .Select(w => (w.SectionId, w.RecordType, w.RecordId))
                .ToHashSet());

        var resolvedAny = false;
        foreach (var open in openParsed)
        {
            if (!openRuleMetadata.TryGetValue(open.RuleId, out var meta))
                continue;

            var stillWaiting = activeRecordSets.TryGetValue(meta.Trigger, out var set)
                && set.Contains((meta.SectionId, open.RecordType, open.RecordId));
            if (!stillWaiting)
            {
                await _publisher.ResolveAsync(open.DedupeKey, ct);
                resolvedAny = true;
            }
        }

        if (resolvedAny)
            await _db.SaveChangesAsync(ct);

        if (activeRules.Count == 0)
            return 0;

        var week = await LoadWorkingWeekAsync(ct);
        if (!WorkingTimeCalculator.IsConfigured(week))
            return 0;

        var nonWorkingDates = (await _db.LabCalendarDates
            .AsNoTracking()
            .Select(d => d.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var sectionNames = await _db.DocumentSections
            .AsNoTracking()
            .Where(d => activeRules.Select(r => r.SectionId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var zone = _clock.LabTimeZone;
        IReadOnlyCollection<int>? cachedAdminRecipients = null;
        var firedCount = 0;

        foreach (var rule in activeRules)
        {
            if (!waitingByTrigger.TryGetValue(rule.Trigger, out var candidates))
                continue;

            var ruleCandidates = candidates.Where(c => c.SectionId == rule.SectionId).ToList();
            if (ruleCandidates.Count == 0)
                continue;

            var recordIdsByType = ruleCandidates
                .GroupBy(c => c.RecordType)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RecordId).Distinct().ToList());

            var existingEvents = await _db.EscalationEvents
                .AsNoTracking()
                .Where(e => e.EscalationRuleId == rule.Id)
                .Select(e => new { e.RecordType, e.RecordId })
                .ToListAsync(ct);
            var alreadyFired = existingEvents
                .Select(e => (e.RecordType, e.RecordId))
                .ToHashSet();

            var thresholdHours = (double)rule.ThresholdWorkingHours;
            var sectionName = sectionNames.GetValueOrDefault(rule.SectionId, $"Section #{rule.SectionId}");

            foreach (var candidate in ruleCandidates)
            {
                if (alreadyFired.Contains((candidate.RecordType, candidate.RecordId)))
                    continue;

                var elapsed = WorkingTimeCalculator.Elapsed(candidate.WaitingSinceUtc, now, week, nonWorkingDates, zone);
                if (elapsed.TotalHours < thresholdHours)
                    continue;

                var roleRecipients = await _recipients.RoleInSectionAsync(rule.NotifyRoleType, rule.SectionId, ct);
                var noRecipient = roleRecipients.Count == 0;
                IReadOnlyCollection<int> recipientsToNotify;
                if (noRecipient)
                {
                    cachedAdminRecipients ??= await _recipients.PermissionHoldersAsync(PermissionConstants.UsersManage, ct);
                    recipientsToNotify = cachedAdminRecipients;
                }
                else
                {
                    recipientsToNotify = roleRecipients;
                }

                var ev = new EscalationEvent
                {
                    EscalationRuleId = rule.Id,
                    RecordType = candidate.RecordType,
                    RecordId = candidate.RecordId,
                    FiredAt = now,
                    RecipientUserIds = JsonSerializer.Serialize(recipientsToNotify),
                    NoRecipient = noRecipient
                };

                // The event and its notification (and outbox rows) are saved together in one
                // save: the unique index IX_EscalationEvents_Rule_Record makes a concurrent pass's
                // save fail as a whole, so it publishes nothing; and a crash can never leave an
                // event recorded whose notification was not, which would silently lose the
                // escalation because later passes skip records that already have an event.
                var pendingBefore = _db.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added)
                    .Select(e => e.Entity)
                    .ToHashSet(ReferenceEqualityComparer.Instance);

                _db.EscalationEvents.Add(ev);

                if (recipientsToNotify.Count > 0)
                {
                    var triggerLabel = DescribeTrigger(rule.Trigger);
                    var message = noRecipient
                        ? $"{sectionName}: {candidate.Summary} exceeded {rule.ThresholdWorkingHours:0.##} working hours ({triggerLabel}); no active {rule.NotifyRoleType} is assigned in this section."
                        : $"{sectionName}: {candidate.Summary} exceeded {rule.ThresholdWorkingHours:0.##} working hours ({triggerLabel}).";

                    await _publisher.PublishAsync(new NotificationRequest(
                        Type: EscalationNotificationType,
                        UserIds: recipientsToNotify,
                        Message: message,
                        TargetType: candidate.RecordType,
                        TargetId: candidate.RecordId,
                        SectionId: rule.SectionId,
                        DedupeKey: BuildDedupeKey(rule.Id, candidate.RecordType, candidate.RecordId),
                        SampleId: candidate.SampleId,
                        TestOrderId: candidate.TestOrderId), ct);
                }

                var inserted = await _db.TrySaveChangesAsync(UniqueIndexNames.EscalationEventRuleRecord);
                if (!inserted)
                {
                    // Another pass fired this record first: drop everything this attempt staged.
                    foreach (var entry in _db.ChangeTracker.Entries()
                                 .Where(e => e.State == EntityState.Added && !pendingBefore.Contains(e.Entity))
                                 .ToList())
                    {
                        entry.State = EntityState.Detached;
                    }
                    continue;
                }

                alreadyFired.Add((candidate.RecordType, candidate.RecordId));
                firedCount++;
            }
        }

        return firedCount;
    }

    private static string BuildDedupeKey(int ruleId, string recordType, int recordId) =>
        $"{EscalationDedupePrefix}{ruleId}:{recordType}:{recordId}";

    private static bool TryParseEscalationDedupeKey(string key, out int ruleId, out string recordType, out int recordId)
    {
        ruleId = 0;
        recordType = string.Empty;
        recordId = 0;

        if (!key.StartsWith(EscalationDedupePrefix, StringComparison.Ordinal))
            return false;

        var remainder = key[EscalationDedupePrefix.Length..];
        var parts = remainder.Split(':', StringSplitOptions.None);
        if (parts.Length != 3)
            return false;

        return int.TryParse(parts[0], out ruleId)
            && !string.IsNullOrEmpty(parts[1])
            && int.TryParse(parts[2], out recordId)
            && (recordType = parts[1]) is not null;
    }

    private async Task<List<WaitingRecord>> LoadWaitingRecordsAsync(
        EscalationTrigger trigger,
        HashSet<int> sectionIds,
        DateTime now,
        CancellationToken ct) =>
        trigger switch
        {
            EscalationTrigger.ReviewWaiting => await LoadReviewWaitingAsync(sectionIds, ct),
            EscalationTrigger.ApprovalWaiting => await LoadApprovalWaitingAsync(sectionIds, ct),
            EscalationTrigger.ReturnedNotCorrected => await LoadReturnedNotCorrectedAsync(sectionIds, ct),
            EscalationTrigger.IncubationNotRead => await LoadIncubationNotReadAsync(sectionIds, now, ct),
            EscalationTrigger.TestingNotStarted => await LoadTestingNotStartedAsync(sectionIds, ct),
            EscalationTrigger.DocumentTaskOverdue => await LoadDocumentTaskOverdueAsync(sectionIds, now, ct),
            _ => new List<WaitingRecord>()
        };

    private async Task<List<WaitingRecord>> LoadReviewWaitingAsync(HashSet<int> sectionIds, CancellationToken ct)
    {
        var candidates = await SectionReviewQueues.Candidates(_db.Samples.AsNoTracking(), SectionSignoffStatus.UnderReview, sectionIds)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .ToListAsync(ct);

        var entries = SectionReviewQueues.Entries(candidates, SectionSignoffStatus.UnderReview, sectionIds);
        if (entries.Count == 0)
            return new List<WaitingRecord>();

        var sampleIds = entries.Select(e => e.Sample.Id).Distinct().ToList();
        var reviewClocks = await SampleWorkflowQueues.GetReviewClockStartsAsync(_db, sampleIds);

        var sectionSubmittedEvents = await _db.ReviewWorkflowEvents
            .AsNoTracking()
            .Where(e => e.EntityType == ReviewEntityTypes.Sample
                     && e.EventType == ReviewWorkflowEventType.SubmittedForReview
                     && e.SectionId != null
                     && sampleIds.Contains(e.EntityId))
            .GroupBy(e => new { e.EntityId, SectionId = e.SectionId!.Value })
            .Select(g => new { g.Key.EntityId, g.Key.SectionId, MaxTimestamp = g.Max(x => x.Timestamp) })
            .ToDictionaryAsync(x => (x.EntityId, x.SectionId), x => x.MaxTimestamp, ct);

        return entries
            .Select(e =>
            {
                var since = e.Signoff?.SubmittedForReviewAt
                    ?? (sectionSubmittedEvents.TryGetValue((e.Sample.Id, e.SectionId), out var secTs)
                        ? secTs
                        : reviewClocks.GetValueOrDefault(e.Sample.Id, e.Sample.ReceivedAt));
                return new WaitingRecord(
                    e.SectionId,
                    "Sample",
                    e.Sample.Id,
                    since,
                    e.Sample.Id,
                    null,
                    $"Sample {e.Sample.ReferenceNumber} awaiting review");
            })
            .ToList();
    }

    private async Task<List<WaitingRecord>> LoadApprovalWaitingAsync(HashSet<int> sectionIds, CancellationToken ct)
    {
        var candidates = await SectionReviewQueues.Candidates(_db.Samples.AsNoTracking(), SectionSignoffStatus.UnderApproval, sectionIds)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .ToListAsync(ct);

        var entries = SectionReviewQueues.Entries(candidates, SectionSignoffStatus.UnderApproval, sectionIds);
        if (entries.Count == 0)
            return new List<WaitingRecord>();

        var sampleIds = entries.Select(e => e.Sample.Id).Distinct().ToList();
        var reviewCompletedEvents = await _db.ReviewWorkflowEvents
            .AsNoTracking()
            .Where(e => e.EntityType == ReviewEntityTypes.Sample
                     && e.EventType == ReviewWorkflowEventType.ReviewCompleted
                     && sampleIds.Contains(e.EntityId))
            .ToListAsync(ct);

        var sectionReviewedMap = reviewCompletedEvents
            .Where(e => e.SectionId.HasValue)
            .GroupBy(e => (e.EntityId, e.SectionId!.Value))
            .ToDictionary(g => g.Key, g => g.Max(x => x.Timestamp));

        var sampleReviewedMap = reviewCompletedEvents
            .Where(e => e.SectionId is null)
            .GroupBy(e => e.EntityId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Timestamp));

        return entries
            .Select(e =>
            {
                DateTime since;
                if (e.Signoff?.ReviewedAt is DateTime signoffReviewed)
                    since = signoffReviewed;
                else if (sectionReviewedMap.TryGetValue((e.Sample.Id, e.SectionId), out var secReviewed))
                    since = secReviewed;
                else if (e.Sample.ReviewedAt is DateTime sampleReviewed)
                    since = sampleReviewed;
                else if (sampleReviewedMap.TryGetValue(e.Sample.Id, out var eventReviewed))
                    since = eventReviewed;
                else
                    since = SampleWorkflowQueues.GetApprovalClockStart(e.Sample);

                return new WaitingRecord(
                    e.SectionId,
                    "Sample",
                    e.Sample.Id,
                    since,
                    e.Sample.Id,
                    null,
                    $"Sample {e.Sample.ReferenceNumber} awaiting approval");
            })
            .ToList();
    }

    private async Task<List<WaitingRecord>> LoadReturnedNotCorrectedAsync(HashSet<int> sectionIds, CancellationToken ct)
    {
        // Mirrors ReturnedTestsSource: non-superseded TestOrder currently in return/rework
        // (Pending/InProgress, not yet resubmitted to Ready/Reviewed/Approved) with no active
        // correction reading or test analysis recorded since return.
        var returned = await _db.TestOrders
            .AsNoTracking()
            .Where(t => !t.IsSuperseded
                     && sectionIds.Contains(t.SectionId)
                     && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                     && t.CurrentStep != WorkflowStep.Ready
                     && t.CurrentStep != WorkflowStep.Reviewed
                     && t.CurrentStep != WorkflowStep.Approved
                     && t.Sample != null
                     && !SampleWorkflowQueues.ClosedSampleStatuses.Contains(t.Sample.Status)
                     && _db.TestReturnEvents.Any(e => e.TestOrderId == t.Id)
                     && !_db.CountTestReadings.Any(r => r.TestOrderId == t.Id && r.IsActive)
                     && !_db.TestAnalyses.Any(a => a.TestOrderId == t.Id && a.IsActive))
            .Select(t => new
            {
                t.Id,
                t.SectionId,
                t.SampleId,
                SampleRef = t.Sample != null ? t.Sample.ReferenceNumber : $"#{t.SampleId}",
                LatestReturn = _db.TestReturnEvents
                    .Where(e => e.TestOrderId == t.Id)
                    .OrderByDescending(e => e.ReturnedAt)
                    .ThenByDescending(e => e.Id)
                    .Select(e => (DateTime?)e.ReturnedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return returned
            .Where(x => x.LatestReturn.HasValue)
            .Select(x => new WaitingRecord(
                x.SectionId,
                "TestOrder",
                x.Id,
                x.LatestReturn!.Value,
                x.SampleId,
                x.Id,
                $"Returned test on sample {x.SampleRef}"))
            .ToList();
    }

    private async Task<List<WaitingRecord>> LoadIncubationNotReadAsync(HashSet<int> sectionIds, DateTime now, CancellationToken ct)
    {
        var readyIncubations = await _db.Incubations
            .AsNoTracking()
            .Where(SampleWorkflowQueues.IsOpenIncubationOnActiveTest)
            .Where(SampleWorkflowQueues.IsIncubationReadyAt(now))
            .Where(i => i.TestOrder != null
                     && sectionIds.Contains(i.TestOrder.SectionId)
                     && i.TestOrder.Sample != null
                     && !SampleWorkflowQueues.ClosedSampleStatuses.Contains(i.TestOrder.Sample.Status))
            .Select(i => new
            {
                i.Id,
                SectionId = i.TestOrder!.SectionId,
                SampleId = i.TestOrder.SampleId,
                SampleRef = i.TestOrder.Sample != null ? i.TestOrder.Sample.ReferenceNumber : $"#{i.TestOrder.SampleId}",
                i.TestOrderId,
                i.ExpectedReadingAt,
                i.IncubationEndUtc,
                i.MinimumDurationOverriddenByUserId,
                i.MinimumDurationOverriddenAt
            })
            .ToListAsync(ct);

        var result = new List<WaitingRecord>(readyIncubations.Count);
        foreach (var inc in readyIncubations)
        {
            var candidates = new List<DateTime>();
            if (inc.ExpectedReadingAt is DateTime exp && exp <= now)
                candidates.Add(exp);
            if (inc.IncubationEndUtc is DateTime end && end <= now)
                candidates.Add(end);
            if (inc.MinimumDurationOverriddenByUserId.HasValue && inc.MinimumDurationOverriddenAt is DateTime ovr && ovr <= now)
                candidates.Add(ovr);

            if (candidates.Count == 0)
                continue;

            var readySince = candidates.Min();
            result.Add(new WaitingRecord(
                inc.SectionId,
                "Incubation",
                inc.Id,
                readySince,
                inc.SampleId,
                inc.TestOrderId,
                $"Incubation for sample {inc.SampleRef} ready to read"));
        }

        return result;
    }

    private async Task<List<WaitingRecord>> LoadTestingNotStartedAsync(HashSet<int> sectionIds, CancellationToken ct)
    {
        // Samples that are open and have no recorded testing step in the section.
        var samples = await _db.Samples
            .AsNoTracking()
            .Where(s => !SampleWorkflowQueues.ClosedSampleStatuses.Contains(s.Status)
                     && (s.TestOrders.Any(t => !t.IsSuperseded && sectionIds.Contains(t.SectionId))
                         || (s.LabSectionId.HasValue
                             && sectionIds.Contains(s.LabSectionId.Value)
                             && !s.TestOrders.Any(t => !t.IsSuperseded))))
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .ToListAsync(ct);

        if (samples.Count == 0)
            return new List<WaitingRecord>();

        var sectionOrders = new List<(Sample Sample, int SectionId, int? OrderId)>();
        foreach (var sample in samples)
        {
            foreach (var secId in SampleSectionRollup.SectionIds(sample))
            {
                if (!sectionIds.Contains(secId))
                    continue;
                if (SampleSectionRollup.StatusOf(sample, secId) != SectionSignoffStatus.InTesting)
                    continue;

                var orders = SampleSectionRollup.CurrentOrders(sample, secId);
                if (orders.Count == 0)
                {
                    sectionOrders.Add((sample, secId, (int?)null));
                }
                else
                {
                    foreach (var o in orders)
                        sectionOrders.Add((sample, secId, (int?)o.Id));
                }
            }
        }

        var orderIds = sectionOrders
            .Where(x => x.OrderId.HasValue)
            .Select(x => x.OrderId!.Value)
            .Distinct()
            .ToList();

        var startedOrderIds = new HashSet<int>();
        if (orderIds.Count > 0)
        {
            foreach (var o in sectionOrders)
            {
                if (o.OrderId.HasValue)
                {
                    var order = o.Sample.TestOrders.First(t => t.Id == o.OrderId.Value);
                    if (order.CurrentStep != WorkflowStep.Waiting || order.Status != ApprovalStatus.Pending)
                        startedOrderIds.Add(order.Id);
                }
            }

            var remainingIds = orderIds.Where(id => !startedOrderIds.Contains(id)).ToList();
            if (remainingIds.Count > 0)
            {
                startedOrderIds.UnionWith(await _db.WorkflowHistories.AsNoTracking()
                    .Where(h => remainingIds.Contains(h.TestOrderId) && h.ToStep != WorkflowStep.Waiting)
                    .Select(h => h.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.Incubations.AsNoTracking()
                    .Where(i => i.TestOrderId.HasValue && remainingIds.Contains(i.TestOrderId.Value))
                    .Select(i => i.TestOrderId!.Value).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.Results.AsNoTracking()
                    .Where(r => remainingIds.Contains(r.TestOrderId))
                    .Select(r => r.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.CountTestReadings.AsNoTracking()
                    .Where(r => remainingIds.Contains(r.TestOrderId) && r.IsActive)
                    .Select(r => r.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.TestAnalyses.AsNoTracking()
                    .Where(a => remainingIds.Contains(a.TestOrderId) && a.IsActive)
                    .Select(a => a.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.PathogenObservations.AsNoTracking()
                    .Where(p => remainingIds.Contains(p.TestOrderId))
                    .Select(p => p.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.WorkflowStepResults.AsNoTracking()
                    .Where(w => remainingIds.Contains(w.TestOrderId))
                    .Select(w => w.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.LocationPathogenObservations.AsNoTracking()
                    .Where(l => remainingIds.Contains(l.TestOrderId))
                    .Select(l => l.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.MediaUsages.AsNoTracking()
                    .Where(m => remainingIds.Contains(m.TestOrderId))
                    .Select(m => m.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.RoomMonitorings.AsNoTracking()
                    .Where(m => remainingIds.Contains(m.TestOrderId))
                    .Select(m => m.TestOrderId).ToListAsync(ct));
                startedOrderIds.UnionWith(await _db.SampleLocations.AsNoTracking()
                    .Where(sl => remainingIds.Contains(sl.TestOrderId) && sl.EnteredAt.HasValue)
                    .Select(sl => sl.TestOrderId).ToListAsync(ct));
            }
        }

        var result = new List<WaitingRecord>();
        foreach (var sample in samples)
        {
            foreach (var secId in SampleSectionRollup.SectionIds(sample))
            {
                if (!sectionIds.Contains(secId))
                    continue;
                if (SampleSectionRollup.StatusOf(sample, secId) != SectionSignoffStatus.InTesting)
                    continue;

                var currentOrders = SampleSectionRollup.CurrentOrders(sample, secId);
                if (currentOrders.Any(o => startedOrderIds.Contains(o.Id)))
                    continue;

                result.Add(new WaitingRecord(
                    secId,
                    "Sample",
                    sample.Id,
                    sample.ReceivedAt,
                    sample.Id,
                    null,
                    $"Sample {sample.ReferenceNumber} testing not started"));
            }
        }

        return result;
    }

    private async Task<List<WaitingRecord>> LoadDocumentTaskOverdueAsync(HashSet<int> sectionIds, DateTime now, CancellationToken ct)
    {
        var reviewTasks = await _db.DocumentReviewTasks
            .AsNoTracking()
            .Where(t => (t.Status == ReviewTaskStatus.Pending || t.Status == ReviewTaskStatus.InProgress)
                     && t.DueDate != null
                     && t.DueDate <= now
                     && t.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Cancelled
                     && t.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Obsolete
                     && t.DocumentRevision.DocumentMaster.RecordStatus == DocumentRecordStatus.Active
                     && sectionIds.Contains(t.DocumentRevision.DocumentMaster.SectionId))
            .Select(t => new
            {
                t.Id,
                t.DocumentRevision.DocumentMaster.SectionId,
                DueDate = t.DueDate!.Value,
                DocNumber = t.DocumentRevision.DocumentMaster.MicroLimsDocumentId
            })
            .ToListAsync(ct);

        var approvalTasks = await _db.DocumentApprovalTasks
            .AsNoTracking()
            .Where(t => t.Status == DocumentApprovalTaskStatus.Pending
                     && t.DueDate != null
                     && t.DueDate <= now
                     && t.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Cancelled
                     && t.DocumentRevision.RevisionStatus != DocumentRevisionStatus.Obsolete
                     && t.DocumentRevision.DocumentMaster.RecordStatus == DocumentRecordStatus.Active
                     && sectionIds.Contains(t.DocumentRevision.DocumentMaster.SectionId))
            .Select(t => new
            {
                t.Id,
                t.DocumentRevision.DocumentMaster.SectionId,
                DueDate = t.DueDate!.Value,
                DocNumber = t.DocumentRevision.DocumentMaster.MicroLimsDocumentId
            })
            .ToListAsync(ct);

        var result = new List<WaitingRecord>(reviewTasks.Count + approvalTasks.Count);
        foreach (var t in reviewTasks)
        {
            result.Add(new WaitingRecord(
                t.SectionId,
                "DocumentReviewTask",
                t.Id,
                t.DueDate,
                null,
                null,
                $"Document {t.DocNumber} review task overdue"));
        }

        foreach (var t in approvalTasks)
        {
            result.Add(new WaitingRecord(
                t.SectionId,
                "DocumentApprovalTask",
                t.Id,
                t.DueDate,
                null,
                null,
                $"Document {t.DocNumber} approval task overdue"));
        }

        return result;
    }

    private async Task<List<WorkingDay>> LoadWorkingWeekAsync(CancellationToken ct)
    {
        var rows = await _db.LabWorkingHours
            .AsNoTracking()
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync(ct);

        return rows
            .Select(h => new WorkingDay(h.DayOfWeek, h.IsWorkingDay, h.StartMinute, h.EndMinute))
            .ToList();
    }

    private async Task EnsureCanConfigureAsync(int userId, CancellationToken ct)
    {
        if (!await _permissions.UserHasPermissionAsync(userId, PermissionConstants.NotificationsConfigure, ct))
            throw new UnauthorizedAccessException("Notifications.Configure permission is required.");
    }

    private async Task EnsureSectionMemberAsync(int userId, int sectionId, CancellationToken ct)
    {
        var memberSections = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (!memberSections.Contains(sectionId))
            throw new UnauthorizedAccessException("You can only configure escalation rules for a laboratory section you belong to.");
    }

    private async Task EnsureWorkingHoursConfiguredAsync(CancellationToken ct)
    {
        var week = await LoadWorkingWeekAsync(ct);
        if (!WorkingTimeCalculator.IsConfigured(week))
            throw new BusinessRuleException("Set the laboratory working hours first.");
    }

    private static void ValidateRuleInputs(EscalationTrigger trigger, decimal thresholdWorkingHours, RoleType notifyRole)
    {
        var errors = new List<FieldError>();
        if (!Enum.IsDefined(trigger))
            errors.Add(new FieldError("trigger", "Select a valid escalation trigger."));
        if (thresholdWorkingHours < MinThresholdWorkingHours || thresholdWorkingHours > MaxThresholdWorkingHours)
            errors.Add(new FieldError("thresholdWorkingHours", "Threshold must be between 1 and 999 working hours."));
        if (!Enum.IsDefined(notifyRole))
            errors.Add(new FieldError("notifyRole", "Select a valid notification role."));

        if (errors.Count > 0)
            throw new FieldValidationException("Escalation rule validation failed.", errors);
    }

    private async Task<EscalationRuleDto> LoadRuleDtoAsync(int ruleId, CancellationToken ct)
    {
        return await _db.EscalationRules
            .AsNoTracking()
            .Where(r => r.Id == ruleId)
            .Select(r => new EscalationRuleDto(
                r.Id,
                r.SectionId,
                r.Section != null ? r.Section.Name : string.Empty,
                r.Trigger,
                r.ThresholdWorkingHours,
                r.NotifyRoleType,
                r.IsActive,
                r.CreatedByUserId,
                r.CreatedByUser != null ? r.CreatedByUser.FullName : null,
                r.CreatedAt,
                r.RetiredAt,
                r.RetiredByUserId,
                r.RetiredByUser != null ? r.RetiredByUser.FullName : null,
                r.ReplacesRuleId))
            .SingleAsync(ct);
    }

    private static string DescribeTrigger(EscalationTrigger trigger) =>
        trigger switch
        {
            EscalationTrigger.ReviewWaiting => "review waiting",
            EscalationTrigger.ApprovalWaiting => "approval waiting",
            EscalationTrigger.ReturnedNotCorrected => "returned test not corrected",
            EscalationTrigger.IncubationNotRead => "incubation ready not read",
            EscalationTrigger.TestingNotStarted => "testing not started",
            EscalationTrigger.DocumentTaskOverdue => "document task overdue",
            _ => trigger.ToString()
        };

    private sealed record WaitingRecord(
        int SectionId,
        string RecordType,
        int RecordId,
        DateTime WaitingSinceUtc,
        int? SampleId,
        int? TestOrderId,
        string Summary);
}
