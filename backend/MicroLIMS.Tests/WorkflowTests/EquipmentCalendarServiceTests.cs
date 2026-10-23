using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class EquipmentCalendarServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 15, 8, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public LabClock Clock = null!;
        public CalendarEventService CalendarEvents = null!;
        public EquipmentCalendarService Service = null!;
        public EquipmentInventory Equipment = null!;
        public User Analyst = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        var timeProvider = new FixedTimeProvider(Now);
        var clock = new LabClock(timeProvider, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var role = new Role
        {
            Name = "Analyst",
            Type = RoleType.Analyst,
            IsSystemRole = true,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var analyst = new User
        {
            Username = "analyst",
            FullName = "Analyst User",
            RoleId = role.Id,
            PasswordHash = "hash",
            IsActive = true
        };
        db.Users.Add(analyst);

        var section = new DocumentSection
        {
            Code = "MICRO",
            Name = "Microbiology"
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        var eq = new EquipmentInventory
        {
            Code = "INC-01",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            SerialNumber = "SN-1234",
            Location = "Room 101",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 3, 10),
            CalibrationDueDate = new DateTime(2027, 4, 14, 0, 0, 0, DateTimeKind.Utc),
            PmIntervalValue = 3,
            PmIntervalUnit = IntervalUnit.Months,
            LastPmOn = new DateOnly(2026, 1, 15),
            SectionId = section.Id
        };
        db.EquipmentInventories.Add(eq);
        await db.SaveChangesAsync();

        var storage = new InMemoryFileStorageService();
        var validator = new MaterialDocumentFileValidator(30 * 1024 * 1024L);
        var calendarEvents = new CalendarEventService(db, storage, validator, timeProvider: timeProvider);
        var service = new EquipmentCalendarService(db, clock, calendarEvents);

        return new World
        {
            Db = db,
            Clock = clock,
            CalendarEvents = calendarEvents,
            Service = service,
            Equipment = eq,
            Analyst = analyst
        };
    }

    [Fact]
    public async Task Status_overdue_when_past_due_and_not_done()
    {
        var w = await BuildAsync();
        // Today is 2026-04-15

        var cert = new EquipmentDocument
        {
            EquipmentInventoryId = w.Equipment.Id,
            DocumentType = EquipmentDocumentType.CalibrationCertificate,
            OriginalFileName = "cert_inc01_2026.pdf",
            StorageKey = "docs/cert.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "dummy-sha",
            UploadedByUserId = w.Analyst.Id,
            Status = MaterialDocumentStatus.Current,
            UploadedAt = Now.UtcDateTime
        };
        w.Db.EquipmentDocuments.Add(cert);
        await w.Db.SaveChangesAsync();

        // 1. Overdue task (DueDate < today, not done, not retired)
        var overdueTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 10),
            DueDate = new DateOnly(2026, 4, 10),
            CompletedOn = null
        };

        // 2. Due task (within 7 days: 2026-04-18 is 3 days ahead)
        var dueTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 2,
            OriginalDueDate = new DateOnly(2026, 4, 18),
            DueDate = new DateOnly(2026, 4, 18),
            CompletedOn = null,
            MaintenanceLogEntryId = 42
        };

        // 3. Upcoming task (> 7 days: 2026-05-15)
        var upcomingTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 3,
            OriginalDueDate = new DateOnly(2026, 5, 15),
            DueDate = new DateOnly(2026, 5, 15),
            CompletedOn = null
        };

        // 4. Done task (CompletedOn is set, even if DueDate < today)
        var doneTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 4,
            OriginalDueDate = new DateOnly(2026, 4, 1),
            DueDate = new DateOnly(2026, 4, 1),
            CompletedOn = new DateOnly(2026, 4, 2),
            CalibrationDocumentId = cert.Id
        };

        // 5. Retired task (RetiredByRevisionId is set)
        var retiredTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 5,
            OriginalDueDate = new DateOnly(2026, 4, 5),
            DueDate = new DateOnly(2026, 4, 5),
            RetiredByRevisionId = 99
        };

        w.Db.EquipmentTasks.AddRange(overdueTask, dueTask, upcomingTask, doneTask, retiredTask);
        await w.Db.SaveChangesAsync();

        var res = await w.Service.GetCalendarAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));

        var itemOverdue = res.Tasks.First(t => t.Id == overdueTask.Id);
        Assert.Equal(EquipmentTaskStatus.Overdue, itemOverdue.Status);
        Assert.Null(itemOverdue.EvidenceLabel);

        var itemDue = res.Tasks.First(t => t.Id == dueTask.Id);
        Assert.Equal(EquipmentTaskStatus.Due, itemDue.Status);
        Assert.Equal("Logbook entry #42", itemDue.EvidenceLabel);

        var itemUpcoming = res.Tasks.First(t => t.Id == upcomingTask.Id);
        Assert.Equal(EquipmentTaskStatus.Upcoming, itemUpcoming.Status);

        var itemDone = res.Tasks.First(t => t.Id == doneTask.Id);
        Assert.Equal(EquipmentTaskStatus.Done, itemDone.Status);
        Assert.Equal("cert_inc01_2026.pdf", itemDone.EvidenceLabel);

        var itemRetired = res.Tasks.First(t => t.Id == retiredTask.Id);
        Assert.Equal(EquipmentTaskStatus.Retired, itemRetired.Status);

        // Invalid range
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.GetCalendarAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1)));
    }

    [Fact]
    public async Task Draft_tasks_are_not_shown_until_approved()
    {
        var w = await BuildAsync();

        // Clear CalibrationDueDate on Equipment so NextCalibrationDue falls back to tasks
        w.Equipment.CalibrationDueDate = null;
        await w.Db.SaveChangesAsync();

        var draftRev = new EquipmentPlanRevision
        {
            Year = 2026,
            RevisionNumber = 1,
            Status = PlanRevisionStatus.Draft,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            GeneratedAt = Now.UtcDateTime,
            GeneratedByUserId = w.Analyst.Id
        };
        w.Db.EquipmentPlanRevisions.Add(draftRev);
        await w.Db.SaveChangesAsync();

        var draftTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 20),
            DueDate = new DateOnly(2026, 4, 20),
            CreatedByRevisionId = draftRev.Id
        };
        w.Db.EquipmentTasks.Add(draftTask);
        await w.Db.SaveChangesAsync();

        // 1. Calendar: draft task must not appear
        var calendar = await w.Service.GetCalendarAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        Assert.DoesNotContain(calendar.Tasks, t => t.Id == draftTask.Id);

        // 2. Month summary: April (index 3) planned count should be 0
        var yearSummary = await w.Service.GetYearAsync(2026);
        Assert.Equal(0, yearSummary[3].Planned);

        // 3. History: draft task must not appear in tasks or next due
        var history = await w.Service.GetInstrumentHistoryAsync(w.Equipment.Id, 2026);
        Assert.DoesNotContain(history.Tasks, t => t.Id == draftTask.Id);
        Assert.Null(history.NextCalibrationDue);

        // 4. Planned vs Done: draft task must not appear
        var plannedVsDone = await w.Service.GetPlannedVsDoneAsync(2026, EquipmentTaskType.Calibration);
        Assert.DoesNotContain(plannedVsDone, t => t.Id == draftTask.Id);

        // Now approve the revision
        draftRev.Status = PlanRevisionStatus.Approved;
        await w.Db.SaveChangesAsync();

        // All queries should now show the task
        calendar = await w.Service.GetCalendarAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        Assert.Contains(calendar.Tasks, t => t.Id == draftTask.Id);

        yearSummary = await w.Service.GetYearAsync(2026);
        Assert.Equal(1, yearSummary[3].Planned);

        history = await w.Service.GetInstrumentHistoryAsync(w.Equipment.Id, 2026);
        Assert.Contains(history.Tasks, t => t.Id == draftTask.Id);
        Assert.Equal(draftTask.DueDate, history.NextCalibrationDue);

        plannedVsDone = await w.Service.GetPlannedVsDoneAsync(2026, EquipmentTaskType.Calibration);
        Assert.Contains(plannedVsDone, t => t.Id == draftTask.Id);
    }

    [Fact]
    public async Task Month_summary_counts_tasks_and_events()
    {
        var w = await BuildAsync();
        // Today is 2026-04-15

        // Tasks in March 2026 (Month 3)
        var taskDoneMarch = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 10),
            CompletedOn = new DateOnly(2026, 3, 10)
        };
        var taskOverdueMarch = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 2,
            OriginalDueDate = new DateOnly(2026, 3, 20),
            DueDate = new DateOnly(2026, 3, 20),
            CompletedOn = null
        };
        var taskRetiredMarch = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 3,
            OriginalDueDate = new DateOnly(2026, 3, 25),
            DueDate = new DateOnly(2026, 3, 25),
            RetiredByRevisionId = 10
        };

        // Task in May 2026 (Month 5)
        var taskUpcomingMay = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 4,
            OriginalDueDate = new DateOnly(2026, 5, 10),
            DueDate = new DateOnly(2026, 5, 10),
            CompletedOn = null
        };

        w.Db.EquipmentTasks.AddRange(taskDoneMarch, taskOverdueMarch, taskRetiredMarch, taskUpcomingMay);

        // Calendar events
        var eventDayMarch = new CalendarEvent
        {
            EventType = CalendarEventType.VendorVisit,
            Title = "Vendor maintenance visit",
            Date = new DateOnly(2026, 3, 15),
            Status = CalendarEventStatus.Active,
            CreatedByUserId = w.Analyst.Id,
            CreatedAt = Now.UtcDateTime,
            LastModifiedByUserId = w.Analyst.Id,
            LastModifiedAt = Now.UtcDateTime
        };
        var eventMonthMarch = new CalendarEvent
        {
            EventType = CalendarEventType.Audit,
            Title = "Internal ISO audit",
            Month = "2026-03",
            Status = CalendarEventStatus.Active,
            CreatedByUserId = w.Analyst.Id,
            CreatedAt = Now.UtcDateTime,
            LastModifiedByUserId = w.Analyst.Id,
            LastModifiedAt = Now.UtcDateTime
        };
        var eventCancelledMarch = new CalendarEvent
        {
            EventType = CalendarEventType.Other,
            Title = "Cancelled session",
            Date = new DateOnly(2026, 3, 22),
            Status = CalendarEventStatus.Cancelled,
            CancelReason = "Postponed",
            CreatedByUserId = w.Analyst.Id,
            CreatedAt = Now.UtcDateTime,
            LastModifiedByUserId = w.Analyst.Id,
            LastModifiedAt = Now.UtcDateTime
        };
        var eventMay = new CalendarEvent
        {
            EventType = CalendarEventType.BreakdownRepair,
            Title = "Heater coil check",
            Date = new DateOnly(2026, 5, 5),
            Status = CalendarEventStatus.Active,
            CreatedByUserId = w.Analyst.Id,
            CreatedAt = Now.UtcDateTime,
            LastModifiedByUserId = w.Analyst.Id,
            LastModifiedAt = Now.UtcDateTime
        };

        w.Db.CalendarEvents.AddRange(eventDayMarch, eventMonthMarch, eventCancelledMarch, eventMay);
        await w.Db.SaveChangesAsync();

        var summaries = await w.Service.GetYearAsync(2026);

        Assert.Equal(12, summaries.Count);

        var march = summaries.First(s => s.Month == 3);
        Assert.Equal(2, march.Planned); // taskDoneMarch + taskOverdueMarch; retired excluded
        Assert.Equal(1, march.Done);
        Assert.Equal(1, march.Overdue);
        Assert.Equal(0, march.Upcoming);
        Assert.Equal(2, march.Events); // 2 active events; cancelled excluded

        var may = summaries.First(s => s.Month == 5);
        Assert.Equal(1, may.Planned);
        Assert.Equal(0, may.Done);
        Assert.Equal(0, may.Overdue);
        Assert.Equal(1, may.Upcoming);
        Assert.Equal(1, may.Events);

        var april = summaries.First(s => s.Month == 4);
        Assert.Equal(0, april.Planned);
        Assert.Equal(0, april.Done);
        Assert.Equal(0, april.Overdue);
        Assert.Equal(0, april.Upcoming);
        Assert.Equal(0, april.Events);

        // Invalid year
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.GetYearAsync(1999));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.GetYearAsync(2101));
    }

    [Fact]
    public async Task History_shows_original_and_actual_dates_and_baseline_label()
    {
        var w = await BuildAsync();
        // Today is 2026-04-15

        var cert = new EquipmentDocument
        {
            EquipmentInventoryId = w.Equipment.Id,
            DocumentType = EquipmentDocumentType.CalibrationCertificate,
            OriginalFileName = "cert_binder_2026.pdf",
            StorageKey = "docs/cert_binder_2026.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048,
            ContentSha256 = "sha-cert",
            UploadedByUserId = w.Analyst.Id,
            Status = MaterialDocumentStatus.Current,
            UploadedAt = Now.UtcDateTime
        };
        w.Db.EquipmentDocuments.Add(cert);
        await w.Db.SaveChangesAsync();

        // Tasks for 2026
        var calTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 10),
            CompletedOn = new DateOnly(2026, 4, 14),
            CalibrationDocumentId = cert.Id
        };
        var pmTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 15),
            DueDate = new DateOnly(2026, 4, 15),
            CompletedOn = null
        };
        w.Db.EquipmentTasks.AddRange(calTask, pmTask);
        await w.Db.SaveChangesAsync();

        // Reschedule event on PM task
        var reschedule = new EquipmentTaskEvent
        {
            EquipmentTaskId = pmTask.Id,
            EventType = EquipmentTaskEventType.Rescheduled,
            OldValue = "2026-04-10",
            NewValue = "2026-04-15",
            Reason = "Re-projected from calibration on 2026-04-14",
            PerformedByUserId = w.Analyst.Id,
            PerformedAt = Now.UtcDateTime
        };
        w.Db.EquipmentTaskEvents.Add(reschedule);

        // Status history
        var statusHistory = new EquipmentStatusHistory
        {
            EquipmentInventoryId = w.Equipment.Id,
            PreviousStatus = EquipmentOperationalStatus.OutOfService,
            NewStatus = EquipmentOperationalStatus.InService,
            Comment = "Calibration recorded; returned to service",
            ChangedByUserId = w.Analyst.Id,
            ChangedAt = Now.UtcDateTime
        };
        w.Db.EquipmentStatusHistories.Add(statusHistory);

        // Maintenance log entry
        var logEntry = new MaintenanceLogEntry
        {
            EquipmentInventoryId = w.Equipment.Id,
            EntryType = MaintenanceEntryType.PreventiveMaintenance,
            PerformedOn = new DateOnly(2026, 4, 14),
            PerformedByUserId = w.Analyst.Id,
            WorkDone = "Quarterly cleaning and seal replacement",
            RecordedAt = Now.UtcDateTime
        };
        w.Db.MaintenanceLogEntries.Add(logEntry);
        await w.Db.SaveChangesAsync();

        var history = await w.Service.GetInstrumentHistoryAsync(w.Equipment.Id, 2026);

        // 1. Instrument header
        Assert.Equal(w.Equipment.Id, history.EquipmentId);
        Assert.Equal("INC-01", history.EquipmentCode);
        Assert.Equal("Incubator", history.InstrumentType);
        Assert.Equal("Binder", history.ManufacturerName);
        Assert.Equal("SN-1234", history.SerialNumber);
        Assert.Equal("Room 101", history.Location);
        Assert.Equal(EquipmentOperationalStatus.InService, history.Status);
        Assert.Equal("Every 12 months", history.CalibrationIntervalDescription);
        Assert.Equal("Every 3 months", history.PmIntervalDescription);

        // 2. Baselines with "Baseline" label
        Assert.NotNull(history.CalibrationBaseline);
        Assert.Equal(new DateOnly(2025, 3, 10), history.CalibrationBaseline.Date);
        Assert.Equal("Baseline", history.CalibrationBaseline.Label);

        Assert.NotNull(history.PmBaseline);
        Assert.Equal(new DateOnly(2026, 1, 15), history.PmBaseline.Date);
        Assert.Equal("Baseline", history.PmBaseline.Label);

        // 3. Tasks with original and actual dates
        var calTaskItem = history.Tasks.First(t => t.Id == calTask.Id);
        Assert.Equal(new DateOnly(2026, 3, 10), calTaskItem.OriginalDueDate);
        Assert.Equal(new DateOnly(2026, 4, 14), calTaskItem.CompletedOn);
        Assert.Equal(EquipmentTaskStatus.Done, calTaskItem.Status);
        Assert.Equal("cert_binder_2026.pdf", calTaskItem.EvidenceLabel);

        // 4. Reschedules
        var resch = Assert.Single(history.Reschedules);
        Assert.Equal("Re-projected from calibration on 2026-04-14", resch.Reason);
        Assert.Equal("2026-04-10", resch.OldValue);
        Assert.Equal("2026-04-15", resch.NewValue);

        // 5. StatusChanges
        var st = Assert.Single(history.StatusChanges);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, st.PreviousStatus);
        Assert.Equal(EquipmentOperationalStatus.InService, st.NewStatus);
        Assert.Equal("Calibration recorded; returned to service", st.Comment);

        // 6. Logbook
        var log = Assert.Single(history.Logbook);
        Assert.Equal("Quarterly cleaning and seal replacement", log.WorkDone);

        // 7. Next due dates
        Assert.Equal(new DateOnly(2027, 4, 14), history.NextCalibrationDue);
        Assert.Equal(new DateOnly(2026, 4, 15), history.NextPmDue);

        // 8. Planned vs Done
        var calPlannedVsDone = await w.Service.GetPlannedVsDoneAsync(2026, EquipmentTaskType.Calibration);
        var pmPlannedVsDone = await w.Service.GetPlannedVsDoneAsync(2026, EquipmentTaskType.PreventiveMaintenance);
        Assert.Single(calPlannedVsDone);
        Assert.Equal(calTask.Id, calPlannedVsDone[0].Id);
        Assert.Equal(EquipmentTaskStatus.Done, calPlannedVsDone[0].Status);
        Assert.Single(pmPlannedVsDone);
        Assert.Equal(pmTask.Id, pmPlannedVsDone[0].Id);

        // 9. Equipment not found
        await Assert.ThrowsAsync<NotFoundException>(() => w.Service.GetInstrumentHistoryAsync(99999, 2026));
    }

    [Fact]
    public async Task Controller_endpoints_return_success()
    {
        var w = await BuildAsync();
        var controller = new MicroLIMS.API.Controllers.EquipmentCalendarController(w.Service);

        var calResult = await controller.GetCalendar(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var okCal = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(calResult);
        Assert.IsType<MicroLIMS.Shared.Responses.ApiResponse<object>>(okCal.Value);

        var yearResult = await controller.GetYear(2026);
        var okYear = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(yearResult);
        Assert.IsType<MicroLIMS.Shared.Responses.ApiResponse<object>>(okYear.Value);

        var histResult = await controller.GetInstrumentHistory(w.Equipment.Id, 2026);
        var okHist = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(histResult);
        Assert.IsType<MicroLIMS.Shared.Responses.ApiResponse<object>>(okHist.Value);

        var pvdResult = await controller.GetPlannedVsDone(2026, EquipmentTaskType.Calibration);
        var okPvd = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(pvdResult);
        Assert.IsType<MicroLIMS.Shared.Responses.ApiResponse<object>>(okPvd.Value);
    }
}
