using MicroLIMS.Application.Services.Kpi;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiMathTests
{
    [Fact]
    public void Percentile_InterpolatesLinearly_LikeExcelPercentileInc()
    {
        var values = new[] { 11m, 2m, 5m, 4m };
        Assert.Equal(4.5m, KpiScope.Percentile(values, 0.5m));
        Assert.Equal(9.2m, KpiScope.Percentile(values, 0.9m));
    }

    [Fact]
    public void Percentile_SingleValue_IsThatValue_AndEmptyIsNull()
    {
        Assert.Equal(7m, KpiScope.Percentile(new[] { 7m }, 0.9m));
        Assert.Null(KpiScope.Percentile(Array.Empty<decimal>(), 0.5m));
    }

    [Fact]
    public void Rate_ZeroDenominator_IsNull_NotZero()
    {
        Assert.Null(KpiScope.Rate(0m, 0m));
        Assert.Equal(0.6667m, KpiScope.Rate(2m, 3m));
        Assert.Equal(0m, KpiScope.Rate(0m, 3m));
    }

    [Fact]
    public void Days_RoundsToTwoDecimals()
    {
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0.5m, KpiScope.Days(from, from.AddHours(12)));
        Assert.Equal(1.04m, KpiScope.Days(from, from.AddHours(25)));
    }

    [Fact]
    public void MonthlySeries_GroupsByCalendarMonth_InTimeOrder()
    {
        var series = KpiScope.MonthlySeries(new (DateTime, decimal, decimal)[]
        {
            (new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), 1m, 1m),
            (new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), 0m, 1m),
            (new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), 0m, 1m),
        });
        Assert.Equal(new[] { "2026-09", "2026-10" }, series.Select(p => p.Label));
        Assert.Equal(0m, series[0].Value);
        Assert.Equal(0.5m, series[1].Value);
        Assert.Equal(2, series[1].N);
    }

    [Fact]
    public void ToResult_StatusRules()
    {
        var gap = new KpiGap("X", "1 thing", 1);
        var noData = KpiScope.ToResult(KpiCore.Nothing(), null, "%");
        Assert.Equal(KpiStatus.NoData, noData.Status);
        Assert.Null(noData.Value);
        Assert.Null(noData.Prior);

        var partial = KpiScope.ToResult(new KpiCore(0.5m, 1, 2, 2, new[] { gap }, KpiCore.NoSeries, KpiCore.NoExtras, KpiCore.NoFlags), null, "%");
        Assert.Equal(KpiStatus.Partial, partial.Status);
        Assert.True(partial.TooFewRecords);

        var count = KpiScope.ToResult(KpiCore.Nothing(isCount: true), null, "tests");
        Assert.Equal(KpiStatus.Ok, count.Status);
        Assert.Equal(0m, count.Value);
        Assert.False(count.TooFewRecords);
    }
}
