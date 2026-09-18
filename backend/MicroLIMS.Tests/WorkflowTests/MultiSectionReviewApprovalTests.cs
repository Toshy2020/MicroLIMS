using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Per-section review/approval on a sample tested by two laboratory
// sections (Microbiology + Finished Product): each section is reviewed and
// approved by its own people, and Sample.Status is rolled up from both.
public class MultiSectionReviewApprovalTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private sealed record World(
        Sample Sample, int Micro, int Fp,
        TestOrder MicroTamc, TestOrder MicroTymc, TestOrder FpAssay,
        int ReviewerMicro, int ReviewerFp, int HeadMicro, int HeadFp, int Admin);

    private static async Task<int> SeedUserAsync(MicroLimsDbContext db, int id, RoleType type, int departmentId, int? sectionId)
    {
        var role = new Role { Type = type, Name = type.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.Users.Add(new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) });
        if (type != RoleType.SystemAdministrator)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = id, DepartmentId = departmentId, SectionId = sectionId });
        await db.SaveChangesAsync();
        return id;
    }

    // Both sections' tests finished (Ready); the sample is still InTesting.
    private static async Task<World> SeedAsync(MicroLimsDbContext db, bool fpReady = true)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, micro.Id);
        await SeedUserAsync(db, 2, RoleType.Analyst, micro.DepartmentId, fp.Id);
        var reviewerMicro = await SeedUserAsync(db, 3, RoleType.Reviewer, micro.DepartmentId, micro.Id);
        var reviewerFp = await SeedUserAsync(db, 4, RoleType.Reviewer, micro.DepartmentId, fp.Id);
        var headMicro = await SeedUserAsync(db, 5, RoleType.SectionHead, micro.DepartmentId, micro.Id);
        var headFp = await SeedUserAsync(db, 6, RoleType.SectionHead, micro.DepartmentId, fp.Id);
        var admin = await SeedUserAsync(db, 7, RoleType.SystemAdministrator, micro.DepartmentId, null);

        var routine = new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-MIX-1", Status = SampleStatus.InTesting, CauseOfTesting = routine };
        var tamc = new TestOrder { SectionId = micro.Id, TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready, AssignedAnalystId = 1 };
        var tymc = new TestOrder { SectionId = micro.Id, TestCode = "TYMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready, AssignedAnalystId = 1 };
        var assay = new TestOrder
        {
            SectionId = fp.Id, TestCode = "ASSAY", AssignedAnalystId = 2,
            Status = fpReady ? ApprovalStatus.ResultEntered : ApprovalStatus.InProgress,
            CurrentStep = fpReady ? WorkflowStep.Ready : WorkflowStep.Incubating
        };
        sample.TestOrders.AddRange(new[] { tamc, tymc, assay });
        db.Samples.Add(sample);
        db.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
        await db.SaveChangesAsync();

        return new World(sample, micro.Id, fp.Id, tamc, tymc, assay, reviewerMicro, reviewerFp, headMicro, headFp, admin);
    }

    private static async Task<SampleSectionSignoff?> SignoffAsync(MicroLimsDbContext db, int sampleId, int sectionId) =>
        await db.SampleSectionSignoffs.AsNoTracking().FirstOrDefaultAsync(s => s.SampleId == sampleId && s.SectionId == sectionId);

    private static async Task<SampleStatus> StatusAsync(MicroLimsDbContext db, int sampleId) =>
        (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == sampleId)).Status;

    // Both sections submitted and reviewed by their own reviewers.
    private static async Task<World> SeedUnderApprovalAsync(MicroLimsDbContext db)
    {
        var w = await SeedAsync(db);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();
        await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerMicro, Password, null, null);
        await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerFp, Password, null, null);
        return w;
    }

    [Fact]
    public async Task AutoSubmit_MovesOnlyTheFinishedSectionToReview_SampleWaitsForTheOther()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        var review = TestServiceFactory.SampleReview(db);

        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();

        Assert.Equal(SectionSignoffStatus.UnderReview, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
        Assert.Null(await SignoffAsync(db, w.Sample.Id, w.Fp));
        Assert.Equal(SampleStatus.InTesting, await StatusAsync(db, w.Sample.Id));

        var assay = await db.TestOrders.FirstAsync(t => t.Id == w.FpAssay.Id);
        assay.CurrentStep = WorkflowStep.Ready;
        await db.SaveChangesAsync();
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 2);
        await db.SaveChangesAsync();

        Assert.Equal(SectionSignoffStatus.UnderReview, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);
        Assert.Equal(SampleStatus.UnderReview, await StatusAsync(db, w.Sample.Id));
        Assert.Equal(2, await db.ReviewWorkflowEvents.CountAsync(e => e.EntityId == w.Sample.Id && e.EventType == ReviewWorkflowEventType.SubmittedForReview && e.SectionId != null));
    }

    [Fact]
    public async Task Review_CoversOnlyTheReviewersSection_AndCannotReachAnotherSection()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();

        // No section named: the FP reviewer's only waiting section is FP.
        await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerFp, Password, null, null);

        var fp = await SignoffAsync(db, w.Sample.Id, w.Fp);
        Assert.Equal(SectionSignoffStatus.UnderApproval, fp!.Status);
        Assert.Equal(w.ReviewerFp, fp.ReviewedByUserId);
        Assert.NotNull(fp.ReviewSignatureId);
        Assert.Equal(SectionSignoffStatus.UnderReview, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
        Assert.Equal(SampleStatus.UnderReview, await StatusAsync(db, w.Sample.Id));
        Assert.Equal(WorkflowStep.Ready, (await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.MicroTamc.Id)).CurrentStep);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            review.CompleteReviewAsync(w.Sample.Id, w.ReviewerFp, Password, null, null, sectionId: w.Micro));
    }

    [Fact]
    public async Task Admin_WithTwoSectionsWaiting_MustChooseASection()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            review.CompleteReviewAsync(w.Sample.Id, w.Admin, Password, null, null));
        Assert.Contains("Choose a laboratory section", ex.Message);

        await review.CompleteReviewAsync(w.Sample.Id, w.Admin, Password, null, null, sectionId: w.Micro);
        Assert.Equal(SectionSignoffStatus.UnderApproval, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
    }

    [Fact]
    public async Task Approve_SampleIsApprovedOnlyWhenTheLastSectionApproves()
    {
        await using var db = NewDb();
        var w = await SeedUnderApprovalAsync(db);
        var approval = TestServiceFactory.SampleApproval(db);

        await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Approve, null, null, certificateRemarks: "Micro remark");

        Assert.Equal(SampleStatus.UnderApproval, await StatusAsync(db, w.Sample.Id));
        Assert.Equal(ApprovalStatus.Approved, (await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.MicroTamc.Id)).Status);
        Assert.Equal(ApprovalStatus.Reviewed, (await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.FpAssay.Id)).Status);

        await approval.DecideAsync(w.Sample.Id, w.HeadFp, Password, ApprovalDecision.Approve, null, null, certificateRemarks: "FP remark");

        var sample = await db.Samples.AsNoTracking().FirstAsync(s => s.Id == w.Sample.Id);
        Assert.Equal(SampleStatus.Approved, sample.Status);
        Assert.Equal(w.HeadFp, sample.ApprovedByUserId);
        Assert.Null(sample.CertificateRemarks); // per section when more than one section tested it
        Assert.Equal("Micro remark", (await SignoffAsync(db, w.Sample.Id, w.Micro))!.CertificateRemarks);
        Assert.Equal("FP remark", (await SignoffAsync(db, w.Sample.Id, w.Fp))!.CertificateRemarks);
        Assert.Equal(ApprovalStatus.Approved, (await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.FpAssay.Id)).Status);
    }

    [Fact]
    public async Task Approve_SectionHeadCannotDecideAnotherSection()
    {
        await using var db = NewDb();
        var w = await SeedUnderApprovalAsync(db);
        var approval = TestServiceFactory.SampleApproval(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approval.DecideAsync(w.Sample.Id, w.HeadFp, Password, ApprovalDecision.Approve, null, null, sectionId: w.Micro));
        Assert.Equal(SectionSignoffStatus.UnderApproval, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
    }

    [Fact]
    public async Task Reject_ByOneSection_RejectsTheSampleAndClosesTheOtherSection()
    {
        await using var db = NewDb();
        var w = await SeedUnderApprovalAsync(db);
        var approval = TestServiceFactory.SampleApproval(db);

        await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, "Out of limits", null);

        Assert.Equal(SampleStatus.Rejected, await StatusAsync(db, w.Sample.Id));
        Assert.Equal(SectionSignoffStatus.Rejected, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
        Assert.Equal(SectionSignoffStatus.Cancelled, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);
        Assert.Equal(ApprovalStatus.Rejected, (await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.FpAssay.Id)).Status);
    }

    [Fact]
    public async Task Reject_KeepsAnotherSectionsEarlierApproval()
    {
        await using var db = NewDb();
        var w = await SeedUnderApprovalAsync(db);
        var approval = TestServiceFactory.SampleApproval(db);

        await approval.DecideAsync(w.Sample.Id, w.HeadFp, Password, ApprovalDecision.Approve, null, null);
        await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, null, null);

        Assert.Equal(SampleStatus.Rejected, await StatusAsync(db, w.Sample.Id));
        Assert.Equal(SectionSignoffStatus.Approved, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);
        Assert.Equal(ApprovalStatus.Approved, (await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.FpAssay.Id)).Status);
    }

    [Fact]
    public async Task Dashboards_QueueRowsArePerSection_AndLimitedToTheViewersSections()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();
        var dashboard = TestServiceFactory.Dashboard(db);
        var scope = new UserSectionScopeService(db);

        // Micro waits for review while FP is still testing; the sample as a
        // whole is still InTesting.
        Assert.Equal(SampleStatus.InTesting, await StatusAsync(db, w.Sample.Id));

        var microQueue = await dashboard.GetReviewerDashboardAsync(w.ReviewerMicro, await scope.GetAccessibleSectionIdsAsync(w.ReviewerMicro));
        var row = Assert.Single(microQueue.ReviewQueue);
        Assert.Equal(w.Micro, row.SectionId);
        Assert.Equal("Microbiology Laboratory", row.SectionName);
        Assert.Equal(new[] { "TAMC", "TYMC" }, row.Tests.Select(t => t.TestCode).OrderBy(c => c).ToArray());

        var fpQueue = await dashboard.GetReviewerDashboardAsync(w.ReviewerFp, await scope.GetAccessibleSectionIdsAsync(w.ReviewerFp));
        Assert.Empty(fpQueue.ReviewQueue);

        // The Micro Section Head's review tile counts the waiting section.
        var head = await dashboard.GetSectionHeadDashboardAsync(await scope.GetAccessibleSectionIdsAsync(w.HeadMicro));
        Assert.Equal(1, head.PendingReview);

        // The workspace's awaiting-review filter finds the sample for Micro only.
        var workspace = new TestingWorkspaceService(db, scope);
        var microPage = await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "awaitingReview" }, w.ReviewerMicro);
        Assert.Single(microPage.Items);
        var fpPage = await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "awaitingReview" }, w.ReviewerFp);
        Assert.Empty(fpPage.Items);
    }

    [Fact]
    public async Task Dashboards_BothSectionsWaiting_UnrestrictedViewerSeesOneRowPerSection()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();

        var reviewer = await TestServiceFactory.Dashboard(db).GetReviewerDashboardAsync(w.Admin);
        Assert.Equal(2, reviewer.PendingReviewCount);
        Assert.Equal(new[] { w.Micro, w.Fp }.OrderBy(x => x), reviewer.ReviewQueue.Select(r => r.SectionId!.Value).OrderBy(x => x));

        var notifications = await TestServiceFactory.DashboardNotification(db).GetNotificationsAsync(RoleType.Reviewer, w.ReviewerFp);
        Assert.Contains(notifications, n => n.Type == "ReviewWaiting" && n.Message == "1 sample(s) awaiting review.");
    }

    [Fact]
    public async Task Kpis_CountOnlyTheViewersSections()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();
        var kpi = TestServiceFactory.Kpi(db);
        var scope = new UserSectionScopeService(db);
        var micro = await scope.GetAccessibleSectionIdsAsync(w.HeadMicro);
        var fp = await scope.GetAccessibleSectionIdsAsync(w.HeadFp);

        Assert.Equal(1, (await kpi.GetSampleQueueCountsAsync(sectionIds: micro)).ReviewQueueCount);
        Assert.Equal(0, (await kpi.GetSampleQueueCountsAsync(sectionIds: fp)).ReviewQueueCount);

        // Each Section Head sees only their own section's analysts.
        Assert.Equal(new[] { 1 }, (await kpi.GetAnalystKpisAsync(sectionIds: micro)).Select(a => a.UserId).ToArray());
        Assert.Equal(new[] { 2 }, (await kpi.GetAnalystKpisAsync(sectionIds: fp)).Select(a => a.UserId).ToArray());
    }

    [Fact]
    public async Task Oos_AGroupBelongsToTheSectionThatOrderedTheRetest()
    {
        await using var db = NewDb();
        var w = await SeedUnderApprovalAsync(db);
        await TestServiceFactory.SampleApproval(db).DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { w.MicroTamc.Id });
        var groupCode = (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == w.Sample.Id)).OosGroupCode!;
        var scope = new UserSectionScopeService(db);
        var oos = new OosTrackingService(db);

        // The origin also carries FP tests, but the OOS is Microbiology's.
        Assert.Single(await oos.GetOosGroupsAsync(await scope.GetAccessibleSectionIdsAsync(w.HeadMicro)));
        Assert.Empty(await oos.GetOosGroupsAsync(await scope.GetAccessibleSectionIdsAsync(w.HeadFp)));

        await scope.EnsureOosGroupAccessAsync(w.HeadMicro, groupCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.EnsureOosGroupAccessAsync(w.HeadFp, groupCode));
    }

    [Fact]
    public async Task Summary_ShowsOnlyTheViewersTests_ButListsEverySectionsState()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        await TestServiceFactory.SampleReview(db).AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();
        var scope = new UserSectionScopeService(db);

        var fpView = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(w.Sample.Id, await scope.GetAccessibleSectionIdsAsync(w.ReviewerFp));
        Assert.Equal(new[] { "ASSAY" }, fpView!.TestOrders.Select(t => t.TestCode).ToArray());
        Assert.False(fpView.AllSectionsVisible);
        var microRow = fpView.Sections.Single(s => s.SectionId == w.Micro);
        Assert.False(microRow.CanView);
        Assert.Equal("UnderReview", microRow.Status);
        Assert.Equal("InTesting", fpView.Sections.Single(s => s.SectionId == w.Fp).Status);

        var adminView = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(w.Sample.Id);
        Assert.Equal(3, adminView!.TestOrders.Count);
        Assert.True(adminView.AllSectionsVisible);
    }

    [Fact]
    public async Task Memberships_AreValidated_AndReplaced()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var org = new LaboratoryOrganizationService(db, new UserSectionScopeService(db));
        var qc = (await db.DocumentSections.FirstAsync(s => s.Id == w.Micro)).DepartmentId;

        // A section must belong to the department it is listed under.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            org.ReplaceMembershipsAsync(w.ReviewerMicro, new[] { new UserOrgMembershipDto(qc + 999, w.Micro) }, w.Admin));

        var result = await org.ReplaceMembershipsAsync(w.ReviewerMicro, new[] { new UserOrgMembershipDto(qc, w.Fp) }, w.Admin);
        Assert.Equal(new[] { new UserOrgMembershipDto(qc, w.Fp) }, result);
        Assert.Equal(new[] { w.Fp }, await new UserSectionScopeService(db).GetAccessibleSectionIdsAsync(w.ReviewerMicro));
    }

    [Fact]
    public async Task Retest_CarriesOnlyTheDecidingSectionsTests_AndItsOutcomeResolvesThatSection()
    {
        await using var db = NewDb();
        var w = await SeedUnderApprovalAsync(db);
        var approval = TestServiceFactory.SampleApproval(db);

        await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.RetestRetainedSample, null, null,
            selectedTestOrderIds: new List<int> { w.MicroTamc.Id });

        var spinoff = await db.Samples.Include(s => s.TestOrders).AsNoTracking().FirstAsync(s => s.OriginSampleId == w.Sample.Id);
        Assert.Equal(new[] { "TAMC" }, spinoff.TestOrders.Select(t => t.TestCode).ToArray());
        Assert.All(spinoff.TestOrders, t => Assert.Equal(w.Micro, t.SectionId));
        Assert.Equal(SectionSignoffStatus.RetestRequested, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
        Assert.Equal(SampleStatus.UnderApproval, await StatusAsync(db, w.Sample.Id)); // FP still to decide

        await approval.DecideAsync(w.Sample.Id, w.HeadFp, Password, ApprovalDecision.Approve, null, null);
        var origin = await db.Samples.AsNoTracking().FirstAsync(s => s.Id == w.Sample.Id);
        Assert.Equal(SampleStatus.RetestRequested, origin.Status); // waiting only on the Micro retest
        Assert.Equal(ApprovalDecision.RetestRetainedSample, origin.ApprovalDecision);

        // The retest comes back and is approved - that resolves Micro on the origin.
        var retest = await db.Samples.FirstAsync(s => s.Id == spinoff.Id);
        retest.Status = SampleStatus.UnderApproval;
        retest.ReviewedByUserId = w.ReviewerMicro;
        await db.SaveChangesAsync();
        await approval.DecideAsync(retest.Id, w.HeadMicro, Password, ApprovalDecision.Approve, null, null);

        Assert.Equal(SectionSignoffStatus.Approved, (await SignoffAsync(db, w.Sample.Id, w.Micro))!.Status);
        Assert.Equal(SampleStatus.Approved, await StatusAsync(db, w.Sample.Id));
    }
}
