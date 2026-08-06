namespace MicroLIMS.Domain.Enums;

// Lifecycle events shared by every gated record. Samples use the full
// two-step set; Media and Cryovial batches use a single approval gate
// and so only ever emit SubmittedForApproval / ApprovalDecisionMade.
public enum ReviewWorkflowEventType
{
    SubmittedForReview,
    ReviewCompleted,
    SubmittedForApproval,
    ApprovalDecisionMade
}
