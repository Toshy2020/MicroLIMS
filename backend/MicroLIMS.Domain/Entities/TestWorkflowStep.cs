namespace MicroLIMS.Domain.Entities;

// One step of a TestDefinition's configurable workflow template - the
// data-driven replacement for what used to be hardcoded step chains in
// PathogenWorkflowEngine ("TSB" -> "RVS" -> "XLD_TSI") and
// CountTestWorkflowEngine (a single unnamed incubation step).
// TestWorkflowEngine reads these in StepOrder to know what media class
// each step requires and what Temperature/Duration to hard-lock -
// nothing about a specific test code or step name is ever compared in
// the engine itself.
public class TestWorkflowStep
{
    public int Id { get; set; }
    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public int StepOrder { get; set; } // 1-based, determines sequence
    public string StepName { get; set; } = string.Empty; // e.g. "TSB", "RVS", "XLD_TSI", "CountIncubation", "Detection"

    public int MediaTypeId { get; set; } // which MediaType class this step requires - analyst picks a released lot of this class
    public MediaType? MediaType { get; set; }

    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
    public decimal TemperatureMin { get; set; }
    public decimal TemperatureMax { get; set; }

    public bool IsFinalStep { get; set; } // true on the last step only - its result determines Detected/Absent or pass/fail
    public bool IsDualPlate { get; set; } // true only for a two-plate step (e.g. XLD+TSI) - expects two media selections/observations that must agree
}
