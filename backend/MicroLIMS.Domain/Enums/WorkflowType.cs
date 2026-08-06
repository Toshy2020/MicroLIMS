namespace MicroLIMS.Domain.Enums;

// Which shape of result-entry a TestDefinition's workflow steps expect -
// read by TestWorkflowEngine to know how to interpret RecordResultAsync's
// payload, instead of the engine hardcoding test codes.
public enum WorkflowType
{
    CountTest,   // plate readings + dilution factor (TAMC, TYMC)
    Observation, // growth yes/no chain, one or more steps (most pathogens)
    DualPlate    // two media read together, both must agree (Salmonella XLD+TSI)
}
