using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiScopeAuthorizationTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Records every query it is asked to compute.
    private sealed class FakeKpi : IKpi
    {
        public List<KpiQuery> Seen { get; } = new();
        public Func<KpiQuery, KpiResult>? Behaviour { get; init; }
        public Func<KpiQuery, int, int, KpiRecordPageDto>? Records { get; init; }
        public FakeKpi(string key, int order, KpiGroup group = KpiGroup.Headline) { Key = key; Order = order; Group = group; }
        public string Key { get; }
        public string Name => Key;
        public string Unit => "%";
        public KpiDirection Direction => KpiDirection.Neutral;
        public KpiGroup Group { get; }
        public int Order { get; }
        public string Definition => "def " + Key;
        public string ClockEvents => "clock";
        public string Population => "pop";
        public string Source => "src";
        public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct)
        {
            Seen.Add(q);
            return Task.FromResult(Behaviour?.Invoke(q) ?? Ok());
        }
        public Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct)
        {
            Seen.Add(q);
            return Task.FromResult(Records?.Invoke(q, page, pageSize) ?? new KpiRecordPageDto(Key, page, pageSize, 0, Array.Empty<KpiRecordDto>()));
        }
        public static KpiResult Ok() => KpiScope.ToResult(new KpiCore(0.5m, 1, 2, 6, KpiCore.NoGaps, KpiCore.NoSeries, KpiCore.NoExtras, KpiCore.NoFlags), null, "%");
    }

    private static KpiDashboardService Service(MicroLimsDbContext db, Func<int, IReadOnlyList<int>?> scope, params IKpi[] kpis) =>
        new(db, new KpiRegistry(kpis), new StubSectionScope(scope), Clock);

    [Fact]
    public async Task Dashboard_ReturnsEveryRegisteredKpi_OrderedByOrder_WithHalfOpenWindow()
    {
        await using var db = NewDb();
        var b = new FakeKpi("b", 2);
        var a = new FakeKpi("a", 1);
        var dto = await Service(db, _ => null, b, a).GetDashboardAsync(1, true, From, To, null, null, null, null);

        Assert.Equal(new[] { "a", "b" }, dto.Kpis.Select(k => k.Key));
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), dto.From);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), dto.To);   // exclusive: to + 1 day
        Assert.Equal(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), dto.PriorFrom);
        Assert.Equal(dto.From, dto.PriorTo);
        Assert.Equal(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc), dto.GeneratedAt);
        Assert.Equal(dto.GeneratedAt, a.Seen.Single().Now);
    }

    [Fact]
    public async Task FailingKpi_BecomesErrorEntry_AndOthersStillCompute()
    {
        await using var db = NewDb();
        var bad = new FakeKpi("bad", 1) { Behaviour = _ => throw new InvalidOperationException("Npgsql: relation \"Secret\" does not exist") };
        var good = new FakeKpi("good", 2);
        var dto = await Service(db, _ => null, bad, good).GetDashboardAsync(1, true, From, To, null, null, null, null);

        var badEntry = dto.Kpis.Single(k => k.Key == "bad");
        Assert.Null(badEntry.Result);
        Assert.False(string.IsNullOrWhiteSpace(badEntry.Error));
        Assert.DoesNotContain("Secret", badEntry.Error);     // never leak the exception text
        Assert.NotNull(dto.Kpis.Single(k => k.Key == "good").Result);
    }

    [Fact]
    public async Task NonTeamViewer_AskingForAnotherAnalyst_Throws()
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetDashboardAsync(5, false, From, To, null, null, null, 9));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetRecordsAsync("a", 5, false, From, To, null, null, null, 9, 1, 50));
    }

    [Fact]
    public async Task NonTeamViewer_OwnAnalystId_IsPassedThrough()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetDashboardAsync(5, false, From, To, null, null, null, 5);
        Assert.Equal(5, kpi.Seen.Single().AnalystId);
    }

    [Fact]
    public async Task NonTeamViewer_NoAnalystId_GetsLabLevelFigures()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetDashboardAsync(5, false, From, To, null, null, null, null);
        var q = kpi.Seen.Single();
        Assert.Null(q.AnalystId);
        Assert.False(q.CanViewTeam);
        Assert.Equal(5, q.ViewerUserId);
    }

    [Fact]
    public async Task TeamViewer_MayAskForAnyAnalyst()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetDashboardAsync(5, true, From, To, null, null, null, 9);
        Assert.Equal(9, kpi.Seen.Single().AnalystId);
    }

    [Fact]
    public async Task Scope_NullStaysNull_EmptyStaysEmpty()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        var svc = Service(db, id => id == 1 ? null : id == 2 ? Array.Empty<int>() : new[] { 4, 7 }, kpi);

        await svc.GetDashboardAsync(1, true, From, To, null, null, null, null);
        await svc.GetDashboardAsync(2, true, From, To, null, null, null, null);
        await svc.GetDashboardAsync(3, true, From, To, null, null, null, null);

        Assert.Null(kpi.Seen[0].SectionIds);                     // administrator: unrestricted
        Assert.NotNull(kpi.Seen[1].SectionIds);                  // no sections: must not collapse to "no filter"
        Assert.Empty(kpi.Seen[1].SectionIds!);
        Assert.Equal(new[] { 4, 7 }, kpi.Seen[2].SectionIds);
    }

    [Theory]
    [InlineData("2026-10-31", "2026-10-01")]
    public async Task Dates_FromAfterTo_IsRejected(string from, string to)
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetDashboardAsync(1, true, DateOnly.Parse(from), DateOnly.Parse(to), null, null, null, null));
        Assert.Contains("start date", ex.Message);
    }

    [Fact]
    public async Task Dates_Missing_AreRejected()
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetDashboardAsync(1, true, default, default, null, null, null, null));
    }

    [Fact]
    public async Task UnknownCategory_IsRejected_AndKnownCategoryIsPassedThrough()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        var svc = Service(db, _ => null, kpi);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetDashboardAsync(1, true, From, To, "Nonsense", null, null, null));
        await svc.GetDashboardAsync(1, true, From, To, "water", null, null, null);
        Assert.Equal("water", kpi.Seen.Single().Category);   // KPIs parse it with ParseCategory (case-insensitive)
    }

    [Fact]
    public async Task Records_PageStartsAtOne_AndSizeIsCappedAt200()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        var svc = Service(db, _ => null, kpi);
        var page = await svc.GetRecordsAsync("a", 1, true, From, To, null, null, null, null, 0, 5000);
        Assert.Equal(1, page.Page);
        Assert.Equal(200, page.PageSize);
        var def = await svc.GetRecordsAsync("a", 1, true, From, To, null, null, null, null, 2, 0);
        Assert.Equal(50, def.PageSize);
    }

    [Fact]
    public async Task Records_UnknownKey_IsNotFound()
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetRecordsAsync("nope", 1, true, From, To, null, null, null, null, 1, 50));
    }

    [Fact]
    public async Task Records_HideOtherAnalystNames_WithoutViewTeam()
    {
        // The KPI decides the label with KpiScope.AnalystLabel; the service must hand it CanViewTeam = false.
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetRecordsAsync("a", 5, false, From, To, null, null, null, null, 1, 50);
        var q = kpi.Seen.Single();
        Assert.False(q.CanViewTeam);
        var names = new Dictionary<int, string> { [5] = "Me", [9] = "Other" };
        Assert.Equal("Me", KpiScope.AnalystLabel(q, 5, names));
        Assert.Null(KpiScope.AnalystLabel(q, 9, names));
        Assert.Equal("Other", KpiScope.AnalystLabel(q with { CanViewTeam = true }, 9, names));
    }

    [Fact]
    public async Task Definitions_ProfilesInForce_AreResolvedAtTo()
    {
        await using var db = NewDb();
        db.WorkflowProfileVersions.AddRange(
            Version(1, WorkflowType.CountTest, null, 2m, 5m, new DateOnly(2026, 1, 1)),
            Version(2, WorkflowType.CountTest, null, 3m, null, new DateOnly(2026, 10, 10)),   // units only: target stays 5
            Version(3, WorkflowType.CountTest, null, 9m, 9m, new DateOnly(2026, 12, 1)),      // after "to": ignored
            Version(4, WorkflowType.CountTest, "TAMC", null, 14m, new DateOnly(2026, 2, 1)),
            Version(5, WorkflowType.Titration, null, 1m, null, new DateOnly(2026, 2, 1), WorkflowProfileStatus.Draft));
        await db.SaveChangesAsync();

        var defs = await Service(db, _ => null, new FakeKpi("a", 1)).GetDefinitionsAsync(From, To);

        Assert.Equal("a", defs.Kpis.Single().Key);
        Assert.Equal("def a", defs.Kpis.Single().Definition);
        Assert.Equal(2, defs.ProfilesInForce.Count);                // Draft and future rows are not in force
        var typeDefault = defs.ProfilesInForce.Single(p => p.TestCode == null);
        Assert.Equal(3m, typeDefault.WorkUnits);
        Assert.Equal(5m, typeDefault.TargetAnalystDays);
        Assert.Equal(2, typeDefault.VersionId);
        Assert.Equal(new DateOnly(2026, 10, 10), typeDefault.EffectiveFrom);
        var over = defs.ProfilesInForce.Single(p => p.TestCode == "TAMC");
        Assert.Null(over.WorkUnits);
        Assert.Equal(14m, over.TargetAnalystDays);
    }

    private static WorkflowProfileVersion Version(int id, WorkflowType type, string? code, decimal? units, decimal? target,
        DateOnly from, WorkflowProfileStatus status = WorkflowProfileStatus.Effective) => new()
    {
        Id = id, WorkflowType = type, TestCode = code, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = from, Status = status, ReasonForChange = "seed", CreatedByUserId = 1
    };
}
