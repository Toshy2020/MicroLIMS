using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkflowProfileResolverTests
{
    private static WorkflowProfileVersion V(int id, WorkflowType type, string? code, decimal? units, decimal? target,
        string from, WorkflowProfileStatus status = WorkflowProfileStatus.Effective) => new()
    {
        Id = id, WorkflowType = type, TestCode = code, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = DateOnly.Parse(from), Status = status, ReasonForChange = "test"
    };

    private static readonly IReadOnlyDictionary<string, WorkflowType> Types = new Dictionary<string, WorkflowType>
    {
        ["TAMC"] = WorkflowType.CountTest,
        ["TYMC"] = WorkflowType.CountTest,
        ["PH"] = WorkflowType.Measurement
    };

    private static readonly DateTime Oct11 = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_PicksLatestEffectiveVersionAtOrBeforeApproval()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, null, 2m, 9m, "2026-10-01"),
            V(3, WorkflowType.CountTest, null, 3m, 11m, "2026-10-20")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(2m, p.WorkUnits);
        Assert.Equal(9m, p.TargetAnalystDays);
        Assert.Equal(2, p.WorkUnitsVersionId);
        Assert.Equal(2, p.TargetVersionId);
        Assert.True(p.IsWeighted);
        Assert.True(p.IsTargetAware);
    }

    [Fact]
    public void Resolve_UsesTheDateOfApproval_NotTheTimeOfDay()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-10-01"),
            V(2, WorkflowType.CountTest, null, 2m, 9m, "2026-10-12")
        }, Types);

        Assert.Equal(1m, r.Resolve("TAMC", new DateTime(2026, 10, 11, 23, 59, 59, DateTimeKind.Utc)).WorkUnits);
        Assert.Equal(2m, r.Resolve("TAMC", new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc)).WorkUnits);
    }

    [Fact]
    public void Resolve_FutureOnlyVersion_IsUnresolved()
    {
        var r = new WorkflowProfileResolver(new[] { V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-10-12") }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.False(p.IsWeighted);
        Assert.False(p.IsTargetAware);
        Assert.Null(p.WorkUnitsVersionId);
    }

    [Fact]
    public void Resolve_OverrideBeatsTypeDefault()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", 4m, 14m, "2026-01-01")
        }, Types);

        Assert.Equal(4m, r.Resolve("TAMC", Oct11).WorkUnits);
        Assert.Equal(1m, r.Resolve("TYMC", Oct11).WorkUnits);
    }

    [Fact]
    public void Resolve_OverrideWithOnlyUnits_TakesTargetFromTypeDefault()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", 4m, null, "2026-01-01")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(4m, p.WorkUnits);
        Assert.Equal(2, p.WorkUnitsVersionId);
        Assert.Equal(7m, p.TargetAnalystDays);
        Assert.Equal(1, p.TargetVersionId);
    }

    [Fact]
    public void Resolve_OverrideWithOnlyTarget_TakesUnitsFromTypeDefault()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", null, 14m, "2026-01-01")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(1m, p.WorkUnits);
        Assert.Equal(14m, p.TargetAnalystDays);
    }

    [Fact]
    public void Resolve_LaterDefaultWithOnlyTarget_DoesNotBlankEarlierUnits()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 2m, null, "2026-01-01"),
            V(2, WorkflowType.CountTest, null, null, 10m, "2026-09-01")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(2m, p.WorkUnits);
        Assert.Equal(1, p.WorkUnitsVersionId);
        Assert.Equal(10m, p.TargetAnalystDays);
        Assert.Equal(2, p.TargetVersionId);
    }

    [Fact]
    public void Resolve_IgnoresDrafts()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 9m, 9m, "2026-01-01", WorkflowProfileStatus.Draft),
            V(2, WorkflowType.CountTest, "TAMC", 9m, 9m, "2026-01-01", WorkflowProfileStatus.Draft)
        }, Types);

        Assert.False(r.Resolve("TAMC", Oct11).IsWeighted);
    }

    [Fact]
    public void Resolve_SameEffectiveFrom_HigherIdWins()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(5, WorkflowType.CountTest, null, 3m, 7m, "2026-10-01"),
            V(9, WorkflowType.CountTest, null, 4m, 7m, "2026-10-01")
        }, Types);

        Assert.Equal(4m, r.Resolve("TAMC", Oct11).WorkUnits);
    }

    [Fact]
    public void Resolve_UnknownTestCode_ResolvesToNothing()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "GHOST", 5m, 5m, "2026-01-01")
        }, Types);

        Assert.False(r.Resolve("GHOST", Oct11).IsWeighted);
    }

    [Fact]
    public void Resolve_AfterTestWorkflowTypeEdit_UsesNewTypeDefault_OverrideUnaffected()
    {
        var versions = new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.Measurement, null, 3m, 5m, "2026-01-01"),
            V(3, WorkflowType.CountTest, "TYMC", 8m, null, "2026-01-01")
        };

        var before = new WorkflowProfileResolver(versions, Types);
        Assert.Equal(1m, before.Resolve("TAMC", Oct11).WorkUnits);

        // An administrator re-types TAMC (and TYMC) as Measurement in the test master.
        var edited = new Dictionary<string, WorkflowType>(Types)
        {
            ["TAMC"] = WorkflowType.Measurement,
            ["TYMC"] = WorkflowType.Measurement
        };
        var after = new WorkflowProfileResolver(versions, edited);

        Assert.Equal(3m, after.Resolve("TAMC", Oct11).WorkUnits);
        // The override is keyed by test code, so it keeps winning for units;
        // its missing target now falls back to the NEW type's default.
        var tymc = after.Resolve("TYMC", Oct11);
        Assert.Equal(8m, tymc.WorkUnits);
        Assert.Equal(5m, tymc.TargetAnalystDays);
    }

    [Fact]
    public void ResolveDefault_AndHasOverride_ReadOnlyTheirOwnScope()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", 4m, null, "2026-01-01"),
            V(3, WorkflowType.CountTest, "TYMC", 4m, null, "2026-12-01")
        }, Types);

        Assert.Equal(1m, r.ResolveDefault(WorkflowType.CountTest, Oct11).WorkUnits);
        Assert.False(r.ResolveDefault(WorkflowType.Gravimetric, Oct11).IsWeighted);
        Assert.True(r.HasOverride("TAMC", Oct11));
        Assert.False(r.HasOverride("TYMC", Oct11));
        Assert.False(r.HasOverride("PH", Oct11));
    }
}
