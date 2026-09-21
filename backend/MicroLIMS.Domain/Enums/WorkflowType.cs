namespace MicroLIMS.Domain.Enums;

public enum WorkflowType
{
    CountTest,   // plate readings + dilution factor (TAMC, TYMC)
    Observation, // staged pathogen detection chain, driven by each step's StepType
    HplcAssay,   // Finished Product HPLC assay workflow
    ElementalAssay, // Finished Product ICP-OES elemental assay workflow
    Measurement,
    Gravimetric,
    Qualitative,
    Dissolution,
    Disintegration
}
