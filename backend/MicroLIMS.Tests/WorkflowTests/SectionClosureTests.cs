using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// After one laboratory rejects a sample, another laboratory's still-open
// tests can be closed rather than finished - cancelled, not judged, each
// keeping the step it had reached. Helpers duplicated from
// MultiSectionReviewApprovalTests rather than shared (they are private
// there).
public class SectionClosureTests
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

    [Fact]
    public async Task Close_AfterOtherLabRejected_CancelsOpenTests_KeepsStage_SampleFinalizes()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);             // FP assay Incubating
        db.Incubations.Add(new Incubation { TestOrderId = w.FpAssay.Id, StageNumber = 2, StartedAt = DateTime.UtcNow, StartedByUserId = 2 });
        await db.SaveChangesAsync();
        await RejectMicroAsync(db, w);                            // helper: auto-submit, review, reject by HeadMicro

        await TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "Sample rejected by Microbiology Laboratory", null);

        var order = await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.FpAssay.Id);
        Assert.Equal(ApprovalStatus.Cancelled, order.Status);
        Assert.Equal(WorkflowStep.Incubating, order.CancelledAtStep);
        Assert.Equal(2, order.CancelledAtStage);
        var signoff = (await db.SampleSectionSignoffs.AsNoTracking().FirstAsync(s => s.SampleId == w.Sample.Id && s.SectionId == w.Fp));
        Assert.Equal(SectionSignoffStatus.Cancelled, signoff.Status);
        Assert.Equal(w.HeadFp, signoff.ClosedByUserId);
        Assert.Equal("Sample rejected by Microbiology Laboratory", signoff.CloseReason);
        Assert.NotNull(signoff.CloseSignatureId);
        Assert.Equal(SampleStatus.Rejected, (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == w.Sample.Id)).Status);
        Assert.True(await db.ReviewWorkflowEvents.AnyAsync(e => e.EntityId == w.Sample.Id && e.EventType == ReviewWorkflowEventType.SectionTestingClosed && e.SectionId == w.Fp));
    }

    [Fact]
    public async Task Close_WhenNoLabRejected_IsRefused()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "x", null));
        Assert.Contains("only after another laboratory rejected", ex.Message);
    }

    [Fact]
    public async Task Close_Twice_IsRefused()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        await RejectMicroAsync(db, w);

        await TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "Sample rejected by Microbiology Laboratory", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "Sample rejected by Microbiology Laboratory", null));
        Assert.Contains("already closed", ex.Message);
    }

    [Fact]
    public async Task Close_ByOtherLabsHead_IsForbidden()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);

        // HeadMicro tries to close the FP section -> UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadMicro, Password, "x", null));
    }

    [Fact]
    public async Task Close_LabAlreadyApproved_IsRefused()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db);
        var review = TestServiceFactory.SampleReview(db);
        var approval = TestServiceFactory.SampleApproval(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();
        await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerMicro, Password, null, null);
        await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerFp, Password, null, null);

        // FP approved before micro rejects -> FP not open -> refused.
        await approval.DecideAsync(w.Sample.Id, w.HeadFp, Password, ApprovalDecision.Approve, null, null);
        await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "x", null));
        Assert.Contains("has no open tests", ex.Message);
    }

    [Fact]
    public async Task Close_RequiresReason()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        await RejectMicroAsync(db, w);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "   ", null));
        Assert.Contains("A reason is required", ex.Message);
    }
}
