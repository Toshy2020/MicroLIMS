import type { TFunction } from "i18next";
import { i18n } from "../i18n";

// Readable names for ReviewWorkflowEventType (backend/MicroLIMS.Domain/Enums/
// ReviewWorkflowEventType.cs), which the summary APIs send as the enum's
// C# name. Keyed by name, not position, so a newly appended event can't
// shift every label after it.
const EVENT_LABEL_KEYS: Record<string, string> = {
  SubmittedForReview: "workflowEvents.SubmittedForReview",
  ReviewCompleted: "workflowEvents.ReviewCompleted",
  SubmittedForApproval: "workflowEvents.SubmittedForApproval",
  ApprovalDecisionMade: "workflowEvents.ApprovalDecisionMade",
  SampleVoided: "workflowEvents.SampleVoided",
  SampleCorrected: "workflowEvents.SampleCorrected",
  SectionTestingClosed: "workflowEvents.SectionTestingClosed",
  LaboratoryAdded: "workflowEvents.LaboratoryAdded",
  StorageLocationChanged: "workflowEvents.StorageLocationChanged"
};

// "SomeNewEvent" -> "Some New Event", so an event added on the backend before
// this map is updated still reads as words rather than a code.
function splitPascalCase(value: string): string {
  return value.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

export function workflowEventLabel(eventType: string, decision?: string | null, t?: TFunction): string {
  const translate = t ?? i18n.getFixedT("en", "common");
  if (eventType === "ApprovalDecisionMade" && decision) {
    if (decision === "Approve") return translate("workflowEvents.approved", { ns: "common" });
    if (decision === "Reject") return translate("workflowEvents.rejected", { ns: "common" });
  }
  const key = EVENT_LABEL_KEYS[eventType];
  const label = key ? translate(key, { ns: "common" }) : splitPascalCase(eventType);
  return decision
    ? translate("workflowEvents.withDecision", { ns: "common", label, decision: splitPascalCase(decision) })
    : label;
}
