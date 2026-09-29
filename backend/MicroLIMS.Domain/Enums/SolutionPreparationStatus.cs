namespace MicroLIMS.Domain.Enums;

// HPLC chain S4 - Solution Preparation area (spec 4).
// InProgress -> Prepared | Cancelled; Prepared -> Expired (automatic) | Discarded.
// Expired, Discarded and Cancelled are final.
public enum SolutionPreparationStatus
{
    InProgress,
    Prepared,
    Expired,
    Discarded,
    Cancelled
}
