using System;
using System.Collections.Generic;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class IncubationWindowResolverTests
{
    [Fact]
    public void TryGet_WhenConfigured_ReturnsWindowWithMediumProperties()
    {
        var medium = new TestWorkflowStepMedia
        {
            Id = 42,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TempMin = 35m,
            TempMax = 37m
        };

        var window = IncubationWindowResolver.TryGet(medium);

        Assert.NotNull(window);
        Assert.Equal(42, window.StepMediaId);
        Assert.Equal(18, window.MinHours);
        Assert.Equal(24, window.MaxHours);
        Assert.Equal(35m, window.TempMin);
        Assert.Equal(37m, window.TempMax);
    }

    [Fact]
    public void TryGet_WhenUnconfiguredOrInvalid_ReturnsNull()
    {
        var zeroMinHours = new TestWorkflowStepMedia
        {
            IncubationMinHours = 0,
            IncubationMaxHours = 24,
            TempMin = 35m,
            TempMax = 37m
        };
        Assert.Null(IncubationWindowResolver.TryGet(zeroMinHours));

        var maxLessThanMin = new TestWorkflowStepMedia
        {
            IncubationMinHours = 24,
            IncubationMaxHours = 18,
            TempMin = 35m,
            TempMax = 37m
        };
        Assert.Null(IncubationWindowResolver.TryGet(maxLessThanMin));

        var zeroTempMax = new TestWorkflowStepMedia
        {
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            TempMin = 0m,
            TempMax = 0m
        };
        Assert.Null(IncubationWindowResolver.TryGet(zeroTempMax));
    }

    [Fact]
    public void IncubationWindow_TimingAndFormatting_CalculatesExpectedValues()
    {
        var window = new IncubationWindow(1, 18, 24, 35m, 37m);
        var start = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(start.AddHours(18), window.MinReadyAt(start));
        Assert.Equal(start.AddHours(24), window.EndAt(start));
        Assert.Equal("18-24 hours", window.DurationText);
        Assert.Equal("35-37 °C", window.TemperatureText);
    }

    [Fact]
    public void Require_UnconfiguredMedium_ThrowsWorkflowStepException()
    {
        var medium = new TestWorkflowStepMedia
        {
            Material = new Material { MaterialName = "TSB" },
            IncubationMinHours = 0
        };

        var ex = Assert.Throws<WorkflowStepException>(() =>
            IncubationWindowResolver.Require(medium, "Broth Enrichment"));

        Assert.Equal(WorkflowErrorCodes.IncubationWindowNotConfigured, ex.ErrorCode);
        Assert.Contains("Broth Enrichment", ex.Message);
        Assert.Contains("TSB", ex.Message);
    }

    [Fact]
    public void MatchStepMedium_ExactMaterialWinsOverSameProductFirst_FallsBackToProduct_OrReturnsNull()
    {
        var sameProductFirst = new TestWorkflowStepMedia
        {
            Id = 1,
            MaterialId = 10,
            IncubationCondition = new MediaIncubationCondition { MediaProductId = 5 }
        };
        var exactMaterial = new TestWorkflowStepMedia
        {
            Id = 2,
            MaterialId = 20,
            IncubationCondition = new MediaIncubationCondition { MediaProductId = 5 }
        };
        var stepMedia = new List<TestWorkflowStepMedia> { sameProductFirst, exactMaterial };

        // Exact MaterialId wins over a same-product medium listed first
        var exactMatch = IncubationWindowResolver.MatchStepMedium(stepMedia, lotMaterialId: 20, lotProductId: 5);
        Assert.Same(exactMaterial, exactMatch);

        // Product match through MediaConfiguration when the lot is another batch of product 5
        var productMatch = IncubationWindowResolver.MatchStepMedium(new[] { sameProductFirst }, lotMaterialId: 99, lotProductId: 5);
        Assert.Same(sameProductFirst, productMatch);

        // Null when nothing matches
        var noMatch = IncubationWindowResolver.MatchStepMedium(stepMedia, lotMaterialId: 999, lotProductId: 999);
        Assert.Null(noMatch);
    }

    [Fact]
    public void MediumForIncubation_ResolvesFromLookup_FallsBackToMedia_ReturnsNullWhenNoMediaIdOrNoMatch()
    {
        var medium1 = new TestWorkflowStepMedia
        {
            Id = 1,
            MaterialId = 10,
            IncubationCondition = new MediaIncubationCondition { MediaProductId = 5 }
        };
        var medium2 = new TestWorkflowStepMedia
        {
            Id = 2,
            MaterialId = 20,
            IncubationCondition = new MediaIncubationCondition { MediaProductId = 6 }
        };
        var step = new TestWorkflowStep
        {
            StepMedia = new List<TestWorkflowStepMedia> { medium1, medium2 }
        };

        // 1. Uses the mediaLookup entry (lot id -> (MaterialId, MediaProductId))
        var lookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            { 100, (20, 6) }
        };
        var incWithLookup = new Incubation { MediaId = 100 };
        var resultFromLookup = IncubationWindowResolver.MediumForIncubation(step, incWithLookup, lookup);
        Assert.Same(medium2, resultFromLookup);

        // 2. Falls back to Incubation.Media (with Material.MediaProductId) when there is no lookup
        var incWithMedia = new Incubation
        {
            MediaId = 200,
            Media = new()
            {
                MaterialId = 10,
                Material = new Material { MediaProductId = 5 }
            }
        };
        var resultFromMedia = IncubationWindowResolver.MediumForIncubation(step, incWithMedia);
        Assert.Same(medium1, resultFromMedia);

        // 3. Returns null when Incubation.MediaId is null
        var incWithoutMediaId = new Incubation { MediaId = null };
        var resultNullMediaId = IncubationWindowResolver.MediumForIncubation(step, incWithoutMediaId, lookup);
        Assert.Null(resultNullMediaId);

        // 4. Returns null (not the first step medium) when the lot matches no medium
        var incNoMatch = new Incubation
        {
            MediaId = 300,
            Media = new()
            {
                MaterialId = 999,
                Material = new Material { MediaProductId = 999 }
            }
        };
        var resultNoMatch = IncubationWindowResolver.MediumForIncubation(step, incNoMatch);
        Assert.Null(resultNoMatch);
    }

    [Fact]
    public void WorkflowTemplateValidator_Validate_SelectivePlating_EnforcesRule9()
    {
        var medium = new TestWorkflowStepMedia
        {
            Id = 1,
            MaterialId = 10,
            IsRequired = true,
            TempMin = 35m,
            TempMax = 37m,
            IncubationMinHours = 0,
            IncubationMaxHours = 24
        };

        var step = new TestWorkflowStep
        {
            StepType = StepType.SelectivePlating,
            StepName = "Selective Plating",
            TargetOrganismId = 1,
            StepMedia = new List<TestWorkflowStepMedia> { medium }
        };

        var errorsWhenZero = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errorsWhenZero, e => e.RuleNumber == 9);

        medium.IncubationMinHours = 18;
        medium.IncubationMaxHours = 24;

        var errorsWhenConfigured = WorkflowTemplateValidator.Validate(step);
        Assert.DoesNotContain(errorsWhenConfigured, e => e.RuleNumber == 9);
    }
}
