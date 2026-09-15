using System;
using System.Collections.Generic;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkflowStateResolverWindowTests
{
    [Fact]
    public void TsbLotMatchesNoStepMedium_IsWindowNotConfigured()
    {
        var utcNow = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = utcNow.AddHours(-30);

        var order = new TestOrder
        {
            Id = 1,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating
        };

        var step = new TestWorkflowStep
        {
            StepName = "Broth Enrichment",
            StepOrder = 1,
            StepType = StepType.BrothEnrichment,
            StepMedia = new List<TestWorkflowStepMedia>
            {
                new()
                {
                    Id = 1,
                    MaterialId = 10,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TempMin = 30,
                    TempMax = 35
                }
            }
        };

        var incubation = new Incubation
        {
            TestOrderId = 1,
            StepName = "Broth Enrichment",
            StageNumber = 1,
            MediaId = 500,
            StartedAt = startedAt,
            IncubationStartUtc = startedAt
        };

        var mediaLookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            [500] = (99, null)
        };

        var result = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: true,
            new[] { incubation },
            stepDtos: null,
            utcNow: utcNow,
            steps: new[] { step },
            sampleStatus: null,
            mediaLookup: mediaLookup);

        Assert.Equal("WINDOW_NOT_CONFIGURED", result.WorkflowState);
        Assert.True(result.IsWorkflowLocked);
        Assert.False(result.IsResultEntryAllowed);
    }

    [Fact]
    public void CountStage1MediumWithZeroHours_IsWindowNotConfigured()
    {
        var utcNow = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = utcNow.AddHours(-1);

        var order = new TestOrder
        {
            Id = 1,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating
        };

        var step = new TestWorkflowStep
        {
            StepName = "CountIncubation",
            StepOrder = 1,
            StepType = StepType.PlateCount,
            StepMedia = new List<TestWorkflowStepMedia>
            {
                new()
                {
                    Id = 1,
                    MaterialId = 10,
                    IncubationMinHours = 0,
                    IncubationMaxHours = 24,
                    TempMin = 30,
                    TempMax = 35
                }
            }
        };

        var incubation = new Incubation
        {
            TestOrderId = 1,
            StepName = "CountIncubation",
            StageNumber = 1,
            MediaId = 500,
            StartedAt = startedAt,
            IncubationStartUtc = startedAt
        };

        var mediaLookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            [500] = (10, null)
        };

        var result = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: false,
            new[] { incubation },
            stepDtos: null,
            utcNow: utcNow,
            steps: new[] { step },
            sampleStatus: null,
            mediaLookup: mediaLookup);

        Assert.Equal("WINDOW_NOT_CONFIGURED", result.WorkflowState);
    }

    [Fact]
    public void TsbTiming_IsPerTest()
    {
        var utcNow = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = utcNow.AddHours(-30);

        var orderA = new TestOrder
        {
            Id = 1,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating
        };

        var stepA = new TestWorkflowStep
        {
            StepName = "Broth Enrichment",
            StepOrder = 1,
            StepType = StepType.BrothEnrichment,
            StepMedia = new List<TestWorkflowStepMedia>
            {
                new()
                {
                    Id = 1,
                    MaterialId = 10,
                    IncubationMinHours = 18,
                    IncubationMaxHours = 24,
                    TempMin = 30,
                    TempMax = 35
                }
            }
        };

        var incA = new Incubation
        {
            TestOrderId = 1,
            StepName = "Broth Enrichment",
            StageNumber = 1,
            MediaId = 500,
            StartedAt = startedAt,
            IncubationStartUtc = startedAt
        };

        var orderB = new TestOrder
        {
            Id = 2,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating
        };

        var stepB = new TestWorkflowStep
        {
            StepName = "Broth Enrichment",
            StepOrder = 1,
            StepType = StepType.BrothEnrichment,
            StepMedia = new List<TestWorkflowStepMedia>
            {
                new()
                {
                    Id = 2,
                    MaterialId = 20,
                    IncubationMinHours = 48,
                    IncubationMaxHours = 72,
                    TempMin = 30,
                    TempMax = 35
                }
            }
        };

        var incB = new Incubation
        {
            TestOrderId = 2,
            StepName = "Broth Enrichment",
            StageNumber = 1,
            MediaId = 600,
            StartedAt = startedAt,
            IncubationStartUtc = startedAt
        };

        var mediaLookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            [500] = (10, null),
            [600] = (20, null)
        };

        var resultA = WorkflowStateResolver.Resolve(
            orderA,
            requiresTsb: true,
            new[] { incA },
            stepDtos: null,
            utcNow: utcNow,
            steps: new[] { stepA },
            sampleStatus: null,
            mediaLookup: mediaLookup);

        var resultB = WorkflowStateResolver.Resolve(
            orderB,
            requiresTsb: true,
            new[] { incB },
            stepDtos: null,
            utcNow: utcNow,
            steps: new[] { stepB },
            sampleStatus: null,
            mediaLookup: mediaLookup);

        Assert.NotEqual("TSB_INCUBATING", resultA.WorkflowState);
        Assert.Equal("TSB_INCUBATING", resultB.WorkflowState);
    }

    [Fact]
    public void Stage2_StillUsesStageConfiguration()
    {
        var utcNow = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = utcNow.AddHours(-30);

        var order = new TestOrder
        {
            Id = 1,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating
        };

        var step = new TestWorkflowStep
        {
            StepName = "CountIncubation",
            StepOrder = 1,
            StepType = StepType.PlateCount,
            RequiresIncubationTransfer = true,
            IncubationStages = new List<TestWorkflowStepIncubationStage>
            {
                new()
                {
                    StageNumber = 2,
                    IncubationMinHours = 48,
                    IncubationMaxHours = 72,
                    TempMin = 20,
                    TempMax = 25
                }
            },
            StepMedia = new List<TestWorkflowStepMedia>
            {
                new()
                {
                    Id = 1,
                    MaterialId = 10,
                    IncubationMinHours = 1,
                    IncubationMaxHours = 2,
                    TempMin = 30,
                    TempMax = 35
                }
            }
        };

        var incubation = new Incubation
        {
            TestOrderId = 1,
            StepName = "CountIncubation",
            StageNumber = 2,
            MediaId = 500,
            StartedAt = startedAt,
            IncubationStartUtc = startedAt
        };

        var mediaLookup = new Dictionary<int, (int MaterialId, int? MediaProductId)>
        {
            [500] = (10, null)
        };

        var result = WorkflowStateResolver.Resolve(
            order,
            requiresTsb: false,
            new[] { incubation },
            stepDtos: null,
            utcNow: utcNow,
            steps: new[] { step },
            sampleStatus: null,
            mediaLookup: mediaLookup);

        Assert.Equal("COUNT_INCUBATING", result.WorkflowState);
    }
}
