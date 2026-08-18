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

    // ConfirmatoryPlating only: number of confirmatory media required (e.g. 2 for Salmonella [XLD+TSI], 1 for single-medium pathogens).
    public int ConfirmatoryMediaCount { get; set; } = 1;

    public List<TestWorkflowStepMedia> StepMedia { get; set; } = new();

    // PlateCount only. When true, the step's own TemperatureMin/Max and
    // IncubationMinHours/MaxHours above describe stage 1; stage 2's window
    // lives in IncubationStages (StageNumber == 2). See
    // WorkflowTemplateValidator rule 7 for the "can't half-configure this"
    // guard.
    public bool RequiresIncubationTransfer { get; set; }

    // Stage 2+ incubation windows for a transfer-enabled PlateCount step.
    // Stage 1's window is NOT duplicated here - it stays on this row's own
    // TemperatureMin/Max/IncubationMinHours/MaxHours, since dozens of
    // existing call sites already read those directly. Keyed by
    // StageNumber so a stage 3 can be added later with no schema change.
    public List<TestWorkflowStepIncubationStage> IncubationStages { get; set; } = new();

    public bool RequiresTargetOrganism =>
        StepType is StepType.SelectivePlating or StepType.ConfirmatoryPlating;

    // BiochemicalTest is bench work with no incubation window, and
    // SelectivePlating is read off plates the previous step incubated.
    public bool RequiresIncubationLock =>
        StepType is StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.ConfirmatoryPlating;
}
