using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: every dashboard, KPI and report query gained a
// laboratory-section filter, and the in-memory provider does no SQL
// translation - a filter EF Core cannot translate passes there and throws
// only against a real database. Each scoped query runs here with a real
// section list; the shared test database is not reset, so results are only
// checked where the test owns the data.
[Collection("PostgresDatabaseCollection")]
public class SectionScopedQueriesPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public SectionScopedQueriesPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EverySectionScopedQuery_TranslatesAndRuns_OnPostgres()
    {
        await using var db = _fixture.CreateDbContext();
        var micro = (await db.DocumentSections.FirstAsync(s => s.Code == "MICRO")).Id;
        var fp = (await db.DocumentSections.FirstAsync(s => s.Code == "FP")).Id;
        var now = DateTime.UtcNow;

        foreach (var scope in new IReadOnlyCollection<int>[] { new[] { micro }, new[] { fp } })
        {
            var dashboard = TestServiceFactory.Dashboard(db);
            await dashboard.GetSummaryAsync(RoleType.SectionHead, _fixture.SeededUserId, scope);
            await dashboard.GetTodaysWorkAsync(RoleType.SectionHead, _fixture.SeededUserId, scope);
            await dashboard.GetIncubationOverviewAsync(false, null, scope);
            await dashboard.GetMonthlyTrendAsync(6, scope);
            await dashboard.GetCategoryDistributionAsync(scope);
            await dashboard.GetStatusDistributionAsync(scope);
            await dashboard.GetKpiDeltasAsync(scope);
            await dashboard.GetSectionHeadDashboardAsync(scope);
            await dashboard.GetReviewerDashboardAsync(_fixture.SeededUserId, scope);

            await SectionReviewQueues.OverdueSampleIdsAsync(db, SectionSignoffStatus.UnderReview, now, TimeSpan.FromHours(24), scope);
            await SectionReviewQueues.OverdueSampleIdsAsync(db, SectionSignoffStatus.UnderApproval, now, TimeSpan.FromHours(24), scope);

            var kpi = TestServiceFactory.Kpi(db);
            await kpi.GetAnalystKpisAsync(sectionIds: scope);
            await kpi.GetCompletionStatsAsync(sectionIds: scope);
            await kpi.GetDelayTrackingAsync(sectionIds: scope);
            await kpi.GetSampleQueueCountsAsync(sectionIds: scope);
            await kpi.GetWorkflowBottleneckDeltasAsync(sectionIds: scope);
            await kpi.GetSampleAssignmentSlaAsync(null, now.AddYears(-1), now, sectionIds: scope);
            await kpi.GetSampleAssignmentSlaByAnalystAsync(now.AddYears(-1), now, sectionIds: scope);
            await kpi.GetOverallOnTimeCompletionAsync(now.AddYears(-1), now, sectionIds: scope);
            await kpi.GetStageTatSummaryAsync(null, now.AddYears(-1), now, sectionIds: scope);
            await kpi.GetTestingTatByMonthAsync(6, sectionIds: scope);
            await kpi.GetStepViolationsAsync(null, now.AddYears(-1), now, sectionIds: scope);
            await kpi.GetReturnToAnalystCountAsync(null, now.AddYears(-1), now, scope);

            var reporting = new ReportingQueryService(db);
            var search = new ResultRecordSearchRequest(null, null, null, null, null, null, null, null, null, null);
            await reporting.SearchAsync(search, scope);
            await reporting.GetFilterOptionsAsync(scope);
            await reporting.GetForExportAsync(search, 100, scope);
            await reporting.GetOverviewAggregateAsync(null, null, scope);
            await reporting.GetQualitativeEventsAsync(null, null, null, null, null, scope);
            await reporting.GetCompletedByMonthAsync(6, scope);
            await reporting.GetByIdAsync(0, scope);

            var gpt = new MediaGptReportService(db);
            await gpt.SearchAsync(new MediaGptSearchRequest(), scope);
            await gpt.GetSummaryAsync(null, null, null, scope);
            await gpt.GetFilterOptionsAsync(scope);

            var strains = new ReferenceStrainReportService(db);
            await strains.SearchAsync(new ReferenceStrainSearchRequest(), scope);
            await strains.GetFilterOptionsAsync(scope);

            await new OosTrackingService(db).GetOosGroupsAsync(scope);
            await new RecentActivityService(db).GetRecentAsync(25, scope);

            var workspace = new TestingWorkspaceService(db, new UserSectionScopeService(db));
            foreach (var filter in new[] { "awaitingReview", "reviewOverdue", "approvalOverdue" })
                await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = filter }, _fixture.SeededUserId);
            await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { SampleStatus = "PendingReview" }, _fixture.SeededUserId);
        }
    }

    [PostgresFact]
    public async Task ReviewQueue_ASectionUnderReview_IsFoundOnlyForThatSection_OnPostgres()
    {
        await using var db = _fixture.CreateDbContext();
        var micro = (await db.DocumentSections.FirstAsync(s => s.Code == "MICRO")).Id;
        var fp = (await db.DocumentSections.FirstAsync(s => s.Code == "FP")).Id;
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync() ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;

        // Micro is under review (its own sign-off row); FP is still testing,
        // so the sample as a whole is InTesting.
        var reference = $"PG-SEC-{Guid.NewGuid():n}"[..20];
        var sample = new Sample { ReferenceNumber = reference, Category = SampleCategory.FinishedProduct, Status = SampleStatus.InTesting, CauseOfTesting = cause, ReceivedAt = DateTime.UtcNow, ReceivedByUserId = _fixture.SeededUserId };
        sample.TestOrders.Add(new TestOrder { SectionId = micro, TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        sample.TestOrders.Add(new TestOrder { SectionId = fp, TestCode = "ASSAY", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        sample.SectionSignoffs.Add(new SampleSectionSignoff { SectionId = micro, Status = SectionSignoffStatus.UnderReview, SubmittedForReviewAt = DateTime.UtcNow });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var microIds = await SectionReviewQueues.Candidates(db.Samples, SectionSignoffStatus.UnderReview, new[] { micro }).Select(s => s.Id).ToListAsync();
        var fpIds = await SectionReviewQueues.Candidates(db.Samples, SectionSignoffStatus.UnderReview, new[] { fp }).Select(s => s.Id).ToListAsync();

        Assert.Contains(sample.Id, microIds);
        Assert.DoesNotContain(sample.Id, fpIds);
    }
}
