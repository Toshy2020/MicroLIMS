namespace MicroLIMS.Domain.Enums;

// Review/approval state of one laboratory section's tests on a sample.
// Persisted as int - append new members, never insert.
public enum SectionSignoffStatus
{
    InTesting,
    UnderReview,
    UnderApproval,
    Approved,
    Rejected,
    RetestRequested,

    // Closed without a decision of its own: another section rejected the
    // sample (Cancelled), or the whole sample was voided (Voided).
    Cancelled,
    Voided
}
