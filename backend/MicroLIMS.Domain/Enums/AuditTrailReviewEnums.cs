namespace MicroLIMS.Domain.Enums;

// Persisted as int: append, never insert or renumber.
// Value 1 (Section) is unused and reserved because AuditLog carries no section column.
public enum AuditReviewScopeKind
{
    All = 0,
    EntityName = 2,
    Record = 3
}

public enum AuditTrailReviewStatus
{
    InProgress = 0,
    Signed = 1
}

public enum AuditTrailReviewOutcome
{
    NoFindings = 0,
    FindingsRecorded = 1
}

public enum AuditFindingStatus
{
    Open = 0,
    Closed = 1
}
