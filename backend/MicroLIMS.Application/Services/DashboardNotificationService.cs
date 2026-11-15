using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// SampleId/TestOrderId are set when a notification is about one test on one
// sample, so the client can open that sample and test directly. They are null
// for lab-wide or non-sample notifications, which route by Type instead.
public record NotificationDto(int? Id, string Type, string Message, DateTime Timestamp, string Severity, bool IsRead, int? SampleId = null, int? TestOrderId = null);

// Reads persisted NotificationLogs for the header bell widget and marks them read.
// Scheduled notifications are generated in the background by NotificationScheduler.
public class DashboardNotificationService
{
    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public DashboardNotificationService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _time = timeProvider ?? TimeProvider.System;
    }

    // Notification types that are Microbiology activity by nature, whatever
    // record they point at: media expiry, incubation readiness and sample
    // preparation configuration (Physicochemical tests have no preparation
    // stage - PreparationRules.NoPreparationSectionCode).
    private static readonly HashSet<string> MicrobiologyOnlyTypes = new(StringComparer.Ordinal)
    {
        "MediaExpiry", "IncubationReady", "PendingPreparationConfigApproval", "TestReturnedForBiochemical"
    };

    // `lab` (a laboratory dashboard) keeps only that laboratory's
    // notifications: by the test order's section, else by whether the sample
    // has tests in the laboratory, else by type. Notifications about no
    // laboratory record (messages, discussions) stay on both.
    public async Task<List<NotificationDto>> GetNotificationsAsync(RoleType role, int userId, DashboardLabScope? lab = null)
    {
        var all = await GetNotificationsAsync(role, userId);
        if (lab?.LabCode == null || lab.SectionIds == null) return all;
        var sections = lab.SectionIds;

        var orderIds = all.Where(n => n.TestOrderId != null).Select(n => n.TestOrderId!.Value).Distinct().ToList();
        var orderSection = await _db.TestOrders.AsNoTracking()
            .Where(t => orderIds.Contains(t.Id))
            .Select(t => new { t.Id, t.SectionId })
            .ToDictionaryAsync(t => t.Id, t => t.SectionId);

        var sampleIds = all.Where(n => n.TestOrderId == null && n.SampleId != null).Select(n => n.SampleId!.Value).Distinct().ToList();
        var samplesInLab = (await _db.TestOrders.AsNoTracking()
            .Where(t => sampleIds.Contains(t.SampleId) && sections.Contains(t.SectionId))
            .Select(t => t.SampleId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        return all.Where(n =>
        {
            if (n.TestOrderId is { } orderId)
                return orderSection.TryGetValue(orderId, out var sectionId) && sections.Contains(sectionId);
            if (MicrobiologyOnlyTypes.Contains(n.Type))
                return lab.IsMicrobiology;
            if (n.SampleId is { } sampleId)
                return samplesInLab.Contains(sampleId);
            return true;
        }).ToList();
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(RoleType role, int userId)
    {
        var persisted = await _db.NotificationLogs
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .ToListAsync();

        return persisted
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.CreatedAt, n.Severity, n.IsRead, n.SampleId, n.TestOrderId))
            .ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var log = await _db.NotificationLogs.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (log is null) return;
        log.IsRead = true;
        log.ReadAt ??= _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await _db.NotificationLogs
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now));
    }
}
