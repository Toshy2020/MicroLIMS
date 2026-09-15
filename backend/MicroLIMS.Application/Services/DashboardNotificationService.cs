using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// SampleId/TestOrderId are set when a notification is about one test on one
// sample, so the client can open that sample and test directly. They are null
// for lab-wide or non-sample notifications, which route by Type instead.
public record NotificationDto(int? Id, string Type, string Message, DateTime Timestamp, string Severity, bool IsRead, int? SampleId = null, int? TestOrderId = null);

// Computed live from current state (media expiry, incubation ready,
// approval waiting, review waiting), then persisted to NotificationLog
// so read/unread survives across sessions, and pushed via
// INotificationService (in-process pub/sub) + emailed for critical ones.
public class DashboardNotificationService
{
    private static readonly TimeSpan ExpiryWarningWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(12);

    private readonly MicroLimsDbContext _db;
    private readonly INotificationService _pushService;
    private readonly IEmailSender _emailSender;
    private readonly NotificationRecomputeThrottle? _recomputeThrottle;

    public DashboardNotificationService(MicroLimsDbContext db, INotificationService pushService, IEmailSender emailSender, NotificationRecomputeThrottle? recomputeThrottle = null)
    {
        _db = db;
        _pushService = pushService;
        _emailSender = emailSender;
        _recomputeThrottle = recomputeThrottle;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(RoleType role, int userId)
    {
        // Without a throttle every call recomputes. With one, polls inside the
        // window return the persisted list only, so a new notification can
        // appear up to one window late.
        var now = DateTime.UtcNow;
        if (_recomputeThrottle is null || _recomputeThrottle.IsDue(userId, now))
        {
            var computed = await ComputeAsync(role, userId);
            await PersistAndDeliverAsync(userId, computed);
            _recomputeThrottle?.MarkComputed(userId, now);
        }

        var persisted = await _db.NotificationLogs
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .ToListAsync();

        return persisted.Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.CreatedAt, n.Severity, n.IsRead, n.SampleId, n.TestOrderId)).ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var log = await _db.NotificationLogs.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (log is null) return;
        log.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await _db.NotificationLogs
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    private sealed record ComputedNotification(string Type, string Message, string Severity, int? SampleId = null, int? TestOrderId = null);

    private async Task<List<ComputedNotification>> ComputeAsync(RoleType role, int userId)
    {
        var results = new List<ComputedNotification>();
        var now = DateTime.UtcNow;

        var expiringMedia = await _db.Media
            .Include(m => m.Material)
            .Where(m => m.Status == MediaStatus.Active && m.ExpiryDate <= now.Add(ExpiryWarningWindow))
            .ToListAsync();
        foreach (var m in expiringMedia)
        {
            var expired = m.ExpiryDate <= now;
            results.Add(new ComputedNotification("MediaExpiry", $"{m.Material?.MaterialName} (Lot {m.LotNumber}) {(expired ? "has expired" : $"expires {m.ExpiryDate:dd-MMM-yyyy}")}.", expired ? "error" : "warning"));
        }

        var readyIncubations = await _db.Incubations
            .Where(i => i.CompletedAt != null)
            .Include(i => i.TestOrder)
                .ThenInclude(t => t!.Sample)
            // Active tests only - a voided, rejected or superseded test can still
            // carry an Incubating step, but nobody should read its plates.
            .Where(i => (i.TestOrder!.CurrentStep == WorkflowStep.Incubating || i.TestOrder.CurrentStep == WorkflowStep.Running)
                && !i.TestOrder.IsSuperseded
                && (i.TestOrder.Status == ApprovalStatus.Pending || i.TestOrder.Status == ApprovalStatus.InProgress))
            .OrderByDescending(i => i.CompletedAt)
            .Take(20)
            .ToListAsync();
        foreach (var i in readyIncubations)
        {
            // Named by test and sample - a bare TestOrder id gives the
            // analyst nothing to find the plates by.
            var testCode = i.TestOrder?.TestCode ?? "Test";
            var step = string.IsNullOrWhiteSpace(i.StepName) ? "incubation" : i.StepName;
            var subject = i.TestOrder?.Sample?.ReferenceNumber is { } reference
                ? $"sample {reference}"
                : $"test order #{i.TestOrderId}";
            results.Add(new ComputedNotification("IncubationReady", $"{testCode} ({step}) for {subject} is ready.", "info", i.TestOrder?.SampleId, i.TestOrderId));
        }

        if (role is RoleType.SectionHead or RoleType.SystemAdministrator)
        {
            var approvalCount = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderApproval);
            if (approvalCount > 0)
                results.Add(new ComputedNotification("ApprovalWaiting", $"{approvalCount} sample(s) awaiting approval.", "info"));

            // Auto-seeded from an analyst's first manual entry - already in
            // use, so this is a review-after-the-fact prompt, not a blocker.
            var pendingConfigCount = await _db.ItemPreparationConfigurations
                .CountAsync(c => c.ApprovalStatus == ApprovalGateStatus.PendingReview);
            if (pendingConfigCount > 0)
                results.Add(new ComputedNotification("PendingPreparationConfigApproval", $"{pendingConfigCount} preparation configuration(s) awaiting approval.", "info"));
        }

        if (role is RoleType.Reviewer or RoleType.SectionHead or RoleType.SystemAdministrator)
        {
            var reviewCount = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderReview);
            if (reviewCount > 0)
                results.Add(new ComputedNotification("ReviewWaiting", $"{reviewCount} sample(s) awaiting review.", "info"));
        }

        var returnedTests = await _db.TestReturnEvents
            .Where(e => e.AssignedAnalystId == userId && e.ReturnedAt >= now.Subtract(DedupeWindow))
            .Include(e => e.TestOrder)
                .ThenInclude(t => t!.Sample)
            .OrderByDescending(e => e.ReturnedAt)
            .ToListAsync();

        foreach (var r in returnedTests)
        {
            var testCode = r.TestOrder?.TestCode ?? "Test";
            var sampleRef = r.TestOrder?.Sample?.ReferenceNumber ?? $"#{r.TestOrderId}";
            var message = string.IsNullOrWhiteSpace(r.Reason)
                ? $"Test {testCode} for sample {sampleRef} was returned for revision."
                : $"Test {testCode} for sample {sampleRef} was returned for revision: {r.Reason.Trim()}";

            results.Add(new ComputedNotification("TestReturnedForRevision", message, "warning", r.TestOrder?.SampleId, r.TestOrderId));
        }

        return results;
    }

    private async Task PersistAndDeliverAsync(int userId, List<ComputedNotification> computed)
    {
        var cutoff = DateTime.UtcNow.Subtract(DedupeWindow);
        var recent = await _db.NotificationLogs
            .Where(n => n.UserId == userId && n.CreatedAt >= cutoff)
            .Select(n => n.Message)
            .ToListAsync();

        foreach (var notification in computed)
        {
            if (recent.Contains(notification.Message)) continue; // don't spam duplicate notifications within the dedupe window

            var log = new NotificationLog
            {
                UserId = userId,
                Type = notification.Type,
                Message = notification.Message,
                Severity = notification.Severity,
                SampleId = notification.SampleId,
                TestOrderId = notification.TestOrderId
            };
            _db.NotificationLogs.Add(log);

            await _pushService.NotifyAsync(userId, notification.Message);

            if (notification.Severity == "error")
            {
                log.EmailSent = true;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user is not null)
                    await _emailSender.SendAsync($"{user.Username}@microlims.local", $"MicroLIMS Alert: {notification.Type}", notification.Message);
            }
        }

        await _db.SaveChangesAsync();
    }
}
