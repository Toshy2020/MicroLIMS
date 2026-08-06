using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SegregationOfDutiesTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Seeds a real User row with a known password - needed for any actor
    // that is expected to reach the signing step (SoD-violation cases
    // never get that far, so they don't need a seeded user).
    private static async Task<User> SeedUser(MicroLimsDbContext db, int id, RoleType roleType = RoleType.Reviewer)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // Seeds a TestOrder at ResultEntered with one Result entered by
    // analystId, and (optionally) AssignedAnalystId set too.
    private static async Task<TestOrder> SeedResultEnteredOrder(MicroLimsDbContext db, int analystId, int? assignedAnalystId = null)
    {
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = $"CTRL-{Guid.NewGuid():N}", Status = SampleStatus.Received };
        var order = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.ResultEntered,
            CurrentStep = WorkflowStep.Ready,
            AssignedAnalystId = assignedAnalystId
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.Results.Add(new Result { TestOrderId = order.Id, RawValue = "10", Type = ResultType.Numeric, EnteredByUserId = analystId });
        await db.SaveChangesAsync();

        return order;
    }

    [Fact]
    public async Task MarkReviewedAsync_ReviewerEnteredTheResult_Throws()
    {
        await using var db = NewDb();
        var order = await SeedResultEnteredOrder(db, analystId: 1);
        var review = new ReviewService(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => review.MarkReviewedAsync(order.Id, reviewerId: 1, "comment", Password, null));
        Assert.Contains("cannot review a test you performed", ex.Message);

        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(ApprovalStatus.ResultEntered, reloaded.Status);
        Assert.Empty(db.ElectronicSignatures);
    }

    [Fact]
    public async Task MarkReviewedAsync_ReviewerWasAssignedAnalyst_ThrowsEvenWithoutEnteringResult()
    {
        await using var db = NewDb();
        // Result entered by user 1, but user 2 is the AssignedAnalystId -
        // still counts as "performed" per the guard.
        var order = await SeedResultEnteredOrder(db, analystId: 1, assignedAnalystId: 2);
        var review = new ReviewService(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => review.MarkReviewedAsync(order.Id, reviewerId: 2, "comment", Password, null));
    }

    [Fact]
    public async Task DecideAsync_ApproverEnteredTheResult_Throws()
    {
        await using var db = NewDb();
        var order = await SeedResultEnteredOrder(db, analystId: 1);
        await SeedUser(db, 2);
        var sod = new SegregationOfDutiesGuard(db);
        var signatures = new ElectronicSignatureService(db);
        var review = new ReviewService(db, sod, signatures);
        var approval = new ApprovalService(db, sod, signatures);

        await review.MarkReviewedAsync(order.Id, reviewerId: 2, "comment", Password, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => approval.DecideAsync(order.Id, ApprovalDecision.Approve, null, decidedByUserId: 1, Password, null));
        Assert.Contains("cannot approve a test you performed", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_ApproverWasTheReviewer_Throws()
    {
        await using var db = NewDb();
        var order = await SeedResultEnteredOrder(db, analystId: 1);
        await SeedUser(db, 2);
        var sod = new SegregationOfDutiesGuard(db);
        var signatures = new ElectronicSignatureService(db);
        var review = new ReviewService(db, sod, signatures);
        var approval = new ApprovalService(db, sod, signatures);

        await review.MarkReviewedAsync(order.Id, reviewerId: 2, "comment", Password, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => approval.DecideAsync(order.Id, ApprovalDecision.Approve, null, decidedByUserId: 2, Password, null));
        Assert.Contains("cannot approve a test you reviewed", ex.Message);

        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(ApprovalStatus.Reviewed, reloaded.Status);
    }

    [Fact]
    public async Task DifferentPersonAtEachStage_Succeeds()
    {
        await using var db = NewDb();
        var order = await SeedResultEnteredOrder(db, analystId: 1);
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        var sod = new SegregationOfDutiesGuard(db);
        var signatures = new ElectronicSignatureService(db);
        var review = new ReviewService(db, sod, signatures);
        var approval = new ApprovalService(db, sod, signatures);

        await review.MarkReviewedAsync(order.Id, reviewerId: 2, "comment", Password, null);
        var result = await approval.DecideAsync(order.Id, ApprovalDecision.Approve, null, decidedByUserId: 3, Password, null);

        Assert.Equal("Approve", result.Decision);
        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(ApprovalStatus.Approved, reloaded.Status);
    }

    [Fact]
    public async Task SystemAdministrator_GetsNoBypass_SameRulesApplyRegardlessOfRole()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.SystemAdministrator);

        // The SystemAdministrator (user 1) both entered the result AND
        // tries to review it - the guard must not special-case this role.
        var order = await SeedResultEnteredOrder(db, analystId: 1);
        var review = new ReviewService(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => review.MarkReviewedAsync(order.Id, reviewerId: 1, "comment", Password, null));
    }

    [Fact]
    public async Task QuickReviewBatchAsync_SkipsIneligibleOrders_AndReportsWhy()
    {
        await using var db = NewDb();
        await SeedUser(db, 5);
        var eligible = await SeedResultEnteredOrder(db, analystId: 1);
        var ownWork = await SeedResultEnteredOrder(db, analystId: 5);
        var review = new ReviewService(db, new SegregationOfDutiesGuard(db), new ElectronicSignatureService(db));

        var result = await review.QuickReviewBatchAsync(new List<int> { eligible.Id, ownWork.Id, 9999 }, reviewerId: 5, Password, null);

        Assert.Single(result.Reviewed);
        Assert.Equal(eligible.Id, result.Reviewed[0]);
        Assert.Equal(2, result.Skipped.Count);
        Assert.Contains(result.Skipped, s => s.TestOrderId == ownWork.Id && s.Reason.Contains("cannot review a test you performed"));
        Assert.Contains(result.Skipped, s => s.TestOrderId == 9999 && s.Reason.Contains("not found"));
    }
}
