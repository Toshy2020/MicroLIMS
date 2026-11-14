using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.MyWork;

public class MyWorkServiceTests
{
    // 21:30 UTC on 14 Nov 2026 is 23:30 in Cairo (UTC+2, no daylight saving in November).
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 14, 21, 30, 0, TimeSpan.Zero);

    private sealed class FakeSettings : ISystemSettingsReader
    {
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == "myWork.lookAheadDays" ? 7 : 0);
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

    private sealed class StubSource : IMyWorkSource
    {
        public StubSource(string key) => Key = key;
        public string Key { get; }
        public bool Applies { get; init; } = true;
        public Exception? Throws { get; init; }
        public MyWorkSourceResult Result { get; init; } = new(Array.Empty<MyWorkItem>(), new Dictionary<string, int>(), new Dictionary<string, int>());
        public IReadOnlyDictionary<MyWorkUrgencyLevel, int> Counts { get; init; } = new Dictionary<MyWorkUrgencyLevel, int>();
        public int GetCalls { get; private set; }
        public MyWorkContext? SeenContext { get; private set; }
        public bool AppliesTo(MyWorkContext ctx) => Applies;
        public Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
        {
            GetCalls++;
            SeenContext = ctx;
            if (Throws is not null) throw Throws;
            return Task.FromResult(Result);
        }
        public Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
        {
            if (Throws is not null) throw Throws;
            return Task.FromResult(Counts);
        }
    }

    private static MyWorkItem Item(string key, string group, MyWorkUrgencyLevel urgency, DateTime sortAt, bool locked = false) =>
        new(key, group, key, key, null, null, urgency, sortAt, locked, locked ? "locked" : null,
            new MyWorkTarget("sample", SampleId: 1), Array.Empty<string>(), null, null, null);

    private static MyWorkSourceResult Result(params MyWorkItem[] items) =>
        new(items,
            items.GroupBy(i => i.GroupKey).ToDictionary(g => g.Key, g => g.Count()),
            items.Where(i => i.Locked).GroupBy(i => i.GroupKey).ToDictionary(g => g.Key, g => g.Count()));

    private sealed record Harness(MyWorkService Service, RecordingErrors Errors, int UserId);

    private static async Task<Harness> BuildAsync(IEnumerable<IMyWorkSource> sources, string zone = "Africa/Cairo")
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { Username = "u", FullName = "U", RoleId = role.Id, IsActive = true };
        var permission = new Permission { Code = "Tests.Probe", Description = "probe" };
        db.Users.Add(user);
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();

        var errors = new RecordingErrors();
        var clock = new LabClock(new FixedTimeProvider(FixedNow), LabClock.ResolveTimeZone(zone));
        var service = new MyWorkService(db, new StubSectionScope(_ => new[] { 10, 11 }), clock, new FakeSettings(), errors, sources);
        return new Harness(service, errors, user.Id);
    }

    [Fact]
    public async Task GroupsFollowFixedOrder_EmptyGroupsOmitted()
    {
        var t = FixedNow.UtcDateTime;
        var source = new StubSource("s")
        {
            Result = Result(
                Item("a", "approval", MyWorkUrgencyLevel.Now, t),
                Item("b", "readyToRead", MyWorkUrgencyLevel.Now, t),
                Item("e", "next24Hours", MyWorkUrgencyLevel.Later, t),
                Item("f", "endingToday", MyWorkUrgencyLevel.Today, t),
                Item("c", "toPerform", MyWorkUrgencyLevel.Now, t),
                Item("d", "materials", MyWorkUrgencyLevel.Now, t))
        };
        var h = await BuildAsync(new[] { source });

        var response = await h.Service.GetAsync(h.UserId, null);

        Assert.Equal(new[] { "readyToRead", "endingToday", "next24Hours", "toPerform", "approval", "materials" },
            response.Groups.Select(g => g.Key));
        Assert.Equal(new[]
            {
                "Ready to read now",
                "Incubation ending today",
                "Incubation ending in the next 24 hours",
                "Tests to perform",
                "Awaiting my approval",
                "Media, solutions and strains"
            },
            response.Groups.Select(g => g.Title));
        Assert.Equal("Africa/Cairo", response.TimeZoneId);
        Assert.Equal(FixedNow.UtcDateTime, response.ServerTimeUtc);
    }

    [Fact]
    public async Task ItemsOrderedUnlockedThenUrgencyThenOldest()
    {
        var t = FixedNow.UtcDateTime;
        var source = new StubSource("s")
        {
            Result = Result(
                Item("lockedOverdue", "review", MyWorkUrgencyLevel.Overdue, t.AddHours(-9), locked: true),
                Item("unlockedLaterOld", "review", MyWorkUrgencyLevel.Later, t.AddHours(-8)),
                Item("unlockedNowNew", "review", MyWorkUrgencyLevel.Now, t.AddHours(-1)),
                Item("unlockedNowOld", "review", MyWorkUrgencyLevel.Now, t.AddHours(-5)),
                Item("unlockedOverdue", "review", MyWorkUrgencyLevel.Overdue, t.AddHours(-2)))
        };
        var h = await BuildAsync(new[] { source });

        var group = Assert.Single((await h.Service.GetAsync(h.UserId, null)).Groups);

        Assert.Equal(new[] { "unlockedOverdue", "unlockedNowOld", "unlockedNowNew", "unlockedLaterOld", "lockedOverdue" },
            group.Items.Select(i => i.Key));
        Assert.Equal(1, group.LockedCount);
    }

    [Fact]
    public async Task GroupCappedAt50_CountIsExact_HasMoreTrue()
    {
        var t = FixedNow.UtcDateTime;
        var items = Enumerable.Range(0, 50).Select(i => Item("i" + i, "toPerform", MyWorkUrgencyLevel.Now, t.AddMinutes(i))).ToArray();
        var source = new StubSource("s")
        {
            Result = new MyWorkSourceResult(items, new Dictionary<string, int> { ["toPerform"] = 137 }, new Dictionary<string, int>())
        };
        var other = new StubSource("o") { Result = Result(Item("x", "toPerform", MyWorkUrgencyLevel.Overdue, t)) };
        var h = await BuildAsync(new IMyWorkSource[] { source, other });

        var group = Assert.Single((await h.Service.GetAsync(h.UserId, null)).Groups);

        Assert.Equal(50, group.Items.Count);
        Assert.Equal(138, group.Count);
        Assert.True(group.HasMore);
        Assert.Equal("x", group.Items[0].Key);
    }

    [Fact]
    public async Task SourceNotApplicable_IsNotRunAndNotReported()
    {
        var skipped = new StubSource("skipped") { Applies = false, Throws = new InvalidOperationException("must not run") };
        var h = await BuildAsync(new[] { skipped });

        var response = await h.Service.GetAsync(h.UserId, null);
        var summary = await h.Service.GetSummaryAsync(h.UserId);

        Assert.Equal(0, skipped.GetCalls);
        Assert.Empty(response.UnavailableSources);
        Assert.Empty(h.Errors.Captured);
        Assert.True(summary.Complete);
    }

    [Fact]
    public async Task ThrowingSource_OthersReturned_KeyListed_ErrorCaptured()
    {
        var t = FixedNow.UtcDateTime;
        var good = new StubSource("good") { Result = Result(Item("a", "review", MyWorkUrgencyLevel.Now, t)) };
        var bad = new StubSource("bad") { Throws = new InvalidOperationException("boom") };
        var h = await BuildAsync(new IMyWorkSource[] { bad, good });

        var response = await h.Service.GetAsync(h.UserId, null);

        Assert.Equal(new[] { "bad" }, response.UnavailableSources);
        Assert.Equal("review", Assert.Single(response.Groups).Key);
        var captured = Assert.Single(h.Errors.Captured);
        Assert.Equal(typeof(InvalidOperationException).FullName, captured.ExceptionType);
        Assert.Equal("boom", captured.Message);
        Assert.Equal(h.UserId, captured.UserId);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        var cancelling = new StubSource("c") { Throws = new OperationCanceledException() };
        var h = await BuildAsync(new[] { cancelling });

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Service.GetAsync(h.UserId, null));
        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Service.GetSummaryAsync(h.UserId));
        Assert.Empty(h.Errors.Captured);
    }

    [Fact]
    public async Task Summary_WithFailedSource_IsIncompleteWithNulls()
    {
        var good = new StubSource("good")
        {
            Counts = new Dictionary<MyWorkUrgencyLevel, int> { [MyWorkUrgencyLevel.Overdue] = 2, [MyWorkUrgencyLevel.Now] = 3, [MyWorkUrgencyLevel.Later] = 4 }
        };
        var bad = new StubSource("bad") { Throws = new InvalidOperationException("boom") };

        var okHarness = await BuildAsync(new[] { good });
        var ok = await okHarness.Service.GetSummaryAsync(okHarness.UserId);
        Assert.Equal(new MyWorkSummary(9, 2, 3, true), ok);

        var h = await BuildAsync(new IMyWorkSource[] { good, bad });
        var failed = await h.Service.GetSummaryAsync(h.UserId);
        Assert.Equal(new MyWorkSummary(null, null, null, false), failed);
        Assert.Single(h.Errors.Captured);
    }

    [Fact]
    public async Task EndOfLabDay_UsesLabClockZone()
    {
        var source = new StubSource("s");
        var h = await BuildAsync(new[] { source }, "Africa/Cairo");

        await h.Service.GetAsync(h.UserId, null);

        var ctx = Assert.IsType<MyWorkContext>(source.SeenContext);
        // 21:30 UTC is 23:30 in Cairo, so the lab day ends at 00:00 +02:00 = 22:00 UTC (the UTC date would say 00:00 UTC).
        Assert.Equal(new DateTime(2026, 11, 14, 22, 0, 0, DateTimeKind.Utc), ctx.EndOfLabDayUtc);
        Assert.Equal(DateTimeKind.Utc, ctx.EndOfLabDayUtc.Kind);
        Assert.Equal(FixedNow.UtcDateTime, ctx.NowUtc);
    }

    [Fact]
    public async Task Context_CarriesCallerIdPermissionsScopeAndSettings()
    {
        var source = new StubSource("s");
        var h = await BuildAsync(new[] { source });

        await h.Service.GetAsync(h.UserId, null);

        var ctx = Assert.IsType<MyWorkContext>(source.SeenContext);
        Assert.Equal(h.UserId, ctx.UserId);
        Assert.Contains("Tests.Probe", ctx.Permissions);
        Assert.Equal(new[] { 10, 11 }, ctx.ReadableSectionIds);
        Assert.Equal(new[] { 10, 11 }, ctx.MemberSectionIds);
        Assert.Equal(7, ctx.LookAheadDays);
        Assert.Equal(0, ctx.ReadOverdueHours);
    }
}
