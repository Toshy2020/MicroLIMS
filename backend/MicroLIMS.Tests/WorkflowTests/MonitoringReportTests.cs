using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Coverage and spacing reports, and the dashboard reminders.
public class MonitoringReportTests
{
    // Wednesday 21 Oct 2026, 09:00 in Cairo.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 6, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public MonitoringReportService Reports = null!;
        public MonitoringReminderService Reminders = null!;
        public Room Room = null!;
        public RoomTestConfiguration Config = null!;
        public MonitoringPlanRevision Revision = null!;
        public User Viewer = null!, Outsider = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var w = new World { Db = new MicroLimsDbContext(options) };
        var clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var view = new Permission { Code = PermissionConstants.MonitoringPlanView, Description = "View" };
        var viewerRole = new Role { Name = "Viewer", Type = RoleType.Analyst, IsSystemRole = true, IsActive = true };
        var otherRole = new Role { Name = "Other", Type = RoleType.Reviewer, IsSystemRole = true, IsActive = true };
        w.Db.Permissions.Add(view);
        w.Db.Roles.AddRange(viewerRole, otherRole);
        await w.Db.SaveChangesAsync();
        w.Db.RolePermissions.Add(new RolePermission { RoleId = viewerRole.Id, PermissionId = view.Id });
        w.Viewer = new User { Username = "viewer", FullName = "Viewer", RoleId = viewerRole.Id, PasswordHash = "x" };
        w.Outsider = new User { Username = "outsider", FullName = "Outsider", RoleId = otherRole.Id, PasswordHash = "x" };
        w.Db.Users.AddRange(w.Viewer, w.Outsider);

        w.Room = new Room { Name = "Room A", Department = new Department { Name = "Filling" }, GradeClassification = "A" };
        w.Db.Rooms.Add(w.Room);
        await w.Db.SaveChangesAsync();
        w.Config = new RoomTestConfiguration { RoomId = w.Room.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.Revision = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 1, Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = w.Viewer.Id
        };
        w.Db.RoomTestConfigurations.Add(w.Config);
        w.Db.MonitoringPlanRevisions.Add(w.Revision);
        await w.Db.SaveChangesAsync();

        var status = new OccurrenceStatusService(w.Db, clock);
        w.Reports = new MonitoringReportService(w.Db, clock, status);
        var occurrences = new MonitoringOccurrenceService(w.Db, clock, status, new ElectronicSignatureService(w.Db));
        w.Reminders = new MonitoringReminderService(w.Db, occurrences);
        return w;
    }

    private static MonitoringOccurrence Occurrence(World w, string period, DateOnly start, DateOnly end, string frequency = "Weekly, Sunday") => new()
    {
        CreatedByRevisionId = w.Revision.Id,
        Category = MonitoringCategory.EnvironmentalMonitoring,
        TargetType = MonitoringTargetType.Room,
        RoomId = w.Room.Id,
        PeriodKey = period,
        FrequencySnapshot = frequency,
        TargetDate = start,
        OriginalTargetDate = start,
        WindowStart = start,
        WindowEnd = end,
        OriginalWindowEnd = end,
        Items = new List<MonitoringOccurrenceItem> { new() { RoomTestConfigurationId = w.Config.Id } }
    };

    // An approved test, sampled on the given date, linked to the occurrence.
    private static async Task FulfilAsync(World w, MonitoringOccurrence occurrence, DateOnly on)
    {
        var at = on.ToDateTime(new TimeOnly(10, 0)).AddHours(-3); // 10:00 Cairo is 07:00 UTC in summer time
        var sample = new Sample
        {
            ReferenceNumber = $"EM-{occurrence.Id}", Category = SampleCategory.EnvironmentalMonitoring,
            ReceivedAt = at, SampledAt = at
        };
        w.Db.Samples.Add(sample);
        await w.Db.SaveChangesAsync();
        var order = new TestOrder
        {
            SampleId = sample.Id, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved
        };
        w.Db.TestOrders.Add(order);
        await w.Db.SaveChangesAsync();
        w.Db.SampleLocations.Add(new SampleLocation
        {
            SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.Room,
            RoomTestConfigurationId = w.Config.Id, MonitoringOccurrenceId = occurrence.Id
        });
        await w.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Coverage_CountsPerformedLateOverdueUpcomingAndSkipped_PerTarget()
    {
        var w = await BuildAsync();
        var done = Occurrence(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        var late = Occurrence(w, "2026-W41", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 8));
        var missed = Occurrence(w, "2026-W39", new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 24));
        var upcoming = Occurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        var skipped = Occurrence(w, "2026-W42", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 22));
        skipped.ExceptionState = OccurrenceExceptionState.Skipped;
        w.Db.MonitoringOccurrences.AddRange(done, late, missed, upcoming, skipped);
        await w.Db.SaveChangesAsync();
        await FulfilAsync(w, done, new DateOnly(2026, 9, 28));
        await FulfilAsync(w, late, new DateOnly(2026, 10, 12));

        var report = await w.Reports.GetCoverageAsync(2026, null);

        var row = Assert.Single(report.Rows);
        Assert.Equal("Room A", row.Target);
        Assert.Equal(4, row.Planned);
        Assert.Equal(2, row.Performed);
        Assert.Equal(1, row.PerformedLate);
        Assert.Equal(1, row.Overdue);
        Assert.Equal(1, row.Upcoming);
        Assert.Equal(1, row.Skipped);
        Assert.Equal(66.7m, row.CoveragePercent);
    }

    [Fact]
    public async Task Spacing_FlagsGapsOutsideTheWeeklyRange()
    {
        var w = await BuildAsync();
        var a = Occurrence(w, "2026-W37", new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 10));
        var b = Occurrence(w, "2026-W38", new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 17));
        var c = Occurrence(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        w.Db.MonitoringOccurrences.AddRange(a, b, c);
        await w.Db.SaveChangesAsync();
        await FulfilAsync(w, a, new DateOnly(2026, 9, 6));
        await FulfilAsync(w, b, new DateOnly(2026, 9, 13));   // 7 days: fine
        await FulfilAsync(w, c, new DateOnly(2026, 9, 29));   // 16 days: too long for a weekly series

        var all = await w.Reports.GetSpacingAsync(2026, null, false);
        var flagged = await w.Reports.GetSpacingAsync(2026, null, true);

        Assert.Equal(2, all.Rows.Count);
        var row = Assert.Single(flagged.Rows);
        Assert.Equal(16, row.GapDays);
        Assert.Equal(4, row.MinDays);
        Assert.Equal(10, row.MaxDays);
        Assert.Equal("Weekly", row.Frequency);
    }

    [Fact]
    public async Task Reminders_OnlyForPeopleWhoCanViewThePlan_AndTellOverdueOpenDeviationsAndStillDue()
    {
        var w = await BuildAsync();
        var missed = Occurrence(w, "2026-W41", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 8));    // past its grace: missed
        var overdue = Occurrence(w, "2026-W42", new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 15)); // still inside its grace
        w.Db.MonitoringOccurrences.AddRange(missed, overdue, Occurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29)));
        await w.Db.SaveChangesAsync();
        // A missed occurrence is tracked by its deviation, not counted as overdue.
        w.Db.MonitoringDeviations.Add(new MonitoringDeviation
        {
            Number = "MD-2026-00001", Kind = DeviationKind.MissedOccurrence, MonitoringOccurrenceId = missed.Id,
            Description = "Room A: nothing was sampled.", OpenedAt = new DateTime(2026, 10, 19, 6, 0, 0, DateTimeKind.Utc),
            DueDate = new DateOnly(2026, 10, 20)
        });
        w.Db.EquipmentTasks.AddRange(
            new EquipmentTask
            {
                EquipmentInventoryId = 1,
                TaskType = EquipmentTaskType.Calibration,
                Year = 2026,
                Sequence = 1,
                OriginalDueDate = new DateOnly(2026, 10, 1),
                DueDate = new DateOnly(2026, 10, 1),
                CompletedOn = null
            },
            new EquipmentTask
            {
                EquipmentInventoryId = 1,
                TaskType = EquipmentTaskType.PreventiveMaintenance,
                Year = 2026,
                Sequence = 1,
                OriginalDueDate = new DateOnly(2026, 10, 5),
                DueDate = new DateOnly(2026, 10, 5),
                CompletedOn = null
            });
        await w.Db.SaveChangesAsync();

        var forViewer = await w.Reminders.GetForUserAsync(w.Viewer.Id);
        var forOutsider = await w.Reminders.GetForUserAsync(w.Outsider.Id);

        Assert.Empty(forOutsider);
        Assert.Contains(forViewer, r => r.Type == "MonitoringOverdue" && r.Message.StartsWith("1 "));
        Assert.Contains(forViewer, r => r.Type == "MonitoringDeviationsOpen" && r.Message.StartsWith("1 ") && r.Message.Contains("1 past their due date"));
        // The settings default warning day (20) has passed on the 21st.
        Assert.Contains(forViewer, r => r.Type == "MonitoringDueThisMonth" && r.Message.StartsWith("1 "));
        Assert.Contains(forViewer, r => r.Type == "EquipmentCalibrationOverdue" && r.Message.StartsWith("1 "));
        Assert.Contains(forViewer, r => r.Type == "EquipmentPmOverdue" && r.Message.StartsWith("1 "));
    }
}
