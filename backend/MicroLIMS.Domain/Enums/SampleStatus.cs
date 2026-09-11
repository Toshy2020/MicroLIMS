namespace MicroLIMS.Domain.Enums;

public enum SampleStatus
{
    Received,
    InTesting,
    UnderReview,
    UnderApproval,
    Approved,
    Rejected,
    RetestRequested,

    // Appended, never inserted: this enum is persisted as int, so the numeric
    // value of every existing member has to stay where it is. Cancelled = 7,
    // Voided = 8.
    //
    // Cancelled - the sample was withdrawn before a result stood; nothing was
    // decided about the material. Voided - a recorded result was struck from
    // the record. Both are terminal and neither is a Rejection, which is a
    // judgement about the material itself.
    Cancelled,
    Voided
}
