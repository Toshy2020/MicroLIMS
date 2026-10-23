using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class EquipmentBaselineServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);
    private const int UserId = 42;

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public LabClock Clock = null!;
        public EquipmentBaselineService Service = null!;
        public EquipmentInventory Equipment = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        var clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));
        var service = new EquipmentBaselineService(db, clock);

        var equipment = new EquipmentInventory
        {
            Code = "EQ-100",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Location = "Microbiology Lab",
            Status = EquipmentOperationalStatus.InService
        };
        db.EquipmentInventories.Add(equipment);
        await db.SaveChangesAsync();

        return new World
        {
            Db = db,
            Clock = clock,
            Service = service,
            Equipment = equipment
        };
    }

    [Fact]
    public async Task Rejects_value_without_unit()
    {
        var w = await BuildAsync();

        // Calibration value without unit
        var reqCal = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 6,
            CalibrationIntervalUnit: null,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqCal, w.Equipment.Version, UserId));

        // PM value without unit
        var reqPm = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: null,
            CalibrationIntervalUnit: null,
            LastCalibratedOn: null,
            PmIntervalValue: 4,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqPm, w.Equipment.Version, UserId));

        // Unit without value
        var reqCalUnitOnly = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: null,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqCalUnitOnly, w.Equipment.Version, UserId));
    }

    [Fact]
    public async Task Rejects_future_baseline()
    {
        var w = await BuildAsync();
        var tomorrow = w.Clock.LabToday.AddDays(1);

        var reqCalFuture = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 6,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: tomorrow,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqCalFuture, w.Equipment.Version, UserId));

        var reqPmFuture = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: null,
            CalibrationIntervalUnit: null,
            LastCalibratedOn: null,
            PmIntervalValue: 1,
            PmIntervalUnit: IntervalUnit.Months,
            LastPmOn: tomorrow);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqPmFuture, w.Equipment.Version, UserId));
    }

    [Fact]
    public async Task Rejects_weeks_over_104()
    {
        var w = await BuildAsync();

        var req = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 105,
            CalibrationIntervalUnit: IntervalUnit.Weeks,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, req, w.Equipment.Version, UserId));

        var reqZero = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 0,
            CalibrationIntervalUnit: IntervalUnit.Weeks,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqZero, w.Equipment.Version, UserId));

        var reqMonthsOver60 = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 61,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqMonthsOver60, w.Equipment.Version, UserId));
    }

    [Fact]
    public async Task Saves_and_audits_change()
    {
        var w = await BuildAsync();
        var baseline = w.Clock.LabToday.AddMonths(-2);

        var req = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 6,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: baseline,
            PmIntervalValue: 2,
            PmIntervalUnit: IntervalUnit.Weeks,
            LastPmOn: baseline);

        var result = await w.Service.SaveScheduleAsync(w.Equipment.Id, req, w.Equipment.Version, UserId);

        Assert.NotNull(result);
        Assert.Equal(6, result.CalibrationIntervalValue);
        Assert.Equal(IntervalUnit.Months, result.CalibrationIntervalUnit);
        Assert.Equal(baseline, result.LastCalibratedOn);
        Assert.Equal(2, result.PmIntervalValue);
        Assert.Equal(IntervalUnit.Weeks, result.PmIntervalUnit);
        Assert.Equal(baseline, result.LastPmOn);

        // Verify entity persisted in DB
        var saved = await w.Db.EquipmentInventories.FindAsync(w.Equipment.Id);
        Assert.NotNull(saved);
        Assert.Equal(6, saved!.CalibrationIntervalValue);
        Assert.Equal(IntervalUnit.Months, saved.CalibrationIntervalUnit);
        Assert.Equal(baseline, saved.LastCalibratedOn);
        Assert.Equal(UserId, saved.LastModifiedByUserId);

        // Verify audit log row contains "Every 6 months"
        var audit = await w.Db.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityName == nameof(EquipmentInventory) &&
                                      a.EntityId == w.Equipment.Id.ToString() &&
                                      a.NewValue != null &&
                                      a.NewValue.Contains("Every 6 months"));
        Assert.NotNull(audit);
        Assert.Contains("Every 6 months", audit!.NewValue);
        Assert.Equal(UserId, audit.UserId);
    }

    [Fact]
    public async Task Rejects_baseline_edit_after_completed_calibration()
    {
        var w = await BuildAsync();
        var initialBaseline = new DateOnly(2026, 1, 15);
        w.Equipment.CalibrationIntervalValue = 6;
        w.Equipment.CalibrationIntervalUnit = IntervalUnit.Months;
        w.Equipment.LastCalibratedOn = initialBaseline;
        await w.Db.SaveChangesAsync();

        // Add a completed calibration task
        var completedTask = new EquipmentTask
        {
            EquipmentInventoryId = w.Equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 7, 15),
            DueDate = new DateOnly(2026, 7, 15),
            CompletedOn = new DateOnly(2026, 7, 10),
            IntervalSnapshot = "Every 6 months"
        };
        w.Db.EquipmentTasks.Add(completedTask);
        await w.Db.SaveChangesAsync();

        // Attempt to edit LastCalibratedOn
        var reqEdit = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 6,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: new DateOnly(2026, 2, 1), // Changed!
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqEdit, w.Equipment.Version, UserId));

        // Leaving LastCalibratedOn unchanged should succeed
        var reqKeep = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 12,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: initialBaseline, // Unchanged
            PmIntervalValue: 1,
            PmIntervalUnit: IntervalUnit.Months,
            LastPmOn: null);

        var result = await w.Service.SaveScheduleAsync(w.Equipment.Id, reqKeep, w.Equipment.Version, UserId);
        Assert.Equal(12, result.CalibrationIntervalValue);
        Assert.Equal(initialBaseline, result.LastCalibratedOn);
    }

    [Fact]
    public async Task Rejects_stale_version_concurrency()
    {
        var w = await BuildAsync();
        w.Equipment.Version = 5;
        await w.Db.SaveChangesAsync();

        var req = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 6,
            CalibrationIntervalUnit: IntervalUnit.Months,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        // Version mismatch: entity is 5, caller sends 4
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, req, version: 4, UserId));
    }

    [Fact]
    public async Task Baseline_rejects_6_years()
    {
        var w = await BuildAsync();

        var reqCal = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: 6,
            CalibrationIntervalUnit: IntervalUnit.Years,
            LastCalibratedOn: null,
            PmIntervalValue: null,
            PmIntervalUnit: null,
            LastPmOn: null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqCal, w.Equipment.Version, UserId));
        Assert.Equal("Calibration interval in years must be between 1 and 5.", ex.Message);

        var reqPm = new SaveEquipmentScheduleRequest(
            CalibrationIntervalValue: null,
            CalibrationIntervalUnit: null,
            LastCalibratedOn: null,
            PmIntervalValue: 6,
            PmIntervalUnit: IntervalUnit.Years,
            LastPmOn: null);

        var exPm = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.SaveScheduleAsync(w.Equipment.Id, reqPm, w.Equipment.Version, UserId));
        Assert.Equal("Maintenance interval in years must be between 1 and 5.", exPm.Message);
    }
}
