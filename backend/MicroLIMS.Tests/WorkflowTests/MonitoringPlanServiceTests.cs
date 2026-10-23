using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Generating, previewing and approving a year's plan: go-live date, moving
// occurrences that have not started, and revising a year that has daily rules.
public class MonitoringPlanServiceTests
{
    // Sunday 25 Oct 2026, 09:00 in Cairo.
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 6, 0, 0, TimeSpan.Zero);
    private const string Password = "Correct-horse-1";

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public MonitoringPlanService Service = null!;
        public User Head = null!;
        public Room Room = null!;
        public RoomTestConfiguration RoomConfig = null!;
    }

    private static async Task<World> BuildAsync(FrequencyType type, int interval = 1, DayOfWeek? weekday = null)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var w = new World { Db = new MicroLimsDbContext(options) };
        var clock = new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        var role = new Role { Name = "Head", Type = RoleType.SectionHead, IsSystemRole = true, IsActive = true };
        w.Db.Roles.Add(role);
        await w.Db.SaveChangesAsync();
        w.Head = new User { Username = "head", FullName = "head", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        w.Db.Users.Add(w.Head);

        var dept = new Department { Name = "Filling" };
        w.Room = new Room
        {
            Name = "Room A", Department = dept, GradeClassification = "A",
            FrequencyType = type, FrequencyInterval = interval, SamplingWeekday = weekday
        };
        w.Db.Rooms.Add(w.Room);
        await w.Db.SaveChangesAsync();
        w.RoomConfig = new RoomTestConfiguration { RoomId = w.Room.Id, TestType = "PassiveAirSample", TestCode = "TAMC" };
        w.Db.RoomTestConfigurations.Add(w.RoomConfig);
        await w.Db.SaveChangesAsync();

        w.Service = new MonitoringPlanService(w.Db, clock, new ElectronicSignatureService(w.Db));
        return w;
    }

    private static Task<PlanRevisionResponse> ApproveAsync(World w, PlanPreviewResponse draft, string? reason = null) =>
        w.Service.ApproveAsync(draft.Revision.Id, new ApprovePlanRequest(Password, reason), w.Head.Id, null);

    // ---- go-live date ----

    [Fact]
    public async Task FirstPlanOfTheCurrentYear_StartsToday_AndPlansNothingBeforeIt()
    {
        var w = await BuildAsync(FrequencyType.Weekly, 1, DayOfWeek.Sunday);

        var draft = await w.Service.GenerateDraftAsync(2026, w.Head.Id);

        Assert.Equal(new DateOnly(2026, 10, 25), draft.Revision.EffectiveFrom);
        var occurrences = await w.Db.MonitoringOccurrences.ToListAsync();
        Assert.NotEmpty(occurrences);
        Assert.All(occurrences, o => Assert.True(o.WindowEnd >= new DateOnly(2026, 10, 25)));
        Assert.Equal("2026-W43", occurrences.OrderBy(o => o.WindowStart).First().PeriodKey);
    }

    [Fact]
    public async Task AChosenGoLiveDate_IsUsed_ButNeverInThePastOrOutsideTheYear()
    {
        var w = await BuildAsync(FrequencyType.Weekly, 1, DayOfWeek.Sunday);

        var draft = await w.Service.GenerateDraftAsync(2026, w.Head.Id, new DateOnly(2026, 11, 8));
        Assert.Equal(new DateOnly(2026, 11, 8), draft.Revision.EffectiveFrom);
        Assert.All(await w.Db.MonitoringOccurrences.ToListAsync(), o => Assert.True(o.WindowEnd >= new DateOnly(2026, 11, 8)));

        var past = await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.GenerateDraftAsync(2026, w.Head.Id, new DateOnly(2026, 10, 1)));
        Assert.Contains("cannot be before", past.Message);
        var outside = await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.GenerateDraftAsync(2026, w.Head.Id, new DateOnly(2027, 1, 4)));
        Assert.Contains("inside 2026", outside.Message);
    }

    [Fact]
    public async Task AFutureYear_StartsOnTheFirstOfJanuary_AndAPastYearIsRefused()
    {
        var w = await BuildAsync(FrequencyType.Weekly, 1, DayOfWeek.Sunday);

        var next = await w.Service.GenerateDraftAsync(2027, w.Head.Id);
        Assert.Equal(new DateOnly(2027, 1, 1), next.Revision.EffectiveFrom);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.GenerateDraftAsync(2025, w.Head.Id));
        Assert.Contains("already ended", ex.Message);
    }

    // ---- moving occurrences that have not started ----

    private static MonitoringOccurrence WeekendWeek(World w, MonitoringPlanRevision revision) => new()
    {
        CreatedByRevisionId = revision.Id,
        Category = MonitoringCategory.EnvironmentalMonitoring,
        TargetType = MonitoringTargetType.Room,
        RoomId = w.Room.Id,
        PeriodKey = "2026-W44",
        FrequencySnapshot = "Weekly (own)",
        // The old generator ran the flexible window to the lab week's Saturday.
        WindowStart = new DateOnly(2026, 11, 1),
        WindowEnd = new DateOnly(2026, 11, 7),
        OriginalWindowEnd = new DateOnly(2026, 11, 7),
        Items = new List<MonitoringOccurrenceItem> { new() { RoomTestConfigurationId = w.RoomConfig.Id } }
    };

    private static async Task<MonitoringPlanRevision> SeedApprovedYearAsync(World w)
    {
        var revision = new MonitoringPlanRevision
        {
            Year = 2026, RevisionNumber = 1, Status = PlanRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1), GeneratedAt = Now.UtcDateTime, GeneratedByUserId = w.Head.Id
        };
        w.Db.MonitoringPlanRevisions.Add(revision);
        await w.Db.SaveChangesAsync();
        return revision;
    }

    [Fact]
    public async Task ARevision_MovesAWeekendWindowToTheLastWorkingDay_WithASignedEvent()
    {
        var w = await BuildAsync(FrequencyType.Weekly, 1);
        var approved = await SeedApprovedYearAsync(w);
        var weekend = WeekendWeek(w, approved);
        w.Db.MonitoringOccurrences.Add(weekend);
        await w.Db.SaveChangesAsync();

        var draft = await w.Service.GenerateDraftAsync(2026, w.Head.Id);

        var move = Assert.Single(draft.ToReschedule, m => m.OccurrenceId == weekend.Id);
        Assert.Equal(new DateOnly(2026, 11, 7), move.OldWindowEnd);
        Assert.Equal(new DateOnly(2026, 11, 5), move.NewWindowEnd);
        Assert.Equal("Room A", move.Target);

        await ApproveAsync(w, draft, "Align windows with working days");

        var after = await w.Db.MonitoringOccurrences.AsNoTracking().SingleAsync(o => o.Id == weekend.Id);
        Assert.Equal(new DateOnly(2026, 11, 1), after.WindowStart);
        Assert.Equal(new DateOnly(2026, 11, 5), after.WindowEnd);
        // The original end stays on the record, so the change is visible later.
        Assert.Equal(new DateOnly(2026, 11, 7), after.OriginalWindowEnd);
        var ev = await w.Db.MonitoringOccurrenceEvents.SingleAsync(e => e.MonitoringOccurrenceId == weekend.Id);
        Assert.Equal(OccurrenceEventType.Rescheduled, ev.EventType);
        Assert.NotNull(ev.ElectronicSignatureId);
        Assert.Contains("2026-11-07", ev.OldValue);
        Assert.Contains("2026-11-05", ev.NewValue);
        Assert.Equal("Align windows with working days", ev.Reason);
    }

    [Fact]
    public async Task AWindowAPersonChangedWithSignatures_IsLeftAlone()
    {
        var w = await BuildAsync(FrequencyType.Weekly, 1);
        var approved = await SeedApprovedYearAsync(w);
        var weekend = WeekendWeek(w, approved);
        w.Db.MonitoringOccurrences.Add(weekend);
        await w.Db.SaveChangesAsync();
        w.Db.MonitoringOccurrenceEvents.Add(new MonitoringOccurrenceEvent
        {
            MonitoringOccurrenceId = weekend.Id, EventType = OccurrenceEventType.WindowExtended,
            OldValue = "2026-11-05", NewValue = "2026-11-07", Reason = "Room busy", PerformedByUserId = w.Head.Id, PerformedAt = Now.UtcDateTime
        });
        await w.Db.SaveChangesAsync();

        var draft = await w.Service.GenerateDraftAsync(2026, w.Head.Id);

        Assert.DoesNotContain(draft.ToReschedule, m => m.OccurrenceId == weekend.Id);
    }

    [Fact]
    public async Task AnOccurrenceWithATestStarted_IsNeverMoved()
    {
        var w = await BuildAsync(FrequencyType.Weekly, 1);
        var approved = await SeedApprovedYearAsync(w);
        var weekend = WeekendWeek(w, approved);
        w.Db.MonitoringOccurrences.Add(weekend);
        await w.Db.SaveChangesAsync();
        w.Db.SampleLocations.Add(new SampleLocation { MonitoringOccurrenceId = weekend.Id, LocationType = LocationType.Room });
        await w.Db.SaveChangesAsync();

        var draft = await w.Service.GenerateDraftAsync(2026, w.Head.Id);

        Assert.DoesNotContain(draft.ToReschedule, m => m.OccurrenceId == weekend.Id);
    }

    // ---- revising a year that has daily rules ----

    [Fact]
    public async Task ApprovedYearWithDailyRules_NeedsNoRevisionWhenNothingChanged()
    {
        var w = await BuildAsync(FrequencyType.Daily);
        var first = await w.Service.GenerateDraftAsync(2026, w.Head.Id);
        await ApproveAsync(w, first);

        // Daily keys look like "D20261026"; they must count as already planned.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => w.Service.GenerateDraftAsync(2026, w.Head.Id));

        Assert.Contains("nothing to revise", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
