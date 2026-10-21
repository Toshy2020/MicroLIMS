using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Monitoring;

public record MonitoringReminder(string Type, string Message, string Severity);

// What the plan wants a person to know today (spec 14): occurrences that are
// overdue, and from the warning day of the month the ones still not done.
// Read live on every dashboard refresh, so a reminder goes away on its own
// when the work is done or the occurrence is skipped.
public class MonitoringReminderService
{
    private readonly IMicroLimsDbContext _db;
    private readonly MonitoringOccurrenceService _occurrences;

    public MonitoringReminderService(IMicroLimsDbContext db, MonitoringOccurrenceService occurrences)
    {
        _db = db;
        _occurrences = occurrences;
    }

    public async Task<List<MonitoringReminder>> GetForUserAsync(int userId)
    {
        var reminders = new List<MonitoringReminder>();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive) return reminders;
        var canView = await _db.RolePermissions.AsNoTracking()
            .AnyAsync(rp => rp.RoleId == user.RoleId && rp.Permission!.Code == PermissionConstants.MonitoringPlanView);
        if (!canView) return reminders;

        var due = await _occurrences.GetDueAsync(null);
        var overdue = due.Items.Count(i => i.Status is OccurrenceStatus.Overdue or OccurrenceStatus.Missed);
        if (overdue > 0)
            reminders.Add(new MonitoringReminder("MonitoringOverdue", $"{overdue} monitoring plan occurrence(s) are overdue.", "warning"));

        var stillDue = due.Items.Count - overdue;
        if (due.ShowWarning && stillDue > 0)
            reminders.Add(new MonitoringReminder("MonitoringDueThisMonth",
                $"{stillDue} monitoring plan occurrence(s) are still to be done this month.", "warning"));
        return reminders;
    }
}
