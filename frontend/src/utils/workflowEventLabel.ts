// Readable names for ReviewWorkflowEventType (backend/MicroLIMS.Domain/Enums/
// ReviewWorkflowEventType.cs), which the summary APIs send as the enum's
// C# name. Keyed by name, not position, so a newly appended event can't
// shift every label after it.
const EVENT_LABELS: Record<string, string> = {
  SubmittedForReview: "Submitted for Review",
  ReviewCompleted: "Review Completed",
  SubmittedForApproval: "Submitted for Approval",
  ApprovalDecisionMade: "Approval Decision",
  SampleVoided: "Sample Voided",
  SampleCorrected: "Sample Corrected",
  SectionTestingClosed: "Section Testing Closed",
  LaboratoryAdded: "Laboratory Added",
  StorageLocationChanged: "Storage Location Changed"
};

// "SomeNewEvent" -> "Some New Event", so an event added on the backend before
// this map is updated still reads as words rather than a code.
function splitPascalCase(value: string): string {
  return value.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

export function workflowEventLabel(eventType: string, decision?: string | null): string {
  if (eventType === "ApprovalDecisionMade" && decision) {
    if (decision === "Approve") return "Approved";
    if (decision === "Reject") return "Rejected";
  }
  const label = EVENT_LABELS[eventType] ?? splitPascalCase(eventType);
  return decision ? `${label} (${splitPascalCase(decision)})` : label;
}
