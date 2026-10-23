using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;
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
        // Missed occurrences are tracked as deviations below; only those still inside their grace period are "overdue".
        var late = due.Items.Count(i => i.Status is OccurrenceStatus.Overdue or OccurrenceStatus.Missed);
        var overdue = due.Items.Count(i => i.Status == OccurrenceStatus.Overdue);
        if (overdue > 0)
            reminders.Add(new MonitoringReminder("MonitoringOverdue", $"{overdue} monitoring plan occurrence(s) are overdue.", "warning"));

        var openDeviations = await _db.MonitoringDeviations.AsNoTracking().CountAsync(d => d.Status == DeviationStatus.Open);
        if (openDeviations > 0)
        {
            var pastDue = await _db.MonitoringDeviations.AsNoTracking()
                .CountAsync(d => d.Status == DeviationStatus.Open && d.DueDate < due.Today);
            reminders.Add(new MonitoringReminder("MonitoringDeviationsOpen",
                $"{openDeviations} monitoring deviation(s) are open{(pastDue > 0 ? $", {pastDue} past their due date" : string.Empty)}.", "warning"));
        }

        var stillDue = due.Items.Count - late;
        if (due.ShowWarning && stillDue > 0)
            reminders.Add(new MonitoringReminder("MonitoringDueThisMonth",
                $"{stillDue} monitoring plan occurrence(s) are still to be done this month.", "warning"));

        var overdueCal = await _db.EquipmentTasks.AsNoTracking()
            .CountAsync(t => t.TaskType == EquipmentTaskType.Calibration
                          && t.CompletedOn == null
                          && t.RetiredByRevisionId == null
                          && t.DueDate < due.Today);
        if (overdueCal > 0)
            reminders.Add(new MonitoringReminder("EquipmentCalibrationOverdue",
                $"{overdueCal} equipment calibration task(s) are overdue.", "warning"));

        var overduePm = await _db.EquipmentTasks.AsNoTracking()
            .CountAsync(t => t.TaskType == EquipmentTaskType.PreventiveMaintenance
                          && t.CompletedOn == null
                          && t.RetiredByRevisionId == null
                          && t.DueDate < due.Today);
        if (overduePm > 0)
            reminders.Add(new MonitoringReminder("EquipmentPmOverdue",
                $"{overduePm} equipment preventive maintenance task(s) are overdue.", "warning"));

        return reminders;
    }
}
