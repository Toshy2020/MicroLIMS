using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Abstractions.Live;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Notifications;

/// <summary>
/// Evaluates scheduled notification conditions (incubation readiness, lot expiries,
/// equipment calibration due dates, overdue SLA items, pending config approvals,
/// optional monitoring reminders), resolves cleared conditions, purges expired rows,
/// and enqueues once-daily email digests.
/// </summary>
public sealed class NotificationScheduler
{
    private const string IncubationPrefix = "IncubationReady:incubation:";
    private const string MediaExpiryPrefix = "MediaExpiry:media:";
    private const string SolutionExpiryPrefix = "SolutionExpiry:solution:";
    private const string StrainExpiryPrefix = "StrainExpiry:cryovial:";
    private const string CalibrationDuePrefix = "CalibrationDue:equipment:";
    private const string ItemOverduePrefix = "ItemOverdue:";
    private const string PendingConfigPrefix = "PendingPreparationConfigApproval:config:";
    private const string MonitoringPrefix = "Monitoring:";

    private readonly IMicroLimsDbContext _db;
    private readonly INotificationPublisher _publisher;
    private readonly NotificationRecipients _recipients;
    private readonly ILabClock _clock;
    private readonly ISystemSettingsReader? _settings;
    private readonly string _appBaseUrl;
    private readonly MonitoringReminderService? _monitoring;
    private readonly EscalationService? _escalation;

    public NotificationScheduler(
        IMicroLimsDbContext db,
        INotificationPublisher publisher,
        NotificationRecipients recipients,
        ILabClock? clock = null,
        ISystemSettingsReader? settings = null,
        IConfiguration? config = null,
        MonitoringReminderService? monitoring = null,
        EscalationService? escalation = null)
    {
        _db = db;
        _publisher = publisher;
        _recipients = recipients;
        _clock = clock ?? new LabClock(TimeProvider.System, LabClock.ResolveTimeZone(config?["Lab:TimeZoneId"] ?? "Africa/Cairo"));
        _settings = settings;
        _appBaseUrl = NotificationEmailComposer.ResolveAppBaseUrl(config);
        _monitoring = monitoring;
        _escalation = escalation;
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var efDb = _db as DbContext;
        var isRelational = efDb is not null && efDb.Database.IsRelational();
        var useTx = isRelational && efDb!.Database.CurrentTransaction is null;
        await using var tx = useTx ? await efDb!.Database.BeginTransactionAsync(ct) : null;

        if (isRelational && efDb!.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await efDb.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended('MicroLIMS.NotificationScheduler', 0))",
                ct);
        }

        var now = _clock.UtcNow.UtcDateTime;
        var lookAheadDays = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.NotificationsLookAheadDays, ct);
        var retentionDays = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.NotificationsRetentionDays, ct);
        var digestHourLocal = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.NotificationsDigestHourLocal, ct);
        var horizon = now.AddDays(Math.Max(0, lookAheadDays));

        var escalationCreatedNotifications = 0;
        if (_escalation is not null)
        {
            var preEscalationMaxId = await _db.NotificationLogs
                .Select(n => (int?)n.Id)
                .MaxAsync(ct) ?? 0;

            await _escalation.EvaluateAsync(ct);

            escalationCreatedNotifications = await _db.NotificationLogs
                .CountAsync(n => n.Id > preEscalationMaxId, ct);
        }

        await ScheduleIncubationReadyAsync(now, ct);
        await ScheduleMediaExpiryAsync(now, horizon, ct);
        await ScheduleSolutionExpiryAsync(now, horizon, ct);
        await ScheduleStrainExpiryAsync(now, horizon, ct);
        await ScheduleCalibrationDueAsync(now, horizon, ct);
        await ScheduleItemOverdueAsync(now, ct);
        await SchedulePendingConfigurationApprovalsAsync(ct);

        if (_monitoring is not null)
        {
            await ScheduleMonitoringRemindersAsync(ct);
        }

        var createdCount = escalationCreatedNotifications + (efDb is not null
            ? efDb.ChangeTracker.Entries<Domain.Entities.NotificationLog>().Count(e => e.State == EntityState.Added)
            : _db.NotificationLogs.Local.Count(n => n.Id == 0));

        await _db.SaveChangesAsync(ct);

        await PurgeExpiredNotificationsAsync(now, retentionDays, ct);
        await ScheduleDigestsAsync(now, digestHourLocal, ct);
        await _db.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return createdCount;
    }

    public async Task ScheduleIncubationReadyAsync(DateTime now, CancellationToken ct = default)
    {
        var readyIncubations = await _db.Incubations
            .AsNoTracking()
            .Where(SampleWorkflowQueues.IsOpenIncubationOnActiveTest)
            .Where(SampleWorkflowQueues.IsIncubationReadyAt(now))
            .Include(i => i.TestOrder)
                .ThenInclude(t => t!.Sample)
            .ToListAsync(ct);

        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var incubation in readyIncubations)
        {
            var dedupeKey = $"{IncubationPrefix}{incubation.Id}";
            activeKeys.Add(dedupeKey);

            var recipients = new List<int>(2);
            if (incubation.TestOrder?.AssignedAnalystId is { } analystId && analystId > 0)
            {
                recipients.Add(analystId);
            }

            if (incubation.StartedByUserId is { } starterId && starterId > 0)
            {
                recipients.Add(starterId);
            }

            if (recipients.Count == 0)
            {
                continue;
            }

            var testCode = incubation.TestOrder?.TestCode ?? "Test";
            var step = string.IsNullOrWhiteSpace(incubation.StepName) ? "incubation" : incubation.StepName;
            var subject = incubation.TestOrder?.Sample?.ReferenceNumber is { } reference
                ? $"sample {reference}"
                : $"test order #{incubation.TestOrderId}";

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "IncubationReady",
                UserIds: recipients,
                Message: $"{testCode} ({step}) for {subject} is ready.",
                TargetType: "TestOrder",
                TargetId: incubation.TestOrderId,
                SectionId: incubation.TestOrder?.SectionId,
                DedupeKey: dedupeKey,
                SampleId: incubation.TestOrder?.SampleId,
                TestOrderId: incubation.TestOrderId,
                Severity: "info",
                DueAtUtc: incubation.IncubationEndUtc ?? incubation.ExpectedReadingAt), ct);

            foreach (var recipientId in recipients.Distinct())
                _db.QueueSignal(LiveSignalKinds.MyWork, $"user:{recipientId}");

            if (incubation.TestOrder?.SectionId is int secId)
                _db.QueueSignal(LiveSignalKinds.Counters, $"section:{secId}");
        }

        await ResolveStaleKeysAsync(IncubationPrefix, activeKeys, ct);
    }

    public async Task ScheduleMediaExpiryAsync(DateTime now, DateTime horizon, CancellationToken ct = default)
    {
        var expiringMedia = await _db.Media
            .AsNoTracking()
            .Include(m => m.Material)
            .Where(m => m.Status == MediaStatus.Active && m.ExpiryDate <= horizon)
            .ToListAsync(ct);

        var sectionHeadsBySection = new Dictionary<int, IReadOnlyCollection<int>>();
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var media in expiringMedia)
        {
            var dedupeKey = $"{MediaExpiryPrefix}{media.Id}";
            activeKeys.Add(dedupeKey);

            var sectionId = media.Material?.SectionId;
            var recipients = await CollectPreparerAndSectionHeadsAsync(
                media.PreparedByUserId,
                sectionId,
                sectionHeadsBySection,
                ct);

            if (recipients.Count == 0)
            {
                continue;
            }

            var expired = media.ExpiryDate <= now;
            var message = $"{media.Material?.MaterialName} (Lot {media.LotNumber}) {(expired ? "has expired" : $"expires {media.ExpiryDate:dd-MMM-yyyy}")}.";

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "MediaExpiry",
                UserIds: recipients,
                Message: message,
                TargetType: "Media",
                TargetId: media.Id,
                SectionId: sectionId,
                DedupeKey: dedupeKey,
                Severity: expired ? "error" : "warning",
                DueAtUtc: media.ExpiryDate), ct);
        }

        await ResolveStaleKeysAsync(MediaExpiryPrefix, activeKeys, ct);
    }

    public async Task ScheduleSolutionExpiryAsync(DateTime now, DateTime horizon, CancellationToken ct = default)
    {
        var expiringSolutions = await _db.SolutionPreparations
            .AsNoTracking()
            .Include(s => s.SolutionMaster)
            .Where(s => s.Status == SolutionPreparationStatus.Prepared
                && s.ExpiresAt != null
                && s.ExpiresAt <= horizon)
            .ToListAsync(ct);

        var sectionHeadsBySection = new Dictionary<int, IReadOnlyCollection<int>>();
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var solution in expiringSolutions)
        {
            var dedupeKey = $"{SolutionExpiryPrefix}{solution.Id}";
            activeKeys.Add(dedupeKey);

            var preparerId = solution.PreparedByUserId ?? solution.StartedByUserId;
            var recipients = await CollectPreparerAndSectionHeadsAsync(
                preparerId,
                solution.SectionId,
                sectionHeadsBySection,
                ct);

            if (recipients.Count == 0)
            {
                continue;
            }

            var expiresAt = solution.ExpiresAt!.Value;
            var expired = expiresAt <= now;
            var name = solution.SolutionMaster?.Name;
            var code = string.IsNullOrWhiteSpace(solution.Code) ? $"#{solution.Id}" : solution.Code;
            var label = string.IsNullOrWhiteSpace(name) ? $"Solution {code}" : $"{name} ({code})";
            var message = $"{label} {(expired ? "has expired" : $"expires {expiresAt:dd-MMM-yyyy}")}.";

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "SolutionExpiry",
                UserIds: recipients,
                Message: message,
                TargetType: "SolutionPreparation",
                TargetId: solution.Id,
                SectionId: solution.SectionId,
                DedupeKey: dedupeKey,
                Severity: expired ? "error" : "warning",
                DueAtUtc: expiresAt), ct);
        }

        await ResolveStaleKeysAsync(SolutionExpiryPrefix, activeKeys, ct);
    }

    public async Task ScheduleStrainExpiryAsync(DateTime now, DateTime horizon, CancellationToken ct = default)
    {
        var expiringStrains = await _db.Cryovials
            .AsNoTracking()
            .Include(c => c.Material)
            .Where(c => !c.IsDestroyed && c.VialsRemaining > 0 && c.ExpiryDate <= horizon)
            .ToListAsync(ct);

        var sectionHeadsBySection = new Dictionary<int, IReadOnlyCollection<int>>();
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cryovial in expiringStrains)
        {
            var dedupeKey = $"{StrainExpiryPrefix}{cryovial.Id}";
            activeKeys.Add(dedupeKey);

            var sectionId = cryovial.Material?.SectionId;
            var recipients = await CollectPreparerAndSectionHeadsAsync(
                cryovial.PreparedByUserId,
                sectionId,
                sectionHeadsBySection,
                ct);

            if (recipients.Count == 0)
            {
                continue;
            }

            var expired = cryovial.ExpiryDate <= now;
            var message = $"{cryovial.OrganismNameSnapshot} (Cryovial {cryovial.Code}) {(expired ? "has expired" : $"expires {cryovial.ExpiryDate:dd-MMM-yyyy}")}.";

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "StrainExpiry",
                UserIds: recipients,
                Message: message,
                TargetType: "Cryovial",
                TargetId: cryovial.Id,
                SectionId: sectionId,
                DedupeKey: dedupeKey,
                Severity: expired ? "error" : "warning",
                DueAtUtc: cryovial.ExpiryDate), ct);
        }

        await ResolveStaleKeysAsync(StrainExpiryPrefix, activeKeys, ct);
    }

    public async Task ScheduleCalibrationDueAsync(DateTime now, DateTime horizon, CancellationToken ct = default)
    {
        var dueEquipment = await _db.EquipmentInventories
            .AsNoTracking()
            .Where(e => e.Status == EquipmentOperationalStatus.InService
                && e.CalibrationDueDate != null
                && e.CalibrationDueDate <= horizon)
            .ToListAsync(ct);

        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyCollection<int>? recipients = null;

        foreach (var equipment in dueEquipment)
        {
            var dedupeKey = $"{CalibrationDuePrefix}{equipment.Id}";
            activeKeys.Add(dedupeKey);

            recipients ??= await _recipients.PermissionHoldersAsync(PermissionConstants.EquipmentWorkRecord, ct);
            if (recipients.Count == 0)
            {
                continue;
            }

            var dueAt = equipment.CalibrationDueDate!.Value;
            var expired = dueAt <= now;
            var message = $"Calibration for {equipment.InstrumentType} ({equipment.Code}) {(expired ? "is overdue" : $"is due {dueAt:dd-MMM-yyyy}")}.";

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "CalibrationDue",
                UserIds: recipients,
                Message: message,
                TargetType: "EquipmentInventory",
                TargetId: equipment.Id,
                SectionId: equipment.SectionId,
                DedupeKey: dedupeKey,
                Severity: expired ? "error" : "warning",
                DueAtUtc: dueAt), ct);
        }

        await ResolveStaleKeysAsync(CalibrationDuePrefix, activeKeys, ct);
    }

    public async Task ScheduleItemOverdueAsync(DateTime now, CancellationToken ct = default)
    {
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        var cutoff = now - SampleWorkflowQueues.OverdueAfter;

        // 1. Assigned in-testing / pending tests past the 24h SLA since sample receipt.
        var overdueAssignedTests = await _db.TestOrders
            .AsNoTracking()
            .Include(t => t.Sample)
            .Where(t => !t.IsSuperseded
                && t.AssignedAnalystId != null
                && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                && t.Sample != null
                && !SampleWorkflowQueues.ClosedSampleStatuses.Contains(t.Sample.Status)
                && t.Sample.Status != SampleStatus.UnderReview
                && t.Sample.Status != SampleStatus.UnderApproval
                && t.Sample.ReceivedAt < cutoff)
            .ToListAsync(ct);

        foreach (var test in overdueAssignedTests)
        {
            var dedupeKey = $"{ItemOverduePrefix}test:{test.Id}";
            activeKeys.Add(dedupeKey);

            var sample = test.Sample!;
            var dueAt = SampleWorkflowQueues.DueAt(sample.Status, sample.ReceivedAt);

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "ItemOverdue",
                UserIds: new[] { test.AssignedAnalystId!.Value },
                Message: $"Test {test.TestCode} for sample {sample.ReferenceNumber} is overdue.",
                TargetType: "TestOrder",
                TargetId: test.Id,
                SectionId: test.SectionId,
                DedupeKey: dedupeKey,
                SampleId: test.SampleId,
                TestOrderId: test.Id,
                Severity: "warning",
                DueAtUtc: dueAt), ct);
        }

        // 2. UnderReview sections past 24h since review clock start (per section, SoD-filtered reviewers).
        var reviewSamples = await SectionReviewQueues.Candidates(
                _db.Samples.AsNoTracking(),
                SectionSignoffStatus.UnderReview,
                sectionIds: null)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .ToListAsync(ct);

        var reviewEntries = SectionReviewQueues.Entries(reviewSamples, SectionSignoffStatus.UnderReview, sectionIds: null);
        if (reviewEntries.Count > 0)
        {
            var reviewClocks = await SampleWorkflowQueues.GetReviewClockStartsAsync(
                _db,
                reviewEntries.Select(e => e.Sample.Id).Distinct().ToList());

            foreach (var entry in reviewEntries)
            {
                var start = entry.Signoff?.SubmittedForReviewAt
                    ?? reviewClocks.GetValueOrDefault(entry.Sample.Id, entry.Sample.ReceivedAt);

                if (start > cutoff)
                {
                    continue;
                }

                var dedupeKey = $"{ItemOverduePrefix}review:{entry.Sample.Id}:section:{entry.SectionId}";
                activeKeys.Add(dedupeKey);

                var orderIds = entry.Orders.Select(o => o.Id).ToList();
                var reviewers = await _recipients.ReviewersAsync(entry.SectionId, orderIds, ct);
                if (reviewers.Count == 0)
                {
                    continue;
                }

                await _publisher.PublishAsync(new NotificationRequest(
                    Type: "ItemOverdue",
                    UserIds: reviewers,
                    Message: $"Sample {entry.Sample.ReferenceNumber} review is overdue.",
                    TargetType: "Sample",
                    TargetId: entry.Sample.Id,
                    SectionId: entry.SectionId,
                    DedupeKey: dedupeKey,
                    SampleId: entry.Sample.Id,
                    Severity: "warning",
                    DueAtUtc: start + SampleWorkflowQueues.OverdueAfter), ct);
            }
        }

        // 3. UnderApproval sections past 24h since approval clock start (per section, SoD-filtered approvers).
        var approvalSamples = await SectionReviewQueues.Candidates(
                _db.Samples.AsNoTracking(),
                SectionSignoffStatus.UnderApproval,
                sectionIds: null)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .ToListAsync(ct);

        var approvalEntries = SectionReviewQueues.Entries(approvalSamples, SectionSignoffStatus.UnderApproval, sectionIds: null);
        foreach (var entry in approvalEntries)
        {
            var start = entry.Signoff?.ReviewedAt ?? SampleWorkflowQueues.GetApprovalClockStart(entry.Sample);
            if (start > cutoff)
            {
                continue;
            }

            var dedupeKey = $"{ItemOverduePrefix}approval:{entry.Sample.Id}:section:{entry.SectionId}";
            activeKeys.Add(dedupeKey);

            var orderIds = entry.Orders.Select(o => o.Id).ToList();
            var approvers = await _recipients.ApproversAsync(entry.SectionId, orderIds, ct);
            if (approvers.Count == 0)
            {
                continue;
            }

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "ItemOverdue",
                UserIds: approvers,
                Message: $"Sample {entry.Sample.ReferenceNumber} approval is overdue.",
                TargetType: "Sample",
                TargetId: entry.Sample.Id,
                SectionId: entry.SectionId,
                DedupeKey: dedupeKey,
                SampleId: entry.Sample.Id,
                Severity: "warning",
                DueAtUtc: start + SampleWorkflowQueues.OverdueAfter), ct);
        }

        await ResolveStaleKeysAsync(ItemOverduePrefix, activeKeys, ct);
    }

    public async Task SchedulePendingConfigurationApprovalsAsync(CancellationToken ct = default)
    {
        var pendingConfigs = await _db.ItemPreparationConfigurations
            .AsNoTracking()
            .Where(c => c.ApprovalStatus == ApprovalGateStatus.PendingReview)
            .Select(c => new { c.Id })
            .ToListAsync(ct);

        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyCollection<int>? recipients = null;

        foreach (var config in pendingConfigs)
        {
            var dedupeKey = $"{PendingConfigPrefix}{config.Id}";
            activeKeys.Add(dedupeKey);

            recipients ??= await _recipients.SectionHeadOrPermissionHoldersAsync(PermissionConstants.DashboardsLabOverview, ct);
            if (recipients.Count == 0)
            {
                continue;
            }

            await _publisher.PublishAsync(new NotificationRequest(
                Type: "PendingPreparationConfigApproval",
                UserIds: recipients,
                Message: $"Preparation configuration #{config.Id} is awaiting approval.",
                TargetType: "PreparationConfig",
                TargetId: config.Id,
                SectionId: null,
                DedupeKey: dedupeKey,
                Severity: "info"), ct);
        }

        await ResolveStaleKeysAsync(PendingConfigPrefix, activeKeys, ct);
    }

    public async Task ScheduleMonitoringRemindersAsync(CancellationToken ct = default)
    {
        if (_monitoring is null)
        {
            return;
        }

        var monitoringUsers = await _recipients.PermissionHoldersAsync(PermissionConstants.MonitoringPlanView, ct);
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var userId in monitoringUsers)
        {
            var reminders = await _monitoring.GetForUserAsync(userId);
            foreach (var reminder in reminders)
            {
                if (!NotificationCatalog.Types.ContainsKey(reminder.Type))
                {
                    continue;
                }

                var dedupeKey = $"{MonitoringPrefix}{reminder.Type}:user:{userId}";
                activeKeys.Add(dedupeKey);

                await _publisher.PublishAsync(new NotificationRequest(
                    Type: reminder.Type,
                    UserIds: new[] { userId },
                    Message: reminder.Message,
                    TargetType: "MonitoringPlan",
                    TargetId: null,
                    SectionId: null,
                    DedupeKey: dedupeKey,
                    Severity: reminder.Severity), ct);
            }
        }

        await ResolveStaleKeysAsync(MonitoringPrefix, activeKeys, ct);
    }

    public async Task PurgeExpiredNotificationsAsync(DateTime now, int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0)
        {
            return;
        }

        var cutoff = now.AddDays(-retentionDays);
        if (_db is DbContext efDb && efDb.Database.IsRelational())
        {
            await _db.NotificationLogs
                .Where(n => n.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            return;
        }

        var expired = await _db.NotificationLogs
            .Where(n => n.CreatedAt < cutoff)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            _db.NotificationLogs.RemoveRange(expired);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ScheduleDigestsAsync(DateTime now, int digestHourLocal, CancellationToken ct = default)
    {
        var localNow = _clock.ToLabLocal(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        if (localNow.Hour < digestHourLocal)
        {
            return;
        }

        var labDate = DateOnly.FromDateTime(localNow);
        var localDayStart = labDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localDayEnd = labDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localDayStart, _clock.LabTimeZone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localDayEnd, _clock.LabTimeZone);

        var digestPrefs = await _db.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.EmailMode == EmailMode.Digest)
            .Select(p => new { p.UserId, p.Type })
            .ToListAsync(ct);

        if (digestPrefs.Count == 0)
        {
            return;
        }

        var digestPrefPairs = digestPrefs
            .Where(p => NotificationCatalog.TryGet(p.Type)?.EmailAllowed == true)
            .Select(p => (p.UserId, p.Type))
            .ToHashSet();

        if (digestPrefPairs.Count == 0)
        {
            return;
        }

        var candidateUserIds = digestPrefPairs.Select(p => p.UserId).Distinct().ToList();

        var alreadySentUserIds = await _db.EmailOutbox
            .AsNoTracking()
            .Where(e => e.Kind == EmailOutboxKind.Digest
                && e.UserId != null
                && candidateUserIds.Contains(e.UserId.Value)
                && e.CreatedAt >= dayStartUtc
                && e.CreatedAt < dayEndUtc)
            .Select(e => e.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var skipUsers = alreadySentUserIds.ToHashSet();
        var eligibleUsers = candidateUserIds.Where(id => !skipUsers.Contains(id)).ToList();
        if (eligibleUsers.Count == 0)
        {
            return;
        }

        var emailsByUser = await _db.Users
            .AsNoTracking()
            .Where(u => eligibleUsers.Contains(u.Id) && u.IsActive && u.Email != null && u.Email != "")
            .ToDictionaryAsync(u => u.Id, u => u.Email!.Trim(), ct);

        if (emailsByUser.Count == 0)
        {
            return;
        }

        var unreadLogs = await _db.NotificationLogs
            .AsNoTracking()
            .Where(n => emailsByUser.Keys.Contains(n.UserId) && !n.IsRead && n.ResolvedAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        foreach (var group in unreadLogs.GroupBy(n => n.UserId))
        {
            var items = group.Where(n => digestPrefPairs.Contains((n.UserId, n.Type))).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            if (!emailsByUser.TryGetValue(group.Key, out var address) || string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            var (subject, body) = NotificationEmailComposer.ComposeDigest(items, _appBaseUrl, _clock.LabTimeZone.Id, labDate);

            _db.EmailOutbox.Add(new Domain.Entities.EmailOutbox
            {
                NotificationLogId = null,
                UserId = group.Key,
                ToAddress = address,
                Subject = subject,
                Body = body,
                Kind = EmailOutboxKind.Digest,
                CreatedAt = now,
                NextAttemptAt = now,
                Attempts = 0
            });
        }
    }

    private async Task<List<int>> CollectPreparerAndSectionHeadsAsync(
        int? preparerId,
        int? sectionId,
        Dictionary<int, IReadOnlyCollection<int>> sectionHeadCache,
        CancellationToken ct)
    {
        var recipients = new List<int>();
        if (preparerId is > 0)
        {
            recipients.Add(preparerId.Value);
        }

        if (sectionId is > 0)
        {
            if (!sectionHeadCache.TryGetValue(sectionId.Value, out var heads))
            {
                heads = await _recipients.RoleInSectionAsync(RoleType.SectionHead, sectionId.Value, ct);
                sectionHeadCache[sectionId.Value] = heads;
            }

            recipients.AddRange(heads);
        }

        return recipients.Distinct().ToList();
    }

    private async Task ResolveStaleKeysAsync(
        string dedupePrefix,
        HashSet<string> activeKeys,
        CancellationToken ct)
    {
        var openKeys = await _db.NotificationLogs
            .AsNoTracking()
            .Where(n => n.ResolvedAt == null && n.DedupeKey != null && n.DedupeKey.StartsWith(dedupePrefix))
            .Select(n => n.DedupeKey!)
            .Distinct()
            .ToListAsync(ct);

        foreach (var key in openKeys)
        {
            if (!activeKeys.Contains(key))
            {
                await _publisher.ResolveAsync(key, ct);
            }
        }
    }
}
