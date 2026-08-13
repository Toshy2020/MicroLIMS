namespace MicroLIMS.Domain.Enums;

// Per-step behaviour discriminator. PlateCount belongs to CountTest
// workflows; the remaining five are the pathogen detection stages, in
// the order a template would normally arrange them.
public enum StepType
{
    PlateCount,          // plate readings + dilution factor (TAMC/TYMC)
    BrothEnrichment,     // no result logic - incubate and record the setup
    SelectiveBroth,      // no result logic - incubate and record the setup
    SelectivePlating,    // single GrowthObservation; non-conforming ends the workflow
    ConfirmatoryPlating, // analyst-selected media panel, one observation per medium
    BiochemicalTest      // free-text confirmation, optional attachment
}
