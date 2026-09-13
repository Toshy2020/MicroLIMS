namespace MicroLIMS.Domain.Enums;

public enum ApprovalStatus
{
    Pending,
    InProgress,
    ResultEntered,
    Reviewed,
    Approved,
    Rejected,
    RetestRequested,

    // Appended, never inserted: persisted as int. The test's sample was
    // voided - struck from the record, not a judgement about the material,
    // so never Rejected. Closed: excluded from every active work queue.
    Voided
}
