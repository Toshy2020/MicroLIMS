// ICP Workspace DTOs and enums. Mirrors IcpRunService.cs, IcpRunService.Samples.cs and IcpElementAvailability.cs.
// The run carries no method: the analyst lists the calibrated elements; the tests define what is measured.
// Enums serialize as strings via JsonStringEnumConverter.

export type IcpRunStatus = "Open" | "Completed" | "Abandoned";
export type IcpCalibrationStatus = "Pending" | "Confirmed";
export type IcpRunSampleStatus = "Assigned" | "Removed";
export type IcpEvidenceContext = "Run" | "Calibration" | "Sample";
export type IcpEvidenceKind = "CalibrationReport" | "SampleReport" | "Other";
// What an ICP test reports (Test Master): assay % of target, or impurity ppm against the item's NMT limit.
export type IcpTestMode = "Assay" | "Impurity";
export type IcpSampleForm = "Solid" | "Liquid";

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

// ---- Instruments ----

export interface IcpActiveRunSummaryDto {
  runId: number;
  code: string;
  analystName: string;
  sampleCount: number;
  calibrationStatus: IcpCalibrationStatus;
}

export interface IcpInstrumentDto {
  equipmentId: number;
  code: string;
  name: string;
  state: "Available" | "Running" | "Unavailable" | string;
  reason?: string | null;
  activeRun?: IcpActiveRunSummaryDto | null;
}

// ---- Requests ----

export interface StartIcpRunRequest {
  equipmentId: number;
}

export interface SaveIcpCalibrationElementInput {
  symbol: string;
  correlationR?: number | null;
}

export interface SaveIcpCalibrationRequest {
  calibrationStandardMaterialId: number | null;
  elements: SaveIcpCalibrationElementInput[];
}

export interface ConfirmIcpCalibrationRequest {
  password: string;
  comment?: string | null;
}

export interface ReasonRequest {
  reason: string;
}

export interface SubmitIcpSampleRequest {
  password: string;
  comment?: string | null;
}

// ---- Run Detail & Calibration ----

export interface IcpEvidenceDto {
  id: number;
  icpRunId: number;
  icpRunSampleId?: number | null;
  context: IcpEvidenceContext;
  kind: IcpEvidenceKind;
  fileName: string;
  contentType: string;
  uploadedByUserId: number;
  uploadedByUserName?: string | null;
  uploadedAt: string;
  isCurrent: boolean;
  supersedeReason?: string | null;
}

export interface IcpCalibrationElementDto {
  id: number;
  symbol: string;
  correlationR?: number | null;
  passed: boolean;
  failureReasons?: string | null;
}

export interface IcpCalibrationDto {
  id: number;
  code: string;
  status: IcpCalibrationStatus;
  calibrationStandardMaterialId?: number | null;
  calibrationStandardLotLabel?: string | null;
  calibrationStandardCode?: string | null;
  confirmedByUserId?: number | null;
  confirmedByUserName?: string | null;
  confirmedAt?: string | null;
  expiresAt?: string | null;
  minCorrelation: number;
  elements: IcpCalibrationElementDto[];
}

export interface IcpRunSampleSummaryDto {
  id: number;
  testOrderId: number;
  status: IcpRunSampleStatus;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
}

export interface IcpRunDto {
  id: number;
  version: number;
  code: string;
  sectionId: number;
  equipmentId: number;
  equipmentCode: string;
  equipmentName: string;
  analystUserId: number;
  analystUserName?: string | null;
  startedAt: string;
  status: IcpRunStatus;
  closedAt?: string | null;
  closeReason?: string | null;
  calibration: IcpCalibrationDto;
  samples: IcpRunSampleSummaryDto[];
  evidence: IcpEvidenceDto[];
  canConfirmCalibration: boolean;
  canConfirmCalibrationReason?: string | null;
  canAssignSamples: boolean;
  canAssignSamplesReason?: string | null;
}

export interface IcpRunListItem {
  id: number;
  code: string;
  analystUserName: string;
  startedAt: string;
  status: IcpRunStatus;
  calibrationStatus: IcpCalibrationStatus;
  sampleCount: number;
}

// ---- Sample Entry ----

export interface EligibleTestDto {
  testOrderId: number;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
  receivedAt: string;
  stageName?: string | null;
}

export interface IcpConcentrationInput {
  icpTestElementId: number;
  resultPpm: number;
}

export interface SaveIcpSampleEntryRequest {
  actualWeightG?: number | null;
  concentrations: IcpConcentrationInput[];
}

export interface AssignIcpSamplesRequest {
  testOrderIds: number[];
}

// One element of the test: its target, whether the run's calibration allows it, and the entered result.
export interface IcpEntryElementDto {
  icpTestElementId: number;
  symbol: string;
  targetPpm?: number | null;
  valid: boolean;
  reason?: string | null;
  resultPpm?: number | null;
}

export interface IcpPreviewResultDto {
  icpTestElementId: number;
  parameterName: string;
  quantity: string;
  value?: number | null;
  display: string;
  unit: string;
  status?: ResultStatus | null;
  specLimit?: string | null;
  problem?: string | null;
}

export interface IcpOfficialResultDto {
  parameterName: string;
  quantity: string;
  display: string;
  status: ResultStatus;
  specLimit?: string | null;
}

export interface IcpSampleEntryDto {
  runSampleId: number;
  icpRunId: number;
  runCode: string;
  calibrationCode: string;
  calibrationStatus: IcpCalibrationStatus;
  testOrderId: number;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
  stageName?: string | null;
  status: IcpRunSampleStatus;
  mode: IcpTestMode;
  sampleForm?: IcpSampleForm | null;
  theoreticalAmount?: number | null;
  specificGravity?: number | null;
  actualWeightG?: number | null;
  elements: IcpEntryElementDto[];
  preview: IcpPreviewResultDto[];
  official: IcpOfficialResultDto[];
  evidence: IcpEvidenceDto[];
  editable: boolean;
  editableReason?: string | null;
  submitted: boolean;
  canSubmit: boolean;
  canSubmitReason?: string | null;
}
