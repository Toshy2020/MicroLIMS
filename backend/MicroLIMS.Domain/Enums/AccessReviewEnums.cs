namespace MicroLIMS.Domain.Enums;

// Persisted as int: append, never insert.
public enum AccessReviewStatus
{
    Draft = 0,
    Signed = 1
}

public enum AccessReviewFindingAction
{
    NoChange = 0,
    ChangeRequested = 1
}
