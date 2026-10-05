// ICP Workspace DTOs and enums (spec 2026-10-03 §4.2, §4.3, §6).
// Mirrors DTOs from IcpRunService.cs, IcpRunService.Samples.cs, and IcpElementAvailability.cs.
// Enums serialize as strings via JsonStringEnumConverter.

export type IcpRunStatus = "Open" | "Completed" | "Abandoned";
export type IcpCalibrationStatus = "Pending" | "Confirmed";
export type IcpRunSampleStatus = "Assigned" | "Removed";
export type IcpEvidenceContext = "Run" | "Calibration" | "Sample";
export type IcpEvidenceKind = "CalibrationReport" | "SampleReport" | "Other";
export type IcpAmountUnit = "Gram" | "Milliliter";

export type {
  IcpMethodMode,
  IcpAnalyteView,
  IcpMethodResponse,
  IcpMethodElementResponse
} from "../laboratoryConfiguration/masterDataSimple/services/IcpMethodService";

import type {
  IcpMethodMode,
  IcpMethodResponse
} from "../laboratoryConfiguration/masterDataSimple/services/IcpMethodService";

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

export interface IcpActiveRunSummaryDto {
  runId: number;
  code: string;
  methodAbbreviation: string;
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

export interface IcpMethodOptionDto {
  id: number;
  abbreviation: string;
  name: string;
  mode: IcpMethodMode;
  eligibleTestOrderCount: number;
}

// ---- Requests ----

export interface StartIcpRunRequest {
  equipmentId: number;
  icpMethodId: number;
}

export interface SaveIcpCalibrationElementInput {
  icpCalibrationElementId: number;
  correlationR?: number | null;
  blankMgPerL?: number | null;
  icvMeasuredMgPerL?: number | null;
}

export interface SaveIcpCalibrationRequest {
  calibrationStandardMaterialId: number | null;
  icvStandardMaterialId: number | null;
  elements: SaveIcpCalibrationElementInput[];
}

export interface ConfirmIcpCalibrationRequest {
  password: string;
  comment?: string | null;
}

export interface AddIcpCcvRequest {
  icpMethodElementId: number;
  measuredMgPerL: number;
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
  icpMethodElementId: number;
  symbol: string;
  correlationR?: number | null;
  blankMgPerL?: number | null;
  icvMeasuredMgPerL?: number | null;
  icvRecoveryPercent?: number | null;
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
  icvStandardMaterialId?: number | null;
  icvStandardLotLabel?: string | null;
  icvStandardCode?: string | null;
  confirmedByUserId?: number | null;
  confirmedByUserName?: string | null;
  confirmedAt?: string | null;
  expiresAt?: string | null;
  elements: IcpCalibrationElementDto[];
}

export interface IcpCcvReadingDto {
  id: number;
  icpMethodElementId: number;
  symbol: string;
  measuredMgPerL: number;
  recoveryPercent: number;
  passed: boolean;
  enteredByUserId: number;
  enteredByUserName?: string | null;
  enteredAt: string;
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

export interface IcpElementStateDto {
  icpMethodElementId: number;
  symbol: string;
  valid: boolean;
  reason?: string | null;
}

export interface IcpRunDto {
  id: number;
  version: number;
  code: string;
  sectionId: number;
  equipmentId: number;
  equipmentCode: string;
  equipmentName: string;
  icpMethodId: number;
  icpMethodAbbreviation: string;
  method: IcpMethodResponse;
  analystUserId: number;
  analystUserName?: string | null;
  startedAt: string;
  status: IcpRunStatus;
  closedAt?: string | null;
  closeReason?: string | null;
  calibration: IcpCalibrationDto;
  ccvReadings: IcpCcvReadingDto[];
  samples: IcpRunSampleSummaryDto[];
  evidence: IcpEvidenceDto[];
  canConfirmCalibration: boolean;
  canConfirmCalibrationReason?: string | null;
  elementStates: IcpElementStateDto[];
  canAssignSamples: boolean;
  canAssignSamplesReason?: string | null;
}

export interface IcpRunListItem {
  id: number;
  code: string;
  methodAbbreviation: string;
  analystUserName: string;
  startedAt: string;
  status: IcpRunStatus;
  calibrationStatus: IcpCalibrationStatus;
  sampleCount: number;
}

// ---- Sample Entry & Replicates (used in slice 2, defined here per contract) ----

export interface EligibleTestDto {
  id: number;
  sampleNumber: string;
  batchNumber?: string | null;
  productName?: string | null;
  testCode: string;
  receivedAt: string;
  stageName?: string | null;
}

export interface IcpConcentrationInput {
  icpMethodElementId: number;
  solutionMgPerL: number;
}

export interface IcpReplicateInputDto {
  sampleAmount: number;
  volumeMl: number;
  dilutionFactor: number;
  concentrations: IcpConcentrationInput[];
}

export interface SaveIcpReplicatesRequest {
  amountUnit: IcpAmountUnit;
  unitAmount?: number | null;
  replicates: IcpReplicateInputDto[];
}

export interface AssignIcpSamplesRequest {
  testOrderIds: number[];
}

export interface IcpReplicateDto {
  replicateNo: number;
  sampleAmount: number;
  volumeMl: number;
  dilutionFactor: number;
  concentrations: IcpConcentrationInput[];
}

export interface IcpPreviewResultDto {
  icpMethodElementId: number;
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
  mode: IcpMethodMode;
  amountUnit: IcpAmountUnit;
  unitAmount?: number | null;
  sampleVolumeMl: number;
  dilutionFactor: number;
  elements: IcpElementStateDto[];
  replicates: IcpReplicateDto[];
  preview: IcpPreviewResultDto[];
  official: IcpOfficialResultDto[];
  evidence: IcpEvidenceDto[];
  editable: boolean;
  editableReason?: string | null;
  submitted: boolean;
  canSubmit: boolean;
  canSubmitReason?: string | null;
}
