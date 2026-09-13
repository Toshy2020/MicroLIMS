using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkflowStateResolverTests
{
    [Fact]
    public void Resolve_WhenStatusIsReviewed_ResolvesToReviewedWithResultEntryNotAllowed()
    {
        var testOrder = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Reviewed,
            CurrentStep = WorkflowStep.Ready
        };

        var result = WorkflowStateResolver.Resolve(
            testOrder,
            requiresTsb: false,
            sharedTsb: null,
            testOrderIncubations: new List<Incubation>(),
            stepDtos: null,
            utcNow: DateTime.UtcNow);

        Assert.Equal("REVIEWED", result.WorkflowState);
        Assert.Equal("Reviewed — Pending Approval", result.WorkflowStateDisplay);
        Assert.Equal("Reviewed", result.WorkflowStatus);
        Assert.True(result.IsWorkflowLocked);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_WhenCurrentStepIsReviewed_ResolvesToReviewedWithResultEntryNotAllowed()
    {
        var testOrder = new TestOrder
        {
            TestCode = "SALM",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Reviewed
        };

        var result = WorkflowStateResolver.Resolve(
            testOrder,
            requiresTsb: true,
            sharedTsb: null,
            testOrderIncubations: new List<Incubation>(),
            stepDtos: null,
            utcNow: DateTime.UtcNow);

        Assert.Equal("REVIEWED", result.WorkflowState);
        Assert.Equal("Reviewed — Pending Approval", result.WorkflowStateDisplay);
        Assert.Equal("Reviewed", result.WorkflowStatus);
        Assert.True(result.IsWorkflowLocked);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_ApprovedAndRejectedTakePrecedenceOverReviewed()
    {
        var approvedOrder = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Approved,
            CurrentStep = WorkflowStep.Reviewed
        };
        var approvedResult = WorkflowStateResolver.Resolve(
            approvedOrder, false, null, new List<Incubation>(), null, DateTime.UtcNow);
        Assert.Equal("APPROVED", approvedResult.WorkflowState);

        var rejectedOrder = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Rejected,
            CurrentStep = WorkflowStep.Reviewed
        };
        var rejectedResult = WorkflowStateResolver.Resolve(
            rejectedOrder, false, null, new List<Incubation>(), null, DateTime.UtcNow);
        Assert.Equal("REJECTED", rejectedResult.WorkflowState);
    }
}
