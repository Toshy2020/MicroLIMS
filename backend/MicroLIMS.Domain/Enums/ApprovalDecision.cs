namespace MicroLIMS.Domain.Enums;

// Full decision set from the gap analysis section 9.
public enum ApprovalDecision
{
    Approve,
    Reject,
    RetestRetainedSample,
    NewSampleRequest,
    Investigation,
    OOSInvestigation
}
