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
    MasterDataChanged
}
