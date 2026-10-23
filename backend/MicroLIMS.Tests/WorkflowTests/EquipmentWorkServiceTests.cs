using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class EquipmentWorkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 15, 8, 0, 0, TimeSpan.Zero);
    private const string Password = "Password123!";

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public LabClock Clock = null!;
        public EquipmentWorkService Service = null!;
        public User Analyst = null!;
        public EquipmentInventory Equipment = null!;
        public Equipment MasterEquipment = null!;
        public EquipmentDocument Certificate = null!;
        public EquipmentInventory Equipment2 = null!;
        public EquipmentDocument Certificate2 = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        var clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var role = new Role
        {
            Name = "Analyst",
            Type = RoleType.Analyst,
            IsSystemRole = true,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var perm = new Permission
        {
            Code = PermissionConstants.EquipmentWorkRecord,
            Description = "Record equipment work"
        };
        db.Permissions.Add(perm);
        await db.SaveChangesAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });

        var analyst = new User
        {
            Username = "analyst",
            FullName = "Analyst User",
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
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

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = analyst.Id,
            DepartmentId = section.DepartmentId,
            SectionId = section.Id
        });
        await db.SaveChangesAsync();

        var eq = new EquipmentInventory
        {
            Code = "INC-01",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Location = "Room 101",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 3, 10),
            CalibrationDueDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            PmIntervalValue = 1,
            PmIntervalUnit = IntervalUnit.Months,
            LastPmOn = new DateOnly(2026, 4, 1),
            SectionId = section.Id
        };
        db.EquipmentInventories.Add(eq);

        var eq2 = new EquipmentInventory
        {
            Code = "INC-02",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Location = "Room 102",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 3, 10),
            SectionId = section.Id
        };
        db.EquipmentInventories.Add(eq2);

        var master = new Equipment
        {
            Code = "INC-01",
            Name = "Incubator INC-01",
            Type = EquipmentType.Incubator,
            CalibrationDueDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            SectionId = section.Id
        };
        db.Equipment.Add(master);
        await db.SaveChangesAsync();

        var cert1 = new EquipmentDocument
        {
            EquipmentInventoryId = eq.Id,
            DocumentType = EquipmentDocumentType.CalibrationCertificate,
            OriginalFileName = "cert1.pdf",
            StorageKey = $"equipment-documents/{eq.Id}/cert1.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "dummy-sha-1",
            UploadedByUserId = analyst.Id,
            Status = MaterialDocumentStatus.Current,
            UploadedAt = Now.UtcDateTime
        };
        var cert2 = new EquipmentDocument
        {
            EquipmentInventoryId = eq2.Id,
            DocumentType = EquipmentDocumentType.CalibrationCertificate,
            OriginalFileName = "cert2.pdf",
            StorageKey = $"equipment-documents/{eq2.Id}/cert2.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "dummy-sha-2",
            UploadedByUserId = analyst.Id,
            Status = MaterialDocumentStatus.Current,
            UploadedAt = Now.UtcDateTime
        };
        db.EquipmentDocuments.AddRange(cert1, cert2);
        await db.SaveChangesAsync();

        var signatures = new ElectronicSignatureService(db);
        var service = new EquipmentWorkService(db, clock, signatures);

        return new World
        {
            Db = db,
            Clock = clock,
            Service = service,
            Analyst = analyst,
            Equipment = eq,
            MasterEquipment = master,
            Certificate = cert1,
            Equipment2 = eq2,
            Certificate2 = cert2
        };
    }

    [Fact]
    public async Task Calibration_rejects_certificate_of_other_instrument()
    {
        var w = await BuildAsync();
        var req = new RecordCalibrationRequest(w.Certificate2.Id, new DateOnly(2026, 4, 14), null, Password);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null));
        Assert.Contains("different instrument", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Calibration_rejects_future_date()
    {
        var w = await BuildAsync();
        var tomorrow = w.Clock.LabToday.AddDays(1);
        var req = new RecordCalibrationRequest(w.Certificate.Id, tomorrow, null, Password);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null));
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Calibration_rejects_date_before_previous_actual()
    {
        var w = await BuildAsync();
        // w.Equipment.LastCalibratedOn is 2025-03-10
        var before = new DateOnly(2025, 3, 9);
        var req = new RecordCalibrationRequest(w.Certificate.Id, before, null, Password);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null));
        Assert.Contains("previous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Late_calibration_reprojects_from_actual()
    {
        var w = await BuildAsync();

        var t1 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 10)
        };
        var t2 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2027,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2027, 3, 10),
            DueDate = new DateOnly(2027, 3, 10)
        };
        w.Db.EquipmentTasks.AddRange(t1, t2);
        await w.Db.SaveChangesAsync();

        var req = new RecordCalibrationRequest(w.Certificate.Id, new DateOnly(2026, 4, 14), null, Password);
        await w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        Assert.Equal(new DateOnly(2026, 4, 14), t1.CompletedOn);
        Assert.Equal(w.Certificate.Id, t1.CalibrationDocumentId);

        // 2026-04-14 + 12 months = 2027-04-14 (Wednesday, working day)
        Assert.Equal(new DateOnly(2027, 4, 14), t2.DueDate);
        Assert.Equal(new DateOnly(2027, 3, 10), t2.OriginalDueDate);

        var rescheduleEvent = await w.Db.EquipmentTaskEvents
            .FirstOrDefaultAsync(e => e.EquipmentTaskId == t2.Id && e.EventType == EquipmentTaskEventType.Rescheduled);
        Assert.NotNull(rescheduleEvent);
        Assert.Equal("2027-03-10", rescheduleEvent.OldValue);
        Assert.Equal("2027-04-14", rescheduleEvent.NewValue);
    }

    [Fact]
    public async Task Recording_ignores_draft_tasks()
    {
        var w = await BuildAsync();

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
            OriginalDueDate = new DateOnly(2026, 4, 10),
            DueDate = new DateOnly(2026, 4, 10),
            CreatedByRevisionId = draftRev.Id
        };
        w.Db.EquipmentTasks.Add(draftTask);
        await w.Db.SaveChangesAsync();

        var req = new RecordCalibrationRequest(w.Certificate.Id, new DateOnly(2026, 4, 14), null, Password);
        await w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        // The draft task should NOT have been completed
        var reloadedDraft = await w.Db.EquipmentTasks.FindAsync(draftTask.Id);
        Assert.NotNull(reloadedDraft);
        Assert.Null(reloadedDraft!.CompletedOn);
        Assert.Null(reloadedDraft.CalibrationDocumentId);

        // A new unplanned task should have been created and completed
        var unplannedTask = await w.Db.EquipmentTasks
            .FirstOrDefaultAsync(t => t.EquipmentInventoryId == w.Equipment.Id
                                   && t.Id != draftTask.Id
                                   && t.TaskType == EquipmentTaskType.Calibration);

        Assert.NotNull(unplannedTask);
        Assert.True(unplannedTask!.IsUnplanned);
        Assert.Equal(new DateOnly(2026, 4, 14), unplannedTask.CompletedOn);
        Assert.Equal(w.Certificate.Id, unplannedTask.CalibrationDocumentId);
    }

    [Fact]
    public async Task Certificate_next_due_wins()
    {
        var w = await BuildAsync();

        var t1 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 10)
        };
        var t2 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2027,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2027, 3, 10),
            DueDate = new DateOnly(2027, 3, 10)
        };
        w.Db.EquipmentTasks.AddRange(t1, t2);
        await w.Db.SaveChangesAsync();

        var certNextDue = new DateOnly(2027, 2, 15);
        var req = new RecordCalibrationRequest(w.Certificate.Id, new DateOnly(2026, 3, 10), certNextDue, Password);
        await w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        Assert.Equal(certNextDue, t2.DueDate);
        Assert.Equal(new DateTime(2027, 2, 15, 0, 0, 0, DateTimeKind.Utc), w.Equipment.CalibrationDueDate);
    }

    [Fact]
    public async Task Calibration_updates_register_and_equipment_due_dates()
    {
        var w = await BuildAsync();

        var t1 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 10)
        };
        w.Db.EquipmentTasks.Add(t1);
        await w.Db.SaveChangesAsync();

        var req = new RecordCalibrationRequest(w.Certificate.Id, new DateOnly(2026, 4, 14), null, Password);
        await w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        var expectedDate = new DateTime(2027, 4, 14, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, w.Equipment.CalibrationDueDate);

        var master = await w.Db.Equipment.FindAsync(w.MasterEquipment.Id);
        Assert.NotNull(master);
        Assert.Equal(expectedDate, master.CalibrationDueDate);
    }

    [Fact]
    public async Task Calibration_returns_auto_out_of_service_to_service()
    {
        var w = await BuildAsync();
        w.Equipment.Status = EquipmentOperationalStatus.OutOfService;
        w.Equipment.AutoOutOfServiceSince = new DateOnly(2026, 3, 11);
        await w.Db.SaveChangesAsync();

        var req = new RecordCalibrationRequest(w.Certificate.Id, new DateOnly(2026, 4, 14), null, Password);
        await w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        Assert.Equal(EquipmentOperationalStatus.InService, w.Equipment.Status);
        Assert.Null(w.Equipment.AutoOutOfServiceSince);

        var history = await w.Db.EquipmentStatusHistories
            .FirstOrDefaultAsync(h => h.EquipmentInventoryId == w.Equipment.Id);
        Assert.NotNull(history);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, history.PreviousStatus);
        Assert.Equal(EquipmentOperationalStatus.InService, history.NewStatus);
        Assert.Equal("Calibration recorded; returned to service", history.Comment);
        Assert.Equal(w.Analyst.Id, history.ChangedByUserId);
    }

    [Fact]
    public async Task Pm_entry_closes_task_and_reprojects()
    {
        var w = await BuildAsync();

        var pm1 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 30),
            DueDate = new DateOnly(2026, 4, 30)
        };
        var pm2 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 2,
            OriginalDueDate = new DateOnly(2026, 5, 31),
            DueDate = new DateOnly(2026, 5, 31)
        };
        w.Db.EquipmentTasks.AddRange(pm1, pm2);
        await w.Db.SaveChangesAsync();

        var performedOn = new DateOnly(2026, 4, 10);
        var req = new AddLogEntryRequest(
            MaintenanceEntryType.PreventiveMaintenance,
            performedOn,
            "Monthly cleaning and filter check",
            "Filter mesh",
            "No abnormalities",
            null,
            Password);

        var result = await w.Service.AddLogEntryAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        Assert.NotNull(result);
        Assert.Equal(performedOn, pm1.CompletedOn);
        Assert.Equal(result.Id, pm1.MaintenanceLogEntryId);

        // Reprojected: 2026-04-10 + 1 month = 2026-05-10 (Sunday, working day)
        Assert.Equal(new DateOnly(2026, 5, 10), pm2.DueDate);
        Assert.Equal(new DateOnly(2026, 5, 31), pm2.OriginalDueDate);
    }

    [Fact]
    public async Task Breakdown_entry_closes_nothing()
    {
        var w = await BuildAsync();

        var pm1 = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 30),
            DueDate = new DateOnly(2026, 4, 30)
        };
        w.Db.EquipmentTasks.Add(pm1);
        await w.Db.SaveChangesAsync();

        var req = new AddLogEntryRequest(
            MaintenanceEntryType.BreakdownRepair,
            new DateOnly(2026, 4, 10),
            "Replaced heating element",
            "Heater element #3",
            "Functional now",
            null,
            Password);

        var result = await w.Service.AddLogEntryAsync(w.Equipment.Id, req, w.Analyst.Id, null);

        Assert.NotNull(result);
        Assert.Null(pm1.CompletedOn);
        Assert.Null(pm1.MaintenanceLogEntryId);
        Assert.Equal(new DateOnly(2026, 4, 30), pm1.DueDate);
    }

    [Fact]
    public async Task Void_requires_reason()
    {
        var w = await BuildAsync();

        var pmTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 30),
            DueDate = new DateOnly(2026, 4, 30)
        };
        w.Db.EquipmentTasks.Add(pmTask);
        await w.Db.SaveChangesAsync();

        var entry = new MaintenanceLogEntry
        {
            EquipmentInventoryId = w.Equipment.Id,
            EntryType = MaintenanceEntryType.PreventiveMaintenance,
            PerformedOn = new DateOnly(2026, 4, 10),
            PerformedByUserId = w.Analyst.Id,
            WorkDone = "Monthly PM",
            RecordedAt = w.Clock.UtcNow.UtcDateTime,
            ElectronicSignatureId = 1
        };
        w.Db.MaintenanceLogEntries.Add(entry);
        await w.Db.SaveChangesAsync();

        pmTask.CompletedOn = entry.PerformedOn;
        pmTask.MaintenanceLogEntryId = entry.Id;
        await w.Db.SaveChangesAsync();

        // Reason empty or whitespace throws
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.VoidLogEntryAsync(entry.Id, "", Password, w.Analyst.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.VoidLogEntryAsync(entry.Id, "   ", Password, w.Analyst.Id, null));

        // Valid reason succeeds and reopens task
        await w.Service.VoidLogEntryAsync(entry.Id, "Mistyped entry against wrong equipment", Password, w.Analyst.Id, null);

        var updated = await w.Db.MaintenanceLogEntries.FindAsync(entry.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.VoidedAt);
        Assert.Equal("Mistyped entry against wrong equipment", updated.VoidReason);
        Assert.Equal(w.Analyst.Id, updated.VoidedByUserId);

        // PM task reopened because no later PM entry exists
        var reloadedTask = await w.Db.EquipmentTasks.FindAsync(pmTask.Id);
        Assert.NotNull(reloadedTask);
        Assert.Null(reloadedTask.CompletedOn);
        Assert.Null(reloadedTask.MaintenanceLogEntryId);
    }

    [Fact]
    public async Task Overdue_calibration_sets_out_of_service_once()
    {
        var w = await BuildAsync();
        var today = w.Clock.LabToday; // 2026-04-15

        var changed = await w.Service.ApplyOverdueAsync(today);
        Assert.Equal(1, changed);

        var eq = await w.Db.EquipmentInventories.FindAsync(w.Equipment.Id);
        Assert.NotNull(eq);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, eq.Status);
        Assert.Equal(today, eq.AutoOutOfServiceSince);

        var history = await w.Db.EquipmentStatusHistories
            .Where(h => h.EquipmentInventoryId == w.Equipment.Id)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(history);
        Assert.Equal(EquipmentOperationalStatus.InService, history.PreviousStatus);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, history.NewStatus);
        Assert.Equal("Calibration overdue since 2026-03-10 (automatic)", history.Comment);
        Assert.True(history.ChangedByUserId > 0);

        // Second call is idempotent and changes nothing
        var changedSecond = await w.Service.ApplyOverdueAsync(today);
        Assert.Equal(0, changedSecond);
    }

    [Fact]
    public async Task Manual_status_change_clears_auto_flag_so_certificate_does_not_restore()
    {
        var w = await BuildAsync();
        var today = w.Clock.LabToday; // 2026-04-15

        // Automatic overdue sets it out of service and sets AutoOutOfServiceSince
        var changed = await w.Service.ApplyOverdueAsync(today);
        Assert.Equal(1, changed);

        var eq = await w.Db.EquipmentInventories.FindAsync(w.Equipment.Id);
        Assert.NotNull(eq);
        Assert.Equal(today, eq.AutoOutOfServiceSince);

        // Manual status update through EquipmentInventoryService.UpdateAsync
        var inventoryService = new EquipmentInventoryService(w.Db, new UserSectionScopeService(w.Db));
        var updateRequest = new SaveEquipmentInventoryRequest(
            eq.InstrumentType,
            eq.ManufacturerName,
            eq.SerialNumber,
            eq.FirmwareVersion,
            eq.Code,
            eq.Location,
            eq.CalibrationDueDate,
            EquipmentOperationalStatus.OutOfService,
            StatusChangeComment: "Manual confirmation: keeping out of service for repair",
            SectionId: eq.SectionId);
        await inventoryService.UpdateAsync(eq.Id, updateRequest, w.Analyst.Id);

        // AutoOutOfServiceSince is now cleared
        eq = await w.Db.EquipmentInventories.FindAsync(w.Equipment.Id);
        Assert.NotNull(eq);
        Assert.Null(eq.AutoOutOfServiceSince);

        // Record calibration with certificate
        var calRequest = new RecordCalibrationRequest(
            w.Certificate.Id,
            new DateOnly(2026, 4, 15),
            null,
            Password);
        await w.Service.RecordCalibrationAsync(eq.Id, calRequest, w.Analyst.Id, "127.0.0.1");

        // Status is STILL OutOfService (certificate did not restore it)
        eq = await w.Db.EquipmentInventories.FindAsync(w.Equipment.Id);
        Assert.NotNull(eq);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, eq.Status);
    }

    [Fact]
    public async Task Overdue_pm_does_not_change_status()
    {
        var w = await BuildAsync();
        var today = w.Clock.LabToday; // 2026-04-15

        // Set calibration due date in the future so calibration is not overdue
        w.Equipment.CalibrationDueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        w.Equipment.Status = EquipmentOperationalStatus.InService;
        w.Equipment.AutoOutOfServiceSince = null;

        // Add an overdue PM task
        var overduePmTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.PreventiveMaintenance,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 3, 15),
            DueDate = new DateOnly(2026, 3, 15),
            CompletedOn = null
        };
        w.Db.EquipmentTasks.Add(overduePmTask);
        await w.Db.SaveChangesAsync();

        var changed = await w.Service.ApplyOverdueAsync(today);
        Assert.Equal(0, changed);

        var eq = await w.Db.EquipmentInventories.FindAsync(w.Equipment.Id);
        Assert.NotNull(eq);
        Assert.Equal(EquipmentOperationalStatus.InService, eq.Status);
        Assert.Null(eq.AutoOutOfServiceSince);
    }

    [Fact]
    public async Task AddLogEntryAsync_refuses_duplicate_pm_on_same_date()
    {
        var w = await BuildAsync();
        var performedOn = new DateOnly(2026, 4, 10);

        var req1 = new AddLogEntryRequest(
            MaintenanceEntryType.PreventiveMaintenance,
            performedOn,
            "First maintenance check",
            null,
            null,
            null,
            Password);
        await w.Service.AddLogEntryAsync(w.Equipment.Id, req1, w.Analyst.Id, null);

        var req2 = new AddLogEntryRequest(
            MaintenanceEntryType.PreventiveMaintenance,
            performedOn,
            "Second maintenance check",
            null,
            null,
            null,
            Password);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.AddLogEntryAsync(w.Equipment.Id, req2, w.Analyst.Id, null));
        Assert.Equal($"A preventive maintenance entry for {w.Equipment.Code} on 2026-04-10 is already recorded.", ex.Message);
    }

    [Fact]
    public async Task AddLogEntryAsync_allows_pm_if_previous_was_voided()
    {
        var w = await BuildAsync();
        var performedOn = new DateOnly(2026, 4, 10);

        var req1 = new AddLogEntryRequest(
            MaintenanceEntryType.PreventiveMaintenance,
            performedOn,
            "First maintenance check",
            null,
            null,
            null,
            Password);
        var entry1 = await w.Service.AddLogEntryAsync(w.Equipment.Id, req1, w.Analyst.Id, null);

        await w.Service.VoidLogEntryAsync(entry1.Id, "Mistake entered", Password, w.Analyst.Id, null);

        var req2 = new AddLogEntryRequest(
            MaintenanceEntryType.PreventiveMaintenance,
            performedOn,
            "Re-done maintenance check",
            null,
            null,
            null,
            Password);
        var entry2 = await w.Service.AddLogEntryAsync(w.Equipment.Id, req2, w.Analyst.Id, null);
        Assert.NotNull(entry2);
    }

    [Fact]
    public async Task AddLogEntryAsync_refuses_exact_repeat_breakdown_or_other()
    {
        var w = await BuildAsync();
        var performedOn = new DateOnly(2026, 4, 10);

        var req1 = new AddLogEntryRequest(
            MaintenanceEntryType.BreakdownRepair,
            performedOn,
            "Replaced heating element",
            null,
            null,
            null,
            Password);
        await w.Service.AddLogEntryAsync(w.Equipment.Id, req1, w.Analyst.Id, null);

        var reqRepeat = new AddLogEntryRequest(
            MaintenanceEntryType.BreakdownRepair,
            performedOn,
            "  replaced HEATING element  ",
            null,
            null,
            null,
            Password);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.AddLogEntryAsync(w.Equipment.Id, reqRepeat, w.Analyst.Id, null));
        Assert.Equal($"This entry is already recorded for {w.Equipment.Code} on 2026-04-10.", ex.Message);

        // Different work on same date should succeed
        var reqDifferentWork = new AddLogEntryRequest(
            MaintenanceEntryType.BreakdownRepair,
            performedOn,
            "Replaced power cord",
            null,
            null,
            null,
            Password);
        var resultDifferent = await w.Service.AddLogEntryAsync(w.Equipment.Id, reqDifferentWork, w.Analyst.Id, null);
        Assert.NotNull(resultDifferent);

        // Same work with Other type should succeed (different type)
        var reqOther = new AddLogEntryRequest(
            MaintenanceEntryType.Other,
            performedOn,
            "Replaced heating element",
            null,
            null,
            null,
            Password);
        var resultOther = await w.Service.AddLogEntryAsync(w.Equipment.Id, reqOther, w.Analyst.Id, null);
        Assert.NotNull(resultOther);

        // Repeat of Other should be refused
        var reqOtherRepeat = new AddLogEntryRequest(
            MaintenanceEntryType.Other,
            performedOn,
            "replaced heating element",
            null,
            null,
            null,
            Password);
        var exOther = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.AddLogEntryAsync(w.Equipment.Id, reqOtherRepeat, w.Analyst.Id, null));
        Assert.Equal($"This entry is already recorded for {w.Equipment.Code} on 2026-04-10.", exOther.Message);
    }

    [Fact]
    public async Task AddLogEntryAsync_allows_breakdown_repeat_if_previous_voided()
    {
        var w = await BuildAsync();
        var performedOn = new DateOnly(2026, 4, 10);

        var req1 = new AddLogEntryRequest(
            MaintenanceEntryType.BreakdownRepair,
            performedOn,
            "Replaced fuse",
            null,
            null,
            null,
            Password);
        var entry1 = await w.Service.AddLogEntryAsync(w.Equipment.Id, req1, w.Analyst.Id, null);

        await w.Service.VoidLogEntryAsync(entry1.Id, "Wrong part name", Password, w.Analyst.Id, null);

        var req2 = new AddLogEntryRequest(
            MaintenanceEntryType.BreakdownRepair,
            performedOn,
            "Replaced fuse",
            null,
            null,
            null,
            Password);
        var entry2 = await w.Service.AddLogEntryAsync(w.Equipment.Id, req2, w.Analyst.Id, null);
        Assert.NotNull(entry2);
    }

    [Fact]
    public async Task RecordCalibration_refuses_same_date_as_previous_actual_with_clear_message()
    {
        var w = await BuildAsync();
        // w.Equipment.LastCalibratedOn is 2025-03-10

        var req = new RecordCalibrationRequest(
            w.Certificate.Id,
            new DateOnly(2025, 3, 10), // Same date as previous actual
            null,
            Password);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.RecordCalibrationAsync(w.Equipment.Id, req, w.Analyst.Id, null));

        Assert.Equal("Calibration date must be after the previous actual date (2025-03-10).", ex.Message);
    }
}
