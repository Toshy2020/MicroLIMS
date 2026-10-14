using MicroLIMS.Application.Services.Kpi;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiPriorPeriodTests
{
    private static KpiQuery Q(DateTime from, DateTime to) =>
        new(from, to, null, null, null, null, null, to, 1, true);

    [Fact]
    public void PriorPeriod_IsEqualLengthWindowEndingAtFrom()
    {
        var q = Q(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var prior = q.PriorPeriod();
        Assert.Equal(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), prior.From);
        Assert.Equal(q.From, prior.To);
        Assert.Equal(q.To - q.From, prior.To - prior.From);
    }

    [Fact]
    public void PriorPeriod_OneDayPeriod_IsThePreviousDay()
    {
        var q = Q(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
        var prior = q.PriorPeriod();
        Assert.Equal(new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), prior.From);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), prior.To);
    }

    [Fact]
    public void PriorPeriod_KeepsEveryOtherField()
    {
        var q = new KpiQuery(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow, new[] { 3 }, "Water", "P1", "TAMC", 9, DateTime.UtcNow, 4, false);
        var prior = q.PriorPeriod();
        Assert.Equal(q.SectionIds, prior.SectionIds);
        Assert.Equal((q.Category, q.Location, q.TestCode, q.AnalystId, q.Now, q.ViewerUserId, q.CanViewTeam),
            (prior.Category, prior.Location, prior.TestCode, prior.AnalystId, prior.Now, prior.ViewerUserId, prior.CanViewTeam));
    }
}
