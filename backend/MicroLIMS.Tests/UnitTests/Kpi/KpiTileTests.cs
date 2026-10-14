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

public class RightFirstTimeKpiTests
{
    private static RightFirstTimeKpi Kpi(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(new DateTimeOffset(Now))));

    private static void Definitions(MicroLimsDbContext db)
    {
        Definition(db, "TAMC", WorkflowType.CountTest);
        Definition(db, "HPLC1", WorkflowType.HplcMethodAssay);
        Definition(db, "PATH", WorkflowType.Observation);
        Definition(db, "NOPROF", WorkflowType.Titration);
    }

    private static void Profiles(MicroLimsDbContext db)
    {
        Profile(db, WorkflowType.CountTest, null, 2m, null, new DateOnly(2026, 1, 1));
        Profile(db, WorkflowType.HplcMethodAssay, null, 4m, null, new DateOnly(2026, 1, 1));
        Profile(db, WorkflowType.Observation, null, 1m, null, new DateOnly(2026, 1, 1));   // configured, still excluded
    }

    private static void Approve(MicroLimsDbContext db, int sampleId, DateTime at) => Signoff(db, sampleId, 1, null, null, at);

    // Counted: 1 (2 u), 2 (2 u, returned), 3 (4 u), 4 (4 u, returned twice) -> 6 of 12 units.
    // Reported gaps: 5 (observation), 6 (no profile). Ignored: 7 (superseded), 8 (Rejected).
    // Prior period: 9 (2 u, never returned).
    private static void Seed(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), "TAMC");    Approve(db, 1, Utc(10, 3));
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 4), "TAMC");    Approve(db, 2, Utc(10, 4));
        ApprovedSample(db, 3, Utc(10, 1), Utc(10, 5), "HPLC1");   Approve(db, 3, Utc(10, 5));
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 6), "HPLC1");   Approve(db, 4, Utc(10, 6));
        ApprovedSample(db, 5, Utc(10, 1), Utc(10, 7), "PATH");    Approve(db, 5, Utc(10, 7));
        ApprovedSample(db, 6, Utc(10, 1), Utc(10, 8), "NOPROF");  Approve(db, 6, Utc(10, 8));
        ApprovedSample(db, 7, Utc(10, 1), Utc(10, 8), "TAMC", superseded: true);  Approve(db, 7, Utc(10, 8));
        ApprovedSample(db, 8, Utc(10, 1), Utc(10, 9), "TAMC", decision: ApprovalDecision.Reject, testStatus: ApprovalStatus.Rejected);
        Signoff(db, 8, 1, null, null, Utc(10, 9), SectionSignoffStatus.Rejected, ApprovalDecision.Reject);   // a Reject also stamps ApprovedAt
        ApprovedSample(db, 9, Utc(9, 10), Utc(9, 20), "TAMC");    Approve(db, 9, Utc(9, 20));
        Return(db, 20, Utc(10, 3, 12));
        Return(db, 40, Utc(10, 4, 12));
        Return(db, 40, Utc(10, 5, 12));
    }

    [Fact]
    public async Task RightFirstTime_WeightsByUnits_ExcludesObservationAndUnweighted()
    {
        await using var db = NewDb();
        Definitions(db);
        Profiles(db);
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.5m, r.Value);
        Assert.Equal(6m, r.Numerator);
        Assert.Equal(12m, r.Denominator);
        Assert.Equal(4, r.N);                      // tests, not units: 4 < 5, so the value is flagged as too few
        Assert.True(r.TooFewRecords);
        Assert.Equal(KpiStatus.Partial, r.Status);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "OBSERVATION_NOT_TRACKED").Count);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS").Count);
        Assert.Equal("2026-10", Assert.Single(r.Series).Label);
        Assert.Equal(0.5m, r.Series[0].Value);
        Assert.Equal(1m, r.Prior!.Value);          // September: 2 u of 2 u
        Assert.Equal(1, r.Prior.N);
    }

    [Fact]
    public async Task RightFirstTime_NoProfiles_IsNoData_NotOneHundredPercent()
    {
        await using var db = NewDb();
        Definitions(db);        // definitions, but no profile versions at all
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);                      // never defaulted to a weight of 1.0
        Assert.Equal(0, r.N);
        var gap = r.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS");
        Assert.Equal(5, gap.Count);                // samples 1, 2, 3, 4, 6
        Assert.Equal("5 tests unweighted", gap.Message);
    }

    [Fact]
    public async Task RightFirstTime_ProfileAddedAfterApproval_DoesNotRewriteEarlierTests()
    {
        await using var db = NewDb();
        Definitions(db);
        Profiles(db);
        Seed(db);
        Profile(db, WorkflowType.CountTest, null, 8m, null, new DateOnly(2026, 10, 20));   // after every approval above
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(12m, r.Denominator);          // still 2 u per count test, as in force when approved
        Assert.Equal(0.5m, r.Value);
    }

    [Fact]
    public async Task RightFirstTime_TestApprovedAfterANewVersion_UsesTheNewVersion()
    {
        await using var db = NewDb();
        Definitions(db);
        Profile(db, WorkflowType.CountTest, null, 2m, null, new DateOnly(2026, 1, 1));
        Profile(db, WorkflowType.CountTest, null, 6m, null, new DateOnly(2026, 10, 5));
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), "TAMC"); Approve(db, 1, Utc(10, 3));    // 2 u
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 6), "TAMC"); Approve(db, 2, Utc(10, 6));    // 6 u
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(8m, r.Denominator);
    }

    [Fact]
    public async Task RightFirstTime_Records_ShowReturnedAndExcludedTests_WithoutOtherAnalystNames()
    {
        await using var db = NewDb();
        Analyst(db, 1, "Ann Analyst");
        Definitions(db);
        Profiles(db);
        Seed(db);
        await db.SaveChangesAsync();

        var all = await Kpi(db).GetRecordsAsync(Query(), 1, 50, default);
        Assert.Equal(6, all.Total);                                           // 4 counted + observation + unweighted
        Assert.Equal("Ann Analyst", all.Items.First().AnalystName);
        Assert.Equal(2, all.Items.Count(i => i.Inclusion == "Excluded"));
        Assert.Equal(2, all.Items.Count(i => i.Inclusion == "Numerator"));
        Assert.Equal(2, all.Items.Count(i => i.Inclusion == "Denominator"));

        var other = await Kpi(db).GetRecordsAsync(Query(canViewTeam: false, viewer: 99), 1, 50, default);
        Assert.All(other.Items, i => Assert.Null(i.AnalystName));             // D8: not mine, not shown
    }

    [Fact]
    public async Task RightFirstTime_EmptyPeriod_IsNoData()
    {
        await using var db = NewDb();
        var r = await Kpi(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Empty(r.Gaps);
    }
}

public class DecisionRateKpiTests
{
    // Mirrors SampleApprovalService: every decision writes the signature (SignedAt = decision time);
    // ApprovedAt is stamped only by Approve and Reject. RetestRetainedSample and NewSampleRequest leave
    // ApprovedAt null and the signoff in RetestRequested until the retest resolves.
    private static Sample Decided(MicroLimsDbContext db, int id, ApprovalDecision decision, DateTime at, string? oos = null,
        int section = 1, int? originSampleId = null)
    {
        var sample = ApprovedSample(db, id, Utc(10, 1), at, decision: decision, sectionId: section, originSampleId: originSampleId);
        sample.OosGroupCode = oos;
        DecidedSignoff(db, id, section, decision, at);
        return sample;
    }

    private static void DecidedSignoff(MicroLimsDbContext db, int sampleId, int section, ApprovalDecision decision, DateTime at)
    {
        var stamped = decision is ApprovalDecision.Approve or ApprovalDecision.Reject;
        Signoff(db, sampleId, section, null, null, stamped ? at : null,
            decision == ApprovalDecision.Approve ? SectionSignoffStatus.Approved
                : decision == ApprovalDecision.Reject ? SectionSignoffStatus.Rejected
                : SectionSignoffStatus.RetestRequested,
            decision, signedAt: at);
    }

    // Seven distinct decided samples in October:
    // 1, 2, 5 Approve | 3 Reject | 4 RetestRetainedSample (OOS-1) | 6 NewSampleRequest (OOS-2)
    // 7 is a two-lab sample: lab 1 approved, lab 2 asked for a retest (OOS-3).
    // Not counted: 9 decided exactly at the exclusive period end; 10, the retest clone of 4 (same OOS-1
    // group, approved in October). Prior period: 8 (Approve).
    private static void Seed(MicroLimsDbContext db)
    {
        Decided(db, 1, ApprovalDecision.Approve, Utc(10, 3));
        Decided(db, 2, ApprovalDecision.Approve, Utc(10, 4));
        Decided(db, 3, ApprovalDecision.Reject, Utc(10, 5));
        Decided(db, 4, ApprovalDecision.RetestRetainedSample, Utc(10, 6), "OOS-1");
        Decided(db, 5, ApprovalDecision.Approve, Utc(10, 7));
        Decided(db, 6, ApprovalDecision.NewSampleRequest, Utc(10, 8), "OOS-2");
        var seven = Decided(db, 7, ApprovalDecision.Approve, Utc(10, 9), "OOS-3");
        Test(db, seven, 71, "TAMC", sectionId: 2);
        DecidedSignoff(db, 7, 2, ApprovalDecision.RetestRetainedSample, Utc(10, 9));
        Decided(db, 10, ApprovalDecision.Approve, Utc(10, 12), "OOS-1", originSampleId: 4);
        Decided(db, 9, ApprovalDecision.Approve, Utc(11, 1));
        Decided(db, 8, ApprovalDecision.Approve, Utc(9, 20));
    }

    [Fact]
    public async Task Retest_SignatureWithoutApprovedAt_CountsInSignedPeriod()
    {
        // Production shape of a retest decision: signature written, ApprovedAt null.
        await using var db = NewDb();
        Decided(db, 1, ApprovalDecision.RetestRetainedSample, Utc(10, 6), "OOS-1");
        Decided(db, 2, ApprovalDecision.RetestRetainedSample, Utc(9, 28), "OOS-2");
        await db.SaveChangesAsync();

        var r = await new RetestRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal((1m, 1m), (r.Numerator!.Value, r.Denominator!.Value));   // sample 1, signed 6 Oct
        Assert.Equal(1, r.Prior!.N);                                          // sample 2, signed 28 Sep
        Assert.Equal(1m, r.Prior.Value);
    }

    [Fact]
    public async Task OosRate_OriginAndCloneInSameGroup_CountOnce()
    {
        await using var db = NewDb();
        Decided(db, 1, ApprovalDecision.RetestRetainedSample, Utc(10, 6), "OOS-1");
        Decided(db, 2, ApprovalDecision.Approve, Utc(10, 12), "OOS-1", originSampleId: 1);   // the clone
        await db.SaveChangesAsync();

        var oos = await new OosRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal((1m, 1m), (oos.Numerator!.Value, oos.Denominator!.Value));
        Assert.Equal(1, oos.N);
    }

    [Fact]
    public async Task RetestAndOos_Rates_CountDistinctDecidedSamples()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var retest = await new RetestRateKpi(db).ComputeAsync(Query(), default);
        Assert.Equal((2m, 7m), (retest.Numerator!.Value, retest.Denominator!.Value));   // samples 4 and 7
        Assert.Equal(0.2857m, retest.Value);
        Assert.Equal(7, retest.N);
        Assert.False(retest.TooFewRecords);
        Assert.Equal(0m, retest.Prior!.Value);                                           // September: 0 of 1
        Assert.Equal(1, retest.Prior.N);

        var oos = await new OosRateKpi(db).ComputeAsync(Query(), default);
        Assert.Equal(3m, oos.Numerator);                                                 // samples 4, 6, 7 carry an OOS group
        Assert.Equal(0.4286m, oos.Value);
        Assert.Equal(KpiStatus.Ok, oos.Status);
    }

    [Fact]
    public async Task Retest_SectionScope_UsesInScopeSignoffsOnly()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        // A lab-1-only viewer never sees lab 2's retest decision on sample 7.
        var lab1 = await new RetestRateKpi(db).ComputeAsync(Query(sections: new[] { 1 }), default);
        Assert.Equal(1m, lab1.Numerator);                 // sample 4 only
        Assert.Equal(7m, lab1.Denominator);

        var lab2 = await new RetestRateKpi(db).ComputeAsync(Query(sections: new[] { 2 }), default);
        Assert.Equal(1m, lab2.Value);                     // sample 7, decided by lab 2
        Assert.Equal(1, lab2.N);
        Assert.True(lab2.TooFewRecords);

        var none = await new RetestRateKpi(db).ComputeAsync(Query(sections: Array.Empty<int>()), default);
        Assert.Equal(KpiStatus.NoData, none.Status);
    }

    [Fact]
    public async Task DecisionRates_EmptyPeriod_IsNoData()
    {
        await using var db = NewDb();
        foreach (IKpi kpi in new IKpi[] { new RetestRateKpi(db), new OosRateKpi(db) })
        {
            var r = await kpi.ComputeAsync(Query(), default);
            Assert.Equal(KpiStatus.NoData, r.Status);
            Assert.Null(r.Value);                         // not 0% and not 100%
            Assert.Equal(0, r.N);
        }
    }

    [Fact]
    public async Task DecisionRates_Records_MarkNumeratorAndDenominator()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await new RetestRateKpi(db).GetRecordsAsync(Query(), 1, 50, default);

        Assert.Equal(7, page.Total);
        Assert.Equal(new[] { 4, 7 }, page.Items.Where(i => i.Inclusion == "Numerator").Select(i => i.SampleId!.Value));
        Assert.Equal(5, page.Items.Count(i => i.Inclusion == "Denominator"));
    }
}

public class BacklogKpiTests
{
    private static void Open(MicroLimsDbContext db, int id, string code, ApprovalStatus status, WorkflowStep step,
        DateTime received, DateTime? prepared, bool superseded = false, int sectionId = 1)
    {
        var sample = new Sample
        {
            Id = id, ControlNumber = $"CN-{id}", ReferenceNumber = $"REF-{id}", Category = SampleCategory.FinishedProduct,
            ReceivedAt = received, Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        Test(db, sample, id * 10, code, sectionId, status, step, 1, superseded);
        if (prepared.HasValue) Prepared(db, id, prepared.Value);
    }

    private static void Gate(MicroLimsDbContext db, int id, SampleStatus sampleStatus, SectionSignoffStatus signoffStatus,
        DateTime submitted, DateTime? reviewed)
    {
        var sample = new Sample
        {
            Id = id, ControlNumber = $"CN-{id}", ReferenceNumber = $"REF-{id}", Category = SampleCategory.FinishedProduct,
            ReceivedAt = Utc(10, 1), Status = sampleStatus
        };
        db.Samples.Add(sample);
        Test(db, sample, id * 10, "TAMC", 1, ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        Signoff(db, id, 1, submitted, reviewed, null, signoffStatus, null);
    }

    // Now = 15 Oct 12:00. TAMC target 4 days (profile); HPLC1 has no target (7-day fallback).
    // Open: 1 (1 d), 2 (2 d), 3 incubating (4 d), 4 HPLC1 (14 d), 5 unprepared, received 3 d ago.
    // Ignored: 6 superseded, 7 ResultEntered, 8 Approved.
    // Gates: 9 under review for 12 h, 10 under approval for 3 days.
    private static void Seed(MicroLimsDbContext db)
    {
        Definition(db, "TAMC", WorkflowType.CountTest);
        Definition(db, "HPLC1", WorkflowType.HplcMethodAssay);
        Profile(db, WorkflowType.CountTest, null, 2m, 4m, new DateOnly(2026, 1, 1));
        Open(db, 1, "TAMC", ApprovalStatus.Pending, WorkflowStep.Waiting, Utc(10, 14), Utc(10, 14, 12));
        Open(db, 2, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Running, Utc(10, 13), Utc(10, 13, 12));
        Open(db, 3, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Incubating, Utc(10, 11), Utc(10, 11, 12));
        Open(db, 4, "HPLC1", ApprovalStatus.Pending, WorkflowStep.Waiting, Utc(10, 1), Utc(10, 1, 12));
        Open(db, 5, "TAMC", ApprovalStatus.Pending, WorkflowStep.Waiting, Utc(10, 12, 12), null);
        Open(db, 6, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Running, Utc(10, 1), Utc(10, 1), superseded: true);
        Open(db, 7, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready, Utc(10, 1), Utc(10, 1));
        Open(db, 8, "TAMC", ApprovalStatus.Approved, WorkflowStep.Approved, Utc(10, 1), Utc(10, 1));
        Gate(db, 9, SampleStatus.UnderReview, SectionSignoffStatus.UnderReview, Utc(10, 15), null);
        Gate(db, 10, SampleStatus.UnderApproval, SectionSignoffStatus.UnderApproval, Utc(10, 11), Utc(10, 12, 12));
    }

    private static BacklogAgingKpi Aging(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(new DateTimeOffset(Now))));

    [Fact]
    public async Task WipBacklog_CountsPendingAndInProgress_NotSuperseded()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await new WipBacklogKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(5m, r.Value);
        Assert.Equal(5, r.N);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.False(r.TooFewRecords);
        Assert.Equal(new[] { "Waiting", "Running", "Incubating" }, r.Series.Select(p => p.Label));
        Assert.Equal(new decimal?[] { 3m, 1m, 1m }, r.Series.Select(p => p.Value));
    }

    [Fact]
    public async Task WipBacklog_HasNoPrior_AndEmptyIsZeroNotNoData()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();
        var kpi = new WipBacklogKpi(db);

        Assert.Null((await kpi.ComputeAsync(Query(), default)).Prior);

        var empty = await kpi.ComputeAsync(Query(sections: new[] { 2 }), default);       // a section with no open work
        Assert.Equal(KpiStatus.Ok, empty.Status);
        Assert.Equal(0m, empty.Value);
        Assert.Empty(empty.Series);
    }

    [Fact]
    public async Task BacklogAging_BucketsRelativeToOwnTarget_BoundariesInclusive()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Aging(db).ComputeAsync(Query(), default);

        var cells = r.Series.ToDictionary(p => $"{p.Group}/{p.Label}", p => p.Value);
        Assert.Equal(12, cells.Count);                                  // every stage x bucket is present, zero included
        Assert.Equal(2m, cells["Testing/within50"]);                    // 1 d of 4 = 0.25; 2 d of 4 = 0.50 (boundary stays in)
        Assert.Equal(1m, cells["Testing/50to100"]);                     // unprepared sample: 3 d since receipt, 0.75
        Assert.Equal(1m, cells["Testing/over100"]);                     // HPLC1: 14 d of the 7-day fallback = 2.0
        Assert.Equal(1m, cells["Incubation/50to100"]);                  // 4 d of 4 = 1.00 (boundary stays in)
        Assert.Equal(1m, cells["In Review/within50"]);                  // 12 h of 24 h
        Assert.Equal(1m, cells["In Approval/over100"]);                 // 3 days of 24 h
        Assert.Equal(7m, r.Value);
        Assert.Equal(7, r.N);
        Assert.Null(r.Prior);
        Assert.Equal(KpiStatus.Partial, r.Status);
        var gap = Assert.Single(r.Gaps);
        Assert.Equal(("DEFAULT_TARGET", 1), (gap.Code, gap.Count));     // only the HPLC1 test had no target
    }

    [Fact]
    public async Task BacklogAging_Empty_IsOkWithZeroCells()
    {
        await using var db = NewDb();
        var r = await Aging(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.Equal(0m, r.Value);
        Assert.Equal(12, r.Series.Count);
        Assert.All(r.Series, p => Assert.Equal(0m, p.Value));
    }

    [Fact]
    public async Task BacklogAging_Records_NameTheBucket()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Aging(db).GetRecordsAsync(Query(), 1, 50, default);

        Assert.Equal(7, page.Total);
        var hplc = Assert.Single(page.Items, i => i.TestCode == "HPLC1");
        Assert.Equal("Testing: over100", hplc.Note);
        Assert.Equal(2m, hplc.Value);
        Assert.Equal("x target", hplc.Unit);
    }
}

