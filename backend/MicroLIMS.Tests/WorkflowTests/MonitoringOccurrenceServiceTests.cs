using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
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

// The plan's calendar queries and the signed changes to one occurrence.
public class MonitoringOccurrenceServiceTests
{
    // Wednesday 21 Oct 2026, 09:00 in Cairo.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 6, 0, 0, TimeSpan.Zero);
    private const string Password = "Correct-horse-1";

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public MonitoringOccurrenceService Service = null!;
        public User Head = null!, Approver = null!, Analyst = null!;
        public Machine Machine = null!;
        public MachinePartConfiguration PartConfig = null!;
        public MonitoringPlanRevision Revision = null!;
        public Room Room = null!;
        public RoomTestConfiguration RoomConfig = null!;
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

        w.Machine = new Machine { Name = "Filler 1" };
        var dept = new Department { Name = "Filling" };
        w.Room = new Room { Name = "Room A", Department = dept, GradeClassification = "A" };
        w.Db.Machines.Add(w.Machine);
        w.Db.Rooms.Add(w.Room);
        await w.Db.SaveChangesAsync();

        var part = new MachinePart { MachineId = w.Machine.Id, Name = "Nozzle" };
        w.Db.MachineParts.Add(part);
        await w.Db.SaveChangesAsync();
        w.PartConfig = new MachinePartConfiguration { MachinePartId = part.Id, TestType = "Swab", TestCode = "TAMC" };
        w.RoomConfig = new RoomTestConfiguration { RoomId = w.Room.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.Revision = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 1, Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = w.Head.Id
        };
        w.Db.MachinePartConfigurations.Add(w.PartConfig);
        w.Db.RoomTestConfigurations.Add(w.RoomConfig);
        w.Db.MonitoringPlanRevisions.Add(w.Revision);
        await w.Db.SaveChangesAsync();

        var status = new OccurrenceStatusService(w.Db, clock);
        w.Service = new MonitoringOccurrenceService(w.Db, clock, status, new ElectronicSignatureService(w.Db));
        return w;
    }

    private static MonitoringOccurrence RoomOccurrence(World w, string period, DateOnly start, DateOnly end, DateOnly? target = null) => new()
    {
        CreatedByRevisionId = w.Revision.Id,
        Category = MonitoringCategory.EnvironmentalMonitoring,
        TargetType = MonitoringTargetType.Room,
        RoomId = w.Room.Id,
        PeriodKey = period,
        FrequencySnapshot = "Weekly, Sunday",
        TargetDate = target ?? start.AddDays(3),
        OriginalTargetDate = target ?? start.AddDays(3),
        WindowStart = start,
        WindowEnd = end,
        OriginalWindowEnd = end,
        Items = new List<MonitoringOccurrenceItem> { new() { RoomTestConfigurationId = w.RoomConfig.Id } }
    };

    private static readonly DateOnly WeekStart = new(2026, 10, 18);
    private static readonly DateOnly WeekEnd = new(2026, 10, 22);

    // ---- manual entries ----

    [Fact]
    public async Task CreateManual_AddsAMachineOccurrenceToTheApprovedPlan()
    {
        var w = await BuildAsync();

        var created = await w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 28), new List<int> { w.PartConfig.Id }, "Cleaning after batch change"),
            w.Head.Id);

        Assert.Equal(MonitoringCategory.AfterCleaning, created.Category);
        Assert.Equal("Filler 1", created.Target);
        Assert.True(created.Manual);
        // Wednesday 28 October plus two working days: Thursday, then Sunday (Friday and Saturday are not working days).
        Assert.Equal(new DateOnly(2026, 11, 1), created.WindowEnd);
        Assert.Equal(OccurrenceStatus.Upcoming, created.Status);
        var ev = await w.Db.MonitoringOccurrenceEvents.SingleAsync();
        Assert.Equal(OccurrenceEventType.Created, ev.EventType);
    }

    [Fact]
    public async Task CreateManual_ForAWeekOrMonth_HasNoTargetDateAndTheWholeWindow()
    {
        var w = await BuildAsync();
        var parts = new List<int> { w.PartConfig.Id };

        var week = await w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 28), parts, null, ManualWindowKind.Week), w.Head.Id);
        var month = await w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 11, 10), parts, null, ManualWindowKind.Month), w.Head.Id);

        Assert.Null(week.TargetDate);
        Assert.Equal(new DateOnly(2026, 10, 25), week.WindowStart);
        // The lab week ends on Saturday 31 October, but Thursday the 29th is its last working day.
        Assert.Equal(new DateOnly(2026, 10, 29), week.WindowEnd);
        Assert.True(week.Manual);
        Assert.Null(month.TargetDate);
        Assert.Equal(new DateOnly(2026, 11, 1), month.WindowStart);
        Assert.Equal(new DateOnly(2026, 11, 30), month.WindowEnd);
    }

    [Fact]
    public async Task CreateManual_OnAWeekend_IsRefused()
    {
        var w = await BuildAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 30), new List<int> { w.PartConfig.Id }, null), w.Head.Id));

        Assert.Contains("not a working day", ex.Message);
    }

    [Fact]
    public async Task ChangeWindow_ToAWeekend_IsRefused_AndToAWorkingDayIsAllowed()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.ChangeWindowAsync(occurrence.Id,
            new ChangeWindowRequest(null, new DateOnly(2026, 10, 31), "Room busy on Thursday", Password, "approver", Password), w.Head.Id, null));
        Assert.Contains("not a working day", ex.Message);

        var changed = await w.Service.ChangeWindowAsync(occurrence.Id,
            new ChangeWindowRequest(null, new DateOnly(2026, 11, 1), "Room busy on Thursday", Password, "approver", Password), w.Head.Id, null);
        Assert.Equal(new DateOnly(2026, 11, 1), changed.WindowEnd);
    }

    [Fact]
    public async Task CreateManual_SameMachineAndDateTwice_GetsTheNextSequence()
    {
        var w = await BuildAsync();
        var request = new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 28), new List<int> { w.PartConfig.Id }, null);

        await w.Service.CreateManualAsync(request, w.Head.Id);
        await w.Service.CreateManualAsync(request, w.Head.Id);

        Assert.Equal(new[] { 1, 2 }, await w.Db.MonitoringOccurrences.OrderBy(o => o.Sequence).Select(o => o.Sequence).ToArrayAsync());
    }

    [Fact]
    public async Task CreateManual_RefusesAPastDate_NoApprovedPlan_AndForeignParts()
    {
        var w = await BuildAsync();
        var other = new Machine { Name = "Filler 2" };
        w.Db.Machines.Add(other);
        await w.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 20), new List<int> { w.PartConfig.Id }, null), w.Head.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2027, 1, 5), new List<int> { w.PartConfig.Id }, null), w.Head.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(other.Id, new DateOnly(2026, 10, 28), new List<int> { w.PartConfig.Id }, null), w.Head.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.CreateManualAsync(
            new CreateManualOccurrenceRequest(w.Machine.Id, new DateOnly(2026, 10, 28), new List<int>(), null), w.Head.Id));
    }

    // ---- skip ----

    [Fact]
    public async Task Skip_NeedsTwoSignaturesAndAReason_AndRecordsBoth()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        var result = await w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("Room closed for validation", Password, "approver", Password), w.Head.Id, "10.0.0.1");

        Assert.Equal(OccurrenceStatus.Skipped, result.Status);
        var ev = await w.Db.MonitoringOccurrenceEvents.SingleAsync();
        Assert.Equal(OccurrenceEventType.Skipped, ev.EventType);
        Assert.Equal("Room closed for validation", ev.Reason);
        Assert.NotNull(ev.ElectronicSignatureId);
        Assert.NotNull(ev.SecondElectronicSignatureId);
        Assert.Equal(2, await w.Db.ElectronicSignatures.CountAsync());
    }

    [Fact]
    public async Task Skip_RefusesTheSamePersonTwice_AndAnApproverWithoutPermission_AndAShortReason()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("Room closed for validation", Password, "head", Password), w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("Room closed for validation", Password, "analyst", Password), w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("no", Password, "approver", Password), w.Head.Id, null));
        await Assert.ThrowsAsync<SignatureVerificationException>(() => w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("Room closed for validation", Password, "nobody", Password), w.Head.Id, null));

        Assert.Equal(OccurrenceExceptionState.None, (await w.Db.MonitoringOccurrences.AsNoTracking().SingleAsync()).ExceptionState);
        Assert.Empty(w.Db.MonitoringOccurrenceEvents);
    }

    [Fact]
    public async Task Skip_WithAWrongPassword_ChangesNothing()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<SignatureVerificationException>(() => w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("Room closed for validation", "wrong", "approver", Password), w.Head.Id, null));
        await Assert.ThrowsAsync<SignatureVerificationException>(() => w.Service.SkipAsync(occurrence.Id,
            new SkipOccurrenceRequest("Room closed for validation", Password, "approver", "wrong"), w.Head.Id, null));

        Assert.Equal(OccurrenceExceptionState.None, (await w.Db.MonitoringOccurrences.AsNoTracking().SingleAsync()).ExceptionState);
    }

    [Fact]
    public async Task Skip_RefusesAnOccurrenceThatIsAlreadySkippedOrHasATestStarted()
    {
        var w = await BuildAsync();
        var skipped = RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        skipped.ExceptionState = OccurrenceExceptionState.Skipped;
        var started = RoomOccurrence(w, "2026-W42", WeekStart, WeekEnd);
        w.Db.MonitoringOccurrences.AddRange(skipped, started);
        await w.Db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "EM1", Category = SampleCategory.EnvironmentalMonitoring, Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Sample = sample, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        w.Db.SampleLocations.Add(new SampleLocation
        {
            Sample = sample, TestOrder = order, LocationType = LocationType.Room,
            RoomTestConfigurationId = w.RoomConfig.Id, MonitoringOccurrenceId = started.Id
        });
        await w.Db.SaveChangesAsync();

        var request = new SkipOccurrenceRequest("Room closed for validation", Password, "approver", Password);
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.SkipAsync(skipped.Id, request, w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.SkipAsync(started.Id, request, w.Head.Id, null));
    }

    // ---- window change ----

    [Fact]
    public async Task ChangeWindow_MovesTheTargetAndExtendsTheEnd_WithOneEventEach()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W42", WeekStart, WeekEnd, new DateOnly(2026, 10, 21));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        var result = await w.Service.ChangeWindowAsync(occurrence.Id,
            new ChangeWindowRequest(new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 25), "Holiday on Wednesday", Password, "approver", Password),
            w.Head.Id, null);

        Assert.Equal(new DateOnly(2026, 10, 22), result.TargetDate);
        Assert.Equal(new DateOnly(2026, 10, 25), result.WindowEnd);
        Assert.Equal(WeekEnd, result.OriginalWindowEnd);
        var types = await w.Db.MonitoringOccurrenceEvents.OrderBy(e => e.Id).Select(e => e.EventType).ToListAsync();
        Assert.Equal(new[] { OccurrenceEventType.TargetDateChanged, OccurrenceEventType.WindowExtended }, types);
    }

    [Fact]
    public async Task ChangeWindow_RefusesAnEmptyChange_ATargetOutsideTheWindow_AndAnEndBeforeTheStart()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W42", WeekStart, WeekEnd, new DateOnly(2026, 10, 21));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();

        ChangeWindowRequest Req(DateOnly? target, DateOnly end) =>
            new(target, end, "Holiday on Wednesday", Password, "approver", Password);

        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.ChangeWindowAsync(occurrence.Id, Req(null, WeekEnd), w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.ChangeWindowAsync(occurrence.Id, Req(new DateOnly(2026, 10, 30), WeekEnd), w.Head.Id, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.ChangeWindowAsync(occurrence.Id, Req(null, new DateOnly(2026, 10, 10)), w.Head.Id, null));
        Assert.Empty(w.Db.MonitoringOccurrenceEvents);
    }

    // ---- reading ----

    [Fact]
    public async Task Detail_ShowsItemsAndTheSignedHistory()
    {
        var w = await BuildAsync();
        var occurrence = RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29));
        w.Db.MonitoringOccurrences.Add(occurrence);
        await w.Db.SaveChangesAsync();
        await w.Service.ChangeWindowAsync(occurrence.Id,
            new ChangeWindowRequest(null, new DateOnly(2026, 11, 1), "Room busy on Thursday", Password, "approver", Password), w.Head.Id, null);

        var detail = await w.Service.GetDetailAsync(occurrence.Id);

        Assert.Equal("Room A", detail.Summary.Target);
        Assert.Equal("Filling", detail.Summary.Group);
        Assert.Equal("TAMC", detail.Items.Single().Label);
        var ev = detail.Events.Single();
        Assert.Equal("head", ev.PerformedBy);
        Assert.Equal("approver", ev.SecondSigner);
        Assert.True(ev.Signed);
    }

    [Fact]
    public async Task Calendar_ReturnsWhatTouchesTheRange_AndHidesRetiredByDefault()
    {
        var w = await BuildAsync();
        var thisWeek = RoomOccurrence(w, "2026-W42", WeekStart, WeekEnd);
        var nextMonth = RoomOccurrence(w, "2026-11", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        var retired = RoomOccurrence(w, "2026-W44", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));
        retired.ExceptionState = OccurrenceExceptionState.Retired;
        retired.RetiredByRevisionId = w.Revision.Id;
        w.Db.MonitoringOccurrences.AddRange(thisWeek, nextMonth, retired);
        await w.Db.SaveChangesAsync();

        var october = await w.Service.GetCalendarAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, false);
        var november = await w.Service.GetCalendarAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, false);
        var withRetired = await w.Service.GetCalendarAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, true);

        Assert.Equal(new[] { thisWeek.Id }, october.Select(o => o.Id));
        Assert.Equal(new[] { nextMonth.Id }, november.Select(o => o.Id));
        Assert.Equal(2, withRetired.Count);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.GetCalendarAsync(new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31), null, false));
    }

    [Fact]
    public async Task YearSummary_CountsEachOccurrenceInTheMonthOfItsTargetDate()
    {
        var w = await BuildAsync();
        w.Db.MonitoringOccurrences.AddRange(
            RoomOccurrence(w, "2026-W40", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 8)),   // overdue (past grace: missed)
            RoomOccurrence(w, "2026-W42", WeekStart, WeekEnd),                                      // open
            RoomOccurrence(w, "2026-W43", new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 29)), // upcoming
            RoomOccurrence(w, "2026-11", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));  // upcoming, month of window end
        await w.Db.SaveChangesAsync();

        var summary = await w.Service.GetYearSummaryAsync(2026, null);

        var october = summary.Months[9];
        Assert.Equal((4 - 1, 0, 1, 1, 1, 0), (october.Total, october.Performed, october.InProgress, october.Overdue, october.Upcoming, october.Skipped));
        Assert.Equal(1, summary.Months[10].Total);
        Assert.Equal(12, summary.Months.Count);
    }

    [Fact]
    public async Task Due_ListsOverdueAndOpenOccurrences_AndFlagsTheWarningDay()
    {
        var w = await BuildAsync();
        var overdue = RoomOccurrence(w, "2026-W40", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 8));
        var open = RoomOccurrence(w, "2026-W42", WeekStart, WeekEnd);
        var november = RoomOccurrence(w, "2026-11", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        w.Db.MonitoringOccurrences.AddRange(overdue, open, november);
        await w.Db.SaveChangesAsync();

        var due = await w.Service.GetDueAsync(null);

        Assert.Equal(new[] { overdue.Id, open.Id }, due.Items.Select(i => i.Id));
        Assert.Equal(20, due.WarningDayOfMonth);
        Assert.True(due.ShowWarning);
    }
}
