import { describe, expect, it } from "vitest";
import { workflowEventLabel } from "./workflowEventLabel";

describe("workflowEventLabel", () => {
  it("names known events in words", () => {
    expect(workflowEventLabel("ReviewCompleted")).toBe("Review Completed");
    expect(workflowEventLabel("StorageLocationChanged")).toBe("Storage Location Changed");
  });

  it("reads an approval decision as its outcome", () => {
    expect(workflowEventLabel("ApprovalDecisionMade", "Approve")).toBe("Approved");
    expect(workflowEventLabel("ApprovalDecisionMade", "Reject")).toBe("Rejected");
    expect(workflowEventLabel("ApprovalDecisionMade", "RetestRetainedSample")).toBe("Approval Decision (Retest Retained Sample)");
  });

  it("splits an event it does not know yet rather than showing the code", () => {
    expect(workflowEventLabel("SomeNewEvent", null)).toBe("Some New Event");
  });
});
