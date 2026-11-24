namespace MicroLIMS.Application.Services;

// The Segregation of Duties refusals of the review and approval decisions, in one place. The decision checks
// (SampleReviewService, SampleApprovalService) and My Work's lock reasons for the same entries all use these
// sentences, so one refusal is never worded two ways.
public static class SignoffRefusalSentences
{
    public const string CannotReviewTested = "You cannot review a sample you tested.";
    public const string CannotReviewOverrode = "You overrode competency for a test on this sample, so you cannot review it.";
    public const string CannotApproveTested = "You cannot approve a sample you tested.";
    public const string CannotApproveReviewed = "You cannot approve a sample you reviewed.";
    public const string CannotApproveOverrode = "You overrode competency for a test on this sample, so you cannot approve it.";
}
