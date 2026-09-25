using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Task 6: the cross-laboratory tracking board (SampleTrackingService) and
// the OverallStatus/CoaAvailable/CancelledAtStep additions to the Sample
// Summary. Seed helpers duplicated from SectionClosureTests rather than
// shared (they are private there).
public class SampleTrackingTests
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
    private static async Task<World> SeedMixedSampleAsync(MicroLimsDbContext db, bool fpReady = true, string controlNumber = "CTRL-MIX-1")
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = db.DocumentSections.FirstOrDefault(s => s.Code == "FP")
            ?? new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        if (fp.Id == 0)
        {
            db.DocumentSections.Add(fp);
            await db.SaveChangesAsync();
        }

        await SeedUserAsync(db, 1, RoleType.Analyst, micro.DepartmentId, micro.Id);
        await SeedUserAsync(db, 2, RoleType.Analyst, micro.DepartmentId, fp.Id);
        var reviewerMicro = await SeedUserAsync(db, 3, RoleType.Reviewer, micro.DepartmentId, micro.Id);
        var reviewerFp = await SeedUserAsync(db, 4, RoleType.Reviewer, micro.DepartmentId, fp.Id);
        var headMicro = await SeedUserAsync(db, 5, RoleType.SectionHead, micro.DepartmentId, micro.Id);
        var headFp = await SeedUserAsync(db, 6, RoleType.SectionHead, micro.DepartmentId, fp.Id);
        var admin = await SeedUserAsync(db, 7, RoleType.SystemAdministrator, micro.DepartmentId, null);

        var routine = db.CausesOfTesting.FirstOrDefault(c => c.Name == "Routine") ?? new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = controlNumber, Status = SampleStatus.InTesting, CauseOfTesting = routine, ReceivedAt = DateTime.UtcNow };
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
        await db.SaveChangesAsync();

        return new World(sample, micro.Id, fp.Id, tamc, tymc, assay, reviewerMicro, reviewerFp, headMicro, headFp, admin);
    }

    // Micro auto-submits, is reviewed, and its Section Head rejects the
    // sample - FP's own tests are left untouched by this.
    private static async Task RejectMicroAsync(MicroLimsDbContext db, World w)
    {
        var review = TestServiceFactory.SampleReview(db);
        var approval = TestServiceFactory.SampleApproval(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();
        await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerMicro, Password, null, null);
        await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, "Out of limits", null);
    }

    private static async Task<(Sample sample, int sectionId, int reviewer, int head)> SeedApprovedFpOnlySampleAsync(MicroLimsDbContext db, string controlNumber = "CTRL-FP-1")
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = db.DocumentSections.FirstOrDefault(s => s.Code == "FP")
            ?? new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        if (fp.Id == 0)
        {
            db.DocumentSections.Add(fp);
            await db.SaveChangesAsync();
        }

        var baseId = (await db.Users.CountAsync()) + 100;
        var analyst = await SeedUserAsync(db, baseId, RoleType.Analyst, fp.DepartmentId, fp.Id);
        var reviewer = await SeedUserAsync(db, baseId + 1, RoleType.Reviewer, fp.DepartmentId, fp.Id);
        var head = await SeedUserAsync(db, baseId + 2, RoleType.SectionHead, fp.DepartmentId, fp.Id);

        var routine = db.CausesOfTesting.FirstOrDefault(c => c.Name == "Routine") ?? new CauseOfTesting { Name = "Routine", IsActive = true };
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = controlNumber, Status = SampleStatus.InTesting, CauseOfTesting = routine, ReceivedAt = DateTime.UtcNow.AddHours(-1) };
        var assay = new TestOrder { SectionId = fp.Id, TestCode = "ASSAY", AssignedAnalystId = analyst, Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready };
        sample.TestOrders.Add(assay);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var review = TestServiceFactory.SampleReview(db);
        var approval = TestServiceFactory.SampleApproval(db);
        await review.AutoSubmitForReviewIfReadyAsync(sample.Id, analyst);
        await db.SaveChangesAsync();
        await review.CompleteReviewAsync(sample.Id, reviewer, Password, null, null);
        await approval.DecideAsync(sample.Id, head, Password, ApprovalDecision.Approve, null, null);

        return (sample, fp.Id, reviewer, head);
    }

    // --- Tracking board ---

    [Fact]
    public async Task GetTrackingAsync_MixedSampleMicroRejectedFpUnderReview_ReportsOverallRejectedAndPerLabStage()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: true);
        await RejectMicroAsync(db, w);
        await SeedApprovedFpOnlySampleAsync(db);

        var result = await TestServiceFactory.SampleTracking(db).GetTrackingAsync(new SampleTrackingFilterDto());

        Assert.Equal(2, result.TotalCount);
        var rowA = result.Items.Single(r => r.SampleId == w.Sample.Id);
        Assert.Equal("Rejected", rowA.OverallStatus);
        var microLab = rowA.Labs.Single(l => l.SectionId == w.Micro);
        Assert.Equal("Microbiology Laboratory", microLab.SectionName);
        Assert.Equal("Rejected", microLab.Stage);
        var fpLab = rowA.Labs.Single(l => l.SectionId == w.Fp);
        Assert.Equal("Finished Product Laboratory", fpLab.SectionName);
        Assert.Equal("UnderReview", fpLab.Stage);
    }

    [Fact]
    public async Task GetTrackingAsync_FpOnlySampleApproved_ReportsOverallApproved()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: true);
        await RejectMicroAsync(db, w);
        var (approvedSample, fpId, _, _) = await SeedApprovedFpOnlySampleAsync(db);

        var result = await TestServiceFactory.SampleTracking(db).GetTrackingAsync(new SampleTrackingFilterDto());

        var rowB = result.Items.Single(r => r.SampleId == approvedSample.Id);
        Assert.Equal("Approved", rowB.OverallStatus);
        Assert.Single(rowB.Labs);
        Assert.Equal(fpId, rowB.Labs[0].SectionId);
        Assert.Equal("Approved", rowB.Labs[0].Stage);
    }

    [Fact]
    public async Task GetTrackingAsync_FilterByLabSectionId_ReturnsOnlySamplesWithThatLabsTests()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: true);
        await RejectMicroAsync(db, w);
        await SeedApprovedFpOnlySampleAsync(db);

        var result = await TestServiceFactory.SampleTracking(db).GetTrackingAsync(new SampleTrackingFilterDto { LabSectionId = w.Micro });

        Assert.Single(result.Items);
        Assert.Equal(w.Sample.Id, result.Items[0].SampleId);
    }

    [Fact]
    public async Task GetTrackingAsync_FilterByOverallRejected_ReturnsOnlyMatchingSample()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: true);
        await RejectMicroAsync(db, w);
        await SeedApprovedFpOnlySampleAsync(db);

        var result = await TestServiceFactory.SampleTracking(db).GetTrackingAsync(new SampleTrackingFilterDto { Overall = "Rejected" });

        Assert.Single(result.Items);
        Assert.Equal(w.Sample.Id, result.Items[0].SampleId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetTrackingAsync_PageSizeRespected_TotalCountStillReflectsAllMatches()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: true);
        await RejectMicroAsync(db, w);
        await SeedApprovedFpOnlySampleAsync(db);

        var result = await TestServiceFactory.SampleTracking(db).GetTrackingAsync(new SampleTrackingFilterDto { Page = 1, PageSize = 1 });

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.PageSize);
    }

    // An OOS origin sample whose only TestOrder moved to a retest sample
    // (fully superseded) is still a received sample of its own - it must
    // stay on the board, not just its retest descendant.
    [Fact]
    public async Task GetTrackingAsync_SampleWithOnlySupersededTestOrder_StillAppearsOnBoard()
    {
        await using var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var routine = new CauseOfTesting { Name = "Routine", IsActive = true };
        db.CausesOfTesting.Add(routine);
        var sample = new Sample
        {
            Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-OOS-1",
            Status = SampleStatus.RetestRequested, CauseOfTesting = routine, ReceivedAt = DateTime.UtcNow
        };
        var supersededOrder = new TestOrder
        {
            SectionId = micro.Id, TestCode = "TAMC", Status = ApprovalStatus.Rejected,
            CurrentStep = WorkflowStep.Reviewed, IsSuperseded = true
        };
        sample.TestOrders.Add(supersededOrder);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.SampleTracking(db).GetTrackingAsync(new SampleTrackingFilterDto());

        var row = result.Items.Single(r => r.SampleId == sample.Id);
        Assert.Equal("RetestRequested", row.OverallStatus);
        Assert.Single(row.Labs);
        Assert.Equal(micro.Id, row.Labs[0].SectionId);
        Assert.Equal("RetestRequested", row.Labs[0].Stage);
    }

    // --- Summary: OverallStatus / CombinedCoaAvailable / CancelledAtStep / section Closed ---

    [Fact]
    public async Task GetSummaryAsync_BeforeClosure_CombinedCoaAvailableFalse_FpCoaAvailableFalse()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: false); // FP assay Incubating
        db.Incubations.Add(new Incubation { TestOrderId = w.FpAssay.Id, StageNumber = 2, StartedAt = DateTime.UtcNow, StartedByUserId = 2 });
        await db.SaveChangesAsync();
        await RejectMicroAsync(db, w); // micro rejected; FP still open (Incubating)

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(w.Sample.Id);

        Assert.NotNull(summary);
        Assert.False(summary!.CombinedCoaAvailable);
        var fpSection = summary.Sections.Single(s => s.SectionId == w.Fp);
        Assert.False(fpSection.CoaAvailable);
    }

    [Fact]
    public async Task GetSummaryAsync_AfterClosure_CancelledAtStepStageSet_SectionClosed_CombinedCoaAvailableTrue()
    {
        await using var db = NewDb();
        var w = await SeedMixedSampleAsync(db, fpReady: false); // FP assay Incubating
        db.Incubations.Add(new Incubation { TestOrderId = w.FpAssay.Id, StageNumber = 2, StartedAt = DateTime.UtcNow, StartedByUserId = 2 });
        await db.SaveChangesAsync();
        await RejectMicroAsync(db, w);

        await TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "Sample rejected by Microbiology Laboratory", null);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(w.Sample.Id);
        Assert.NotNull(summary);

        var fpOrder = summary!.TestOrders.Single(t => t.TestOrderId == w.FpAssay.Id);
        Assert.Equal("Incubating", fpOrder.CancelledAtStep);
        Assert.Equal(2, fpOrder.CancelledAtStage);

        var fpSection = summary.Sections.Single(s => s.SectionId == w.Fp);
        Assert.Equal("Closed", fpSection.Status);
        Assert.Equal("Sample rejected by Microbiology Laboratory", fpSection.CloseReason);
        Assert.Equal("User 6", fpSection.ClosedByName); // HeadFp = user id 6

        Assert.True(summary.CombinedCoaAvailable);
        Assert.Equal("Rejected", summary.OverallStatus);
    }

    [Fact]
    public async Task GetSummaryAsync_ApprovedSection_CoaAvailableTrue()
    {
        await using var db = NewDb();
        var (sample, fpId, _, _) = await SeedApprovedFpOnlySampleAsync(db);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sample.Id);

        Assert.NotNull(summary);
        var fpSection = summary!.Sections.Single(s => s.SectionId == fpId);
        Assert.Equal("Approved", fpSection.Status);
        Assert.True(fpSection.CoaAvailable);
        Assert.True(summary.CombinedCoaAvailable);
        Assert.Equal("Approved", summary.OverallStatus);
    }
}
