using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record NotificationDto(int? Id, string Type, string Message, DateTime Timestamp, string Severity, bool IsRead);

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

    public DashboardNotificationService(MicroLimsDbContext db, INotificationService pushService, IEmailSender emailSender)
    {
        _db = db;
        _pushService = pushService;
        _emailSender = emailSender;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(RoleType role, int userId)
    {
        var computed = await ComputeAsync(role, userId);
        await PersistAndDeliverAsync(userId, computed);

        var persisted = await _db.NotificationLogs
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .ToListAsync();

        return persisted.Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.CreatedAt, n.Severity, n.IsRead)).ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var log = await _db.NotificationLogs.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (log is null) return;
        log.IsRead = true;
        await _db.SaveChangesAsync();
    }

    private async Task<List<(string Type, string Message, string Severity)>> ComputeAsync(RoleType role, int userId)
    {
        var results = new List<(string, string, string)>();
        var now = DateTime.UtcNow;

        var expiringMedia = await _db.Media
            .Include(m => m.MediaType)
            .Where(m => m.Status == MediaStatus.Active && m.ExpiryDate <= now.Add(ExpiryWarningWindow))
            .ToListAsync();
        foreach (var m in expiringMedia)
        {
            var expired = m.ExpiryDate <= now;
            results.Add(("MediaExpiry", $"{m.MediaType?.Name} (Lot {m.LotNumber}) {(expired ? "has expired" : $"expires {m.ExpiryDate:dd-MMM-yyyy}")}.", expired ? "error" : "warning"));
        }

        var readyIncubations = await _db.Incubations
            .Where(i => i.CompletedAt != null)
            .Include(i => i.TestOrder)
            .Where(i => i.TestOrder!.CurrentStep == WorkflowStep.Incubating || i.TestOrder.CurrentStep == WorkflowStep.Running)
            .OrderByDescending(i => i.CompletedAt)
            .Take(20)
            .ToListAsync();
        foreach (var i in readyIncubations)
            results.Add(("IncubationReady", $"{i.StepName} for TestOrder #{i.TestOrderId} is ready.", "info"));

        if (role is RoleType.SectionHead or RoleType.SystemAdministrator)
        {
            var approvalCount = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Reviewed);
            if (approvalCount > 0)
                results.Add(("ApprovalWaiting", $"{approvalCount} test order(s) awaiting approval.", "info"));
        }

        if (role is RoleType.Reviewer or RoleType.SectionHead or RoleType.SystemAdministrator)
        {
            var reviewCount = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.ResultEntered);
            if (reviewCount > 0)
                results.Add(("ReviewWaiting", $"{reviewCount} test order(s) awaiting review.", "info"));
        }

        return results;
    }

    private async Task PersistAndDeliverAsync(int userId, List<(string Type, string Message, string Severity)> computed)
    {
        var cutoff = DateTime.UtcNow.Subtract(DedupeWindow);
        var recent = await _db.NotificationLogs
            .Where(n => n.UserId == userId && n.CreatedAt >= cutoff)
            .Select(n => n.Message)
            .ToListAsync();

        foreach (var (type, message, severity) in computed)
        {
            if (recent.Contains(message)) continue; // don't spam duplicate notifications within the dedupe window

            var log = new NotificationLog { UserId = userId, Type = type, Message = message, Severity = severity };
            _db.NotificationLogs.Add(log);

            await _pushService.NotifyAsync(userId, message);

            if (severity == "error")
            {
                log.EmailSent = true;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user is not null)
                    await _emailSender.SendAsync($"{user.Username}@microlims.local", $"MicroLIMS Alert: {type}", message);
            }
        }

        await _db.SaveChangesAsync();
    }
}
