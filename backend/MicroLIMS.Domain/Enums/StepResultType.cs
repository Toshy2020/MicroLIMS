namespace MicroLIMS.Domain.Enums;

// Per-step result shape a TestWorkflowStep expects - finer-grained than
// WorkflowType (which is per-TestDefinition): a DualPlate-typed test has
// plain Growth steps (TSB, RVS) followed by one DualGrowth final step.
// TestWorkflowEngine reads this to know which ResultPayload record a
// step accepts and how to interpret it, instead of hardcoding step names.
public enum StepResultType
{
    PlateCount, // plate readings + dilution factor (TAMC/TYMC's CountIncubation step)
    Growth,     // single growth yes/no (TSB, RVS, non-final pathogen steps)
    DualGrowth  // two plates read together, both must agree (Salmonella's XLD+TSI step)
}
