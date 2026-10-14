using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class AnalystStageTargetTests
{
    private static readonly DateTime At = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    private static WorkflowProfileVersion Row(int id, WorkflowType type, string? code, decimal? target) => new()
    {
        Id = id, WorkflowType = type, TestCode = code, WorkUnits = 1m, TargetAnalystDays = target,
        EffectiveFrom = new DateOnly(2026, 1, 1), Status = WorkflowProfileStatus.Effective, ReasonForChange = "seed", CreatedByUserId = 1
    };

    private static WorkflowProfileResolver Resolver(params WorkflowProfileVersion[] rows) =>
        new(rows, new Dictionary<string, WorkflowType>
        {
            ["TAMC"] = WorkflowType.CountTest, ["TYMC"] = WorkflowType.CountTest, ["HPLC1"] = WorkflowType.HplcMethodAssay
        });

    [Fact]
    public void DaysFor_TakesMaxOfResolvedTargets()
    {
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 5m), Row(2, WorkflowType.CountTest, "TYMC", 14m));
        var r = AnalystStageTarget.DaysFor(new[] { "TAMC", "TYMC" }, At, resolver);
        Assert.Equal((14m, 0, 2), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_UnresolvedTestsFallBackToSevenDays_AndAreCounted()
    {
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 5m));
        var r = AnalystStageTarget.DaysFor(new[] { "TAMC", "HPLC1" }, At, resolver);
        // HPLC1 has no profile: 7 days, which beats TAMC's 5.
        Assert.Equal((7m, 1, 2), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_ResolvedTargetBelowSeven_IsNotRaisedToTheFallback()
    {
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 3m));
        var r = AnalystStageTarget.DaysFor(new[] { "TAMC" }, At, resolver);
        Assert.Equal((3m, 0, 1), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_NoTests_IsSevenDays()
    {
        var r = AnalystStageTarget.DaysFor(Array.Empty<string>(), At, Resolver());
        Assert.Equal((7m, 0, 0), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_UsesTheVersionInForceAtTheGivenTime()
    {
        var later = Row(2, WorkflowType.CountTest, null, 20m);
        later.EffectiveFrom = new DateOnly(2026, 10, 20);
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 5m), later);
        Assert.Equal(5m, AnalystStageTarget.DaysFor(new[] { "TAMC" }, At, resolver).Days);
        Assert.Equal(20m, AnalystStageTarget.DaysFor(new[] { "TAMC" }, At.AddDays(11), resolver).Days);
    }
}
