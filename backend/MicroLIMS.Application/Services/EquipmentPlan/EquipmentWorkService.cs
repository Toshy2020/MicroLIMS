using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public record RecordCalibrationRequest(
    int CertificateDocumentId,
    DateOnly CalibratedOn,
    DateOnly? CertificateNextDue,
    string Password);

public record AddLogEntryRequest(
    MaintenanceEntryType EntryType,
    DateOnly PerformedOn,
    string WorkDone,
    string? PartsReplaced,
    string? Findings,
    int? CalendarEventId,
    string Password);

public record MaintenanceLogEntryResponse(
    int Id,
    int EquipmentInventoryId,
    MaintenanceEntryType EntryType,
    DateOnly PerformedOn,
    int PerformedByUserId,
    string? PerformedByUserName,
    string WorkDone,
    string? PartsReplaced,
    string? Findings,
    int? CalendarEventId,
    DateTime RecordedAt,
    DateTime? VoidedAt,
    int? VoidedByUserId,
    string? VoidReason,
    int ElectronicSignatureId)
{
    public static MaintenanceLogEntryResponse From(MaintenanceLogEntry e, string? userName = null) => new(
        e.Id,
        e.EquipmentInventoryId,
        e.EntryType,
        e.PerformedOn,
        e.PerformedByUserId,
        userName,
        e.WorkDone,
        e.PartsReplaced,
        e.Findings,
        e.CalendarEventId,
        e.RecordedAt,
        e.VoidedAt,
        e.VoidedByUserId,
        e.VoidReason,
        e.ElectronicSignatureId);
}

public record VoidLogEntryRequest(string Reason, string Password);

public class EquipmentWorkService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly IElectronicSignatureService _signatures;

    public EquipmentWorkService(
        IMicroLimsDbContext db,
        ILabClock clock,
        IElectronicSignatureService signatures)
    {
        _db = db;
        _clock = clock;
        _signatures = signatures;
    }

    public async Task<EquipmentTaskResponse> RecordCalibrationAsync(
        int equipmentId,
        RecordCalibrationRequest r,
        int userId,
        string? ip)
    {
        var equipment = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");

        var cert = await _db.EquipmentDocuments.FirstOrDefaultAsync(d => d.Id == r.CertificateDocumentId)
            ?? throw new NotFoundException($"Calibration certificate {r.CertificateDocumentId} not found.");

        if (cert.EquipmentInventoryId != equipmentId)
            throw new BusinessRuleException("The calibration certificate belongs to a different instrument.");

        if (cert.DocumentType != EquipmentDocumentType.CalibrationCertificate)
            throw new BusinessRuleException("The document is not a calibration certificate.");

        if (cert.Status != MaterialDocumentStatus.Current)
            throw new BusinessRuleException("The calibration certificate is not current.");

        if (r.CalibratedOn > _clock.LabToday)
            throw new BusinessRuleException("Calibration date cannot be in the future.");

        var completedDates = await _db.EquipmentTasks
            .LiveTasks()
            .Where(t => t.EquipmentInventoryId == equipmentId && t.TaskType == EquipmentTaskType.Calibration && t.CompletedOn != null)
            .Select(t => t.CompletedOn!.Value)
            .ToListAsync();

        DateOnly? previousActual = completedDates.Count > 0 ? completedDates.Max() : null;
        if (previousActual == null)
        {
            previousActual = equipment.LastCalibratedOn;
        }
        else if (equipment.LastCalibratedOn.HasValue && equipment.LastCalibratedOn.Value > previousActual.Value)
        {
            previousActual = equipment.LastCalibratedOn.Value;
        }

        if (previousActual.HasValue && r.CalibratedOn <= previousActual.Value)
        {
            throw new BusinessRuleException($"Calibration date must be after the previous actual date ({previousActual.Value:yyyy-MM-dd}).");
        }

        if (r.CertificateNextDue.HasValue && r.CertificateNextDue.Value <= r.CalibratedOn)
        {
            throw new BusinessRuleException("Certificate next due date must be after the calibration date.");
        }

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var openTasks = await _db.EquipmentTasks
                .LiveTasks()
                .Where(t => t.EquipmentInventoryId == equipmentId &&
                            t.TaskType == EquipmentTaskType.Calibration &&
                            t.CompletedOn == null &&
                            t.RetiredByRevisionId == null)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Sequence)
                .ToListAsync();

            EquipmentTask taskToComplete;
            bool isUnplanned = false;

            if (openTasks.Count > 0)
            {
                taskToComplete = openTasks[0];
                taskToComplete.CompletedOn = r.CalibratedOn;
                taskToComplete.CalibrationDocumentId = cert.Id;
            }
            else
            {
                isUnplanned = true;
                var calYear = r.CalibratedOn.Year;
                var maxSeq = await _db.EquipmentTasks
                    .Where(t => t.EquipmentInventoryId == equipmentId && t.TaskType == EquipmentTaskType.Calibration && t.Year == calYear)
                    .MaxAsync(t => (int?)t.Sequence) ?? 0;

                var liveRevision = await _db.EquipmentPlanRevisions
                    .Where(rev => rev.Year == calYear && rev.Status == PlanRevisionStatus.Approved)
                    .OrderByDescending(rev => rev.RevisionNumber)
                    .FirstOrDefaultAsync();

                var intervalText = equipment.CalibrationIntervalValue.HasValue && equipment.CalibrationIntervalUnit.HasValue
                    ? EquipmentScheduleCalculator.Describe(equipment.CalibrationIntervalValue.Value, equipment.CalibrationIntervalUnit.Value)
                    : string.Empty;

                taskToComplete = new EquipmentTask
                {
                    EquipmentInventoryId = equipmentId,
                    TaskType = EquipmentTaskType.Calibration,
                    Year = calYear,
                    Sequence = maxSeq + 1,
                    IsUnplanned = true,
                    CreatedByRevisionId = liveRevision?.Id,
                    OriginalDueDate = r.CalibratedOn,
                    DueDate = r.CalibratedOn,
                    CompletedOn = r.CalibratedOn,
                    CalibrationDocumentId = cert.Id,
                    IntervalSnapshot = intervalText
                };
                _db.EquipmentTasks.Add(taskToComplete);
                await _db.SaveChangesAsync();
            }

            var signature = await _signatures.SignAsync(
                userId,
                r.Password,
                SignatureMeaning.CalibrationRecorded,
                nameof(EquipmentTask),
                taskToComplete.Id,
                "Calibration recorded",
                ip);

            var now = _clock.UtcNow.UtcDateTime;

            var completedEvent = new EquipmentTaskEvent
            {
                EquipmentTaskId = taskToComplete.Id,
                EventType = EquipmentTaskEventType.Completed,
                OldValue = null,
                NewValue = r.CalibratedOn.ToString("yyyy-MM-dd"),
                Reason = "Calibration recorded",
                PerformedByUserId = userId,
                PerformedAt = now,
                ElectronicSignatureId = signature.Id
            };
            _db.EquipmentTaskEvents.Add(completedEvent);

            var followingOpenTasks = isUnplanned ? openTasks : openTasks.Skip(1).ToList();

            var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
            var workingWeekdays = settings.WorkingWeekdays;
            var nonWorking = (await _db.LabCalendarDates.AsNoTracking().Select(d => d.Date).ToListAsync()).ToHashSet();

            DateOnly? nextDueDate = null;

            if (equipment.CalibrationIntervalValue.HasValue && equipment.CalibrationIntervalUnit.HasValue)
            {
                var calVal = equipment.CalibrationIntervalValue.Value;
                var calUnit = equipment.CalibrationIntervalUnit.Value;

                var firstRaw = r.CertificateNextDue ?? EquipmentScheduleCalculator.AddInterval(r.CalibratedOn, calVal, calUnit);
                var firstDue = EquipmentScheduleCalculator.DueAfter(
                    r.CalibratedOn,
                    calVal,
                    calUnit,
                    r.CertificateNextDue,
                    _clock.LabToday,
                    workingWeekdays,
                    nonWorking);

                nextDueDate = firstDue;

                for (int i = 0; i < followingOpenTasks.Count; i++)
                {
                    var followingTask = followingOpenTasks[i];
                    DateOnly rawDate = (i == 0 && r.CertificateNextDue.HasValue)
                        ? r.CertificateNextDue.Value
                        : EquipmentScheduleCalculator.AddInterval(firstRaw, i * calVal, calUnit);

                    DateOnly computedDue = EquipmentScheduleCalculator.DueAfter(
                        r.CalibratedOn,
                        calVal,
                        calUnit,
                        rawDate,
                        _clock.LabToday,
                        workingWeekdays,
                        nonWorking);

                    if (followingTask.DueDate != computedDue)
                    {
                        var oldDueDateStr = followingTask.DueDate.ToString("yyyy-MM-dd");
                        var newDueDateStr = computedDue.ToString("yyyy-MM-dd");
                        followingTask.DueDate = computedDue;

                        _db.EquipmentTaskEvents.Add(new EquipmentTaskEvent
                        {
                            EquipmentTaskId = followingTask.Id,
                            EventType = EquipmentTaskEventType.Rescheduled,
                            OldValue = oldDueDateStr,
                            NewValue = newDueDateStr,
                            Reason = $"Re-projected from calibration on {r.CalibratedOn:yyyy-MM-dd}",
                            PerformedByUserId = userId,
                            PerformedAt = now,
                            ElectronicSignatureId = signature.Id
                        });
                    }
                }
            }
            else if (r.CertificateNextDue.HasValue)
            {
                nextDueDate = r.CertificateNextDue.Value;
            }

            if (nextDueDate.HasValue)
            {
                var newDateTime = new DateTime(nextDueDate.Value.Year, nextDueDate.Value.Month, nextDueDate.Value.Day, 0, 0, 0, DateTimeKind.Utc);
                equipment.CalibrationDueDate = newDateTime;

                var normalizedCode = equipment.Code.Trim().ToLower();
                var matchingEquipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Code.ToLower() == normalizedCode);
                if (matchingEquipment != null)
                {
                    matchingEquipment.CalibrationDueDate = newDateTime;
                }
            }

            if (equipment.AutoOutOfServiceSince != null && equipment.Status == EquipmentOperationalStatus.OutOfService)
            {
                var prevStatus = equipment.Status;
                equipment.Status = EquipmentOperationalStatus.InService;
                equipment.AutoOutOfServiceSince = null;

                _db.EquipmentStatusHistories.Add(new EquipmentStatusHistory
                {
                    EquipmentInventoryId = equipment.Id,
                    PreviousStatus = prevStatus,
                    NewStatus = EquipmentOperationalStatus.InService,
                    Comment = "Calibration recorded; returned to service",
                    ChangedByUserId = userId,
                    ChangedAt = now
                });
            }

            equipment.LastModifiedByUserId = userId;
            equipment.LastModifiedAt = now;

            await _db.SaveChangesAsync();

            return EquipmentTaskResponse.From(taskToComplete);
        });
    }

    public async Task<MaintenanceLogEntryResponse> AddLogEntryAsync(
        int equipmentId,
        AddLogEntryRequest r,
        int userId,
        string? ip)
    {
        var equipment = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");

        if (string.IsNullOrWhiteSpace(r.WorkDone))
            throw new BusinessRuleException("Work done is required.");

        if (r.PerformedOn > _clock.LabToday)
            throw new BusinessRuleException("Maintenance performed date cannot be in the future.");

        if (r.CalendarEventId.HasValue)
        {
            var calEvent = await _db.CalendarEvents.FirstOrDefaultAsync(c => c.Id == r.CalendarEventId.Value)
                ?? throw new NotFoundException($"Calendar event {r.CalendarEventId.Value} not found.");

            if (calEvent.EquipmentInventoryId != null && calEvent.EquipmentInventoryId != equipmentId)
                throw new BusinessRuleException("The calendar event belongs to a different instrument.");
        }

        if (r.EntryType == MaintenanceEntryType.PreventiveMaintenance)
        {
            var pmExists = await _db.MaintenanceLogEntries
                .AsNoTracking()
                .AnyAsync(m => m.EquipmentInventoryId == equipmentId &&
                               m.VoidedAt == null &&
                               m.PerformedOn == r.PerformedOn &&
                               m.EntryType == MaintenanceEntryType.PreventiveMaintenance);

            if (pmExists)
            {
                throw new BusinessRuleException($"A preventive maintenance entry for {equipment.Code} on {r.PerformedOn:yyyy-MM-dd} is already recorded.");
            }
        }
        else
        {
            var trimmedWork = r.WorkDone.Trim();
            var existingWorkEntries = await _db.MaintenanceLogEntries
                .AsNoTracking()
                .Where(m => m.EquipmentInventoryId == equipmentId &&
                            m.VoidedAt == null &&
                            m.PerformedOn == r.PerformedOn &&
                            m.EntryType == r.EntryType)
                .Select(m => m.WorkDone)
                .ToListAsync();

            if (existingWorkEntries.Any(w => string.Equals(w.Trim(), trimmedWork, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessRuleException($"This entry is already recorded for {equipment.Code} on {r.PerformedOn:yyyy-MM-dd}.");
            }
        }

        return await UnitOfWork.RunAsync(_db, async () =>
        {
            var signature = await _signatures.SignAsync(
                userId,
                r.Password,
                SignatureMeaning.MaintenanceEntryRecorded,
                nameof(MaintenanceLogEntry),
                0,
                r.WorkDone.Trim(),
                ip);

            var now = _clock.UtcNow.UtcDateTime;

            var entry = new MaintenanceLogEntry
            {
                EquipmentInventoryId = equipmentId,
                EntryType = r.EntryType,
                PerformedOn = r.PerformedOn,
                PerformedByUserId = userId,
                WorkDone = r.WorkDone.Trim(),
                PartsReplaced = string.IsNullOrWhiteSpace(r.PartsReplaced) ? null : r.PartsReplaced.Trim(),
                Findings = string.IsNullOrWhiteSpace(r.Findings) ? null : r.Findings.Trim(),
                ElectronicSignatureId = signature.Id,
                RecordedAt = now,
                CalendarEventId = r.CalendarEventId
            };
            _db.MaintenanceLogEntries.Add(entry);
            await _db.SaveChangesAsync();

            signature.EntityId = entry.Id;

            if (r.EntryType == MaintenanceEntryType.PreventiveMaintenance)
            {
                var openPmTasks = await _db.EquipmentTasks
                    .LiveTasks()
                    .Where(t => t.EquipmentInventoryId == equipmentId &&
                                t.TaskType == EquipmentTaskType.PreventiveMaintenance &&
                                t.CompletedOn == null &&
                                t.RetiredByRevisionId == null)
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.Sequence)
                    .ToListAsync();

                if (openPmTasks.Count > 0)
                {
                    var taskToComplete = openPmTasks[0];
                    taskToComplete.CompletedOn = r.PerformedOn;
                    taskToComplete.MaintenanceLogEntryId = entry.Id;

                    _db.EquipmentTaskEvents.Add(new EquipmentTaskEvent
                    {
                        EquipmentTaskId = taskToComplete.Id,
                        EventType = EquipmentTaskEventType.Completed,
                        OldValue = null,
                        NewValue = r.PerformedOn.ToString("yyyy-MM-dd"),
                        Reason = "Preventive maintenance recorded",
                        PerformedByUserId = userId,
                        PerformedAt = now,
                        ElectronicSignatureId = signature.Id
                    });

                    var followingPmTasks = openPmTasks.Skip(1).ToList();

                    if (equipment.PmIntervalValue.HasValue && equipment.PmIntervalUnit.HasValue)
                    {
                        var settings = await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings();
                        var workingWeekdays = settings.WorkingWeekdays;
                        var nonWorking = (await _db.LabCalendarDates.AsNoTracking().Select(d => d.Date).ToListAsync()).ToHashSet();

                        var pmVal = equipment.PmIntervalValue.Value;
                        var pmUnit = equipment.PmIntervalUnit.Value;
                        var firstRaw = EquipmentScheduleCalculator.AddInterval(r.PerformedOn, pmVal, pmUnit);

                        for (int i = 0; i < followingPmTasks.Count; i++)
                        {
                            var followingTask = followingPmTasks[i];
                            DateOnly rawDate = EquipmentScheduleCalculator.AddInterval(firstRaw, i * pmVal, pmUnit);

                            DateOnly computedDue = EquipmentScheduleCalculator.DueAfter(
                                r.PerformedOn,
                                pmVal,
                                pmUnit,
                                rawDate,
                                _clock.LabToday,
                                workingWeekdays,
                                nonWorking);

                            if (followingTask.DueDate != computedDue)
                            {
                                var oldDueDateStr = followingTask.DueDate.ToString("yyyy-MM-dd");
                                var newDueDateStr = computedDue.ToString("yyyy-MM-dd");
                                followingTask.DueDate = computedDue;

                                _db.EquipmentTaskEvents.Add(new EquipmentTaskEvent
                                {
                                    EquipmentTaskId = followingTask.Id,
                                    EventType = EquipmentTaskEventType.Rescheduled,
                                    OldValue = oldDueDateStr,
                                    NewValue = newDueDateStr,
                                    Reason = $"Re-projected from preventive maintenance on {r.PerformedOn:yyyy-MM-dd}",
                                    PerformedByUserId = userId,
                                    PerformedAt = now,
                                    ElectronicSignatureId = signature.Id
                                });
                            }
                        }
                    }
                }
                else
                {
                    var calYear = r.PerformedOn.Year;
                    var maxSeq = await _db.EquipmentTasks
                        .Where(t => t.EquipmentInventoryId == equipmentId && t.TaskType == EquipmentTaskType.PreventiveMaintenance && t.Year == calYear)
                        .MaxAsync(t => (int?)t.Sequence) ?? 0;

                    var liveRevision = await _db.EquipmentPlanRevisions
                        .Where(rev => rev.Year == calYear && rev.Status == PlanRevisionStatus.Approved)
                        .OrderByDescending(rev => rev.RevisionNumber)
                        .FirstOrDefaultAsync();

                    var intervalText = equipment.PmIntervalValue.HasValue && equipment.PmIntervalUnit.HasValue
                        ? EquipmentScheduleCalculator.Describe(equipment.PmIntervalValue.Value, equipment.PmIntervalUnit.Value)
                        : string.Empty;

                    var taskToComplete = new EquipmentTask
                    {
                        EquipmentInventoryId = equipmentId,
                        TaskType = EquipmentTaskType.PreventiveMaintenance,
                        Year = calYear,
                        Sequence = maxSeq + 1,
                        IsUnplanned = true,
                        CreatedByRevisionId = liveRevision?.Id,
                        OriginalDueDate = r.PerformedOn,
                        DueDate = r.PerformedOn,
                        CompletedOn = r.PerformedOn,
                        MaintenanceLogEntryId = entry.Id,
                        IntervalSnapshot = intervalText
                    };
                    _db.EquipmentTasks.Add(taskToComplete);
                    await _db.SaveChangesAsync();

                    _db.EquipmentTaskEvents.Add(new EquipmentTaskEvent
                    {
                        EquipmentTaskId = taskToComplete.Id,
                        EventType = EquipmentTaskEventType.Completed,
                        OldValue = null,
                        NewValue = r.PerformedOn.ToString("yyyy-MM-dd"),
                        Reason = "Preventive maintenance recorded (unplanned)",
                        PerformedByUserId = userId,
                        PerformedAt = now,
                        ElectronicSignatureId = signature.Id
                    });
                }
            }

            equipment.LastModifiedByUserId = userId;
            equipment.LastModifiedAt = now;

            await _db.SaveChangesAsync();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return MaintenanceLogEntryResponse.From(entry, user?.FullName);
        });
    }

    public async Task VoidLogEntryAsync(
        int entryId,
        string reason,
        string password,
        int userId,
        string? ip)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("A reason is required to void a maintenance log entry.");

        var entry = await _db.MaintenanceLogEntries.FirstOrDefaultAsync(e => e.Id == entryId)
            ?? throw new NotFoundException($"Maintenance log entry {entryId} not found.");

        if (entry.VoidedAt.HasValue)
            throw new BusinessRuleException("Maintenance log entry is already voided.");

        await UnitOfWork.RunAsync(_db, async () =>
        {
            var trimmedReason = reason.Trim();
            var signature = await _signatures.SignAsync(
                userId,
                password,
                SignatureMeaning.MaintenanceEntryVoided,
                nameof(MaintenanceLogEntry),
                entry.Id,
                trimmedReason,
                ip);

            var now = _clock.UtcNow.UtcDateTime;
            entry.VoidedAt = now;
            entry.VoidedByUserId = userId;
            entry.VoidReason = trimmedReason;

            if (entry.EntryType == MaintenanceEntryType.PreventiveMaintenance)
            {
                var hasLaterPmEntry = await _db.MaintenanceLogEntries.AnyAsync(m =>
                    m.EquipmentInventoryId == entry.EquipmentInventoryId &&
                    m.EntryType == MaintenanceEntryType.PreventiveMaintenance &&
                    m.Id != entry.Id &&
                    m.VoidedAt == null &&
                    (m.PerformedOn > entry.PerformedOn || (m.PerformedOn == entry.PerformedOn && m.Id > entry.Id)));

                if (!hasLaterPmEntry)
                {
                    var task = await _db.EquipmentTasks.FirstOrDefaultAsync(t => t.MaintenanceLogEntryId == entry.Id);
                    if (task != null)
                    {
                        task.CompletedOn = null;
                        task.MaintenanceLogEntryId = null;
                    }
                }
            }

            await _db.SaveChangesAsync();
        });
    }

    public async Task<List<MaintenanceLogEntryResponse>> GetMaintenanceLogAsync(int equipmentId)
    {
        var exists = await _db.EquipmentInventories.AnyAsync(e => e.Id == equipmentId);
        if (!exists)
            throw new NotFoundException($"Equipment {equipmentId} not found.");

        var entries = await _db.MaintenanceLogEntries
            .Where(e => e.EquipmentInventoryId == equipmentId)
            .OrderByDescending(e => e.PerformedOn)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        var userIds = entries.Select(e => e.PerformedByUserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return entries.Select(e => MaintenanceLogEntryResponse.From(e, users.GetValueOrDefault(e.PerformedByUserId))).ToList();
    }

    public async Task<int> ApplyOverdueAsync(DateOnly today)
    {
        var todayUtc = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var candidates = await _db.EquipmentInventories
            .Where(e => e.Status == EquipmentOperationalStatus.InService
                     && e.CalibrationDueDate != null
                     && e.CalibrationDueDate.Value < todayUtc)
            .ToListAsync();

        var overdue = candidates
            .Where(e => DateOnly.FromDateTime(e.CalibrationDueDate!.Value) < today)
            .ToList();

        if (overdue.Count == 0) return 0;

        var systemUserId = await ResolveSystemUserIdAsync();
        var now = _clock.UtcNow.UtcDateTime;

        await UnitOfWork.RunAsync(_db, async () =>
        {
            foreach (var inst in overdue)
            {
                var due = DateOnly.FromDateTime(inst.CalibrationDueDate!.Value);
                var prevStatus = inst.Status;
                inst.Status = EquipmentOperationalStatus.OutOfService;
                inst.AutoOutOfServiceSince = today;
                inst.LastModifiedAt = now;
                inst.LastModifiedByUserId = systemUserId;

                _db.EquipmentStatusHistories.Add(new EquipmentStatusHistory
                {
                    EquipmentInventoryId = inst.Id,
                    PreviousStatus = prevStatus,
                    NewStatus = EquipmentOperationalStatus.OutOfService,
                    Comment = $"Calibration overdue since {due:yyyy-MM-dd} (automatic)",
                    ChangedByUserId = systemUserId,
                    ChangedAt = now
                });
            }

            await _db.SaveChangesAsync();
        });

        return overdue.Count;
    }

    private async Task<int> ResolveSystemUserIdAsync()
    {
        var adminId = await _db.Users
            .Where(u => (u.Role != null && u.Role.Type == RoleType.SystemAdministrator) || u.Username == "admin" || u.Username == "system")
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (adminId != 0) return adminId;

        var fallbackId = await _db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
        return fallbackId != 0 ? fallbackId : 1;
    }
}
