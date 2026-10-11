using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ActionableGroupBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static CandidateActionItem Item(int orderId, string type = "SETUP_INCUBATION", int[]? materials = null,
        decimal tMin = 30, decimal tMax = 35, DateTime? receivedAt = null)
    {
        var step = new TestWorkflowStep { StepName = "TSB" };
        var sample = new Sample { Id = orderId, ReferenceNumber = $"S-{orderId}", ReceivedAt = receivedAt ?? Now };
        return new CandidateActionItem(new TestOrder { Id = orderId, TestCode = "TAMC" }, sample, step, step, type, type, "TSB", "BrothEnrichment", "TSB",
            null, "TAMC", $"Item {orderId}", (materials ?? Array.Empty<int>()).ToList(), "TSB lot", tMin, tMax, 18, 24, null);
    }

    [Fact]
    public void CompatibleCandidates_ShareOneGroup_WithIntersectedMaterialsAndTightestTemperature()
    {
        var r = ActionableGroupBuilder.Build(new() { Item(1, materials: new[] { 1, 2 }, tMin: 30, tMax: 35), Item(2, materials: new[] { 2, 3 }, tMin: 32, tMax: 37) }, null, Now, new());

        var g = Assert.Single(r.Groups);
        Assert.Equal(2, g.TestOrderCount);
        Assert.Equal(new[] { 2 }, g.PermittedMaterialIds);
        Assert.Equal((32m, 35m), (g.TempMin, g.TempMax));
    }

    [Fact]
    public void IncompatibleTemperatureWindows_SplitIntoSeparateGroups()
    {
        var r = ActionableGroupBuilder.Build(new() { Item(1, tMin: 20, tMax: 25), Item(2, tMin: 30, tMax: 35) }, null, Now, new());

        Assert.Equal(2, r.Groups.Count);
    }

    [Fact]
    public void ActionTypeFilter_DropsOtherTransitions()
    {
        var r = ActionableGroupBuilder.Build(new() { Item(1), Item(2, type: "TRANSFER_SELECTIVE") }, "transfer_selective", Now, new());

        Assert.Equal("TRANSFER_SELECTIVE", Assert.Single(r.Groups).ActionType);
    }

    [Fact]
    public void Urgency_IsOverdue_WhenAnySampleIsOlderThan24Hours()
    {
        var r = ActionableGroupBuilder.Build(new() { Item(1), Item(2, receivedAt: Now.AddHours(-25)) }, null, Now, new());

        Assert.Equal("Overdue", Assert.Single(r.Groups).Urgency);
    }

    [Fact]
    public void PermittedMedia_AddsEverySiblingBatchOfTheSameProduct()
    {
        var medium = new TestWorkflowStepMedia { MaterialId = 5, Material = new Material { MaterialName = "TSB", MediaProductId = 9 } };

        var (ids, names) = ActionableGroupBuilder.PermittedMedia(new[] { medium }, new() { [9] = new List<int> { 5, 6 } });

        Assert.Equal(new[] { 5, 6 }, ids);
        Assert.Equal("TSB", names);
    }
}

public class BatchSelectMediaRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Colony Count")]
    [InlineData("Plate Reading")]
    [InlineData("Result Entry")]
    public void ResultEntrySteps_AreRefused(string step) =>
        Assert.Throws<InvalidOperationException>(() => BatchSelectMediaRules.EnsureNotResultEntry(step));

    [Theory]
    [InlineData("TAMC Incubation")]
    [InlineData("TSB Enrichment")]
    public void IncubationSteps_AreAllowed(string step) => BatchSelectMediaRules.EnsureNotResultEntry(step);

    [Fact]
    public void ExpiredMediaLot_IsRefused()
    {
        var media = new Media { LotNumber = "L1", IsReleasedForUse = true, ExpiryDate = Now.AddDays(-1) };

        var ex = Assert.Throws<InvalidOperationException>(() => BatchSelectMediaRules.EnsureMediaLotUsable(media, Now));
        Assert.Contains("expired", ex.Message);
    }

    [Fact]
    public void IncubatorWithoutSetPoint_IsRefused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => BatchSelectMediaRules.EnsureIncubatorUsable(new Equipment { Code = "INC-1" }, 1, Now));
        Assert.Contains("set point", ex.Message);
    }

    [Fact]
    public void ReadyPlateCountStage2_MustBeEnteredIndividually()
    {
        var step = new TestWorkflowStep { StepType = StepType.PlateCount, RequiresIncubationTransfer = true };

        Assert.NotNull(BatchSelectMediaRules.ResultEntrySkipReason(step, new Incubation { StageNumber = 2 }, isReady: true));
        Assert.Null(BatchSelectMediaRules.ResultEntrySkipReason(step, new Incubation { StageNumber = 1 }, isReady: true));
        Assert.Null(BatchSelectMediaRules.ResultEntrySkipReason(step, new Incubation { StageNumber = 2 }, isReady: false));
    }
}
