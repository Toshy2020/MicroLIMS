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

    private static WorkflowStateResult ResolveFor(TestOrder order, SampleStatus? sampleStatus) =>
        WorkflowStateResolver.Resolve(order, false, null, new List<Incubation>(), null, DateTime.UtcNow, 24, null, sampleStatus);

    // Sample #85's shape among them: a rejected sample whose tests sat at
    // CurrentStep Ready used to fall through to "Next: Ready".
    [Theory]
    [InlineData(SampleStatus.Rejected, ApprovalStatus.Rejected, "REJECTED", "Rejected")]
    [InlineData(SampleStatus.Rejected, ApprovalStatus.ResultEntered, "REJECTED", "Rejected")]
    [InlineData(SampleStatus.Voided, ApprovalStatus.InProgress, "VOIDED", "Voided")]
    [InlineData(SampleStatus.InTesting, ApprovalStatus.Voided, "VOIDED", "Voided")]
    [InlineData(SampleStatus.Cancelled, ApprovalStatus.Pending, "CANCELLED", "Cancelled")]
    [InlineData(SampleStatus.RetestRequested, ApprovalStatus.Reviewed, "ON_HOLD", "OnHold")]
    public void Resolve_ClosedSampleOrTest_IsLockedWithNoResultEntry(
        SampleStatus sampleStatus, ApprovalStatus testStatus, string expectedState, string expectedStatus)
    {
        var order = new TestOrder { TestCode = "TAMC", Status = testStatus, CurrentStep = WorkflowStep.Ready };

        var result = ResolveFor(order, sampleStatus);

        Assert.Equal(expectedState, result.WorkflowState);
        Assert.Equal(expectedStatus, result.WorkflowStatus);
        Assert.True(result.IsWorkflowLocked);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_SupersededTest_ReadsSuperseded_WhateverItsSampleStatus()
    {
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Rejected, CurrentStep = WorkflowStep.Reviewed, IsSuperseded = true };

        var result = ResolveFor(order, SampleStatus.Approved);

        Assert.Equal("SUPERSEDED", result.WorkflowState);
        Assert.True(result.IsWorkflowLocked);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_ApprovedSampleWithStaleTestStatus_ReadsApproved()
    {
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready };

        var result = ResolveFor(order, SampleStatus.Approved);

        Assert.Equal("APPROVED", result.WorkflowState);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_LiveTest_IsUnchangedByAnOpenSampleStatus()
    {
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready };

        Assert.Equal("RESULTS_RECORDED", ResolveFor(order, SampleStatus.InTesting).WorkflowState);
        Assert.Equal("RESULTS_RECORDED", ResolveFor(order, null).WorkflowState);
    }
}
