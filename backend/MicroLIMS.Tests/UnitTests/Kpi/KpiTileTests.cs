using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.KpiTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class SampleTatKpiTests
{
    // In period: 2, 4, 6, 10 days and a 0.5-day sample approved exactly at the period start.
    // Not counted: a retest clone, a Reject (which also stamps ApprovedAt), a sample approved
    // exactly at the exclusive period end, and a September sample (prior period).
    private static void Seed(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3));
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 5));
        ApprovedSample(db, 3, Utc(10, 2), Utc(10, 8));
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 11));
        ApprovedSample(db, 8, Utc(9, 30, 12), Utc(10, 1));
        ApprovedSample(db, 5, Utc(10, 2), Utc(10, 6), originSampleId: 1);
        ApprovedSample(db, 6, Utc(10, 1), Utc(10, 6), decision: ApprovalDecision.Reject);
        ApprovedSample(db, 7, Utc(10, 20), Utc(11, 1));
        ApprovedSample(db, 9, Utc(9, 10), Utc(9, 20));
    }

    private static Task<KpiResult> Run(MicroLimsDbContext db, KpiQuery q) => new SampleTatKpi(db).ComputeAsync(q, default);

    [Fact]
    public async Task SampleTat_MedianAndP90_ForApprovedSamplesOnly()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Run(db, Query());

        Assert.Equal(4m, r.Value);                  // median of 0.5, 2, 4, 6, 10
        Assert.Equal(8.4m, r.Extras["p90"]);        // 6 + 0.6 * (10 - 6)
        Assert.Equal(5, r.N);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.False(r.TooFewRecords);              // unit is days, not a rate
        Assert.NotNull(r.Prior);
        Assert.Equal(10m, r.Prior!.Value);
        Assert.Equal(1, r.Prior.N);
    }

    [Fact]
    public async Task SampleTat_EmptyPeriod_IsNoData_NotZero()
    {
        await using var db = NewDb();
        var r = await Run(db, Query());

        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Equal(0, r.N);
        Assert.Null(r.Prior);
        Assert.False(r.Extras.ContainsKey("p90"));
    }

    [Fact]
    public async Task SampleTat_SingleSample_MedianEqualsP90()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 4, 12));
        await db.SaveChangesAsync();

        var r = await Run(db, Query());

        Assert.Equal(3.5m, r.Value);
        Assert.Equal(3.5m, r.Extras["p90"]);
        Assert.Equal(1, r.N);
    }

    [Fact]
    public async Task SampleTat_SectionScope_NullSeesAll_EmptySeesNothing()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), sectionId: 1);
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 5), sectionId: 2);
        await db.SaveChangesAsync();

        Assert.Equal(2, (await Run(db, Query())).N);                                   // null = unrestricted
        var lab1 = await Run(db, Query(sections: new[] { 1 }));
        Assert.Equal(1, lab1.N);
        Assert.Equal(2m, lab1.Value);
        var none = await Run(db, Query(sections: Array.Empty<int>()));                 // empty = nothing, not "no filter"
        Assert.Equal(KpiStatus.NoData, none.Status);
        Assert.Equal(0, none.N);
        Assert.Equal(KpiStatus.NoData, (await Run(db, Query(sections: new[] { 3 }))).Status);
    }

    [Fact]
    public async Task SampleTat_TwoLabSample_UsesWholeSampleApproval_ForAnyScope()
    {
        // Lab 1 approved on day 3, lab 2 on day 10. Sample.ApprovedAt is stamped by the last section (day 10),
        // so a lab-1-only viewer sees the same 10 days. This is the documented whole-sample rule.
        await using var db = NewDb();
        var sample = ApprovedSample(db, 1, Utc(10, 1), Utc(10, 11), sectionId: 1);
        Test(db, sample, 11, "TYMC", sectionId: 2);
        Signoff(db, 1, 1, null, null, Utc(10, 4));
        Signoff(db, 1, 2, null, null, Utc(10, 11));
        await db.SaveChangesAsync();

        Assert.Equal(10m, (await Run(db, Query())).Value);
        var lab1 = await Run(db, Query(sections: new[] { 1 }));
        Assert.Equal(1, lab1.N);
        Assert.Equal(10m, lab1.Value);
    }

    [Fact]
    public async Task SampleTat_Filters_Category_TestCode_AndAnalyst()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), testCode: "TAMC", category: SampleCategory.Water, analyst: 7);
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 5), testCode: "TYMC", category: SampleCategory.FinishedProduct, analyst: 8);
        await db.SaveChangesAsync();

        Assert.Equal(2m, (await Run(db, Query(category: "water"))).Value);
        Assert.Equal(4m, (await Run(db, Query(testCode: "TYMC"))).Value);
        Assert.Equal(2m, (await Run(db, Query(analystId: 7))).Value);
        Assert.Equal(KpiStatus.NoData, (await Run(db, Query(category: "GPT"))).Status);
    }

    [Fact]
    public async Task SampleTat_Records_ArePagedInApprovalOrder()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();
        var kpi = new SampleTatKpi(db);

        var page = await kpi.GetRecordsAsync(Query(), 1, 2, default);

        Assert.Equal("sample-tat", page.Key);
        Assert.Equal(5, page.Total);
        Assert.Equal(new[] { 8, 1 }, page.Items.Select(i => i.SampleId!.Value));   // by ApprovedAt: the boundary sample first
        Assert.Equal(0.5m, page.Items[0].Value);
        Assert.Equal("days", page.Items[0].Unit);
        Assert.All(page.Items, i => Assert.Equal("Denominator", i.Inclusion));
        Assert.Single((await kpi.GetRecordsAsync(Query(), 3, 2, default)).Items);   // the third page holds the last record
    }
}

public class OnTimeAllStagesKpiTests
{
    private static OnTimeAllStagesKpi Kpi(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(new DateTimeOffset(Now))));

    private static void Profiles(MicroLimsDbContext db)
    {
        Definition(db, "TAMC", WorkflowType.CountTest);
        Definition(db, "TYMC", WorkflowType.CountTest);
        Definition(db, "UNPROFILED", WorkflowType.HplcMethodAssay);
        Profile(db, WorkflowType.CountTest, null, 2m, 5m, new DateOnly(2026, 1, 1));         // type default: 5 days
        Profile(db, WorkflowType.CountTest, "TYMC", null, 14m, new DateOnly(2026, 1, 1));    // override: 14 days
    }

    // With the profiles above: 1, 4, 5, 7 on time; 2, 3 late; 6 cannot be judged (no preparation record).
    private static void Samples(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 6, 8), "TAMC");
        Timeline(db, 1, Utc(10, 1, 8), Utc(10, 5, 8), Utc(10, 5, 20), Utc(10, 6, 8));        // 4 d, 12 h, 12 h
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 7, 12), "TAMC");
        Timeline(db, 2, Utc(10, 1, 8), Utc(10, 7, 8), Utc(10, 7, 10), Utc(10, 7, 12));       // 6 d > 5 d: late
        ApprovedSample(db, 3, Utc(10, 1), Utc(10, 13, 10), "TYMC");
        Timeline(db, 3, Utc(10, 1, 8), Utc(10, 12, 8), Utc(10, 13, 9), Utc(10, 13, 10));     // 11 d <= 14 d, but review 25 h: late
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 8, 8), "TAMC");
        Timeline(db, 4, Utc(10, 1, 8), Utc(10, 6, 8), Utc(10, 7, 8), Utc(10, 8, 8));         // exactly 5 d, 24 h, 24 h: on time
        var five = ApprovedSample(db, 5, Utc(10, 1), Utc(10, 11, 8), "TAMC");
        Test(db, five, 51, "TYMC");                                                           // the longest target (14 d) governs
        Timeline(db, 5, Utc(10, 1, 8), Utc(10, 10, 8), Utc(10, 10, 20), Utc(10, 11, 8));     // 9 d
        ApprovedSample(db, 6, Utc(10, 1), Utc(10, 9, 8), "TAMC");
        Signoff(db, 6, 1, Utc(10, 8, 8), Utc(10, 8, 12), Utc(10, 9, 8));                     // no SamplePreparation row
        ApprovedSample(db, 7, Utc(10, 1), Utc(10, 7, 14), "UNPROFILED");
        Timeline(db, 7, Utc(10, 1, 8), Utc(10, 7, 8), Utc(10, 7, 12), Utc(10, 7, 14));       // 6 d <= default 7 d
    }

    [Fact]
    public async Task OnTime_AllStages_BoundariesAreInclusive_AndMaxTargetRuleApplies()
    {
        await using var db = NewDb();
        Profiles(db);
        Samples(db);
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.6667m, r.Value);
        Assert.Equal(4m, r.Numerator);
        Assert.Equal(6m, r.Denominator);
        Assert.Equal(6, r.N);
        Assert.False(r.TooFewRecords);
        Assert.Equal(KpiStatus.Partial, r.Status);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "NO_ASSIGNMENT_TIME").Count);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "DEFAULT_TARGET").Count);   // only UNPROFILED fell back
        Assert.Null(r.Prior);
    }

    [Fact]
    public async Task OnTime_NoProfiles_UsesSevenDayFallback_AndReportsGap()
    {
        await using var db = NewDb();
        Samples(db);                                   // no definitions, no profiles at all
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        // 7 days for everybody: 1, 2, 4, 7 on time; 3 (review 25 h) and 5 (9 d) late.
        Assert.Equal(0.6667m, r.Value);
        var gap = r.Gaps.Single(g => g.Code == "DEFAULT_TARGET");
        Assert.Equal(7, gap.Count);                    // one per judged test (sample 5 has two)
        Assert.Equal("7 tests on the default 7-day target", gap.Message);
        Assert.Equal(KpiStatus.Partial, r.Status);
    }

    [Fact]
    public async Task OnTime_SampleWithoutPreparation_IsExcludedAndReported()
    {
        await using var db = NewDb();
        Profiles(db);
        Samples(db);
        await db.SaveChangesAsync();

        var page = await Kpi(db).GetRecordsAsync(Query(), 1, 50, default);

        var excluded = Assert.Single(page.Items, i => i.Inclusion == "Excluded");
        Assert.Equal(6, excluded.SampleId);
        Assert.Contains("assignment", excluded.Note);
        Assert.Equal(4, page.Items.Count(i => i.Inclusion == "Numerator"));
        Assert.Equal(2, page.Items.Count(i => i.Inclusion == "Denominator"));
    }

    [Fact]
    public async Task OnTime_NothingApproved_IsNoData()
    {
        await using var db = NewDb();
        var r = await Kpi(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
    }

    [Fact]
    public async Task OnTime_OnlyUnjudgeableSamples_IsNoDataWithTheGap()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 6));          // no preparation, no signoff
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "NO_ASSIGNMENT_TIME").Count);
    }

    [Fact]
    public async Task OnTime_OutOfScopeSection_IsNotJudged()
    {
        // Lab 2 was late, but a viewer who only sees lab 1 is judged on lab 1 alone.
        await using var db = NewDb();
        Definition(db, "TAMC", WorkflowType.CountTest);
        Profile(db, WorkflowType.CountTest, null, 2m, 5m, new DateOnly(2026, 1, 1));
        var sample = ApprovedSample(db, 1, Utc(10, 1), Utc(10, 12, 8), "TAMC");
        Test(db, sample, 11, "TAMC", sectionId: 2);
        Prepared(db, 1, Utc(10, 1, 8));
        Signoff(db, 1, 1, Utc(10, 3, 8), Utc(10, 3, 12), Utc(10, 3, 14));
        Signoff(db, 1, 2, Utc(10, 11, 8), Utc(10, 11, 12), Utc(10, 12, 8));      // 10 d: late for lab 2
        await db.SaveChangesAsync();

        Assert.Equal(1m, (await Kpi(db).ComputeAsync(Query(sections: new[] { 1 }), default)).Value);
        Assert.Equal(0m, (await Kpi(db).ComputeAsync(Query(), default)).Value);
    }
}

