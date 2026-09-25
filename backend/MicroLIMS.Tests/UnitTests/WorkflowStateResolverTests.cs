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
            approvedOrder, false, new List<Incubation>(), null, DateTime.UtcNow);
        Assert.Equal("APPROVED", approvedResult.WorkflowState);

        var rejectedOrder = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Rejected,
            CurrentStep = WorkflowStep.Reviewed
        };
        var rejectedResult = WorkflowStateResolver.Resolve(
            rejectedOrder, false, new List<Incubation>(), null, DateTime.UtcNow);
        Assert.Equal("REJECTED", rejectedResult.WorkflowState);
    }

    private static WorkflowStateResult ResolveFor(TestOrder order, SampleStatus? sampleStatus) =>
        WorkflowStateResolver.Resolve(order, false, new List<Incubation>(), null, DateTime.UtcNow, null, sampleStatus);

    // Sample #85's shape among them: a rejected sample whose tests sat at
    // CurrentStep Ready used to fall through to "Next: Ready".
    [Theory]
    [InlineData(SampleStatus.Rejected, ApprovalStatus.Rejected, "REJECTED", "Rejected")]
    [InlineData(SampleStatus.Rejected, ApprovalStatus.ResultEntered, "REJECTED", "Rejected")]
    [InlineData(SampleStatus.Voided, ApprovalStatus.InProgress, "VOIDED", "Voided")]
    [InlineData(SampleStatus.InTesting, ApprovalStatus.Voided, "VOIDED", "Voided")]
    [InlineData(SampleStatus.Cancelled, ApprovalStatus.Pending, "CANCELLED", "Cancelled")]
    [InlineData(SampleStatus.RetestRequested, ApprovalStatus.Reviewed, "ON_HOLD", "OnHold")]
    // A section closed its own testing (SectionClosureService) after another
    // lab rejected the sample - the order's own Status is Cancelled, never
    // Rejected, whatever the sample-level status reads as (Rejected here, or
    // still open below).
    [InlineData(SampleStatus.Rejected, ApprovalStatus.Cancelled, "CANCELLED", "Cancelled")]
    [InlineData(SampleStatus.UnderApproval, ApprovalStatus.Cancelled, "CANCELLED", "Cancelled")]
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

    [Fact]
    public void Resolve_WithMediaLookup_DoesNotConfuseMediaLotIdWithMaterialId()
    {
        var utcNow = DateTime.UtcNow;
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating };

        // medium1 belongs to product 5 and requires 48h
        var medium1 = new TestWorkflowStepMedia
        {
            MaterialId = 10,
            Material = new Material { Id = 10, MediaProductId = 5 },
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            TempMin = 30,
            TempMax = 35
        };

        // medium2 has MaterialId = 50, unrelated product, and requires 24h
        var medium2 = new TestWorkflowStepMedia
        {
            MaterialId = 50,
            Material = new Material { Id = 50, MediaProductId = 99 },
            IncubationMinHours = 24,
            IncubationMaxHours = 48,
            TempMin = 30,
            TempMax = 35
        };

        var step = new TestWorkflowStep
        {
            StepName = "Plating",
            IncubationMinHours = 24,
            StepMedia = new List<TestWorkflowStepMedia> { medium2, medium1 }
        };

        // Incubation used media lot #50, which was prepared from Material #10 (MediaProduct #5)
        var incubation = new Incubation
        {
            StepName = "Plating",
            MediaId = 50,
            IncubationStartUtc = utcNow.AddHours(-30)
        };

        var mediaLookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            [50] = (MaterialId: 10, MediaProductId: 5)
        };

        var result = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: false,
            testOrderIncubations: new List<Incubation> { incubation },
            stepDtos: null,
            utcNow: utcNow,
            steps: new List<TestWorkflowStep> { step },
            sampleStatus: SampleStatus.InTesting,
            mediaLookup: mediaLookup);

        // 30h elapsed < 48h required for product 5 -> COUNT_INCUBATING
        // If it confused MediaId 50 with MaterialId 50, 30h >= 24h would have produced AWAITING_RESULTS
        Assert.Equal("COUNT_INCUBATING", result.WorkflowState);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_WithMediaLookup_MatchesDifferentBatchOfSameProduct()
    {
        var utcNow = DateTime.UtcNow;
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating };

        // Step is configured with batch A (MaterialId 10) of MediaProduct 5
        var stepMedium = new TestWorkflowStepMedia
        {
            MaterialId = 10,
            Material = new Material { Id = 10, MediaProductId = 5 },
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            TempMin = 30,
            TempMax = 35
        };

        var step = new TestWorkflowStep
        {
            StepName = "Plating",
            IncubationMinHours = 24,
            StepMedia = new List<TestWorkflowStepMedia> { stepMedium }
        };

        // Test order used media lot 100 prepared from batch B (MaterialId 20) of MediaProduct 5
        var incubation = new Incubation
        {
            StepName = "Plating",
            MediaId = 100,
            IncubationStartUtc = utcNow.AddHours(-30)
        };

        var mediaLookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            [100] = (MaterialId: 20, MediaProductId: 5)
        };

        var resultAt30h = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: false,
            testOrderIncubations: new List<Incubation> { incubation },
            stepDtos: null,
            utcNow: utcNow,
            steps: new List<TestWorkflowStep> { step },
            sampleStatus: SampleStatus.InTesting,
            mediaLookup: mediaLookup);

        Assert.Equal("COUNT_INCUBATING", resultAt30h.WorkflowState);

        var resultAt50h = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: false,
            testOrderIncubations: new List<Incubation> { incubation },
            stepDtos: null,
            utcNow: incubation.IncubationStartUtc.Value.AddHours(50),
            steps: new List<TestWorkflowStep> { step },
            sampleStatus: SampleStatus.InTesting,
            mediaLookup: mediaLookup);

        Assert.Equal("AWAITING_RESULTS", resultAt50h.WorkflowState);
        Assert.True(resultAt50h.IsResultEntryAllowed);
    }

    [Fact]
    public void Resolve_WithoutMediaLookup_FallsBackToIncubationMediaNavigationProperty()
    {
        var utcNow = DateTime.UtcNow;
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating };

        var stepMedium = new TestWorkflowStepMedia
        {
            MaterialId = 10,
            Material = new Material { Id = 10, MediaProductId = 5 },
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            TempMin = 30,
            TempMax = 35
        };

        var step = new TestWorkflowStep
        {
            StepName = "Plating",
            IncubationMinHours = 24,
            StepMedia = new List<TestWorkflowStepMedia> { stepMedium }
        };

        // Navigation property is loaded, mediaLookup is null
        var incubation = new Incubation
        {
            StepName = "Plating",
            MediaId = 100,
            IncubationStartUtc = utcNow.AddHours(-30),
            Media = new Media
            {
                Id = 100,
                MaterialId = 20,
                Material = new Material { Id = 20, MediaProductId = 5 }
            }
        };

        var result = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: false,
            testOrderIncubations: new List<Incubation> { incubation },
            stepDtos: null,
            utcNow: utcNow,
            steps: new List<TestWorkflowStep> { step },
            sampleStatus: SampleStatus.InTesting,
            mediaLookup: null);

        Assert.Equal("COUNT_INCUBATING", result.WorkflowState);
        Assert.False(result.IsResultEntryAllowed);
    }
}

