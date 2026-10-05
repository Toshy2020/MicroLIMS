namespace MicroLIMS.Domain.Enums;

public enum SignatureMeaning
{
    Reviewed,
    Approved,
    Rejected,
    RetestRequested,
    InvestigationOrdered,
    PreparationConfirmed,

    // Appended, never inserted: persisted as int.
    SampleCorrected,
    SampleVoided,
    MasterDataChanged,
    SuitabilityRunPerformed,
    ResultRecorded,
    CalibrationRunPerformed, // retired (slice R)
    CalibrationRunWithdrawn, // retired (slice R)
    TestingClosed,
    LaboratoryAdded,
    TitrantStandardized,
    TitrantDueAcknowledged
}

