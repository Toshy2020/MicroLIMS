using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class TestWorkflowStep
{
    public int Id { get; set; }
    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;

    public int MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }

    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
    public decimal TemperatureMin { get; set; }
    public decimal TemperatureMax { get; set; }

    public bool IsFinalStep { get; set; }

    public StepType StepType { get; set; }

    // Required for SelectivePlating and ConfirmatoryPlating; null otherwise.
    public int? TargetOrganismId { get; set; }
    public Organism? TargetOrganism { get; set; }

    public List<TestWorkflowStepMedia> StepMedia { get; set; } = new();

    public bool RequiresTargetOrganism =>
        StepType is StepType.SelectivePlating or StepType.ConfirmatoryPlating;

    // BiochemicalTest is bench work with no incubation window, and
    // SelectivePlating is read off plates the previous step incubated.
    public bool RequiresIncubationLock =>
        StepType is StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.ConfirmatoryPlating;
}
