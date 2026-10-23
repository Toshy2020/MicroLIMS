namespace MicroLIMS.Domain.Enums;

// Stored as int; member names are frozen by InstrumentIngestModelTests.
public enum InstrumentIngestStatus
{
    Quarantined = 1,
    PendingReview = 2,
    Adopted = 3,
    Superseded = 4,
    Dismissed = 5
}
