using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class AssignedTestStepBuilderTests
{
    private static readonly IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)> NoLots =
        new Dictionary<int, (int, int?)>();

    private static TestWorkflowStep Step(int order, string name, StepType type, int minHours = 0, int maxHours = 0) => new()
    {
        StepOrder = order, StepName = name, StepType = type,
        StepMedia = minHours == 0 ? new() : new()
        {
            new TestWorkflowStepMedia { Id = order, MaterialId = 10, DisplayOrder = 1, IncubationMinHours = minHours, IncubationMaxHours = maxHours, TempMin = 30m, TempMax = 35m }
        }
    };

    private static List<SessionWorkflowStepDto> Build(
        TestWorkflowStep[] steps, Incubation[]? incubations = null, WorkflowStepResult[]? results = null,
        bool requiresTsb = false, bool ownTsbComplete = false) =>
        AssignedTestStepBuilder.Build(steps, incubations ?? Array.Empty<Incubation>(), results ?? Array.Empty<WorkflowStepResult>(),
            requiresTsb, ownTsbComplete, NoLots);

    [Fact]
    public void CompletedIncubation_MarksTheStepDone_WithItsOutcomeAndTime()
    {
        var done = new DateTime(2026, 10, 9, 8, 0, 0);
        var dto = Assert.Single(Build(
            new[] { Step(1, "Plate Count", StepType.PlateCount) },
            new[] { new Incubation { StepName = "Plate Count", CompletedAt = done, Outcome = "Growth" } }));

        Assert.True(dto.IsCompleted);
        Assert.Equal("Growth", dto.Outcome);
        Assert.Equal(done, dto.CompletedAt);
    }

    [Fact]
    public void SubmittedStepResult_MarksTheStepComplete_WithoutAnIncubation()
    {
        var submitted = new DateTime(2026, 10, 9, 9, 0, 0);
        var dto = Assert.Single(Build(
            new[] { Step(1, "Biochemical", StepType.BiochemicalTest) },
            results: new[] { new WorkflowStepResult { StepName = "Biochemical", SubmittedAtUtc = submitted } }));

        Assert.True(dto.IsCompleted);
        Assert.Equal("Complete", dto.Outcome);
        Assert.Equal(submitted, dto.CompletedAt);
        Assert.Null(dto.MediaTypeName);   // biochemical steps carry no media type name
    }

    [Fact]
    public void FirstStepOfATsbTest_IsDoneOnceItsOwnTsbMinimumHasElapsed()
    {
        var steps = new[] { Step(1, "TSB Enrichment", StepType.BrothEnrichment, 18, 24) };

        Assert.False(Assert.Single(Build(steps, requiresTsb: true, ownTsbComplete: false)).IsCompleted);
        Assert.True(Assert.Single(Build(steps, requiresTsb: true, ownTsbComplete: true)).IsCompleted);
    }

    [Fact]
    public void StepWithNoIncubation_ShowsItsConfiguredWindow_AndZerosWhenUnconfigured()
    {
        var dtos = Build(new[]
        {
            Step(1, "Plate Count", StepType.PlateCount, 48, 72),
            Step(2, "Selective", StepType.SelectivePlating)
        });

        Assert.Equal((48, 72, 30m, 35m), (dtos[0].IncubationMinHours, dtos[0].IncubationMaxHours, dtos[0].TemperatureMin, dtos[0].TemperatureMax));
        Assert.Equal((0, 0, 0m, 0m), (dtos[1].IncubationMinHours, dtos[1].IncubationMaxHours, dtos[1].TemperatureMin, dtos[1].TemperatureMax));
    }
}
