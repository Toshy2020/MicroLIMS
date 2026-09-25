namespace MicroLIMS.Domain.Enums;

// Lifecycle events shared by every gated record. Samples use the full
// two-step set; Media and Cryovial batches use a single approval gate
// and so only ever emit SubmittedForApproval / ApprovalDecisionMade.
public enum ReviewWorkflowEventType
{
    SubmittedForReview,
    ReviewCompleted,
    SubmittedForApproval,
    ApprovalDecisionMade,

    // Appended, never inserted: persisted as int. A sample struck from the
    // record - not an approval decision. The reason is the event's Comment.
    SampleVoided,

    // A signed correction of a received sample's details; the Comment holds
    // the reason and the fields changed.
    SampleCorrected,

    // A laboratory section closed its own testing after another section
    // rejected the sample. The Comment holds the reason.
    SectionTestingClosed,

    // A second laboratory's tests were added to an already-received
    // sample. SectionId holds the laboratory added; the Comment holds
    // the reason.
    LaboratoryAdded
}
