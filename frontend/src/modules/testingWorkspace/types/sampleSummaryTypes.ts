// Mirrors backend MicroLIMS.Application.DTOs.SampleSummaryDto and friends.

export interface IncubationDetail {
  stepName: string;
  stageNumber: number;
  mediaLotNumber: string | null;
  mediaMaterialName: string | null;
  incubatorName: string | null;
  temperature: string | null;
  duration: string | null;
  startedAt: string;
  expectedReadingAt: string | null;
  completedAt: string | null;
  outcome: string | null;
  startedByName: string | null;
  transferredAt: string | null;
  transferredByName: string | null;
  completedByName: string | null;
  sameAnalystBothStages: boolean | null;
}

export interface ResultDetail {
  rawValue: string;
  interpretedValue: string | null;
  type: string;
  enteredByName: string;
  enteredAt: string;
}

export interface CountTestReadingDetail {
  stepName: string | null;
  plateReadings: string;
  dilutionFactor: number;
  average: number;
  calculatedResult: number;
  reportedResult: string;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  status: string;
  enteredByName: string;
  enteredAt: string;
}

export interface PathogenObservationDetail {
  stepName: string;
  stepOrder: number;
  observation: "NoGrowth" | "GrowthNonConforming" | "GrowthConforming";
  observedByName: string;
  observedAt: string;
}

export interface WorkflowHistoryDetail {
  fromStep: string;
  toStep: string;
  note: string | null;
  performedByName: string;
  timestamp: string;
}

export interface SampleLocationDetail {
  locationName: string;
  gradeClassification: string | null;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  cfuResult: number | null;
  calculatedResult: number | null;
  reportedResult: string | null;
  status: string | null;
  enteredByName: string | null;
  enteredAt: string | null;
}

export interface TestOrderSummaryDetail {
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  status: string;
  currentStep: string;
  workflowState?: string;
  workflowStateDisplay?: string;
  usesSharedTsb?: boolean;
  isWorkflowLocked?: boolean;
  isResultEntryAllowed?: boolean;
  resultLockReason?: string | null;
  isSuperseded: boolean;
  incubations: IncubationDetail[];
  results: ResultDetail[];
  countTestReadings: CountTestReadingDetail[];
  pathogenObservations: PathogenObservationDetail[];
  workflowHistory: WorkflowHistoryDetail[];
  locations: SampleLocationDetail[];
}

export interface SamplePreparationSummary {
  amount: number;
  unit: string;
  technique: string;
  filtrationVolume: number | null;
  washingVolume: number | null;
  neutralizerName: string;
  preparedByName: string;
  preparedAt: string;
}

export interface SampleWorkflowEvent {
  eventType: string;
  performedByName: string;
  timestamp: string;
  comment: string | null;
  decision: string | null;
}

export interface SignatureTrailItem {
  printedName: string;
  // Full names collide across accounts, so this is what actually proves
  // two signatures came from different people on a printed record.
  username: string;
  role: string;
  meaning: string;
  signedAt: string;
  comment: string | null;
}

export interface SampleSummary {
  sampleId: number;
  referenceNumber: string;
  category: string;
  displayName: string;
  productionStage: string | null;
  causeOfTesting: string;
  batchNumber: string | null;
  controlNumber: string;
  status: string;
  receivedByName: string;
  receivedAt: string;
  sampledBy: string;
  sampleQuantity: string | null;
  mfgDate: string | null;
  expDate: string | null;
  waterSamplingPointCode: string | null;
  waterSamplingPointLocation: string | null;
  storageCondition: string | null;
  storageTimeHours: number | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  approvedByName: string | null;
  approvedAt: string | null;
  approvalDecision: string | null;
  preparation: SamplePreparationSummary | null;
  testOrders: TestOrderSummaryDetail[];
  timeline: SampleWorkflowEvent[];
  signatures: SignatureTrailItem[];
}
