using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class OccurrenceStatusTests
{
    private static readonly DateOnly Start = new(2026, 10, 18);
    private static readonly DateOnly End = new(2026, 10, 22);
    private static readonly DateOnly Target = new(2026, 10, 21);

    private static OccurrenceStatusResult Run(DateOnly today, params ItemProgress[] items) =>
        Run(today, OccurrenceExceptionState.None, null, items);

    private static OccurrenceStatusResult Run(DateOnly today, OccurrenceExceptionState state, DateOnly? fulfilledOn, params ItemProgress[] items) =>
        OccurrenceStatusCalculator.Calculate(new OccurrenceStatusInput(Start, End, Target, state, 7, today, items, fulfilledOn));

    [Theory]
    [InlineData(2026, 10, 10, OccurrenceStatus.Upcoming)]
    [InlineData(2026, 10, 18, OccurrenceStatus.Open)]
    [InlineData(2026, 10, 21, OccurrenceStatus.DueToday)]
    [InlineData(2026, 10, 22, OccurrenceStatus.Open)]
    [InlineData(2026, 10, 23, OccurrenceStatus.Overdue)]
    [InlineData(2026, 10, 29, OccurrenceStatus.Overdue)]
    [InlineData(2026, 10, 30, OccurrenceStatus.Missed)]
    public void NothingLinked_FollowsTheWindowAndGrace(int y, int m, int d, OccurrenceStatus expected) =>
        Assert.Equal(expected, Run(new DateOnly(y, m, d), ItemProgress.None, ItemProgress.None).Status);

    [Fact]
    public void SomeItemsLinked_WithinTheWindow_IsPartlyPerformed()
    {
        var r = Run(Target, ItemProgress.InTesting, ItemProgress.None);
        Assert.Equal(OccurrenceStatus.PartlyPerformed, r.Status);
        Assert.Equal((2, 1, 0), (r.ItemsTotal, r.ItemsLinked, r.ItemsPerformed));
    }

    [Fact]
    public void SomeItemsLinked_AfterTheWindow_StaysOverdueThenMissed()
    {
        Assert.Equal(OccurrenceStatus.Overdue, Run(new DateOnly(2026, 10, 25), ItemProgress.Performed, ItemProgress.None).Status);
        Assert.Equal(OccurrenceStatus.Missed, Run(new DateOnly(2026, 11, 5), ItemProgress.Performed, ItemProgress.None).Status);
    }

    [Fact]
    public void AllLinked_ReportsTheSlowestItem()
    {
        Assert.Equal(OccurrenceStatus.InTesting, Run(Target, ItemProgress.InTesting, ItemProgress.Performed).Status);
        Assert.Equal(OccurrenceStatus.AwaitingReview, Run(Target, ItemProgress.AwaitingReview, ItemProgress.AwaitingApproval).Status);
        Assert.Equal(OccurrenceStatus.AwaitingApproval, Run(Target, ItemProgress.AwaitingApproval, ItemProgress.Performed).Status);
    }

    [Fact]
    public void AllPerformed_IsPerformed_OrPerformedExcursionWhenAnyExceeded()
    {
        Assert.Equal(OccurrenceStatus.Performed, Run(Target, ItemProgress.Performed, ItemProgress.Performed).Status);
        Assert.Equal(OccurrenceStatus.PerformedExcursion, Run(Target, ItemProgress.Performed, ItemProgress.PerformedExcursion).Status);
    }

    [Fact]
    public void ExceptionStates_WinOverEverythingElse()
    {
        Assert.Equal(OccurrenceStatus.Skipped, Run(Target, OccurrenceExceptionState.Skipped, null, ItemProgress.Performed).Status);
        Assert.Equal(OccurrenceStatus.Retired, Run(Target, OccurrenceExceptionState.Retired, null, ItemProgress.None).Status);
    }

    [Fact]
    public void FulfilmentAfterTheWindow_IsMarkedLate()
    {
        var late = Run(new DateOnly(2026, 10, 25), OccurrenceExceptionState.None, new DateOnly(2026, 10, 24), ItemProgress.Performed);
        var onTime = Run(new DateOnly(2026, 10, 25), OccurrenceExceptionState.None, End, ItemProgress.Performed);
        Assert.True(late.Late);
        Assert.False(onTime.Late);
    }

    [Theory]
    [InlineData(ApprovalStatus.Voided, WorkflowStep.Running, false, null, ItemProgress.None)]
    [InlineData(ApprovalStatus.Cancelled, WorkflowStep.Waiting, false, null, ItemProgress.None)]
    [InlineData(ApprovalStatus.Pending, WorkflowStep.Waiting, false, null, ItemProgress.InTesting)]
    [InlineData(ApprovalStatus.InProgress, WorkflowStep.Incubating, false, null, ItemProgress.InTesting)]
    [InlineData(ApprovalStatus.ResultEntered, WorkflowStep.Ready, false, null, ItemProgress.AwaitingReview)]
    [InlineData(ApprovalStatus.Reviewed, WorkflowStep.Reviewed, false, null, ItemProgress.AwaitingApproval)]
    [InlineData(ApprovalStatus.Approved, WorkflowStep.Approved, false, ResultStatus.WithinLimits, ItemProgress.Performed)]
    [InlineData(ApprovalStatus.Approved, WorkflowStep.Approved, false, ResultStatus.Absent, ItemProgress.Performed)]
    [InlineData(ApprovalStatus.Approved, WorkflowStep.Approved, false, ResultStatus.ActionLimitExceeded, ItemProgress.PerformedExcursion)]
    [InlineData(ApprovalStatus.Approved, WorkflowStep.Approved, false, ResultStatus.Detected, ItemProgress.PerformedExcursion)]
    [InlineData(ApprovalStatus.Rejected, WorkflowStep.Reviewed, false, null, ItemProgress.PerformedExcursion)]
    [InlineData(ApprovalStatus.RetestRequested, WorkflowStep.Reviewed, false, null, ItemProgress.PerformedExcursion)]
    [InlineData(ApprovalStatus.Pending, WorkflowStep.Waiting, true, null, ItemProgress.PerformedExcursion)]
    public void ProgressRules_FollowTheTestOrder(
        ApprovalStatus status, WorkflowStep step, bool superseded, ResultStatus? result, ItemProgress expected) =>
        Assert.Equal(expected, OccurrenceProgressRules.For(status, step, superseded, result));
}
