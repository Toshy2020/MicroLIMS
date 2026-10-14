import { SampleApprovalDecision } from "../services/SampleSummaryService";
import { TestOrderSummaryDetail } from "../types/sampleSummaryTypes";

export const formatSectionStatus = (status: string): string => {
  switch (status) {
    case "InTesting":
      return "In testing";
    case "UnderReview":
      return "Under review";
    case "UnderApproval":
      return "Under approval";
    case "Approved":
      return "Approved";
    case "Rejected":
      return "Rejected";
    case "RetestRequested":
      return "Retest requested";
    case "Cancelled":
      return "Cancelled";
    case "Closed":
      return "Closed";
    case "Voided":
      return "Voided";
    default:
      return status.replace(/([a-z])([A-Z])/g, "$1 $2");
  }
};

export const formatDate = (d: string | null | undefined) =>
  d
    ? new Date(d).toLocaleString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      })
    : "—";

export const formatExactTime = (d: string | null | undefined) =>
  d
    ? new Date(d).toLocaleString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit"
      })
    : "—";

// Advisory only - pre-checks the retest checklist toward whichever tests
// actually failed, mirroring overallStatus's own per-sample rollup below
// but scoped to a single TestOrder. The Section Head can still freely
// check/uncheck anything; the backend is the actual authority on which
// TestOrderIds are valid to retest (it just requires "at least one").
export function isTestOrderNonPassing(order: TestOrderSummaryDetail): boolean {
  if (order.locations.some((l) => l.status && l.status !== "WithinLimits" && l.status !== "Absent")) return true;
  if (order.countTestReadings.some((r) => r.status !== "WithinLimits")) return true;
  if (order.analysis && order.analysis.parameterResults.some((p) => p.comparisonStatus !== "WithinLimits")) return true;
  const biochemical = order.biochemicalResults;
  if (biochemical.some((b) => b.organismDetected === true)) return true;
  const pathogens = order.pathogenObservations;
  const anyConforming = pathogens.some((p) => p.observation === "GrowthConforming");
  const biochemicalRulesOutAll = biochemical.length > 0 && biochemical.every((b) => b.organismDetected === false);
  if (anyConforming && !biochemicalRulesOutAll) return true;
  return false;
}

export const DECISION_OPTIONS: { value: SampleApprovalDecision; label: string }[] = [
  { value: "Approve", label: "Approve: Results conform to specifications" },
  { value: "Reject", label: "Not Conform (Final Conclusion): Close this sample" },
  { value: "NewSampleRequest", label: "New Sample Required: Close and request new sample" },
  { value: "RetestRetainedSample", label: "Retest Retained Sample: Return to testing" }
];

// Only for placeholders shown before a signature record exists (a lab
// decided but the signature trail has not loaded) - a recorded signature
// renders its own server-worded meaningText. Must match the server's
// ReportVocabulary.SignatureMeaning word for word.
export const SIGNATURE_STATEMENTS: Record<string, string> = {
  Reviewed: "I have reviewed the test data and confirm it is complete and accurate.",
  Approved: "I approve the release of this record for its intended use.",
  Rejected: "I reject this record; it does not conform to specification.",
  RetestRequested: "I am ordering a retest of the retained sample.",
  InvestigationOrdered: "I am ordering an investigation into these results."
};
