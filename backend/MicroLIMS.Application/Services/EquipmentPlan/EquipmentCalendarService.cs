using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public enum EquipmentTaskStatus
{
    Upcoming,
    Due,
    Overdue,
    Done,
    Retired
}

public record EquipmentTaskItem(
    int Id,
    int EquipmentId,
    string EquipmentCode,
    string InstrumentType,
    EquipmentTaskType Type,
    DateOnly OriginalDueDate,
    DateOnly DueDate,
    DateOnly? CompletedOn,
    EquipmentTaskStatus Status,
    string? EvidenceLabel);

public record EquipmentMonthSummary(
    int Month,
    int Planned,
    int Done,
    int Overdue,
    int Upcoming,
    int Events);

public record TaskEventLine(
    int TaskId,
    EquipmentTaskType TaskType,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime PerformedAt,
    int? PerformedByUserId,
    string? PerformedByName = null);

public record StatusLine(
    int Id,
    EquipmentOperationalStatus PreviousStatus,
    EquipmentOperationalStatus NewStatus,
    string Comment,
    DateTime ChangedAt,
    int ChangedByUserId,
    string? ChangedByName = null);

public record BaselineLine(
    DateOnly Date,
    string Label = "Baseline");

public record InstrumentHistoryResponse(
    int EquipmentId,
    string EquipmentCode,
    string InstrumentType,
    string ManufacturerName,
    string? SerialNumber,
    string Location,
    EquipmentOperationalStatus Status,
    string? CalibrationIntervalDescription,
    string? PmIntervalDescription,
    IReadOnlyList<EquipmentTaskItem> Tasks,
    IReadOnlyList<TaskEventLine> Reschedules,
    IReadOnlyList<StatusLine> StatusChanges,
    IReadOnlyList<MaintenanceLogEntryResponse> Logbook,
    BaselineLine? CalibrationBaseline,
    BaselineLine? PmBaseline,
    DateOnly? NextCalibrationDue,
    DateOnly? NextPmDue);

public record EquipmentCalendarResponse(
    IReadOnlyList<EquipmentTaskItem> Tasks,
    IReadOnlyList<CalendarEventResponse> Events);

public class EquipmentCalendarService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly CalendarEventService _calendarEvents;

    public EquipmentCalendarService(
        IMicroLimsDbContext db,
        ILabClock clock,
        CalendarEventService calendarEvents)
    {
        _db = db;
        _clock = clock;
        _calendarEvents = calendarEvents;
    }

    public static EquipmentTaskStatus DeriveStatus(EquipmentTask task, DateOnly today)
    {
        if (task.RetiredByRevisionId.HasValue)
            return EquipmentTaskStatus.Retired;

        if (task.CompletedOn.HasValue)
            return EquipmentTaskStatus.Done;

        if (task.DueDate < today)
            return EquipmentTaskStatus.Overdue;

        if (task.DueDate <= today.AddDays(7))
            return EquipmentTaskStatus.Due;

        return EquipmentTaskStatus.Upcoming;
    }

    public static string? GetEvidenceLabel(EquipmentTask task)
    {
        if (task.CalibrationDocument != null)
            return task.CalibrationDocument.OriginalFileName;

        if (task.MaintenanceLogEntryId.HasValue)
            return $"Logbook entry #{task.MaintenanceLogEntryId.Value}";

        return null;
    }

    public async Task<EquipmentCalendarResponse> GetCalendarAsync(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new BusinessRuleException("The end of the range is before its start.");

        var today = _clock.LabToday;

        var tasks = await _db.EquipmentTasks
            .Include(t => t.EquipmentInventory)
            .Include(t => t.CalibrationDocument)
            .Include(t => t.MaintenanceLogEntry)
            .Where(t => (t.DueDate >= from && t.DueDate <= to)
                     || (t.CompletedOn.HasValue && t.CompletedOn.Value >= from && t.CompletedOn.Value <= to))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.EquipmentInventory != null ? t.EquipmentInventory.Code : string.Empty)
            .ThenBy(t => t.Sequence)
            .ToListAsync();

        var taskItems = tasks.Select(t => new EquipmentTaskItem(
            t.Id,
            t.EquipmentInventoryId,
            t.EquipmentInventory?.Code ?? string.Empty,
            t.EquipmentInventory?.InstrumentType ?? string.Empty,
            t.TaskType,
            t.OriginalDueDate,
            t.DueDate,
            t.CompletedOn,
            DeriveStatus(t, today),
            GetEvidenceLabel(t)
        )).ToList();

        var allEvents = await _calendarEvents.GetCalendarEventsAsync(from, to);
        var activeEvents = allEvents.Where(e => e.Status == CalendarEventStatus.Active).ToList();

        return new EquipmentCalendarResponse(taskItems, activeEvents);
    }

    public async Task<List<EquipmentMonthSummary>> GetYearAsync(int year)
    {
        if (year is < 2000 or > 2100)
            throw new BusinessRuleException("Choose a plan year between 2000 and 2100.");

        var today = _clock.LabToday;
        var firstDay = new DateOnly(year, 1, 1);
        var lastDay = new DateOnly(year, 12, 31);

        var tasks = await _db.EquipmentTasks
            .Where(t => t.RetiredByRevisionId == null && t.DueDate >= firstDay && t.DueDate <= lastDay)
            .ToListAsync();

        var allEvents = await _calendarEvents.GetCalendarEventsAsync(firstDay, lastDay);
        var activeEvents = allEvents.Where(e => e.Status == CalendarEventStatus.Active).ToList();

        var planned = new int[12];
        var done = new int[12];
        var overdue = new int[12];
        var upcoming = new int[12];
        var eventCounts = new int[12];

        foreach (var t in tasks)
        {
            var m = t.DueDate.Month - 1;
            planned[m]++;
            if (t.CompletedOn.HasValue)
            {
                done[m]++;
            }
            else if (t.DueDate < today)
            {
                overdue[m]++;
            }
            else
            {
                upcoming[m]++;
            }
        }

        foreach (var ev in activeEvents)
        {
            if (ev.Date.HasValue && ev.Date.Value.Year == year)
            {
                var m = ev.Date.Value.Month - 1;
                eventCounts[m]++;
            }
            else if (!string.IsNullOrWhiteSpace(ev.Month) && ev.Month.Length >= 7)
            {
                if (int.TryParse(ev.Month[..4], out var evYear) && evYear == year &&
                    int.TryParse(ev.Month[5..7], out var evMonth) && evMonth is >= 1 and <= 12)
                {
                    eventCounts[evMonth - 1]++;
                }
            }
        }

        return Enumerable.Range(1, 12)
            .Select(m => new EquipmentMonthSummary(
                m,
                planned[m - 1],
                done[m - 1],
                overdue[m - 1],
                upcoming[m - 1],
                eventCounts[m - 1]))
            .ToList();
    }

    public async Task<InstrumentHistoryResponse> GetInstrumentHistoryAsync(int equipmentId, int year)
    {
        var eq = await _db.EquipmentInventories
            .FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");

        var today = _clock.LabToday;

        string? calIntervalDesc = eq.CalibrationIntervalValue.HasValue && eq.CalibrationIntervalUnit.HasValue
            ? EquipmentScheduleCalculator.Describe(eq.CalibrationIntervalValue.Value, eq.CalibrationIntervalUnit.Value)
            : null;

        string? pmIntervalDesc = eq.PmIntervalValue.HasValue && eq.PmIntervalUnit.HasValue
            ? EquipmentScheduleCalculator.Describe(eq.PmIntervalValue.Value, eq.PmIntervalUnit.Value)
            : null;

        var tasks = await _db.EquipmentTasks
            .Include(t => t.EquipmentInventory)
            .Include(t => t.CalibrationDocument)
            .Include(t => t.MaintenanceLogEntry)
            .Where(t => t.EquipmentInventoryId == equipmentId && t.Year == year)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Sequence)
            .ToListAsync();

        var taskItems = tasks.Select(t => new EquipmentTaskItem(
            t.Id,
            t.EquipmentInventoryId,
            eq.Code,
            eq.InstrumentType,
            t.TaskType,
            t.OriginalDueDate,
            t.DueDate,
            t.CompletedOn,
            DeriveStatus(t, today),
            GetEvidenceLabel(t)
        )).ToList();

        var taskIds = tasks.Select(t => t.Id).ToList();
        var taskTypeMap = tasks.ToDictionary(t => t.Id, t => t.TaskType);

        var reschedules = await _db.EquipmentTaskEvents
            .Where(e => taskIds.Contains(e.EquipmentTaskId) && e.EventType == EquipmentTaskEventType.Rescheduled)
            .OrderBy(e => e.PerformedAt)
            .ToListAsync();

        var rescheduleUserIds = reschedules.Where(e => e.PerformedByUserId.HasValue).Select(e => e.PerformedByUserId!.Value).Distinct().ToList();
        var rescheduleUsers = await _db.Users
            .Where(u => rescheduleUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var rescheduleLines = reschedules.Select(e => new TaskEventLine(
            e.EquipmentTaskId,
            taskTypeMap[e.EquipmentTaskId],
            e.OldValue,
            e.NewValue,
            e.Reason,
            e.PerformedAt,
            e.PerformedByUserId,
            e.PerformedByUserId.HasValue ? rescheduleUsers.GetValueOrDefault(e.PerformedByUserId.Value) : null
        )).ToList();

        var statusChanges = await _db.EquipmentStatusHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.EquipmentInventoryId == equipmentId && h.ChangedAt.Year == year)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        var statusLines = statusChanges.Select(h => new StatusLine(
            h.Id,
            h.PreviousStatus,
            h.NewStatus,
            h.Comment,
            h.ChangedAt,
            h.ChangedByUserId,
            h.ChangedByUser?.FullName
        )).ToList();

        var logbookEntries = await _db.MaintenanceLogEntries
            .Where(e => e.EquipmentInventoryId == equipmentId && e.PerformedOn.Year == year)
            .OrderByDescending(e => e.PerformedOn)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        var logbookUserIds = logbookEntries.Select(e => e.PerformedByUserId).Distinct().ToList();
        var logbookUsers = await _db.Users
            .Where(u => logbookUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var logbookResponses = logbookEntries
            .Select(e => MaintenanceLogEntryResponse.From(e, logbookUsers.GetValueOrDefault(e.PerformedByUserId)))
            .ToList();

        BaselineLine? calBaseline = eq.LastCalibratedOn.HasValue
            ? new BaselineLine(eq.LastCalibratedOn.Value, "Baseline")
            : null;

        BaselineLine? pmBaseline = eq.LastPmOn.HasValue
            ? new BaselineLine(eq.LastPmOn.Value, "Baseline")
            : null;

        DateOnly? nextCalDue = eq.CalibrationDueDate.HasValue
            ? DateOnly.FromDateTime(eq.CalibrationDueDate.Value)
            : tasks.Where(t => t.TaskType == EquipmentTaskType.Calibration && t.CompletedOn == null && t.RetiredByRevisionId == null)
                   .OrderBy(t => t.DueDate)
                   .Select(t => (DateOnly?)t.DueDate)
                   .FirstOrDefault();

        if (!nextCalDue.HasValue)
        {
            nextCalDue = await _db.EquipmentTasks
                .Where(t => t.EquipmentInventoryId == equipmentId && t.TaskType == EquipmentTaskType.Calibration && t.CompletedOn == null && t.RetiredByRevisionId == null)
                .OrderBy(t => t.DueDate)
                .Select(t => (DateOnly?)t.DueDate)
                .FirstOrDefaultAsync();
        }

        DateOnly? nextPmDue = tasks
            .Where(t => t.TaskType == EquipmentTaskType.PreventiveMaintenance && t.CompletedOn == null && t.RetiredByRevisionId == null)
            .OrderBy(t => t.DueDate)
            .Select(t => (DateOnly?)t.DueDate)
            .FirstOrDefault();

        if (!nextPmDue.HasValue)
        {
            nextPmDue = await _db.EquipmentTasks
                .Where(t => t.EquipmentInventoryId == equipmentId && t.TaskType == EquipmentTaskType.PreventiveMaintenance && t.CompletedOn == null && t.RetiredByRevisionId == null)
                .OrderBy(t => t.DueDate)
                .Select(t => (DateOnly?)t.DueDate)
                .FirstOrDefaultAsync();
        }

        return new InstrumentHistoryResponse(
            eq.Id,
            eq.Code,
            eq.InstrumentType,
            eq.ManufacturerName,
            eq.SerialNumber,
            eq.Location,
            eq.Status,
            calIntervalDesc,
            pmIntervalDesc,
            taskItems,
            rescheduleLines,
            statusLines,
            logbookResponses,
            calBaseline,
            pmBaseline,
            nextCalDue,
            nextPmDue);
    }

    public async Task<List<EquipmentTaskItem>> GetPlannedVsDoneAsync(int year, EquipmentTaskType type)
    {
        var today = _clock.LabToday;

        var tasks = await _db.EquipmentTasks
            .Include(t => t.EquipmentInventory)
            .Include(t => t.CalibrationDocument)
            .Include(t => t.MaintenanceLogEntry)
            .Where(t => t.Year == year && t.TaskType == type && t.RetiredByRevisionId == null)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.EquipmentInventory != null ? t.EquipmentInventory.Code : string.Empty)
            .ThenBy(t => t.Sequence)
            .ToListAsync();

        return tasks.Select(t => new EquipmentTaskItem(
            t.Id,
            t.EquipmentInventoryId,
            t.EquipmentInventory?.Code ?? string.Empty,
            t.EquipmentInventory?.InstrumentType ?? string.Empty,
            t.TaskType,
            t.OriginalDueDate,
            t.DueDate,
            t.CompletedOn,
            DeriveStatus(t, today),
            GetEvidenceLabel(t)
        )).ToList();
    }
}
