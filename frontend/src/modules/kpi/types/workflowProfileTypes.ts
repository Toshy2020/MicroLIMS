// Wire types for the workflow profile API (KPI remodel S1). Enums travel as strings,
// dates as "yyyy-MM-dd". Mirrors WorkflowProfileDtos.cs.

export type LiveWorkflowType =
  | "CountTest"
  | "Observation"
  | "Measurement"
  | "Gravimetric"
  | "Qualitative"
  | "Dissolution"
  | "Disintegration"
  | "WeightVariation"
  | "HplcMethodAssay"
  | "Titration"
  | "IcpMethodAssay";

export type WorkflowProfileStatus = "Draft" | "Effective";
export type ProfileCoverage = "Configured" | "Partial" | "NeedsConfiguration";

export interface WorkflowProfileVersion {
  id: number;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  testName: string | null;
  workUnits: number | null;
  targetAnalystDays: number | null;
  effectiveFrom: string;
  status: WorkflowProfileStatus;
  reasonForChange: string;
  electronicSignatureId: number | null;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  signedAt: string | null;
  version: number;
}

export interface CreateWorkflowProfileVersionRequest {
  workflowType: LiveWorkflowType;
  testCode: string | null;
  workUnits: number | null;
  targetAnalystDays: number | null;
  effectiveFrom: string;
  reasonForChange: string;
  password: string;
}

export interface SignWorkflowProfileDraftRequest {
  workUnits: number | null;
  targetAnalystDays: number | null;
  effectiveFrom: string;
  reasonForChange: string;
  password: string;
}

export interface WorkflowProfileTestRow {
  testCode: string;
  testName: string;
  effectiveWorkUnits: number | null;
  effectiveTargetAnalystDays: number | null;
  hasOverride: boolean;
  coverage: ProfileCoverage;
}

export interface WorkflowProfileTypeRow {
  workflowType: LiveWorkflowType;
  effectiveWorkUnits: number | null;
  effectiveTargetAnalystDays: number | null;
  lastEffectiveFrom: string | null;
  lastChangeAt: string | null;
  lastChangeByName: string | null;
  coverage: ProfileCoverage;
  draftCount: number;
  tests: WorkflowProfileTestRow[];
}

export interface WorkflowProfileOverview {
  types: WorkflowProfileTypeRow[];
}

export interface WorkflowProfileCoverage {
  typesWithoutUnits: LiveWorkflowType[];
  typesWithoutTarget: LiveWorkflowType[];
  testsNeedingConfiguration: WorkflowProfileTestRow[];
}

// "HplcMethodAssay" -> "Hplc Method Assay"
export function workflowTypeLabel(type: LiveWorkflowType): string {
  return type.replace(/([a-z])([A-Z])/g, "$1 $2");
}

// Errors read response.data.message; anything else falls back to a fixed sentence.
export function profileErrorMessage(e: unknown, fallback: string): string {
  const message = (e as { response?: { data?: { message?: string } } } | null)?.response?.data?.message;
  return message ?? fallback;
}
