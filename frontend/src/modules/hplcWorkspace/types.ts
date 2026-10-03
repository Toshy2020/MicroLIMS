// HPLC Workspace (HPLC chain S7, spec §5)
// Mirrors DTOs from HplcRunService.cs and HplcRunService.Samples.cs.
// Enums serialize as strings via JsonStringEnumConverter.

export type HplcEvidenceContext = "Run" | "Sst" | "Sample";
export type HplcEvidenceKind = "StandardReport" | "SampleReport" | "Other";
export type HplcRunStatus = "Open" | "Completed" | "Abandoned";
export type HplcSstStatus = "Pending" | "Passed" | "Failed";
export type HplcRunSampleStatus = "Assigned" | "Removed";
export type ResultStatus =
  | "WithinLimits"
  | "AlertLimitExceeded"
  | "ActionLimitExceeded"
  | "OutOfSpecification"
  | "LimitsNotConfigured"
  | "RequiresReview"
  | "Absent"
  | "Detected"
  | "PendingConfirmation"
  | "NotDetected"
  | "NextStageRequired"
  | "Inconclusive";

// ---- Instruments / Methods ----

export interface HplcActiveRunSummaryDto {
  runId: number;
  code: string;
  methodAbbreviation: string;
  analystName: string;
  sampleCount: number;
  sstStatus: HplcSstStatus;
}

export interface HplcInstrumentDto {
  equipmentId: number;
  code: string;
  name: string;
  state: "Available" | "Running" | "Unavailable" | string;
  reason?: string | null;
  activeRun?: HplcActiveRunSummaryDto | null;
}

export interface HplcMethodOptionDto {
  id: number;
  abbreviation: string;
  name: string;
  // USP column packing the method requires, e.g. "L1".
  columnDesignation: string;
  eligibleTestOrderCount: number;
}

// ---- Start Run ----

export interface HplcMobilePhaseAssignmentInput {
  channel: string;
  solutionPreparationId: number;
}

export interface StartHplcRunRequest {
  equipmentId: number;
  hplcMethodId: number;
  chromatographyColumnId: number;
  mobilePhases: HplcMobilePhaseAssignmentInput[];
}

// ---- SST ----

export interface SaveSstAnalyteInput {
  hplcSstAnalyteId: number;
  standardMaterialId: number;
  standardWeightMg: number;
  responses: number[];
  reportedRsdPercent?: number | null;
  resolution?: number | null;
  tailingFactor?: number | null;
  theoreticalPlates?: number | null;
  retentionFactor?: number | null;
  signalToNoise?: number | null;
  peakToValley?: number | null;
}

export interface SaveSstRequest {
  analytes: SaveSstAnalyteInput[];
}

export interface ConfirmSstRequest {
  password: string;
  comment?: string | null;
}

// ---- Evidence ----

export interface HplcEvidenceDto {
  id: number;
  hplcRunId: number;
  hplcRunSampleId?: number | null;
  context: HplcEvidenceContext;
  kind: HplcEvidenceKind;
  fileName: string;
  contentType: string;
  uploadedByUserId: number;
  uploadedByUserName?: string | null;
  uploadedAt: string;
  isCurrent: boolean;
  supersedeReason?: string | null;
}

// ---- Run Detail ----

export interface HplcRunMobilePhaseDto {
  id: number;
  channel: string;
  solutionPreparationId: number;
  solutionPreparationCode?: string | null;
  solutionMasterName: string;
  expiresAt?: string | null;
}

export interface HplcSstInjectionDto {
  injectionNo: number;
  response: number;
}

export interface HplcSstAnalyteDto {
  id: number;
  hplcMethodAnalyteId: number;
  analyteName: string;
  // Material master entry of the method's reference standard for this analyte.
  standardEntryId: number;
  standardMaterialId?: number | null;
  standardMaterialBatch?: string | null;
  standardPurityPercent?: number | null;
  standardMoisturePercent?: number | null;
  standardWeightMg?: number | null;
  injections: HplcSstInjectionDto[];
  reportedRsdPercent?: number | null;
  resolution?: number | null;
  tailingFactor?: number | null;
  theoreticalPlates?: number | null;
  retentionFactor?: number | null;
  signalToNoise?: number | null;
  peakToValley?: number | null;
  meanResponse?: number | null;
  computedRsdPercent?: number | null;
  passed: boolean;
  failureReasons?: string | null;
}

export interface HplcSstRecordDto {
  id: number;
  code: string;
  status: HplcSstStatus;
  failureReasons?: string | null;
  confirmedByUserId?: number | null;
  confirmedByUserName?: string | null;
  confirmedAt?: string | null;
  analytes: HplcSstAnalyteDto[];
}

export interface HplcRunSampleSummaryDto {
  id: number;
  testOrderId: number | null;
  workingStandardQualificationId?: number | null;
  status: HplcRunSampleStatus;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
  submitted: boolean;
  isDissolution: boolean;
}

export interface HplcDissolutionStandardValues {
  hplcRunId: number;
  runSampleId: number;
  equipmentId: number;
  runCode: string;
  sstCode: string;
  meanResponse: number;
  standardWeightMg: number;
  purityPercent: number;
  moisturePercent: number;
  standardDilution: number;
  cs: number;
}

export interface HplcDissolutionStandardResult {
  standard: HplcDissolutionStandardValues | null;
  problem: string | null;
}

export interface HplcRunDto {
  id: number;
  version: number;
  code: string;
  sectionId: number;
  equipmentId: number;
  equipmentCode: string;
  equipmentName: string;
  chromatographyColumnId: number;
  chromatographyColumnCode: string;
  hplcMethodId: number;
  hplcMethodAbbreviation: string;
  analystUserId: number;
  analystUserName?: string | null;
  startedAt: string;
  status: HplcRunStatus;
  closedAt?: string | null;
  closeReason?: string | null;
  mobilePhases: HplcRunMobilePhaseDto[];
  sst?: HplcSstRecordDto | null;
  samples: HplcRunSampleSummaryDto[];
  evidence: HplcEvidenceDto[];
  canConfirmSst: boolean;
  canConfirmSstReason?: string | null;
  canAssignSamples: boolean;
  canAssignSamplesReason?: string | null;
}

export interface HplcRunListItem {
  id: number;
  code: string;
  methodAbbreviation: string;
  analystUserName: string;
  startedAt: string;
  status: HplcRunStatus;
  sstStatus: HplcSstStatus;
  closedAt?: string | null;
}

// ---- Samples & Entry (Part B / S7b) ----

export interface EligibleTestDto {
  testOrderId: number;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
  receivedAt: string;
  stageName?: string | null;
}

export interface HplcReplicateResponseInput {
  hplcMethodAnalyteId: number;
  response: number;
}

export interface HplcReplicateInput {
  actualWeightMg: number;
  responses: HplcReplicateResponseInput[];
}

export interface AssignHplcSamplesRequest {
  testOrderIds: number[];
}

export interface SaveReplicatesRequest {
  replicates: HplcReplicateInput[];
}

export interface SubmitHplcSampleRequest {
  password: string;
  comment?: string | null;
}

export interface HplcReplicateDto {
  replicateNo: number;
  actualWeightMg: number;
  responses: HplcReplicateResponseInput[];
}

export interface HplcMethodWeightDto {
  hplcMethodAnalyteId: number;
  analyteName: string;
  theoreticalWeightStdMg: number;
  theoreticalWeightTestMg: number;
}

export interface HplcPreviewResultDto {
  hplcMethodAnalyteId: number;
  parameterName: string;
  quantity: string;
  replicateNo?: number | null;
  value: number;
  display: string;
  unit: string;
  status: ResultStatus;
  specLimit?: string | null;
}

export interface HplcOfficialResultDto {
  parameterName: string;
  quantity: string;
  replicateNo?: number | null;
  display: string;
  status: ResultStatus;
  specLimit?: string | null;
}

export interface HplcSampleEntryDto {
  runSampleId: number;
  hplcRunId: number;
  runCode: string;
  sstCode: string;
  sstStatus: HplcSstStatus;
  testOrderId: number;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
  stageName?: string | null;
  status: HplcRunSampleStatus;
  basis: string;
  requiredReplicates?: number | null;
  methodWeights: HplcMethodWeightDto[];
  replicates: HplcReplicateDto[];
  preview: HplcPreviewResultDto[];
  // The stored results once the sample has been sent for review.
  official: HplcOfficialResultDto[];
  evidence: HplcEvidenceDto[];
  editable: boolean;
  editableReason?: string | null;
  submitted: boolean;
  canSubmit: boolean;
  canSubmitReason?: string | null;
}

export interface ReasonRequest {
  reason: string;
}

// ---- Working Standard Qualifications (Task 10) ----

export type WorkingStandardQualificationStatus =
  | "Draft"
  | "Assayed"
  | "Reviewed"
  | "Approved"
  | "Rejected";

export type WorkingStandardQualificationKind =
  | "Initial"
  | "Requalification";

export interface EligibleQualificationDto {
  qualificationId: number;
  code: string;
  kind: WorkingStandardQualificationKind;
  materialName: string;
  batchNumber: string;
  analyteName: string;
}

export interface AssignQualificationsRequest {
  qualificationIds: number[];
}

export interface WorkingStandardPreviewDto {
  replicateAssayPercents: number[];
  meanAssayPercent: number;
  rsdPercent?: number | null;
  potencyPercent: number;
  passed: boolean;
  failureReasons?: string | null;
}

export interface HplcQualificationEntryDto {
  runSampleId: number;
  hplcRunId: number;
  runCode: string;
  sstCode: string;
  sstStatus: HplcSstStatus;
  qualificationId: number;
  qualificationCode: string;
  qualificationStatus: WorkingStandardQualificationStatus;
  materialName: string;
  batchNumber: string;
  moisturePercent?: number | null;
  methodWeights: HplcMethodWeightDto[];
  requiredReplicates: number;
  replicates: HplcReplicateDto[];
  preview?: WorkingStandardPreviewDto | null;
  evidence: HplcEvidenceDto[];
  editable: boolean;
  editableReason?: string | null;
  submitted: boolean;
  canSubmit: boolean;
  canSubmitReason?: string | null;
}
