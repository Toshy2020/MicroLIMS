namespace MicroLIMS.Domain.Enums;

public enum WorkflowType
{
    CountTest,   // plate readings + dilution factor (TAMC, TYMC)
    Observation, // staged pathogen detection chain, driven by each step's StepType
    HplcAssay,   // retired (SC-3): replaced by HplcMethodAssay; value kept, stored as int
    ElementalAssay, // retired (slice R): replaced by IcpMethodAssay; value kept, stored as int
    Measurement,
    Gravimetric,
    Qualitative,
    Dissolution,
    Disintegration,
    WeightVariation,
    HplcMultiAnalyte, // retired (SC-3): replaced by HplcMethodAssay; value kept, stored as int
    StandardComparison, // retired: replaced by HplcMethodAssay; value kept, stored as int
    HplcMethodAssay, // HPLC chain S3 - assay against an HplcMethod master (spec 3.3-3.4)
    Titration, // volumetric titration assay
    IcpMethodAssay // ICP workspace - assay against an IcpMethod master (spec 2026-10-03 §4.1)
}
