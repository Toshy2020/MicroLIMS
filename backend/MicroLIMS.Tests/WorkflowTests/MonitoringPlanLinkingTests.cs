using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Starting an EM session from the plan: the checklist, the links the
// engine saves with the sample, and what refuses a link.
public class MonitoringPlanLinkingTests
{
    // Wednesday 21 Oct 2026, 09:00 in Cairo.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 6, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public LabClock Clock = null!;
        public EMWorkflowEngine Engine = null!;
        public MonitoringLinkService Links = null!;
        public Department Dept = null!;
        public Room RoomA = null!, RoomB = null!, Elsewhere = null!;
        public RoomTestConfiguration ConfigA = null!, ConfigB = null!, ConfigElsewhere = null!;
        public MonitoringPlanRevision Revision = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var w = new World { Db = new MicroLimsDbContext(options) };
        w.Clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        w.Dept = new Department { Name = "Filling" };
        var other = new Department { Name = "Packing" };
        w.RoomA = new Room { Name = "Room A", Department = w.Dept, GradeClassification = "A" };
        w.RoomB = new Room { Name = "Room B", Department = w.Dept, GradeClassification = "B" };
        w.Elsewhere = new Room { Name = "Pack 1", Department = other, GradeClassification = "C" };
        w.Db.Rooms.AddRange(w.RoomA, w.RoomB, w.Elsewhere);
        w.Db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest });
        await w.Db.SaveChangesAsync();

        w.ConfigA = new RoomTestConfiguration { RoomId = w.RoomA.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.ConfigB = new RoomTestConfiguration { RoomId = w.RoomB.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.ConfigElsewhere = new RoomTestConfiguration { RoomId = w.Elsewhere.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.Db.RoomTestConfigurations.AddRange(w.ConfigA, w.ConfigB, w.ConfigElsewhere);

        w.Revision = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 1, Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = 1
        };
        w.Db.MonitoringPlanRevisions.Add(w.Revision);
        await w.Db.SaveChangesAsync();

        w.Links = new MonitoringLinkService(w.Db, w.Clock, new OccurrenceStatusService(w.Db, w.Clock));
        w.Engine = new EMWorkflowEngine(w.Db, new ReferenceNumberGenerator(w.Db), null, w.Links);
        return w;
    }

    private static MonitoringOccurrence Occurrence(World w, Room room, RoomTestConfiguration config, string period, DateOnly start, DateOnly end) => new()
    {
        CreatedByRevisionId = w.Revision.Id,
        Category = MonitoringCategory.EnvironmentalMonitoring,
        TargetType = MonitoringTargetType.Room,
        RoomId = room.Id,
        PeriodKey = period,
        FrequencySnapshot = "Weekly, Sunday",
        TargetDate = start.AddDays(3),
        OriginalTargetDate = start.AddDays(3),
        WindowStart = start,
        WindowEnd = end,
        OriginalWindowEnd = end,
        Items = new List<MonitoringOccurrenceItem> { new() { RoomTestConfigurationId = config.Id } }
    };

    private static readonly DateOnly ThisWeekStart = new(2026, 10, 18);
    private static readonly DateOnly ThisWeekEnd = new(2026, 10, 22);

    private static async Task<Sample> ReceiveAsync(World w) =>
        await w.Engine.ReceiveAsync(new EMReceiveRequest(w.Dept.Id, 0, "Analyst", "CTRL-1", 1));

    [Fact]
    public async Task Prepare_WithATickedOccurrence_LinksTheLocationAndRecordsAnEvent()
    {
        var w = await BuildAsync();
        var occurrence = Occurrence(w, w.RoomA, w.ConfigA, "2026-W42", ThisWeekStart, ThisWeekEnd);
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();
        var sample = await ReceiveAsync(w);

        var prepared = await w.Engine.PrepareAsync(sample.Id, new List<int> { w.ConfigA.Id, w.ConfigB.Id }, 1, new[] { occurrence.Id });

        var linked = prepared.Locations.Single(l => l.RoomTestConfigurationId == w.ConfigA.Id);
        var unlinked = prepared.Locations.Single(l => l.RoomTestConfigurationId == w.ConfigB.Id);
        Assert.Equal(occurrence.Id, linked.MonitoringOccurrenceId);
        Assert.Null(unlinked.MonitoringOccurrenceId);
        var ev = await w.Db.MonitoringOccurrenceEvents.SingleAsync();
        Assert.Equal(OccurrenceEventType.Linked, ev.EventType);
        Assert.Contains(sample.ReferenceNumber, ev.NewValue);

        var detail = (await new OccurrenceStatusService(w.Db, w.Clock).ComputeAsync(
            await w.Db.MonitoringOccurrences.Include(o => o.Items).ToListAsync()))[occurrence.Id];
        Assert.Equal(OccurrenceStatus.InTesting, detail.Result.Status);
    }

    [Fact]
    public async Task Prepare_WithoutTickingAnything_LinksNothing()
    {
        var w = await BuildAsync();
        w.Db.MonitoringOccurrences.Add(Occurrence(w, w.RoomA, w.ConfigA, "2026-W42", ThisWeekStart, ThisWeekEnd));
        await w.Db.SaveChangesAsync();
        var sample = await ReceiveAsync(w);

        var prepared = await w.Engine.PrepareAsync(sample.Id, new List<int> { w.ConfigA.Id }, 1);

        Assert.All(prepared.Locations, l => Assert.Null(l.MonitoringOccurrenceId));
        Assert.Empty(w.Db.MonitoringOccurrenceEvents);
    }

    [Fact]
    public async Task Prepare_RefusesAnOccurrenceWhoseRoomIsNotInTheSession()
    {
        var w = await BuildAsync();
        var occurrence = Occurrence(w, w.RoomB, w.ConfigB, "2026-W42", ThisWeekStart, ThisWeekEnd);
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();
        var sample = await ReceiveAsync(w);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Engine.PrepareAsync(sample.Id, new List<int> { w.ConfigA.Id }, 1, new[] { occurrence.Id }));
        Assert.Contains("Room B", ex.Message);
    }

    [Fact]
    public async Task Prepare_RefusesAnOccurrenceOfAnotherDepartment()
    {
        var w = await BuildAsync();
        var occurrence = Occurrence(w, w.Elsewhere, w.ConfigElsewhere, "2026-W42", ThisWeekStart, ThisWeekEnd);
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();
        var sample = await ReceiveAsync(w);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Engine.PrepareAsync(sample.Id, new List<int> { w.ConfigA.Id }, 1, new[] { occurrence.Id }));
    }

    [Fact]
    public async Task Prepare_RefusesASkippedOrDraftOccurrence()
    {
        var w = await BuildAsync();
        var skipped = Occurrence(w, w.RoomA, w.ConfigA, "2026-W42", ThisWeekStart, ThisWeekEnd);
        skipped.ExceptionState = OccurrenceExceptionState.Skipped;
        var draft = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 2, Status = PlanRevisionStatus.Draft,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = 1
        };
        w.Db.MonitoringPlanRevisions.Add(draft);
        await w.Db.SaveChangesAsync();
        var inDraft = Occurrence(w, w.RoomA, w.ConfigA, "2026-W43", ThisWeekStart.AddDays(7), ThisWeekEnd.AddDays(7));
        inDraft.CreatedByRevisionId = draft.Id;
        w.Db.MonitoringOccurrences.AddRange(skipped, inDraft);
        await w.Db.SaveChangesAsync();

        var sample = await ReceiveAsync(w);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Engine.PrepareAsync(sample.Id, new List<int> { w.ConfigA.Id }, 1, new[] { skipped.Id }));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Engine.PrepareAsync(sample.Id, new List<int> { w.ConfigA.Id }, 1, new[] { inDraft.Id }));
    }

    [Fact]
    public async Task Prepare_RefusesATestThatIsAlreadyFulfilled_UntilItsSampleIsVoided()
    {
        var w = await BuildAsync();
        var occurrence = Occurrence(w, w.RoomA, w.ConfigA, "2026-W42", ThisWeekStart, ThisWeekEnd);
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        var first = await w.Engine.PrepareAsync((await ReceiveAsync(w)).Id, new List<int> { w.ConfigA.Id }, 1, new[] { occurrence.Id });

        var second = await ReceiveAsync(w);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Engine.PrepareAsync(second.Id, new List<int> { w.ConfigA.Id }, 1, new[] { occurrence.Id }));
        Assert.Contains(first.ReferenceNumber, ex.Message);

        // The first sample is voided: its test no longer counts.
        first.TestOrders.Single().Status = ApprovalStatus.Voided;
        await w.Db.SaveChangesAsync();

        var retry = await w.Engine.PrepareAsync(second.Id, new List<int> { w.ConfigA.Id }, 1, new[] { occurrence.Id });
        Assert.Equal(occurrence.Id, retry.Locations.Single().MonitoringOccurrenceId);
    }

    [Fact]
    public async Task Checklist_SplitsOverdueDueNowAndLaterThisMonth_AndTicksTheOldestPerRoom()
    {
        var w = await BuildAsync();
        var lastWeek = Occurrence(w, w.RoomA, w.ConfigA, "2026-W41", ThisWeekStart.AddDays(-7), ThisWeekEnd.AddDays(-7));
        var thisWeek = Occurrence(w, w.RoomA, w.ConfigA, "2026-W42", ThisWeekStart, ThisWeekEnd);
        var roomBThisWeek = Occurrence(w, w.RoomB, w.ConfigB, "2026-W42", ThisWeekStart, ThisWeekEnd);
        var nextWeek = Occurrence(w, w.RoomA, w.ConfigA, "2026-W43", ThisWeekStart.AddDays(7), ThisWeekEnd.AddDays(7));
        var nextMonth = Occurrence(w, w.RoomA, w.ConfigA, "2026-11", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        w.Db.MonitoringOccurrences.AddRange(lastWeek, thisWeek, roomBThisWeek, nextWeek, nextMonth);
        await w.Db.SaveChangesAsync();
        var sample = await ReceiveAsync(w);

        var checklist = await w.Links.GetChecklistAsync(sample.Id);

        var overdue = checklist.Sections.Single(s => s.Key == "overdue").Occurrences;
        var dueNow = checklist.Sections.Single(s => s.Key == "dueNow").Occurrences;
        var later = checklist.Sections.Single(s => s.Key == "laterThisMonth").Occurrences;
        Assert.Equal(new[] { lastWeek.Id }, overdue.Select(o => o.OccurrenceId));
        Assert.Equal(OccurrenceStatus.Overdue, overdue[0].Status);
        Assert.Equal(new[] { thisWeek.Id, roomBThisWeek.Id }.OrderBy(i => i), dueNow.Select(o => o.OccurrenceId).OrderBy(i => i));
        Assert.Equal(new[] { nextWeek.Id }, later.Select(o => o.OccurrenceId));

        // Room A's oldest (last week) is ticked, this week's is not; room B's is.
        Assert.True(overdue[0].PreTicked);
        Assert.False(dueNow.Single(o => o.OccurrenceId == thisWeek.Id).PreTicked);
        Assert.True(dueNow.Single(o => o.OccurrenceId == roomBThisWeek.Id).PreTicked);
        Assert.False(later[0].PreTicked);
        Assert.Equal("TAMC", overdue[0].RemainingItems.Single().Label);
    }

    [Fact]
    public async Task Checklist_LeavesOutFulfilledSkippedRetiredAndOtherDepartments()
    {
        var w = await BuildAsync();
        var done = Occurrence(w, w.RoomA, w.ConfigA, "2026-W42", ThisWeekStart, ThisWeekEnd);
        var skipped = Occurrence(w, w.RoomB, w.ConfigB, "2026-W42", ThisWeekStart, ThisWeekEnd);
        skipped.ExceptionState = OccurrenceExceptionState.Skipped;
        var otherDepartment = Occurrence(w, w.Elsewhere, w.ConfigElsewhere, "2026-W42", ThisWeekStart, ThisWeekEnd);
        w.Db.MonitoringOccurrences.AddRange(done, skipped, otherDepartment);
        await w.Db.SaveChangesAsync();
        await w.Engine.PrepareAsync((await ReceiveAsync(w)).Id, new List<int> { w.ConfigA.Id }, 1, new[] { done.Id });

        var checklist = await w.Links.GetChecklistAsync((await ReceiveAsync(w)).Id);

        Assert.All(checklist.Sections, s => Assert.Empty(s.Occurrences));
    }

    [Fact]
    public async Task Checklist_RefusesASampleThatIsNotMonitoring()
    {
        var w = await BuildAsync();
        var fp = new Sample { ReferenceNumber = "FP1", Category = SampleCategory.FinishedProduct, Status = SampleStatus.Received };
        w.Db.Samples.Add(fp);
        await w.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Links.GetChecklistAsync(fp.Id));
    }
}
