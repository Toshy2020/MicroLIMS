export enum GrowthObservation {
  NoGrowth = "NoGrowth",
  GrowthNonConforming = "GrowthNonConforming",
  GrowthConforming = "GrowthConforming"
}

export enum ConfirmationResult {
  Detected = "Detected (+)",
  NotDetected = "Not Detected (-)",
  Inconclusive = "Inconclusive (Retest)"
}

export interface LocationPathogenObservationDto {
  id: number;
  sampleLocationId: number;
  testOrderId: number;
  growthObservation: GrowthObservation | string;
  selectiveMediaSnapshot?: string | null;
  observedAt: string; // ISO datetime
  observedByUserId: number;
  rowVersion?: string;
}

export interface PrimaryObservationInput {
  sampleLocationId: number;
  testCode: string;
  observation: GrowthObservation | string;
}

export interface SavePrimaryObservationsRequest {
  observations: PrimaryObservationInput[];
}

export interface EligibleLocationForConfirmationDto {
  locationId: number;
  primaryObservationId: number;
  locationName: string;
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  growthObservation: GrowthObservation | string;
  growthObservationDisplay: string;
  requiredConfirmatoryMediaCount: number;
}

export interface GetEligibleConfirmationsResponse {
  eligibleLocations: EligibleLocationForConfirmationDto[];
}

export interface ConfirmatoryPlateObservationDetailDto {
  id: number;
  mediumIndex: number;
  materialId?: number;
  mediumName?: string | null;
  observation: GrowthObservation | string;
  expectedAppearanceSnapshot?: string | null;
  recordedAtUtc: string; // ISO datetime
  recordedByUserName?: string | null;
}

export interface BatchConfirmatorySetupRequest {
  testOrderId: number;
  locationIds: number[];
  mediaMaterialIds: number[];
  mediaLotIds?: number[] | null;
  incubatorEquipmentId: number;
  incubationStartUtc?: string | null;
}

export interface BatchConfirmatoryPlateReadingInput {
  locationPathogenObservationId: number;
  mediumIndex: number;
  materialId: number;
  observation: GrowthObservation | string;
}

export interface SaveBatchConfirmatoryPlateReadingsRequest {
  readings: BatchConfirmatoryPlateReadingInput[];
  biochemicalComment?: string | null;
}

export interface SessionLocationDto {
  id: number;
  primarySampleLocationId: number;
  locationName: string;
  locationType: string;
  gradeClassification: string | null;
  testLocationMap: Record<string, number>;
}

export interface SessionWorkflowStepDto {
  stepOrder: number;
  stepName: string;
  stepType: string;
  mediaTypeId: number | null;
  mediaTypeName: string | null;
  incubationMinHours: number;
  incubationMaxHours: number;
  temperatureMin: number;
  temperatureMax: number;
  isCompleted: boolean;
  outcome: string | null;
  completedAt: string | null;
}

export interface SessionAssignedTestDto {
  testOrderId: number;
  testCode: string;
  displayName: string;
  workflowType: string;
  status: string;
  currentStep: string;
  assignedAnalystName: string | null;
  requiresTsb: boolean;
  testSessionState: string;
  testSessionStateDisplay: string;
  workflowStatus?:
    | "Pending"
    | "InProgress"
    | "ReadyToRead"
    | "EnterResult"
    | "PendingReview"
    | "Completed"
    | string
    | null;
  isResultEntryAllowed: boolean;
  isWorkflowLocked: boolean;
  lockReason: string | null;
  steps: SessionWorkflowStepDto[];
  confirmatoryMediaCount?: number;

  // Primary observation tracking & confirmatory gating metadata
  primaryObservationId?: number | null;
  primaryObservation?: GrowthObservation | string | null;
  primaryObservedAt?: string | null;
  isEligibleForConfirmation?: boolean;
  confirmationStatus?: "NotApplicable" | "Eligible" | "InProgress" | "Completed" | null;
  confirmationResult?: ConfirmationResult | string | null;
  confirmatoryPlateDetails?: ConfirmatoryPlateObservationDetailDto[] | null;

  // Result metadata
  resultCode?: string | null;
  resultDisplay?: string | null;
  numericValue?: number | null;
}

export interface SharedTsbStateDto {
  isStarted: boolean;
  isIncubating: boolean;
  isCompleted: boolean;
  isLocked: boolean;
  mediaLotId: number | null;
  mediaLotNumber: string | null;
  mediaMaterialName: string | null;
  gptStatus: string | null;
  sterilityStatus: string | null;
  incubatorEquipmentId: number | null;
  incubatorCode: string | null;
  requiredTemperatureRange: string | null;
  requiredDurationRange: string | null;
  temperature: string | null;
  incubationDurationHours: number | null;
  actualStartUtc: string | null;
  minReadyAt?: string | null;
  expectedCompletionUtc: string | null;
  completedAtUtc: string | null;
  startedByUserId: number | null;
  startedByUserName: string | null;
  applicableTestCodes: string[];
  applicableLocationCount: number;
}

export interface MatrixCellResultDto {
  sampleLocationId: number;
  testCode: string;
  locationName: string;
  resultCode: string | null;
  resultDisplay: string | null;
  numericValue: number | null;
  resultType: "Qualitative" | "Quantitative" | string;
  status: string | null;
  enteredAt: string | null;
  enteredByUserName: string | null;
  isEditable: boolean;
  cellState: "COMPLETED" | "AVAILABLE" | "LOCKED_PREREQUISITE" | string;
  lockReason: string | null;

  // Multi-location confirmation tracking properties
  primaryObservationId?: number | null;
  primaryObservation?: GrowthObservation | string | null;
  isEligibleForConfirmation?: boolean;
  confirmationStatus?: "NotApplicable" | "Eligible" | "InProgress" | "Completed" | string | null;
  confirmatoryPlates?: ConfirmatoryPlateObservationDetailDto[] | null;
}

export interface MissingResultDto {
  locationName: string;
  testCode: string;
  testDisplayName: string;
}

export interface PathogenTestingSessionDto {
  sessionId: string;
  sampleId: number;
  sampleReferenceNumber: string;
  category: string;
  programName: string;
  departmentOrAreaName: string;
  controlNumber: string;
  batchNumber: string | null;
  samplingDate: string;
  overallSessionStatus: string;
  overallSessionStatusDisplay: string;
  totalLocations: number;
  totalAssignedTests: number;
  requiredResultCount: number;
  completedResultCount: number;
  availableResultCount: number;
  lockedResultCount: number;
  pendingResultCount: number;
  locations: SessionLocationDto[];
  assignedTests: SessionAssignedTestDto[];
  sharedTsb: SharedTsbStateDto;
  resultMatrix: MatrixCellResultDto[];
  missingResults: MissingResultDto[];
}

export interface StartSharedTsbRequest {
  mediaLotId: number;
  incubatorEquipmentId: number;
  incubationStartUtc?: string | null;
}

export interface MatrixCellInput {
  sampleLocationId: number;
  testCode: string;
  resultCode: string;
  resultDisplay: string;
  numericValue: number | null;
  resultType: string;
}

export interface SaveResultMatrixRequest {
  cells: MatrixCellInput[];
}
