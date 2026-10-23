using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Deviations of the monitoring plan: opened by a scan for missed occurrences
// and action-limit results, closed with a reason and two signatures.
public class MonitoringDeviationServiceTests
{
    // Sunday 25 Oct 2026, 09:00 in Cairo. Grace period is 7 days, so a window
    // ending on or before Sunday 18 October is missed.
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 6, 0, 0, TimeSpan.Zero);
    private const string Password = "Correct-horse-1";

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public MonitoringDeviationService Service = null!;
        public MonitoringOccurrenceService Occurrences = null!;
        public User Head = null!, Approver = null!, Analyst = null!;
        public Room Room = null!;
        public RoomTestConfiguration Config = null!;
        public MonitoringPlanRevision Revision = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var w = new World { Db = new MicroLimsDbContext(options) };
        var clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var approve = new Permission { Code = PermissionConstants.MonitoringPlanApprove, Description = "Sign" };
        var headRole = new Role { Name = "Head", Type = RoleType.SectionHead, IsSystemRole = true, IsActive = true };
        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsSystemRole = true, IsActive = true };
        w.Db.Permissions.Add(approve);
        w.Db.Roles.AddRange(headRole, analystRole);
        await w.Db.SaveChangesAsync();
        w.Db.RolePermissions.Add(new RolePermission { RoleId = headRole.Id, PermissionId = approve.Id });

        User NewUser(string name, Role role) => new()
        {
            Username = name, FullName = name, RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
        };
        w.Head = NewUser("head", headRole);
        w.Approver = NewUser("approver", headRole);
        w.Analyst = NewUser("analyst", analystRole);
        w.Db.Users.AddRange(w.Head, w.Approver, w.Analyst);

        w.Room = new Room { Name = "Room A", Department = new Department { Name = "Filling" }, GradeClassification = "A" };
        w.Db.Rooms.Add(w.Room);
        await w.Db.SaveChangesAsync();
        w.Config = new RoomTestConfiguration { RoomId = w.Room.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.Revision = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 1, Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = w.Head.Id
        };
        w.Db.RoomTestConfigurations.Add(w.Config);
        w.Db.MonitoringPlanRevisions.Add(w.Revision);
        await w.Db.SaveChangesAsync();

        var status = new OccurrenceStatusService(w.Db, clock);
        var signatures = new ElectronicSignatureService(w.Db);
        w.Service = new MonitoringDeviationService(w.Db, clock, status, signatures);
        w.Occurrences = new MonitoringOccurrenceService(w.Db, clock, status, signatures);
        return w;
    }

    private static async Task<MonitoringOccurrence> AddOccurrenceAsync(World w, string period, DateOnly start, DateOnly end)
    {
        var o = new MonitoringOccurrence
        {
            CreatedByRevisionId = w.Revision.Id,
            Category = MonitoringCategory.EnvironmentalMonitoring,
            TargetType = MonitoringTargetType.Room,
            RoomId = w.Room.Id,
            PeriodKey = period,
            FrequencySnapshot = "Weekly (own)",
            WindowStart = start,
            WindowEnd = end,
            OriginalWindowEnd = end,
            Items = new List<MonitoringOccurrenceItem> { new() { RoomTestConfigurationId = w.Config.Id } }
        };
        w.Db.MonitoringOccurrences.Add(o);
        await w.Db.SaveChangesAsync();
        return o;
    }

    private static async Task LinkResultAsync(World w, MonitoringOccurrence o, ResultStatus result)
    {
        var at = new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc);
        var sample = new Sample { ReferenceNumber = $"EM-{o.Id}", Category = SampleCategory.EnvironmentalMonitoring, ReceivedAt = at, SampledAt = at };
        w.Db.Samples.Add(sample);
        await w.Db.SaveChangesAsync();
        var order = new TestOrder { SampleId = sample.Id, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved };
        w.Db.TestOrders.Add(order);
        await w.Db.SaveChangesAsync();
        w.Db.SampleLocations.Add(new SampleLocation
        {
            SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.Room,
            RoomTestConfigurationId = w.Config.Id, MonitoringOccurrenceId = o.Id, Status = result
        });
        await w.Db.SaveChangesAsync();
    }

    private static CloseDeviationsRequest Close(IEnumerable<int> ids, DeviationClosure closure, string reason = "Plan went live in October") =>
        new(ids.ToList(), closure, reason, Password, "approver", Password);

    // ---- opening ----

    [Fact]
    public async Task Scan_OpensOneDeviationForEachMissedOccurrence_AndIgnoresTheRest()
    {
        var w = await BuildAsync();
        var missed = await AddOccurrenceAsync(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        await AddOccurrenceAsync(w, "2026-W42", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 22)); // overdue, still in grace
        await AddOccurrenceAsync(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29)); // open now

        var result = await w.Service.DetectAsync();

        Assert.Equal(1, result.MissedOpened);
        var deviation = await w.Db.MonitoringDeviations.SingleAsync();
        Assert.Equal(missed.Id, deviation.MonitoringOccurrenceId);
        Assert.Equal(DeviationKind.MissedOccurrence, deviation.Kind);
        Assert.Equal(DeviationStatus.Open, deviation.Status);
        Assert.Equal($"MD-2026-{deviation.Id:D5}", deviation.Number);
        Assert.Equal(new DateOnly(2026, 11, 24), deviation.DueDate); // 30 days
        Assert.Contains("Room A", deviation.Description);
    }

    [Fact]
    public async Task Scan_IsRepeatable_NothingIsOpenedTwice()
    {
        var w = await BuildAsync();
        await AddOccurrenceAsync(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));

        await w.Service.DetectAsync();
        var again = await w.Service.DetectAsync();

        Assert.Equal(0, again.MissedOpened);
        Assert.Equal(1, await w.Db.MonitoringDeviations.CountAsync());
    }

    [Fact]
    public async Task Scan_ForAnActionLimitResult_OpensADeviation_AndFollowUpSampling()
    {
        var w = await BuildAsync();
        var performed = await AddOccurrenceAsync(w, "2026-W42", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 22));
        await LinkResultAsync(w, performed, ResultStatus.ActionLimitExceeded);

        var result = await w.Service.DetectAsync();

        Assert.Equal(1, result.ExcursionsOpened);
        Assert.Equal(1, result.FollowUpsCreated);
        var deviation = await w.Db.MonitoringDeviations.SingleAsync();
        Assert.Equal(DeviationKind.ActionLimitExceeded, deviation.Kind);
        Assert.Contains("over the action limit", deviation.Description);
        Assert.Contains("EM-", deviation.Description);

        var followUp = await w.Db.MonitoringOccurrences.Include(o => o.Items).SingleAsync(o => o.Id == deviation.FollowUpOccurrenceId);
        Assert.Equal($"resp-{deviation.Id}", followUp.PeriodKey);
        Assert.Equal(w.Room.Id, followUp.RoomId);
        Assert.Equal($"Follow-up to {deviation.Number}", followUp.FrequencySnapshot);
        Assert.Equal(w.Config.Id, followUp.Items.Single().RoomTestConfigurationId);
        // Sunday 25 October is a working day; five working days later is Sunday 1 November.
        Assert.Equal(new DateOnly(2026, 10, 25), followUp.WindowStart);
        Assert.Equal(new DateOnly(2026, 11, 1), followUp.WindowEnd);
        var ev = await w.Db.MonitoringOccurrenceEvents.SingleAsync(e => e.MonitoringOccurrenceId == followUp.Id);
        Assert.Equal(OccurrenceEventType.Created, ev.EventType);
        Assert.Null(ev.PerformedByUserId);
    }

    [Fact]
    public async Task Scan_IgnoresAlertLevelResultsAndResultsWithinLimits()
    {
        var w = await BuildAsync();
        var alert = await AddOccurrenceAsync(w, "2026-W41", new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 15));
        var fine = await AddOccurrenceAsync(w, "2026-W42", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 22));
        await LinkResultAsync(w, alert, ResultStatus.AlertLimitExceeded);
        await LinkResultAsync(w, fine, ResultStatus.WithinLimits);

        var result = await w.Service.DetectAsync();

        Assert.Equal(0, result.ExcursionsOpened);
        Assert.Empty(await w.Db.MonitoringDeviations.ToListAsync());
    }

    // ---- closing ----

    [Fact]
    public async Task Close_AsNotApplicable_SkipsTheOccurrences_UnderOneSharedSignaturePair()
    {
        var w = await BuildAsync();
        var a = await AddOccurrenceAsync(w, "2026-W38", new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 17));
        var b = await AddOccurrenceAsync(w, "2026-W39", new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 24));
        await w.Service.DetectAsync();
        var ids = await w.Db.MonitoringDeviations.Select(d => d.Id).ToListAsync();

        var closed = await w.Service.CloseAsync(Close(ids, DeviationClosure.NotApplicableBeforeGoLive), w.Head.Id, null);

        Assert.Equal(2, closed);
        var rows = await w.Db.MonitoringDeviations.ToListAsync();
        Assert.All(rows, d =>
        {
            Assert.Equal(DeviationStatus.Closed, d.Status);
            Assert.Equal(DeviationClosure.NotApplicableBeforeGoLive, d.Closure);
            Assert.Equal(w.Head.Id, d.ClosedByUserId);
            Assert.NotNull(d.ClosureSignatureId);
            Assert.NotNull(d.ClosureSecondSignatureId);
        });
        Assert.Single(rows.Select(d => d.ClosureSignatureId).Distinct());
        Assert.All(await w.Db.MonitoringOccurrences.Where(o => o.Id == a.Id || o.Id == b.Id).ToListAsync(),
            o => Assert.Equal(OccurrenceExceptionState.Skipped, o.ExceptionState));
        var skippedEvents = await w.Db.MonitoringOccurrenceEvents.Where(e => e.EventType == OccurrenceEventType.Skipped).ToListAsync();
        Assert.Equal(2, skippedEvents.Count);
        Assert.All(skippedEvents, e => Assert.NotNull(e.SecondElectronicSignatureId));
    }

    [Fact]
    public async Task Close_AsJustified_LeavesTheOccurrenceAlone_AndTakesItOffTheDueList()
    {
        var w = await BuildAsync();
        var missed = await AddOccurrenceAsync(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        await w.Service.DetectAsync();
        Assert.Contains((await w.Occurrences.GetDueAsync(null)).Items, i => i.Id == missed.Id);

        await w.Service.CloseAsync(Close(new[] { (await w.Db.MonitoringDeviations.SingleAsync()).Id }, DeviationClosure.Justified, "Room was shut down for maintenance"), w.Head.Id, null);

        Assert.Equal(OccurrenceExceptionState.None, (await w.Db.MonitoringOccurrences.SingleAsync(o => o.Id == missed.Id)).ExceptionState);
        Assert.DoesNotContain((await w.Occurrences.GetDueAsync(null)).Items, i => i.Id == missed.Id);
    }

    [Fact]
    public async Task Close_RefusesAClosureThatDoesNotFitTheKind()
    {
        var w = await BuildAsync();
        var performed = await AddOccurrenceAsync(w, "2026-W42", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 22));
        await LinkResultAsync(w, performed, ResultStatus.OutOfSpecification);
        await w.Service.DetectAsync();
        var id = (await w.Db.MonitoringDeviations.SingleAsync()).Id;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CloseAsync(Close(new[] { id }, DeviationClosure.NotApplicableBeforeGoLive), w.Head.Id, null));
        Assert.Contains("cannot be closed as", ex.Message);

        await w.Service.CloseAsync(Close(new[] { id }, DeviationClosure.Investigated, "Resampled, within limits"), w.Head.Id, null);
        Assert.Equal(DeviationStatus.Closed, (await w.Db.MonitoringDeviations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Close_NeedsAReasonADifferentSecondSignerAndAnOpenDeviation()
    {
        var w = await BuildAsync();
        await AddOccurrenceAsync(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        await w.Service.DetectAsync();
        var id = (await w.Db.MonitoringDeviations.SingleAsync()).Id;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CloseAsync(Close(new[] { id }, DeviationClosure.Justified, "no"), w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CloseAsync(new CloseDeviationsRequest(new List<int> { id }, DeviationClosure.Justified, "Explained in writing", Password, "head", Password), w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CloseAsync(Close(Array.Empty<int>(), DeviationClosure.Justified, "Explained in writing"), w.Head.Id, null));

        await w.Service.CloseAsync(Close(new[] { id }, DeviationClosure.Justified, "Explained in writing"), w.Head.Id, null);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CloseAsync(Close(new[] { id }, DeviationClosure.Justified, "Explained in writing"), w.Head.Id, null));
        Assert.Contains("already closed", again.Message);
    }

    [Fact]
    public async Task Close_AsNotApplicable_IsRefusedWhenATestWasAlreadyStarted()
    {
        var w = await BuildAsync();
        var partly = await AddOccurrenceAsync(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        await w.Service.DetectAsync();
        // Someone starts a test after the deviation was opened.
        await LinkResultAsync(w, partly, ResultStatus.WithinLimits);
        var id = (await w.Db.MonitoringDeviations.SingleAsync()).Id;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CloseAsync(Close(new[] { id }, DeviationClosure.NotApplicableBeforeGoLive), w.Head.Id, null));

        Assert.Contains("already started", ex.Message);
    }

    // ---- listing ----

    [Fact]
    public async Task List_ShowsTargetsDueDatesAndCounts()
    {
        var w = await BuildAsync();
        await AddOccurrenceAsync(w, "2026-W40", new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 1));
        await w.Service.DetectAsync();

        var list = await w.Service.ListAsync(DeviationStatus.Open, null);

        Assert.Equal(1, list.OpenCount);
        Assert.Equal(0, list.PastDueCount);
        var item = Assert.Single(list.Items);
        Assert.Equal("Room A", item.Target);
        Assert.Equal("Filling", item.Group);
        Assert.False(item.PastDue);
    }
}
