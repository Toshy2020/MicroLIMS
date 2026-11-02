namespace MicroLIMS.Domain.Enums;

// Where a reference strain comes from (stored as int, append only).
public enum CultureCollection
{
    ATCC,
    NCTC,
    NCIMB,
    WDCM,
    DSMZ,
    // In-house or any other source; the only collection that may have no strain number.
    Other
}
