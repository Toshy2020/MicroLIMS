using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

/// <summary>
/// Characterisation tests for B4 Task 3: cross-section read access and
/// section write refusal for the QA system role (RoleType.Qa, holds
/// PermissionConstants.QaGrants including Sections.ViewAll, no org membership).
/// Uses real PostgreSQL via PostgresTestFixture with separate DbContexts for
/// Arrange, Act, and AsNoTracking Assert phases.
/// </summary>
[Collection("PostgresDatabaseCollection")]
public sealed class QaCrossSectionReadPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public QaCrossSectionReadPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    // -------------------------------------------------------------------------
    // Test 1
    // -------------------------------------------------------------------------
    [PostgresFact]
    public async Task Qa_reads_samples_of_both_sections()
    {
        var w = await QaScopeTestSeed.SeedWorldAsync(_fixture);

        await using (var actDb = _fixture.CreateDbContext())
        {
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            var readable = await scope.GetReadableSectionIdsAsync(w.QaUserId);
            Assert.Null(readable);

            await scope.EnsureSampleAccessAsync(w.QaUserId, w.SampleAId, CancellationToken.None, SectionAccess.Read);
            await scope.EnsureSampleAccessAsync(w.QaUserId, w.SampleBId, CancellationToken.None, SectionAccess.Read);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var memberships = await assertDb.UserOrgMemberships.AsNoTracking()
            .Where(m => m.UserId == w.QaUserId)
            .CountAsync();
        Assert.Equal(0, memberships);

        var qaRoleCodes = await assertDb.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == w.QaRoleId)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();
        Assert.Contains(PermissionConstants.SectionsViewAll, qaRoleCodes);
    }

    // -------------------------------------------------------------------------
    // Test 2
    // -------------------------------------------------------------------------
    [PostgresFact]
    public async Task Qa_reads_test_orders_results_media_equipment_of_both_sections()
    {
        var w = await QaScopeTestSeed.SeedWorldAsync(_fixture);

        var familiesA = QaScopeTestSeed.All15ScopeFamilies(w, useSectionA: true);
        var familiesB = QaScopeTestSeed.All15ScopeFamilies(w, useSectionA: false);
        Assert.Equal(15, familiesA.Count);

        await PostgresCases.RunAsync(familiesA, async item =>
        {
            await using var actDb = _fixture.CreateDbContext();
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await item.Call(scope, w.QaUserId, SectionAccess.Read);
        });

        await PostgresCases.RunAsync(familiesB, async item =>
        {
            await using var actDb = _fixture.CreateDbContext();
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await item.Call(scope, w.QaUserId, SectionAccess.Read);
        });

        // Multi-section combined batch calls
        await using (var actDb = _fixture.CreateDbContext())
        {
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await scope.EnsureTestOrdersAccessAsync(w.QaUserId, new[] { w.OrderAId, w.OrderBId }, CancellationToken.None, SectionAccess.Read);
            await scope.EnsureSectionsAccessAsync(w.QaUserId, new[] { w.SectionAId, w.SectionBId }, CancellationToken.None, SectionAccess.Read);
        }
    }

    // -------------------------------------------------------------------------
    // Test 3
    // -------------------------------------------------------------------------
    [PostgresFact]
    public async Task Analyst_still_reads_only_own_section()
    {
        var w = await QaScopeTestSeed.SeedWorldAsync(_fixture);

        await using (var actDb = _fixture.CreateDbContext())
        {
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            var readable = await scope.GetReadableSectionIdsAsync(w.AnalystAId);
            Assert.NotNull(readable);
            Assert.Contains(w.SectionAId, readable);
            Assert.DoesNotContain(w.SectionBId, readable);
        }

        // Section A succeeds for Analyst A across all 15 families
        await PostgresCases.RunAsync(QaScopeTestSeed.All15ScopeFamilies(w, useSectionA: true), async item =>
        {
            await using var actDb = _fixture.CreateDbContext();
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await item.Call(scope, w.AnalystAId, SectionAccess.Read);
        });

        // Section B refuses Analyst A across all 15 families with UnauthorizedAccessException
        await PostgresCases.RunAsync(QaScopeTestSeed.All15ScopeFamilies(w, useSectionA: false), async item =>
        {
            await using var actDb = _fixture.CreateDbContext();
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => item.Call(scope, w.AnalystAId, SectionAccess.Read));
        });
    }

    // -------------------------------------------------------------------------
    // Test 4
    // -------------------------------------------------------------------------
    [PostgresFact]
    public async Task Qa_is_refused_section_write_access_everywhere()
    {
        var w = await QaScopeTestSeed.SeedWorldAsync(_fixture);

        await using (var actDb = _fixture.CreateDbContext())
        {
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            var memberSections = await scope.GetMemberSectionIdsAsync(w.QaUserId);
            Assert.NotNull(memberSections);
            Assert.Empty(memberSections);
        }

        // Every Ensure*AccessAsync family throws UnauthorizedAccessException with SectionAccess.Write on BOTH Section A and Section B.
        await PostgresCases.RunAsync(QaScopeTestSeed.All15ScopeFamilies(w, useSectionA: true), async item =>
        {
            await using var actDb = _fixture.CreateDbContext();
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => item.Call(scope, w.QaUserId, SectionAccess.Write));
        });

        await PostgresCases.RunAsync(QaScopeTestSeed.All15ScopeFamilies(w, useSectionA: false), async item =>
        {
            await using var actDb = _fixture.CreateDbContext();
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => item.Call(scope, w.QaUserId, SectionAccess.Write));
        });
    }

    // -------------------------------------------------------------------------
    // Test 5
    // -------------------------------------------------------------------------
    [PostgresFact]
    public async Task Qa_dashboard_and_my_work_queries_return_all_sections()
    {
        var w = await QaScopeTestSeed.SeedWorldAsync(_fixture);

        // Act as QA: resolve dashboard scope and call read/overview services
        await using (var actDb = _fixture.CreateDbContext())
        {
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            var labScope = new DashboardLabScopeService(actDb, scope);
            var dashboard = TestServiceFactory.Dashboard(actDb);
            var workspace = TestServiceFactory.TestingWorkspace(actDb, scope);
            var notifications = TestServiceFactory.DashboardNotification(actDb);
            var recent = new RecentActivityService(actDb);
            var kpi = TestServiceFactory.KpiDashboard(actDb, scope: scope);
            var myTasks = TestServiceFactory.MyTasks(actDb);

            // DashboardLabScopeService.ResolveAsync returns SectionIds == null for QA with no lab filter
            var qaScopeAll = await labScope.ResolveAsync(w.QaUserId, null);
            Assert.Null(qaScopeAll.SectionIds);

            // DashboardService.GetSectionHeadDashboardAsync reaches line 629 (UserOrgMemberships).
            // With null sectionIds (QA cross-section), both Analyst A and Analyst B appear.
            var qaSecHeadAll = await dashboard.GetSectionHeadDashboardAsync(qaScopeAll.SectionIds);
            Assert.Contains(qaSecHeadAll.AnalystWorkloads, a => a.AnalystId == w.AnalystAId);
            Assert.Contains(qaSecHeadAll.AnalystWorkloads, a => a.AnalystId == w.AnalystBId);

            // When narrowed to Section B only, line 629 scopes analysts to Section B only.
            var qaSecHeadB = await dashboard.GetSectionHeadDashboardAsync(new[] { w.SectionBId });
            Assert.Contains(qaSecHeadB.AnalystWorkloads, a => a.AnalystId == w.AnalystBId);
            Assert.DoesNotContain(qaSecHeadB.AnalystWorkloads, a => a.AnalystId == w.AnalystAId);

            // Reviewer dashboard, incubation overview, and summary accept QA's null scope cleanly
            var qaReviewer = await dashboard.GetReviewerDashboardAsync(w.QaUserId, qaScopeAll.SectionIds);
            Assert.Contains(qaReviewer.ReviewQueue, s => s.SampleId == w.ReviewSampleAId);
            var qaInc = await dashboard.GetIncubationOverviewAsync(false, w.QaUserId, qaScopeAll.SectionIds);
            Assert.True(qaInc.Count >= 2);
            var qaSummary = await dashboard.GetSummaryAsync(RoleType.Qa, w.QaUserId, qaScopeAll.SectionIds);
            Assert.NotNull(qaSummary);

            // TestingWorkspaceService takes acting userId directly -> returns both Section A & Section B samples
            var qaActive = await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Page = 1, PageSize = 200 }, w.QaUserId);
            Assert.Contains(qaActive.Items, s => s.SampleId == w.SampleAId);
            Assert.Contains(qaActive.Items, s => s.SampleId == w.SampleBId);

            var qaCounts = await workspace.GetWorkloadCountsAsync(w.QaUserId);
            Assert.True(qaCounts.ReadyToRead >= 0);

            // RecentActivityService, DashboardNotificationService, MyTasksService, KpiDashboardService execute cleanly for QA
            var recentRows = await recent.GetRecentAsync(25, qaScopeAll.SectionIds);
            Assert.NotNull(recentRows);
            var notifs = await notifications.GetNotificationsAsync(RoleType.Qa, w.QaUserId);
            Assert.NotNull(notifs);
            var tasks = await myTasks.GetMyTasksAsync(w.QaUserId);
            Assert.NotNull(tasks);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var kpiDash = await kpi.GetDashboardAsync(w.QaUserId, canViewTeam: true, today.AddDays(-30), today, null, null, null, null);
            Assert.NotNull(kpiDash);
        }

        // Act as Analyst A: sees ONLY Section A, NOT Section B
        await using (var actDb = _fixture.CreateDbContext())
        {
            IUserSectionScopeService scope = new UserSectionScopeService(actDb);
            var labScope = new DashboardLabScopeService(actDb, scope);
            var dashboard = TestServiceFactory.Dashboard(actDb);
            var workspace = TestServiceFactory.TestingWorkspace(actDb, scope);

            var anaScope = await labScope.ResolveAsync(w.AnalystAId, null);
            Assert.NotNull(anaScope.SectionIds);
            Assert.Contains(w.SectionAId, anaScope.SectionIds);
            Assert.DoesNotContain(w.SectionBId, anaScope.SectionIds);

            var anaSecHead = await dashboard.GetSectionHeadDashboardAsync(anaScope.SectionIds);
            Assert.Contains(anaSecHead.AnalystWorkloads, a => a.AnalystId == w.AnalystAId);
            Assert.DoesNotContain(anaSecHead.AnalystWorkloads, a => a.AnalystId == w.AnalystBId);

            var anaActive = await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { Page = 1, PageSize = 200 }, w.AnalystAId);
            Assert.Contains(anaActive.Items, s => s.SampleId == w.SampleAId);
            Assert.DoesNotContain(anaActive.Items, s => s.SampleId == w.SampleBId);
        }
    }
}
