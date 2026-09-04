namespace MicroLIMS.Domain.Enums;

public enum DocumentRevisionStatus
{
    Draft = 1,
    Cancelled = 2,
    InReview = 3,
    AwaitingApproval = 4,
    FutureEffective = 5,
    Effective = 6,
    Superseded = 7,
    Obsolete = 8
}
