using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// A closed sample's tests (rejected, voided, cancelled, superseded by a retest,
// or on hold awaiting one) must read as closed everywhere - never as a test
// with a next step, and never inside an active work queue.
public class ClosedTestStatesTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<CauseOfTesting> SeedCauseAsync(MicroLimsDbContext db)
    {
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause;
    }

    private static async Task<SampleDto> WorkspaceRowAsync(MicroLimsDbContext db, int sampleId)
    {
        var page = await new TestingWorkspaceService(db, new UserSectionScopeService(db)).GetActiveSamplesAsync(new TestingWorkspaceFilterDto { PageSize = 200 });
        return Assert.Single(page.Items, s => s.SampleId == sampleId);
    }

    [Fact]
    public async Task Workspace_RejectedSample_EveryTestReadsRejected_IncludingStaleTestStatuses()
    {
        await using var db = NewDb();
        var cause = await SeedCauseAsync(db);

        // Sample #85's shape: rejected, tests left at CurrentStep Ready - one
        // test's own status even predates the sample-level decision.
        var sample = new Sample { ReferenceNumber = "FP0926008", Status = SampleStatus.Rejected, CauseOfTesting = cause };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Rejected, CurrentStep = WorkflowStep.Ready });
        sample.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var row = await WorkspaceRowAsync(db, sample.Id);

        Assert.Equal(2, row.AssignedTests.Count);
        Assert.All(row.AssignedTests, t =>
        {
            Assert.Equal("REJECTED", t.WorkflowState);
            Assert.Equal("Rejected", t.WorkflowStatus);
            Assert.True(t.IsWorkflowLocked);
            Assert.False(t.IsResultEntryAllowed);
        });
    }

    [Fact]
    public async Task Workspace_RetestOrigin_SupersededTestReadsSuperseded_RemainingTestReadsOnHold()
    {
        await using var db = NewDb();
        var cause = await SeedCauseAsync(db);

        var origin = new Sample { ReferenceNumber = "FP-ORIGIN", Status = SampleStatus.RetestRequested, CauseOfTesting = cause };
        var retested = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed, IsSuperseded = true };
        var waiting = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed };
        origin.TestOrders.Add(retested);
        origin.TestOrders.Add(waiting);
        db.Samples.Add(origin);
        await db.SaveChangesAsync();

        var row = await WorkspaceRowAsync(db, origin.Id);

        Assert.Equal("SUPERSEDED", Assert.Single(row.AssignedTests, t => t.TestOrderId == retested.Id).WorkflowState);
        var onHold = Assert.Single(row.AssignedTests, t => t.TestOrderId == waiting.Id);
        Assert.Equal("ON_HOLD", onHold.WorkflowState);
        Assert.False(onHold.IsResultEntryAllowed);
    }

    [Fact]
    public async Task VoidedSample_ReadsVoided_AndLeavesActiveQueuesAndNotifications()
    {
        await using var db = NewDb();
        var cause = await SeedCauseAsync(db);

        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        var headRole = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.AddRange(role, headRole);
        await db.SaveChangesAsync();
        var analyst = new User { FullName = "Ana Lyst", Username = "analyst", RoleId = role.Id, PasswordHash = "not-used" };
        var sectionHead = new User { FullName = "Sam Head", Username = "samhead", RoleId = headRole.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Void-Pass-1") };
        db.Users.AddRange(analyst, sectionHead);
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "FP-VOID", Status = SampleStatus.InTesting, CauseOfTesting = cause };
        var incubating = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating };
        incubating.Incubations.Add(new Incubation { StepNumber = 1, StepName = "Stage 1", StartedAt = DateTime.UtcNow.AddDays(-2), CompletedAt = DateTime.UtcNow.AddMinutes(-5) });
        sample.TestOrders.Add(incubating);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // Before the void the test is active work.
        Assert.Equal(1, (await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync()).ActiveTests);

        await TestServiceFactory.SampleCorrection(db).VoidAsync(sample.Id, "Registered twice", "Void-Pass-1", sectionHead.Id, null);

        var test = Assert.Single((await WorkspaceRowAsync(db, sample.Id)).AssignedTests);
        Assert.Equal("VOIDED", test.WorkflowState);
        Assert.Equal("Voided", test.Status);
        Assert.False(test.IsResultEntryAllowed);

        Assert.Equal(0, (await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync()).ActiveTests);

        var notifications = await TestServiceFactory.DashboardNotification(db).GetNotificationsAsync(RoleType.Analyst, analyst.Id);
        Assert.DoesNotContain(notifications, n => n.Type == "IncubationReady");
    }

    [Fact]
    public async Task StatusDistribution_DoesNotCountVoidedTestsAsPending()
    {
        await using var db = NewDb();
        var cause = await SeedCauseAsync(db);

        var approved = new Sample { ReferenceNumber = "FP-OK", Status = SampleStatus.Approved, CauseOfTesting = cause };
        approved.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        var voided = new Sample { ReferenceNumber = "FP-VOIDED", Status = SampleStatus.Voided, CauseOfTesting = cause };
        voided.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Voided, CurrentStep = WorkflowStep.Incubating });
        db.Samples.AddRange(approved, voided);
        await db.SaveChangesAsync();

        var slices = await TestServiceFactory.Dashboard(db).GetStatusDistributionAsync();

        int CountOf(string status) => slices
            .Select(s => new { Status = (string)s.GetType().GetProperty("status")!.GetValue(s)!, Count = (int)s.GetType().GetProperty("count")!.GetValue(s)! })
            .Single(s => s.Status == status).Count;

        Assert.Equal(1, CountOf("Approved"));
        Assert.Equal(0, CountOf("Pending"));
        Assert.Equal(0, CountOf("Rejected"));
    }

    // A cancelled test (SectionClosureService: this lab stopped testing
    // after another lab rejected the sample) is struck from active work the
    // same way a voided test is - never counted as still-Pending.
    [Fact]
    public async Task StatusDistribution_DoesNotCountCancelledTestsAsPending()
    {
        await using var db = NewDb();
        var cause = await SeedCauseAsync(db);

        var approved = new Sample { ReferenceNumber = "FP-OK", Status = SampleStatus.Approved, CauseOfTesting = cause };
        approved.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        var cancelled = new Sample { ReferenceNumber = "FP-CANCELLED", Status = SampleStatus.Rejected, CauseOfTesting = cause };
        cancelled.TestOrders.Add(new TestOrder { TestCode = "ASSAY", Status = ApprovalStatus.Cancelled, CurrentStep = WorkflowStep.Incubating, CancelledAtStep = WorkflowStep.Incubating });
        db.Samples.AddRange(approved, cancelled);
        await db.SaveChangesAsync();

        var slices = await TestServiceFactory.Dashboard(db).GetStatusDistributionAsync();

        int CountOf(string status) => slices
            .Select(s => new { Status = (string)s.GetType().GetProperty("status")!.GetValue(s)!, Count = (int)s.GetType().GetProperty("count")!.GetValue(s)! })
            .Single(s => s.Status == status).Count;

        Assert.Equal(1, CountOf("Approved"));
        Assert.Equal(0, CountOf("Pending"));
        Assert.Equal(0, CountOf("Rejected"));
    }

    // A cancelled test is nobody's active task - MyTasksService must never
    // surface it as something an analyst still needs to read, even with an
    // open, due incubation window sitting on the row (SectionClosureService
    // never touches CurrentStep/Incubations, only Status).
    [Fact]
    public async Task MyTasks_ExcludesCancelledTests()
    {
        await using var db = NewDb();
        var cause = await SeedCauseAsync(db);

        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var analyst = new User { FullName = "Ana Lyst", Username = "analyst-cancelled", RoleId = role.Id, PasswordHash = "not-used" };
        db.Users.Add(analyst);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, analyst.Id);

        var sample = new Sample { ReferenceNumber = "FP-CANCELLED-TASK", Status = SampleStatus.Rejected, CauseOfTesting = cause };
        var order = new TestOrder
        {
            TestCode = "ASSAY",
            SectionId = section.Id,
            Status = ApprovalStatus.Cancelled,
            CurrentStep = WorkflowStep.Incubating,
            AssignedAnalystId = analyst.Id
        };
        // Still physically due soon - if the Status filter didn't exclude
        // Cancelled this would surface as a "Read Test" task.
        order.Incubations.Add(new Incubation { StepName = "Stage 1", ExpectedReadingAt = DateTime.UtcNow.AddHours(1), CompletedAt = null });
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var tasks = await new MyTasksService(db, new UserSectionScopeService(db)).GetMyTasksAsync(analyst.Id);

        Assert.Empty(tasks);
    }

    [Theory]
    [InlineData(SampleStatus.Approved, "Approved")]
    [InlineData(SampleStatus.Rejected, "Rejected")]
    [InlineData(SampleStatus.Voided, "Voided")]
    [InlineData(SampleStatus.Cancelled, "Cancelled")]
    [InlineData(SampleStatus.UnderApproval, "Pending")]
    public void Reports_DeriveApprovalStatus_NeverReadsAVoidAsRejectedOrPending(SampleStatus status, string expected)
    {
        Assert.Equal(expected, ReportingQueryService.DeriveApprovalStatus(status));
    }
}
