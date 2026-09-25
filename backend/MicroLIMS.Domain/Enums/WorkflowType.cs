namespace MicroLIMS.Domain.Enums;

public enum WorkflowType
{
    CountTest,   // plate readings + dilution factor (TAMC, TYMC)
    Observation, // staged pathogen detection chain, driven by each step's StepType
    HplcAssay,   // retired (SC-3): folded into StandardComparison; value kept, stored as int
    ElementalAssay, // Finished Product ICP-OES elemental assay workflow
    Measurement,
    Gravimetric,
    Qualitative,
    Dissolution,
    Disintegration,
    WeightVariation,
    HplcMultiAnalyte, // retired (SC-3): folded into StandardComparison; value kept, stored as int
    StandardComparison
}
