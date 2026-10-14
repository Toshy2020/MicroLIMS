using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.KpiTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiRegistryTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(Now));

    [Fact]
    public void Registry_HasTheElevenKpis_WithGroupsOrdersUnitsAndDirections()
    {
        using var db = NewDb();
        var registry = TestServiceFactory.BuildKpiRegistry(db, Clock);

        var expected = new (string Key, KpiGroup Group, int Order, string Unit, KpiDirection Direction)[]
        {
            ("sample-tat", KpiGroup.Headline, 1, "days", KpiDirection.LowerIsBetter),
            ("on-time-all-stages", KpiGroup.Headline, 2, "%", KpiDirection.HigherIsBetter),
            ("right-first-time", KpiGroup.Headline, 3, "%", KpiDirection.HigherIsBetter),
            ("retest-rate", KpiGroup.Headline, 4, "%", KpiDirection.LowerIsBetter),
            ("oos-rate", KpiGroup.Headline, 5, "%", KpiDirection.LowerIsBetter),
            ("wip-backlog", KpiGroup.Headline, 6, "tests", KpiDirection.Neutral),
            ("incubation-reads-on-time", KpiGroup.Headline, 7, "%", KpiDirection.HigherIsBetter),
            ("em-excursion-rate", KpiGroup.Headline, 8, "%", KpiDirection.LowerIsBetter),
            ("stage-tat", KpiGroup.Flow, 20, "days", KpiDirection.LowerIsBetter),
            ("backlog-aging", KpiGroup.Flow, 21, "tests", KpiDirection.Neutral),
            ("returns-by-test", KpiGroup.Quality, 30, "tests", KpiDirection.Neutral),
        };
        Assert.Equal(expected, registry.All.Select(k => (k.Key, k.Group, k.Order, k.Unit, k.Direction)));
    }

    [Fact]
    public void Registry_EveryKpiExplainsItself_ForTheDefinitionsDrawer()
    {
        using var db = NewDb();
        Assert.All(TestServiceFactory.BuildKpiRegistry(db, Clock).All, k =>
        {
            Assert.False(string.IsNullOrWhiteSpace(k.Name), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.Definition), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.ClockEvents), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.Population), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.Source), k.Key);
        });
    }

    [Fact]
    public void Registry_DuplicateKey_IsRejected_AndFindIsCaseInsensitive()
    {
        using var db = NewDb();
        var one = new SampleTatKpi(db);
        Assert.Throws<InvalidOperationException>(() => new KpiRegistry(new IKpi[] { one, new SampleTatKpi(db) }));
        Assert.Same(one, new KpiRegistry(new IKpi[] { one }).Find("SAMPLE-TAT"));
        Assert.Null(new KpiRegistry(new IKpi[] { one }).Find("nope"));
    }

    [Fact]
    public async Task Dashboard_EmptyDatabase_EveryRealKpiIsNoDataOrZero()
    {
        await using var db = NewDb();
        var svc = TestServiceFactory.KpiDashboard(db, Clock, new StubSectionScope(_ => null));

        var dto = await svc.GetDashboardAsync(1, true, From, To, null, null, null, null);

        Assert.Equal(11, dto.Kpis.Count);
        Assert.All(dto.Kpis, k => { Assert.Null(k.Error); Assert.NotNull(k.Result); });
        var counts = new[] { "wip-backlog", "backlog-aging", "returns-by-test" };
        foreach (var entry in dto.Kpis.Where(k => !counts.Contains(k.Key)))
        {
            Assert.Equal(KpiStatus.NoData, entry.Result!.Status);   // never 0% / 100% / 0 days
            Assert.Null(entry.Result.Value);
            Assert.Null(entry.Result.Prior);
        }
        foreach (var entry in dto.Kpis.Where(k => counts.Contains(k.Key)))
        {
            Assert.Equal(KpiStatus.Ok, entry.Result!.Status);
            Assert.Equal(0m, entry.Result.Value);
        }
    }

    [Fact]
    public async Task Dashboard_EmptyScope_SeesNothing_EvenWithData()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3));
        await db.SaveChangesAsync();

        var unrestricted = await TestServiceFactory.KpiDashboard(db, Clock, new StubSectionScope(_ => null))
            .GetDashboardAsync(1, true, From, To, null, null, null, null);
        var nothing = await TestServiceFactory.KpiDashboard(db, Clock, new StubSectionScope(_ => Array.Empty<int>()))
            .GetDashboardAsync(1, true, From, To, null, null, null, null);

        Assert.Equal(1, unrestricted.Kpis.Single(k => k.Key == "sample-tat").Result!.N);
        Assert.Equal(KpiStatus.NoData, nothing.Kpis.Single(k => k.Key == "sample-tat").Result!.Status);
        Assert.Equal(0m, nothing.Kpis.Single(k => k.Key == "wip-backlog").Result!.Value);
    }
}
