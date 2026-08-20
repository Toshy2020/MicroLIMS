// Mirrors backend/MicroLIMS.API/Controllers/TestWorkflowController.cs and
// TestWorkflowEngine.cs record types. StepType/GrowthObservation/WorkflowType
// are sent and received as their C# enum's string name, never an ordinal.

export type StepType =
  | "PlateCount"
  | "BrothEnrichment"
  | "SelectiveBroth"
  | "SelectivePlating"
  | "ConfirmatoryPlating"
  | "BiochemicalTest";

export type GrowthObservation = "NoGrowth" | "GrowthNonConforming" | "GrowthConforming";

export type WorkflowType = "CountTest" | "Observation";

export type ConfirmatoryResult = "AllConforming" | "Inconclusive";

export type AnalystDecision = "SubmitAsDetected" | "ProceedToBiochemical";

export interface TestWorkflowStepDto {
  id: number;
  stepOrder: number;
  stepName: string;
  mediaTypeId: number | null;
  stepType: StepType;
  targetOrganismId: number | null;
  mediaType: { id: number; class: string } | null;
  incubationMinHours: number;
  incubationMaxHours: number;
  temperatureMin: number;
  temperatureMax: number;
  isFinalStep: boolean;
  requiresIncubationTransfer?: boolean;
  incubationStages?: {
    stageNumber: number;
    tempMin: number;
    tempMax: number;
    incubationMinHours: number;
    incubationMaxHours: number;
  }[];
}

export interface IncubationLock {
  isLocked: boolean;
  incubationEndUtc: string;
  remainingSeconds: number;
  stageNumber?: number;
}

export interface SampleContext {
  sampleName: string;
  batchNumber: string | null;
  controlNumber: string | null;
  reason: string | null;
  systemReferenceNumber: string | null;
  sampleType: string;
  stage?: string; // present only when sampleType === "FinishedProduct" - omitted entirely otherwise, never null
  preparationUnit?: string | null;
  cfuUnit?: string | null;
}

export interface SiblingPathogenOrder {
  testOrderId: number;
  pathogenName: string;
  testCode: string;
}

export interface SharedTsbSummary {
  mediaLotNumber: string | null;
  incubatorCode: string | null;
  incubationStartUtc: string | null;
  incubationEndUtc: string | null;
  minReadyAt: string | null;
  startedByUserName: string;
  isCompleted: boolean;
}

export interface PreviousStepDetail {
  stepNumber: number;
  stepName: string;
  status: "Incubating" | "Complete";
  mediaName: string | null;
  lotNumber: string | null;
  incubatorName: string | null;
  incubatorSetTemp: number | null;
  incubationStartUtc: string | null;
  incubationEndUtc: string | null;
  observation: string | null;
  isSharedSessionStep?: boolean;
}

export interface CompletedStepSummary {
  stepOrder: number;
  stepName: string;
  stepType: StepType;
  isFinalStep: boolean;
  outcome: string | null;
  observedAt: string | null;
  reportedResult: string | null;
  calculatedResult: number | null;
  status: string | null;
}

export interface CurrentStepResponse {
  testOrderId: number;
  step: TestWorkflowStepDto | null;
  stepNumber: number | null;
  totalSteps: number;
  testName: string;
  workflowType: WorkflowType;
  sampleContext: SampleContext;
  incubationLock: IncubationLock | null;
  previousSteps: PreviousStepDetail[];
  sharedTsbSummary?: SharedTsbSummary | null;
  allStepsComplete: boolean;
  finalResult: string | null;
  allSteps: { stepOrder: number; stepName: string }[];
  completedSteps: CompletedStepSummary[];
  // Legacy CountTest/EM fields, still present on the same response for those workflow types:
  incubation?: {
    id: number; mediaId?: number; equipmentId?: number;
    temperature: string; duration: string; startedAt: string; expectedReadingAt: string;
  } | null;
}

export interface StepResultDto {
  stepInstanceId: number;
  stepType: string;
  status: string;
  submittedByUserId: number;
  submittedAtUtc: string;
  nextStepUnlocked: boolean;
  workflowFinalResult: string | null;
  flags: string[];
}

export interface ConfirmatoryOutcomeDto {
  stepInstanceId: number;
  confirmatoryResult: ConfirmatoryResult;
  analystDecisionRequired: boolean;
  flags: string[];
}

export interface PermittedConfirmatoryMediaEntry {
  stepMediaId: number;
  materialId: number;
  mediaName: string;
  expectedAppearance: string | null;
  tempMin: number;
  tempMax: number;
  availableLots: { id: number; lotNumber: string; expiryDate: string }[];
}

export interface PermittedConfirmatoryMediaResponse {
  testOrderId: number;
  stepName: string;
  organism: { id: number; name: string } | null;
  permittedMedia: PermittedConfirmatoryMediaEntry[];
}

export interface EligibleIncubatorsResponse {
  stepMediaId: number;
  tempMin: number;
  tempMax: number;
  eligibleIncubators: { id: number; name: string; code: string; setTemperature: number; calibrationStatus: string }[];
}
