using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Application.Services.MyWork.Sources;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The analyst sources of My Work (ready to read, returned, tests to perform, materials) against a real
// database. Every act goes through MyWorkService on its own DbContext; arrange and assert use others.
[Collection("PostgresDatabaseCollection")]
public class MyWorkAnalystSourcesPostgresTests
{
    // 12:00 UTC on 14 Nov 2026 is 14:00 in Cairo (UTC+2); the laboratory day ends at 22:00 UTC.
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public MyWorkAnalystSourcesPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class Settings : ISystemSettingsReader
    {
        private readonly int _lookAheadDays;
        private readonly int _readOverdueHours;
        public Settings(int lookAheadDays, int readOverdueHours)
        {
            _lookAheadDays = lookAheadDays;
            _readOverdueHours = readOverdueHours;
        }

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == SystemSettingKeys.MyWorkLookAheadDays ? _lookAheadDays : _readOverdueHours);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private sealed class RecordingErrors : IErrorCaptureService
    {
        public List<ErrorCaptureRequest> Captured { get; } = new();
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default)
        {
            Captured.Add(request);
            return Task.CompletedTask;
        }
    }

    private static MyWorkService BuildService(MicroLimsDbContext db, int readOverdueHours = 0, RecordingErrors? errors = null) =>
        new(db, new UserSectionScopeService(db),
            new LabClock(new FixedTimeProvider(new DateTimeOffset(Now)), LabClock.ResolveTimeZone("Africa/Cairo")),
            new Settings(7, readOverdueHours), errors ?? new RecordingErrors(),
            new IMyWorkSource[] { new IncubationSource(db), new ReturnedTestsSource(db), new AssignedTestsSource(db), new MaterialsSource(db) });

    private async Task<MyWorkResponse> GetAsync(int userId, string? lab = null, int readOverdueHours = 0)
    {
        await using var actDb = _fixture.CreateDbContext();
        var errors = new RecordingErrors();
        var service = BuildService(actDb, readOverdueHours, errors);
        DashboardLabScope? scope = null;
        if (lab is not null)
            scope = await new DashboardLabScopeService(actDb, new UserSectionScopeService(actDb)).ResolveAsync(userId, lab);
        var response = await service.GetAsync(userId, scope);
        Assert.Empty(response.UnavailableSources);
        Assert.Empty(errors.Captured);
        return response;
    }

    private async Task<MyWorkSummary> SummaryAsync(int userId, int readOverdueHours = 0)
    {
        await using var actDb = _fixture.CreateDbContext();
        var errors = new RecordingErrors();
        var summary = await BuildService(actDb, readOverdueHours, errors).GetSummaryAsync(userId);
        Assert.Empty(errors.Captured);
        return summary;
    }

    private static MyWorkGroup? Group(MyWorkResponse r, string key) => r.Groups.SingleOrDefault(g => g.Key == key);

    private static IEnumerable<string> Keys(MyWorkResponse r) => r.Groups.SelectMany(g => g.Items).Select(i => i.Key);

    // ---- arrange helpers (each call uses the context it is given; callers pass a dedicated arrange context) ----

    private sealed record World(DocumentSection Micro, DocumentSection Fp, int CauseId);

    private static async Task<World> WorldAsync(MicroLimsDbContext db)
    {
        var micro = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "FP");
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        return new World(micro, fp, cause.Id);
    }

    private static async Task<User> NewUserAsync(MicroLimsDbContext db, string label, bool viewAll, params DocumentSection[] memberOf)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        Role role;
        if (viewAll)
        {
            role = new Role { Type = RoleType.Analyst, Name = $"Reader {uid}", IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var permission = await db.Permissions.SingleAsync(p => p.Code == "Sections.ViewAll");
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        else
        {
            role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
        }

        var user = new User { Username = $"{label}_{uid}", FullName = $"{label} {uid}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<TestOrder> NewTestAsync(MicroLimsDbContext db, World w, int receivedBy, DocumentSection section,
        int? assignedTo, string? testCode = null, bool superseded = false, ApprovalStatus status = ApprovalStatus.InProgress,
        DateTime? receivedAt = null)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var sample = new Sample
        {
            ReferenceNumber = $"MW-{uid}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = w.CauseId,
            ReceivedAt = receivedAt ?? Now.AddHours(-2),
            ReceivedByUserId = receivedBy,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testCode ?? $"T{uid}",
            SectionId = section.Id,
            Status = status,
            CurrentStep = WorkflowStep.Incubating,
            AssignedAnalystId = assignedTo,
            IsSuperseded = superseded
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<Incubation> NewIncubationAsync(MicroLimsDbContext db, TestOrder order, DateTime? expected, DateTime? end = null,
        int? overriddenBy = null, bool completed = false, int? startedBy = null)
    {
        var incubation = new Incubation
        {
            TestOrderId = order.Id,
            StepNumber = 1,
            StepName = "Plate incubation",
            StartedAt = Now.AddDays(-2),
            StartedByUserId = startedBy,
            ExpectedReadingAt = expected,
            IncubationEndUtc = end,
            MinimumDurationOverriddenByUserId = overriddenBy,
            MinimumDurationOverriddenAt = overriddenBy is null ? null : Now.AddHours(-1),
            CompletedAt = completed ? Now.AddHours(-1) : null
        };
        db.Incubations.Add(incubation);
        await db.SaveChangesAsync();
        return incubation;
    }

    // ---- tests ----

    [PostgresFact]
    public async Task ReadyIncubation_OnMyTest_AppearsInReadyToRead_NotForAnotherAnalyst()
    {
        int a, b;
        Incubation incubation;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var ua = await NewUserAsync(arrange, "mine", false, w.Micro);
            var ub = await NewUserAsync(arrange, "other", false, w.Micro);
            a = ua.Id; b = ub.Id;
            var test = await NewTestAsync(arrange, w, a, w.Micro, a);
            incubation = await NewIncubationAsync(arrange, test, expected: Now.AddHours(-1));
        }

        var mine = await GetAsync(a);
        var group = Assert.IsType<MyWorkGroup>(Group(mine, "readyToRead"));
        var item = Assert.Single(group.Items);
        Assert.Equal(1, group.Count);
        Assert.Equal($"incubation:{incubation.Id}", item.Key);
        Assert.Equal(MyWorkUrgencyLevel.Now, item.Urgency);
        Assert.Equal("incubation-read", item.Target.Type);
        Assert.Equal(incubation.Id, item.Target.IncubationId);
        Assert.Equal("MICRO", item.Lab);
        Assert.Null(Group(mine, "endingToday"));

        var theirs = await GetAsync(b);
        Assert.Empty(theirs.Groups);
    }

    [PostgresFact]
    public async Task IncubationReadyByEndTimeOnly_IsReady()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            var test = await NewTestAsync(arrange, w, a, w.Micro, a);
            await NewIncubationAsync(arrange, test, expected: null, end: Now.AddHours(-1));
        }

        var response = await GetAsync(a);
        Assert.Equal(1, Assert.IsType<MyWorkGroup>(Group(response, "readyToRead")).Count);
    }

    [PostgresFact]
    public async Task IncubationReadyByMinimumDurationOverride_IsReady()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var ua = await NewUserAsync(arrange, "mine", false, w.Micro);
            a = ua.Id;
            var test = await NewTestAsync(arrange, w, a, w.Micro, a);
            // The planned reading and the declared end are both still in the future; only the override makes it ready.
            await NewIncubationAsync(arrange, test, expected: Now.AddHours(30), end: Now.AddHours(30), overriddenBy: a);
        }

        var response = await GetAsync(a);
        Assert.Equal(1, Assert.IsType<MyWorkGroup>(Group(response, "readyToRead")).Count);
    }

    [PostgresFact]
    public async Task SameIncubation_MyWork_MyTasks_AndIncubationOverview_Agree()
    {
        int a;
        var codes = new Dictionary<string, int>();
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            var uid = Guid.NewGuid().ToString("N")[..6];
            // Three incubations, each ready by a different arm of the rule, and one that is not ready and not due soon.
            foreach (var (label, expected, end, over) in new (string, DateTime?, DateTime?, int?)[]
                     {
                         ("EXP", Now.AddHours(-1), null, null),
                         ("END", null, Now.AddHours(-1), null),
                         ("OVR", Now.AddHours(40), Now.AddHours(40), a),
                         ("NOT", Now.AddHours(72), Now.AddHours(72), null), // beyond the 2-day look-ahead of My Tasks
                     })
            {
                var test = await NewTestAsync(arrange, w, a, w.Micro, a, testCode: $"AGR_{label}_{uid}");
                var inc = await NewIncubationAsync(arrange, test, expected, end, over);
                codes[test.TestCode] = inc.Id;
            }
        }

        var myWork = await GetAsync(a);
        var readyIds = Group(myWork, "readyToRead")!.Items.Select(i => i.Target.IncubationId!.Value).ToHashSet();

        List<MyTaskDto> tasks;
        List<IncubationOverviewDto> overview;
        await using (var tasksDb = _fixture.CreateDbContext())
        {
            tasks = await new MyTasksService(tasksDb, new UserSectionScopeService(tasksDb), new FixedTimeProvider(new DateTimeOffset(Now)))
                .GetMyTasksAsync(a);
        }
        await using (var overviewDb = _fixture.CreateDbContext())
        {
            var kpi = new KpiService(overviewDb, new FixedTimeProvider(new DateTimeOffset(Now)));
            overview = await new DashboardService(overviewDb, kpi, new FixedTimeProvider(new DateTimeOffset(Now)))
                .GetIncubationOverviewAsync(myIncubationsOnly: true, userId: a);
        }

        foreach (var (code, incubationId) in codes)
        {
            var readyInMyWork = readyIds.Contains(incubationId);
            var readyInTasks = tasks.Any(t => t.TaskType == "Read Test" && t.Title.StartsWith($"Read {code}"));
            var overviewRow = overview.SingleOrDefault(o => o.TestCode == code);
            Assert.NotNull(overviewRow);
            var readyInOverview = overviewRow!.ReadyToRead == 1;
            Assert.Equal(!code.StartsWith("AGR_NOT"), readyInMyWork);
            Assert.Equal(readyInMyWork, readyInTasks);
            Assert.Equal(readyInMyWork, readyInOverview);
        }
    }

    [PostgresFact]
    public async Task Incubation_Due2330LabTime_IsToday_Due0030NextDay_IsNext24Hours()
    {
        int a;
        Incubation today, next24;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            // Cairo is UTC+2 in November: 23:30 local = 21:30 UTC (the 14th), 00:30 local on the 15th = 22:30 UTC (10.5h from Now).
            today = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: new DateTime(2026, 11, 14, 21, 30, 0, DateTimeKind.Utc));
            next24 = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc));
        }

        var response = await GetAsync(a);
        var endingGroup = Assert.IsType<MyWorkGroup>(Group(response, "endingToday"));
        var endingItem = Assert.Single(endingGroup.Items);
        Assert.Equal($"incubation:{today.Id}", endingItem.Key);
        Assert.Equal(MyWorkUrgencyLevel.Today, endingItem.Urgency);
        Assert.Equal(1, endingGroup.Count);

        var next24Group = Assert.IsType<MyWorkGroup>(Group(response, "next24Hours"));
        Assert.Equal("Incubation ending in the next 24 hours", next24Group.Title);
        var next24Item = Assert.Single(next24Group.Items);
        Assert.Equal($"incubation:{next24.Id}", next24Item.Key);
        Assert.Equal(MyWorkUrgencyLevel.Later, next24Item.Urgency);
        Assert.Equal(1, next24Group.Count);

        // The summary agrees: both today and next24Hours count in total.
        Assert.Equal(2, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task Incubation_LabMidnightAnd24HourBoundaries_PartitionedCleanly_AndSectionScoped()
    {
        int a;
        // Cairo is UTC+2 in November; Now = 12:00 UTC (14:00 Cairo) -> EndOfLabDayUtc = 22:00 UTC (00:00 Cairo on Nov 15).
        var endOfLabDayUtc = new DateTime(2026, 11, 14, 22, 0, 0, DateTimeKind.Utc);
        Incubation ready, oneMinBeforeMidnight, oneMinAfterMidnight, exactly24h, past24h, otherSection;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id; // member of MICRO only
            ready = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddMinutes(-1));
            oneMinBeforeMidnight = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: endOfLabDayUtc.AddMinutes(-1));
            oneMinAfterMidnight = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: endOfLabDayUtc.AddMinutes(1));
            exactly24h = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(24));
            past24h = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(24).AddMinutes(1));
            otherSection = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Fp, a), expected: endOfLabDayUtc.AddMinutes(1));
        }

        var response = await GetAsync(a);
        Assert.Equal(new[] { "readyToRead", "endingToday", "next24Hours" }, response.Groups.Select(g => g.Key));

        var readyGroup = Group(response, "readyToRead")!;
        Assert.Equal(new[] { $"incubation:{ready.Id}" }, readyGroup.Items.Select(i => i.Key));
        Assert.Equal(MyWorkUrgencyLevel.Now, Assert.Single(readyGroup.Items).Urgency);
        Assert.Equal(1, readyGroup.Count);

        var endingGroup = Group(response, "endingToday")!;
        Assert.Equal(new[] { $"incubation:{oneMinBeforeMidnight.Id}" }, endingGroup.Items.Select(i => i.Key));
        Assert.Equal(MyWorkUrgencyLevel.Today, Assert.Single(endingGroup.Items).Urgency);
        Assert.Equal(1, endingGroup.Count);

        var next24Group = Group(response, "next24Hours")!;
        Assert.Equal("Incubation ending in the next 24 hours", next24Group.Title);
        Assert.Equal(new[] { $"incubation:{oneMinAfterMidnight.Id}", $"incubation:{exactly24h.Id}" },
            next24Group.Items.Select(i => i.Key));
        Assert.All(next24Group.Items, i => Assert.Equal(MyWorkUrgencyLevel.Later, i.Urgency));
        Assert.Equal(2, next24Group.Count);

        var allKeys = Keys(response).ToList();
        Assert.Equal(allKeys.Distinct().Count(), allKeys.Count);
        Assert.DoesNotContain($"incubation:{past24h.Id}", allKeys);
        Assert.DoesNotContain($"incubation:{otherSection.Id}", allKeys);

        var summary = await SummaryAsync(a);
        Assert.Equal(new MyWorkSummary(4, 0, 1, true), summary);
    }

    [PostgresFact]
    public async Task ReadyIncubation_WaitedLongerThanSetting_IsOverdue_CountedAsOverdueInSummary()
    {
        int a;
        Incubation old, fresh;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            old = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(-3));
            fresh = await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(-1));
        }

        var response = await GetAsync(a, readOverdueHours: 2);
        var items = Group(response, "readyToRead")!.Items;
        Assert.Equal(MyWorkUrgencyLevel.Overdue, items.Single(i => i.Key == $"incubation:{old.Id}").Urgency);
        Assert.Equal(MyWorkUrgencyLevel.Now, items.Single(i => i.Key == $"incubation:{fresh.Id}").Urgency);
        Assert.Equal(items[0].Key, $"incubation:{old.Id}"); // oldest ready first

        var summary = await SummaryAsync(a, readOverdueHours: 2);
        Assert.Equal(new MyWorkSummary(2, 1, 1, true), summary);

        var off = await SummaryAsync(a, readOverdueHours: 0);
        Assert.Equal(new MyWorkSummary(2, 0, 2, true), off);
    }

    [PostgresFact]
    public async Task CompletedIncubation_Absent()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(-1), completed: true);
        }

        // The reading is done, so the test is something to perform, not something to read.
        Assert.Equal(new[] { "toPerform" }, (await GetAsync(a)).Groups.Select(g => g.Key));
        Assert.Equal(1, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task SupersededTest_Absent()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            var superseded = await NewTestAsync(arrange, w, a, w.Micro, a, superseded: true);
            await NewIncubationAsync(arrange, superseded, expected: Now.AddHours(-1));
            await NewTestAsync(arrange, w, a, w.Micro, a, superseded: true); // also not a test to perform
        }

        Assert.Empty((await GetAsync(a)).Groups);
        Assert.Equal(0, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task ReturnedTest_AppearsWithReason_DisappearsAfterActiveReadingRecorded()
    {
        int a;
        TestOrder test;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var ua = await NewUserAsync(arrange, "mine", false, w.Micro);
            var reviewer = await NewUserAsync(arrange, "reviewer", false, w.Micro);
            a = ua.Id;
            test = await NewTestAsync(arrange, w, a, w.Micro, a);
            // A returned count test reopens its incubation row; it must still appear once, as a returned test.
            await NewIncubationAsync(arrange, test, expected: Now.AddHours(-5));
            arrange.TestReturnEvents.Add(new TestReturnEvent
            {
                TestOrderId = test.Id, ReviewerUserId = reviewer.Id, AssignedAnalystId = a,
                Reason = "Recount plate 2", ReturnedAt = Now.AddHours(-3)
            });
            await arrange.SaveChangesAsync();
        }

        var before = await GetAsync(a);
        var group = Assert.IsType<MyWorkGroup>(Group(before, "returned"));
        var item = Assert.Single(group.Items);
        Assert.Equal($"returned:{test.Id}", item.Key);
        Assert.Equal("Recount plate 2", item.Context);
        Assert.Equal(MyWorkUrgencyLevel.Now, item.Urgency);
        Assert.Equal(test.Id, item.Target.TestOrderId);
        // Once, and only here: neither its reopened incubation nor the assignment lists it again.
        Assert.Equal(new[] { "returned" }, before.Groups.Select(g => g.Key));
        Assert.Equal(1, (await SummaryAsync(a)).Total);

        await using (var arrange2 = _fixture.CreateDbContext())
        {
            var reopened = await arrange2.Incubations.SingleAsync(i => i.TestOrderId == test.Id);
            reopened.CompletedAt = Now.AddHours(-1); // recording the reading closes the incubation again
            arrange2.CountTestReadings.Add(new CountTestReading
            {
                TestOrderId = test.Id, PlateReadings = "12,14", DilutionFactor = 1m, ReportedResult = "13",
                IsActive = true, EnteredByUserId = a, EnteredAt = Now.AddHours(-1)
            });
            await arrange2.SaveChangesAsync();
        }

        var after = await GetAsync(a);
        Assert.Equal(new[] { "toPerform" }, after.Groups.Select(g => g.Key));
        Assert.Equal(1, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task ReturnedToAnotherAnalyst_NotInMyReturned()
    {
        int a, b;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            b = (await NewUserAsync(arrange, "other", false, w.Micro)).Id;
            var test = await NewTestAsync(arrange, w, b, w.Micro, b);
            arrange.TestReturnEvents.Add(new TestReturnEvent
            {
                TestOrderId = test.Id, ReviewerUserId = a, AssignedAnalystId = b, Reason = "Fix", ReturnedAt = Now.AddHours(-3)
            });
            await arrange.SaveChangesAsync();
        }

        Assert.Empty((await GetAsync(a)).Groups);
        Assert.Equal(new[] { "returned" }, (await GetAsync(b)).Groups.Select(g => g.Key));
    }

    [PostgresFact]
    public async Task AssignedTest_WithOpenIncubation_AppearsOnlyInIncubationGroup()
    {
        int a;
        TestOrder withIncubation, plain, waiting;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id;
            withIncubation = await NewTestAsync(arrange, w, a, w.Micro, a);
            await NewIncubationAsync(arrange, withIncubation, expected: Now.AddHours(-1));
            plain = await NewTestAsync(arrange, w, a, w.Micro, a);
            waiting = await NewTestAsync(arrange, w, a, w.Micro, a);
            await NewIncubationAsync(arrange, waiting, expected: Now.AddHours(40)); // still incubating, not today
        }

        var response = await GetAsync(a);
        Assert.Equal(new[] { "readyToRead", "toPerform" }, response.Groups.Select(g => g.Key));
        var perform = Group(response, "toPerform")!;
        var item = Assert.Single(perform.Items);
        Assert.Equal($"test:{plain.Id}", item.Key);
        Assert.Equal("test-step", item.Target.Type);
        Assert.Equal(plain.Id, item.Target.TestOrderId);
        Assert.Equal(1, perform.Count);
        Assert.Equal(withIncubation.Id, Assert.Single(Group(response, "readyToRead")!.Items).Target.TestOrderId);
        Assert.Equal(2, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task IncubationIStartedOnSomeoneElsesTest_IsMine_ButNotTheirToPerform()
    {
        int a, b;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "starter", false, w.Micro)).Id;
            b = (await NewUserAsync(arrange, "assignee", false, w.Micro)).Id;
            var test = await NewTestAsync(arrange, w, a, w.Micro, b);
            await NewIncubationAsync(arrange, test, expected: Now.AddHours(-1), startedBy: a);
        }

        Assert.Equal(1, Group(await GetAsync(a), "readyToRead")!.Count);
        Assert.Equal(1, Group(await GetAsync(b), "readyToRead")!.Count); // the assignee sees it too
    }

    [PostgresFact]
    public async Task LabFilterMicro_ExcludesPhysicochemical_SummaryCountsBoth()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "twolabs", false, w.Micro, w.Fp)).Id;
            await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(-1));
            await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Fp, a), expected: Now.AddHours(-1));
            await NewTestAsync(arrange, w, a, w.Fp, a); // physicochemical test to perform
        }

        var both = await GetAsync(a);
        Assert.Equal(2, Group(both, "readyToRead")!.Count);

        var micro = await GetAsync(a, lab: "MICRO");
        var ready = Assert.Single(micro.Groups);
        Assert.Equal("readyToRead", ready.Key);
        Assert.All(micro.Groups.SelectMany(g => g.Items), i => Assert.Equal("MICRO", i.Lab));
        Assert.Equal(1, ready.Count);

        var fp = await GetAsync(a, lab: "FP");
        Assert.All(fp.Groups.SelectMany(g => g.Items), i => Assert.Equal("FP", i.Lab));
        Assert.Equal(2, fp.Groups.Sum(g => g.Count));

        // The sidebar badge is never narrowed to a laboratory: it is the sum over both.
        Assert.Equal(micro.Groups.Sum(g => g.Count) + fp.Groups.Sum(g => g.Count), (await SummaryAsync(a)).Total);
        Assert.Equal(3, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task SectionsViewAll_DoesNotAddOtherAnalystsTests()
    {
        int reader, b;
        TestOrder mine;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            reader = (await NewUserAsync(arrange, "reader", viewAll: true)).Id;
            b = (await NewUserAsync(arrange, "other", false, w.Micro)).Id;
            var theirs = await NewTestAsync(arrange, w, b, w.Micro, b);
            await NewIncubationAsync(arrange, theirs, expected: Now.AddHours(-1));
            await NewTestAsync(arrange, w, b, w.Fp, b);
            arrange.TestReturnEvents.Add(new TestReturnEvent
            {
                TestOrderId = theirs.Id, ReviewerUserId = reader, AssignedAnalystId = b, Reason = "x", ReturnedAt = Now.AddHours(-2)
            });
            mine = await NewTestAsync(arrange, w, reader, w.Fp, reader);
            await arrange.SaveChangesAsync();
        }

        var response = await GetAsync(reader);
        Assert.Equal(new[] { "toPerform" }, response.Groups.Select(g => g.Key));
        Assert.Equal($"test:{mine.Id}", Assert.Single(Group(response, "toPerform")!.Items).Key);
        Assert.Equal(1, (await SummaryAsync(reader)).Total);
    }

    [PostgresFact]
    public async Task TestInSectionINoLongerBelongTo_Absent()
    {
        int a;
        TestOrder inMine;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mine", false, w.Micro)).Id; // no longer in FP
            inMine = await NewTestAsync(arrange, w, a, w.Micro, a);
            var gone = await NewTestAsync(arrange, w, a, w.Fp, a);
            await NewIncubationAsync(arrange, gone, expected: Now.AddHours(-1));
        }

        var response = await GetAsync(a);
        Assert.Equal(new[] { "toPerform" }, response.Groups.Select(g => g.Key));
        Assert.Equal($"test:{inMine.Id}", Assert.Single(Group(response, "toPerform")!.Items).Key);
        Assert.Equal(1, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task MaterialsIPreparedInSectionINoLongerBelongTo_Absent()
    {
        int a;
        Media inMine;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "prep", false, w.Micro)).Id; // no longer in FP
            inMine = await NewMediaAsync(arrange, await NewMaterialAsync(arrange, w.Micro), a, Now.AddDays(1));
            var goneLot = await NewMediaAsync(arrange, await NewMaterialAsync(arrange, w.Fp), a, Now.AddDays(1));
            var goneEvalLot = await NewMediaAsync(arrange, await NewMaterialAsync(arrange, w.Fp), a, Now.AddDays(60));
            arrange.MediaEvaluations.Add(new MediaEvaluation { MediaId = goneEvalLot.Id, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Assigned });
            await arrange.SaveChangesAsync();
        }

        var group = Assert.IsType<MyWorkGroup>(Group(await GetAsync(a), "materials"));
        Assert.Equal($"media:{inMine.Id}", Assert.Single(group.Items).Key);
        Assert.Equal(1, group.Count);
        Assert.Equal(1, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task MediaIPrepared_ExpiringIn7Days_Appears_In8Days_Absent()
    {
        int a, b;
        Media in7, in8, others, completedEval, openEval;
        MediaEvaluation evaluationOpen;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "prep", false, w.Micro)).Id;
            b = (await NewUserAsync(arrange, "other", false, w.Micro)).Id;
            var material = await NewMaterialAsync(arrange, w.Micro);
            in7 = await NewMediaAsync(arrange, material, a, Now.AddDays(7));
            in8 = await NewMediaAsync(arrange, material, a, Now.AddDays(8));
            others = await NewMediaAsync(arrange, material, b, Now.AddDays(1));
            completedEval = await NewMediaAsync(arrange, material, a, Now.AddDays(60));
            openEval = await NewMediaAsync(arrange, material, a, Now.AddDays(60));
            arrange.MediaEvaluations.Add(new MediaEvaluation { MediaId = completedEval.Id, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Completed });
            evaluationOpen = new MediaEvaluation { MediaId = openEval.Id, EvaluationType = EvaluationType.GrowthPromotion, Status = MediaEvaluationStatus.Assigned };
            arrange.MediaEvaluations.Add(evaluationOpen);
            await arrange.SaveChangesAsync();
        }

        var response = await GetAsync(a);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "materials"));
        var keys = group.Items.Select(i => i.Key).ToList();
        Assert.Contains($"media:{in7.Id}", keys);
        Assert.DoesNotContain($"media:{in8.Id}", keys);
        Assert.DoesNotContain($"media:{others.Id}", keys);
        Assert.Contains($"media-evaluation:{evaluationOpen.Id}", keys);
        Assert.Equal(2, group.Count);
        Assert.Equal("media", group.Items.Single(i => i.Key == $"media:{in7.Id}").Target.Type);
        Assert.Equal("media-evaluation", group.Items.Single(i => i.Key == $"media-evaluation:{evaluationOpen.Id}").Target.Type);
        Assert.Equal(MyWorkUrgencyLevel.Later, group.Items.Single(i => i.Key == $"media:{in7.Id}").Urgency);

        Assert.DoesNotContain($"media:{in7.Id}", Keys(await GetAsync(b)));
    }

    [PostgresFact]
    public async Task SolutionAlreadyExpired_IsOverdue()
    {
        int a;
        SolutionPreparation expired, valid, discarded;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "prep", false, w.Micro)).Id;
            var master = new SolutionMaster
            {
                SectionId = w.Micro.Id, Name = $"Diluent {Guid.NewGuid():N}", Type = SolutionType.Diluent, ShelfLifeValue = 7,
                ShelfLifeUnit = ShelfLifeUnit.Days, StorageCondition = "2-8C", FinalVolumeMl = 100m, Instructions = "Mix"
            };
            arrange.SolutionMasters.Add(master);
            await arrange.SaveChangesAsync();
            expired = await NewSolutionAsync(arrange, master, w.Micro, a, SolutionPreparationStatus.Prepared, Now.AddDays(-1));
            valid = await NewSolutionAsync(arrange, master, w.Micro, a, SolutionPreparationStatus.Prepared, Now.AddDays(30));
            discarded = await NewSolutionAsync(arrange, master, w.Micro, a, SolutionPreparationStatus.Discarded, Now.AddDays(-1));
        }

        var response = await GetAsync(a);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "materials"));
        var item = Assert.Single(group.Items);
        Assert.Equal($"solution:{expired.Id}", item.Key);
        Assert.Equal(MyWorkUrgencyLevel.Overdue, item.Urgency);
        Assert.Equal("solution", item.Target.Type);
        Assert.Equal(new MyWorkSummary(1, 1, 0, true), await SummaryAsync(a));
        Assert.DoesNotContain($"solution:{valid.Id}", Keys(response));
        Assert.DoesNotContain($"solution:{discarded.Id}", Keys(response));
    }

    [PostgresFact]
    public async Task ThawedCryovialStillNotDestroyed_AppearsOnceItsReminderIsDue()
    {
        int a, b;
        ThawEvent due, fresh, others;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "thaw", false, w.Micro)).Id;
            b = (await NewUserAsync(arrange, "other", false, w.Micro)).Id;
            var material = await NewMaterialAsync(arrange, w.Micro);
            var organism = new Organism { ScientificName = $"Org {Guid.NewGuid():N}" };
            arrange.Organisms.Add(organism);
            await arrange.SaveChangesAsync();
            var cryovial = new Cryovial
            {
                Code = $"CV-{Guid.NewGuid():N}"[..12], MaterialId = material.Id, OrganismId = organism.Id, OrganismNameSnapshot = organism.ScientificName,
                ManufacturerName = "ATCC", ExpiryDate = Now.AddDays(100), NumberOfVialsPrepared = 3, VialsRemaining = 3,
                StorageCondition = "-20C", PreparedByUserId = a
            };
            arrange.Cryovials.Add(cryovial);
            await arrange.SaveChangesAsync();
            due = new ThawEvent { CryovialId = cryovial.Id, ThawedByUserId = a, ThawedAt = Now.AddHours(-49) };
            fresh = new ThawEvent { CryovialId = cryovial.Id, ThawedByUserId = a, ThawedAt = Now.AddHours(-2) };
            others = new ThawEvent { CryovialId = cryovial.Id, ThawedByUserId = b, ThawedAt = Now.AddHours(-49) };
            arrange.ThawEvents.AddRange(due, fresh, others);
            await arrange.SaveChangesAsync();
        }

        var response = await GetAsync(a);
        var item = Assert.Single(Assert.IsType<MyWorkGroup>(Group(response, "materials")).Items);
        Assert.Equal($"cryovial:{due.Id}", item.Key);
        Assert.Equal("cryovial", item.Target.Type);
        Assert.Equal(MyWorkUrgencyLevel.Overdue, item.Urgency);
    }

    [PostgresFact]
    public async Task FiftyOneAssignedTests_Returns50_Count51()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "busy", false, w.Micro)).Id;
            for (var i = 0; i < 51; i++)
                await NewTestAsync(arrange, w, a, w.Micro, a, receivedAt: Now.AddMinutes(-i));
        }

        var response = await GetAsync(a);
        var group = Assert.IsType<MyWorkGroup>(Group(response, "toPerform"));
        Assert.Equal(50, group.Items.Count);
        Assert.Equal(51, group.Count);
        Assert.True(group.HasMore);
        // Oldest first: the 51st-newest (the one received 50 minutes ago) is the first row.
        Assert.Equal(51, (await SummaryAsync(a)).Total);
    }

    [PostgresFact]
    public async Task SummaryCounts_MatchTheListedItemsByUrgency()
    {
        int a;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = (await NewUserAsync(arrange, "mixed", false, w.Micro)).Id;
            await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(-5));   // overdue read (setting 2 h)
            await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddMinutes(-30)); // ready now
            await NewIncubationAsync(arrange, await NewTestAsync(arrange, w, a, w.Micro, a), expected: Now.AddHours(3));    // ending today
            await NewTestAsync(arrange, w, a, w.Micro, a, receivedAt: Now.AddDays(-3));                                    // overdue to perform
            await NewTestAsync(arrange, w, a, w.Micro, a);                                                                 // to perform, no urgency
            var returned = await NewTestAsync(arrange, w, a, w.Micro, a);
            arrange.TestReturnEvents.Add(new TestReturnEvent { TestOrderId = returned.Id, ReviewerUserId = a, AssignedAnalystId = a, ReturnedAt = Now.AddHours(-1) });
            var material = await NewMaterialAsync(arrange, w.Micro);
            await NewMediaAsync(arrange, material, a, Now.AddDays(-2));  // overdue
            await NewMediaAsync(arrange, material, a, Now.AddDays(3));   // later
            await arrange.SaveChangesAsync();
        }

        var list = await GetAsync(a, readOverdueHours: 2);
        var items = list.Groups.SelectMany(g => g.Items).ToList();
        var summary = await SummaryAsync(a, readOverdueHours: 2);

        Assert.Equal(items.Count, list.Groups.Sum(g => g.Count)); // nothing capped here
        Assert.Equal(items.Count, summary.Total);
        Assert.Equal(items.Count(i => i.Urgency == MyWorkUrgencyLevel.Overdue), summary.Overdue);
        Assert.Equal(items.Count(i => i.Urgency == MyWorkUrgencyLevel.Now), summary.Now);
        Assert.Equal(8, items.Count);
        Assert.Equal(3, summary.Overdue); // read overdue, perform overdue, media expired
    }

    // ---- inventory seeding ----

    private static async Task<Material> NewMaterialAsync(MicroLimsDbContext db, DocumentSection section)
    {
        var material = new Material
        {
            SectionId = section.Id, MaterialType = MaterialType.DehydratedMedia, MaterialName = $"TSA {Guid.NewGuid():N}"[..12],
            ManufacturerName = "Acme", BatchNumber = $"B-{Guid.NewGuid():N}"[..10], ReceivingDate = Now.AddDays(-30),
            ExpiryDate = Now.AddDays(300), Location = "Shelf", QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return material;
    }

    private static async Task<Media> NewMediaAsync(MicroLimsDbContext db, Material material, int preparedBy, DateTime expiry)
    {
        var media = new Media
        {
            MaterialId = material.Id, LotNumber = $"LOT-{Guid.NewGuid():N}"[..14], ManufacturerLot = material.BatchNumber, ManufacturerName = "Acme",
            TotalWeight = 10m, TotalVolume = "500 ml", AutoclaveProgram = "P1", LoadType = "agar (500 ml)", Temperature = 121m, CycleTime = 15,
            CycleNumber = 1, Ph = 7.0m, ExpiryDate = expiry, PreparedAt = Now.AddDays(-5), PreparedByUserId = preparedBy, Status = MediaStatus.Active
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return media;
    }

    private static async Task<SolutionPreparation> NewSolutionAsync(MicroLimsDbContext db, SolutionMaster master, DocumentSection section,
        int preparedBy, SolutionPreparationStatus status, DateTime expiresAt)
    {
        var prep = new SolutionPreparation
        {
            SectionId = section.Id, SolutionMasterId = master.Id, Type = master.Type, Status = status, StartedByUserId = preparedBy,
            StartedAt = Now.AddDays(-10), PreparedByUserId = preparedBy, PreparedAt = Now.AddDays(-10), ExpiresAt = expiresAt
        };
        db.SolutionPreparations.Add(prep);
        await db.SaveChangesAsync();
        return prep;
    }
}
