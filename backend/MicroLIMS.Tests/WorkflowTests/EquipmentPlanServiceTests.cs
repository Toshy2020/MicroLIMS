using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class EquipmentPlanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private const string Password = "Correct-horse-1";

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public EquipmentPlanService Service = null!;
        public User Generator = null!;
        public User Approver = null!;
    }

    private static async Task<World> BuildAsync(DateTimeOffset? now = null)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var w = new World { Db = new MicroLimsDbContext(options) };
        var clock = new LabClock(new FixedTimeProvider(now ?? Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var role = new Role { Name = "SectionHead", Type = RoleType.SectionHead, IsSystemRole = true, IsActive = true };
        w.Db.Roles.Add(role);
        await w.Db.SaveChangesAsync();

        w.Generator = new User
        {
            Username = "generator",
            FullName = "Plan Generator",
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
        };
        w.Approver = new User
        {
            Username = "approver",
            FullName = "Plan Approver",
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
        };
        w.Db.Users.AddRange(w.Generator, w.Approver);

        w.Db.MonitoringSettings.Add(new MonitoringSettings
        {
            WorkingWeekdays = 79 // Sun-Thu
        });
        await w.Db.SaveChangesAsync();

        w.Service = new EquipmentPlanService(w.Db, clock, new ElectronicSignatureService(w.Db));
        return w;
    }

    [Fact]
    public async Task Draft_excludes_instrument_without_baseline_and_reports_exception()
    {
        var w = await BuildAsync();

        var eq1 = new EquipmentInventory
        {
            Code = "EQ-NOBASE-CAL",
            InstrumentType = "Balance",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 6,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = null
        };

        var eq2 = new EquipmentInventory
        {
            Code = "EQ-NOBASE-PM",
            InstrumentType = "Centrifuge",
            Status = EquipmentOperationalStatus.InService,
            PmIntervalValue = 3,
            PmIntervalUnit = IntervalUnit.Months,
            LastPmOn = null
        };

        var eq3 = new EquipmentInventory
        {
            Code = "EQ-VALID",
            InstrumentType = "Incubator",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 6, 1)
        };

        w.Db.EquipmentInventories.AddRange(eq1, eq2, eq3);
        await w.Db.SaveChangesAsync();

        var draft = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));

        Assert.DoesNotContain(draft.Tasks, t => t.EquipmentInventoryId == eq1.Id);
        Assert.DoesNotContain(draft.Tasks, t => t.EquipmentInventoryId == eq2.Id);
        Assert.Contains(draft.Tasks, t => t.EquipmentInventoryId == eq3.Id);

        var calEx = Assert.Single(draft.Exceptions, e => e.EquipmentInventoryId == eq1.Id && e.TaskType == EquipmentTaskType.Calibration);
        Assert.Equal(EquipmentPlanExceptionKind.NoBaseline, calEx.Kind);
        Assert.Equal("No last calibration date", calEx.Message);

        var pmEx = Assert.Single(draft.Exceptions, e => e.EquipmentInventoryId == eq2.Id && e.TaskType == EquipmentTaskType.PreventiveMaintenance);
        Assert.Equal(EquipmentPlanExceptionKind.NoBaseline, pmEx.Kind);
        Assert.Equal("No last maintenance date", pmEx.Message);
    }

    [Fact]
    public async Task Draft_projects_monthly_and_weekly()
    {
        var w = await BuildAsync();

        // 6-month calibration from 2026-03-10 with go-live 2026-01-01 -> due 2026-09-10 only
        var eq1 = new EquipmentInventory
        {
            Code = "EQ-CAL-6M",
            InstrumentType = "HPLC",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 6,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2026, 3, 10)
        };

        // Weekly PM from 2025-12-28
        var eq2 = new EquipmentInventory
        {
            Code = "EQ-PM-1W",
            InstrumentType = "Autoclave",
            Status = EquipmentOperationalStatus.InService,
            PmIntervalValue = 1,
            PmIntervalUnit = IntervalUnit.Weeks,
            LastPmOn = new DateOnly(2025, 12, 28)
        };

        w.Db.EquipmentInventories.AddRange(eq1, eq2);
        await w.Db.SaveChangesAsync();

        var draft = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));

        var calTasks = draft.Tasks.Where(t => t.EquipmentInventoryId == eq1.Id && t.TaskType == EquipmentTaskType.Calibration).ToList();
        var calTask = Assert.Single(calTasks);
        Assert.Equal(new DateOnly(2026, 9, 10), calTask.DueDate);
        Assert.Equal(new DateOnly(2026, 9, 10), calTask.OriginalDueDate);
        Assert.Equal(1, calTask.Sequence);

        var pmTasks = draft.Tasks.Where(t => t.EquipmentInventoryId == eq2.Id && t.TaskType == EquipmentTaskType.PreventiveMaintenance).ToList();
        Assert.Equal(52, pmTasks.Count);
        Assert.All(pmTasks, t =>
        {
            Assert.Equal(2026, t.DueDate.Year);
            Assert.NotEqual(DayOfWeek.Friday, t.DueDate.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Saturday, t.DueDate.DayOfWeek);
        });
    }

    [Fact]
    public async Task Approve_rejects_same_user()
    {
        var w = await BuildAsync();

        var eq = new EquipmentInventory
        {
            Code = "EQ-1",
            InstrumentType = "Balance",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 6, 1)
        };
        w.Db.EquipmentInventories.Add(eq);
        await w.Db.SaveChangesAsync();

        var draft = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ApproveAsync(draft.Revision.Id, new ApprovePlanRequest(Password, null), w.Generator.Id, null));
        Assert.Equal("The plan must be approved by someone other than the person who generated it.", ex.Message);

        var approved = await w.Service.ApproveAsync(draft.Revision.Id, new ApprovePlanRequest(Password, null), w.Approver.Id, null);
        Assert.Equal(PlanRevisionStatus.Approved, approved.Status);
    }

    [Fact]
    public async Task Approve_requires_reason_for_second_revision()
    {
        var w = await BuildAsync();

        var eq = new EquipmentInventory
        {
            Code = "EQ-1",
            InstrumentType = "Balance",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 6, 1)
        };
        w.Db.EquipmentInventories.Add(eq);
        await w.Db.SaveChangesAsync();

        var draft1 = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));
        await w.Service.ApproveAsync(draft1.Revision.Id, new ApprovePlanRequest(Password, null), w.Approver.Id, null);

        // Add a new instrument so master data has changed
        var eq2 = new EquipmentInventory
        {
            Code = "EQ-2",
            InstrumentType = "pH Meter",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 6,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 11, 1)
        };
        w.Db.EquipmentInventories.Add(eq2);
        await w.Db.SaveChangesAsync();

        var draft2 = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));

        var exNull = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ApproveAsync(draft2.Revision.Id, new ApprovePlanRequest(Password, null), w.Approver.Id, null));
        Assert.Equal("A reason is required to approve a revision of a year that already has an approved plan.", exNull.Message);

        var exEmpty = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.ApproveAsync(draft2.Revision.Id, new ApprovePlanRequest(Password, "   "), w.Approver.Id, null));
        Assert.Equal("A reason is required to approve a revision of a year that already has an approved plan.", exEmpty.Message);

        var approved2 = await w.Service.ApproveAsync(draft2.Revision.Id, new ApprovePlanRequest(Password, "Added pH Meter"), w.Approver.Id, null);
        Assert.Equal(PlanRevisionStatus.Approved, approved2.Status);
        Assert.Equal("Added pH Meter", approved2.Reason);
    }

    [Fact]
    public async Task Second_revision_keeps_original_due_date_and_records_rescheduled_event()
    {
        var w = await BuildAsync();

        // 2025-04-15 + 12 months = 2026-04-15 (Wednesday, normal working day)
        var eq = new EquipmentInventory
        {
            Code = "EQ-RESCH-01",
            InstrumentType = "Oven",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 4, 15)
        };
        w.Db.EquipmentInventories.Add(eq);
        await w.Db.SaveChangesAsync();

        var draft1 = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));
        await w.Service.ApproveAsync(draft1.Revision.Id, new ApprovePlanRequest(Password, null), w.Approver.Id, null);

        var taskBefore = await w.Db.EquipmentTasks.SingleAsync(t => t.EquipmentInventoryId == eq.Id);
        Assert.Equal(new DateOnly(2026, 4, 15), taskBefore.OriginalDueDate);
        Assert.Equal(new DateOnly(2026, 4, 15), taskBefore.DueDate);

        // Add 2026-04-15 as a public holiday -> moves back to previous working day 2026-04-14 (Tuesday)
        w.Db.LabCalendarDates.Add(new LabCalendarDate
        {
            Date = new DateOnly(2026, 4, 15),
            Description = "Spring Holiday"
        });
        await w.Db.SaveChangesAsync();

        var draft2 = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));
        var resched = Assert.Single(draft2.ToReschedule, r => r.TaskId == taskBefore.Id);
        Assert.Equal(new DateOnly(2026, 4, 15), resched.OldDueDate);
        Assert.Equal(new DateOnly(2026, 4, 14), resched.NewDueDate);

        await w.Service.ApproveAsync(draft2.Revision.Id, new ApprovePlanRequest(Password, "Holiday shift"), w.Approver.Id, null);

        var taskAfter = await w.Db.EquipmentTasks.Include(t => t.Events).SingleAsync(t => t.Id == taskBefore.Id);
        Assert.Equal(new DateOnly(2026, 4, 15), taskAfter.OriginalDueDate); // Kept frozen!
        Assert.Equal(new DateOnly(2026, 4, 14), taskAfter.DueDate); // Updated!

        var evt = Assert.Single(taskAfter.Events, e => e.EventType == EquipmentTaskEventType.Rescheduled);
        Assert.Equal("2026-04-15", evt.OldValue);
        Assert.Equal("2026-04-14", evt.NewValue);
        Assert.Equal("Holiday shift", evt.Reason);
        Assert.Equal(w.Approver.Id, evt.PerformedByUserId);
        Assert.NotNull(evt.ElectronicSignatureId);
    }

    [Fact]
    public async Task Retired_instrument_tasks_are_retired_not_deleted()
    {
        var w = await BuildAsync();

        var eq = new EquipmentInventory
        {
            Code = "EQ-RET-01",
            InstrumentType = "Incubator",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            LastCalibratedOn = new DateOnly(2025, 10, 15)
        };
        w.Db.EquipmentInventories.Add(eq);
        await w.Db.SaveChangesAsync();

        var draft1 = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));
        await w.Service.ApproveAsync(draft1.Revision.Id, new ApprovePlanRequest(Password, null), w.Approver.Id, null);

        var taskId = (await w.Db.EquipmentTasks.SingleAsync(t => t.EquipmentInventoryId == eq.Id)).Id;

        // Retire the instrument
        eq.Status = EquipmentOperationalStatus.Retired;
        await w.Db.SaveChangesAsync();

        var draft2 = await w.Service.GenerateDraftAsync(2026, w.Generator.Id, new DateOnly(2026, 1, 1));
        var ret = Assert.Single(draft2.ToRetire, r => r.TaskId == taskId);
        Assert.Equal(taskId, ret.TaskId);

        await w.Service.ApproveAsync(draft2.Revision.Id, new ApprovePlanRequest(Password, "Decommissioned instrument"), w.Approver.Id, null);

        var taskInDb = await w.Db.EquipmentTasks.Include(t => t.Events).SingleOrDefaultAsync(t => t.Id == taskId);
        Assert.NotNull(taskInDb); // Not deleted!
        Assert.Equal(draft2.Revision.Id, taskInDb.RetiredByRevisionId);

        var evt = Assert.Single(taskInDb.Events, e => e.EventType == EquipmentTaskEventType.Retired);
        Assert.Equal("Decommissioned instrument", evt.Reason);
        Assert.Equal(w.Approver.Id, evt.PerformedByUserId);
        Assert.NotNull(evt.ElectronicSignatureId);
    }
}
