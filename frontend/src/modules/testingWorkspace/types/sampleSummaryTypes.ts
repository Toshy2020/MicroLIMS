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

export interface BiochemicalResultDetail {
  stepName: string;
  biochemicalResultText: string;
  // The analyst's explicit interpretation - never inferred from the free
  // text. Null only for historical rows the backfill couldn't confidently
  // assign.
  organismDetected: boolean | null;
  submittedByName: string;
  submittedAt: string;
}

export interface WorkflowHistoryDetail {
  fromStep: string;
  toStep: string;
  note: string | null;
  performedByName: string;
  timestamp: string;
}

export interface SampleLocationDetail {
  // Stable identity of the physical location (Room/MachinePart/
  // WaterSamplingPoint id) - use this to group/match rows across test
  // orders (e.g. building the Summary Matrix). locationName is a display
  // string and is NOT guaranteed unique (multiple water sampling points
  // can share the same label).
  locationKey: string;
  locationName: string;
  gradeClassification: string | null;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  cfuResult: number | null;
  calculatedResult: number | null;
  reportedResult: string | null;
  status: string | null;
  // Populated at result-entry time - null for qualitative (Detected/Absent)
  // locations. Never assume "CFU" or "CFU/Plate": EM/After Cleaning/Water
  // mix CFU/plate/4 hours, CFU/25 cm2, and CFU/mL depending on sampling
  // method.
  unit: string | null;
  enteredByName: string | null;
  enteredAt: string | null;
}

export interface TestOrderSummaryDetail {
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  // Specification pass/fail text from Test Master's per-Item Specifications
  // (Item+TestCode keyed) - covers quantitative and qualitative tests
  // alike. Null when nobody has configured one for this Item/TestCode yet.
  specificationText: string | null;
  // Set only when this row was pulled in from a different (retest) sample
  // whose results resolved an OOS chain this sample's own tests were fully
  // superseded into. Null for every ordinarily-owned row.
  sourceSampleReferenceNumber?: string | null;
  status: string;
  currentStep: string;
  workflowState?: string;
  workflowStateDisplay?: string;
  usesSharedTsb?: boolean;
  isWorkflowLocked?: boolean;
  isResultEntryAllowed?: boolean;
  resultLockReason?: string | null;
  isSuperseded: boolean;
  sectionId?: number;
  sectionName?: string;
  incubations: IncubationDetail[];
  results: ResultDetail[];
  countTestReadings: CountTestReadingDetail[];
  // HPLC Assay only - the active result, null for other tests.
  hplcAssay?: HplcAssayDetail | null;
  // Elemental Assay only - the active result, null for other tests.
  elementalAssay?: ElementalAssayDetail | null;
  // Generic TestAnalysis workflow result, null for other tests.
  analysis?: AnalysisDetail | null;
  pathogenObservations: PathogenObservationDetail[];
  biochemicalResults: BiochemicalResultDetail[];
  workflowHistory: WorkflowHistoryDetail[];
  locations: SampleLocationDetail[];
}

export type LaboratorySectionStatus =
  | "InTesting"
  | "UnderReview"
  | "UnderApproval"
  | "Approved"
  | "Rejected"
  | "RetestRequested"
  | "Cancelled"
  | "Voided";

export interface SampleSectionSummaryDetail {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  status: LaboratorySectionStatus;
  canView: boolean;
  reviewedByName: string | null;
  reviewedAt: string | null;
  approvedByName: string | null;
  approvedAt: string | null;
  approvalDecision: string | null;
  certificateRemarks: string | null;
}

export interface SamplePreparationSummary {
  amount: number;
  technique: string;
  filtrationVolume: number | null;
  washingVolume: number | null;
  diluent: string;
  neutralizer: string;
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
  assignedAnalystId?: number | null;
  assignedAnalystName?: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  approvedByName: string | null;
  approvedAt: string | null;
  approvalDecision: string | null;
  // Customer-facing remark typed by the Approver at approval only - never
  // derived from any internal review/approval comment. Null/empty renders
  // as "No remarks." wherever printed (the Product/RM/PM COA).
  certificateRemarks: string | null;
  previousProductName?: string | null;
  previousProductBatchNumber?: string | null;
  preparation: SamplePreparationSummary | null;
  testOrders: TestOrderSummaryDetail[];
  timeline: SampleWorkflowEvent[];
  signatures: SignatureTrailItem[];
  sections?: SampleSectionSummaryDetail[];
  allSectionsVisible?: boolean;
}

export interface HplcAssayDetail {
  reportedResult: string;
  meanAssayPercent: number;
  status: string;
  specLimit: string | null;
  sampleWeightMg: number;
  sampleDilution: number;
  replicates: { replicateNumber: number; area: number; assayPercent: number }[];
  suitabilityRunCode: string;
  enteredByName: string | null;
  enteredAt: string;
  standardPurityPercent: number;
  standardWeightMg: number;
  standardDilution: number;
  standardMeanArea: number;
  suitabilityPassed: boolean;
  suitabilityPerformedByName: string | null;
  suitabilityPerformedAt: string;
  equipmentCode: string | null;
  equipmentName: string | null;
  columnCode: string | null;
  columnName: string | null;
  referenceStandardName: string | null;
  referenceStandardBatch: string | null;
  rsdPercent: number | null;
  resolution: number | null;
  tailingFactor: number | null;
  theoreticalPlates: number | null;
  sstMaxRsdPercent: number | null;
  sstMinResolution: number | null;
  sstMaxTailingFactor: number | null;
  sstMinTheoreticalPlates: number | null;
}

export interface ElementalAssayElementDetail {
  parameterName: string;
  element: string;
  runCode: string;
  runAnalytePassed: boolean;
  reportedPpm: number;
  overRange: boolean;
  belowLoq: boolean;
  mgPerUnit: number | null;
  resultClaim: number | null;
  percentLabelClaim: number | null;
  reportedDisplay: string;
  specLimit: string | null;
  unit: string | null;
  status: string;
}

export interface ElementalAssayDetail {
  sampleMatrix: "Solid" | "Liquid" | string;
  unitAmount: number;
  unitAmountUnit: string;
  analysedAt: string;
  enteredByName: string | null;
  enteredAt: string;
  elements: ElementalAssayElementDetail[];
}

export type ReadingKind =
  | "Replicate"
  | "Unit"
  | "Vessel"
  | "TimePoint"
  | "Weight"
  | "Titration";

export type ResultBasis = "MgPerKg" | "MgPerUnit" | "PercentLabelClaim";

export type SampleMatrix = "Solid" | "Liquid";

export interface ResultReadingDetail {
  id: number;
  kind: ReadingKind;
  index: number;
  stage: number | null;
  timePointMinutes: number | null;
  value1: number | null;
  value2: number | null;
  value3: number | null;
  text: string | null;
  computedValue: number | null;
  passed: boolean | null;
}

export interface ParameterResultDetail {
  id: number;
  specificationId: number;
  parameterName: string;
  reportedValue: number | null;
  reportedDisplay: string;
  unit: string | null;
  specLimit: string | null;
  resultBasis: ResultBasis | null;
  comparisonStatus: string;
  overRange: boolean;
  belowLoq: boolean;
  validityRecordItemId: number | null;
  calculationJson: string | null;
  stageReached: number | null;
  readings: ResultReadingDetail[];
}

export interface AnalysisDetail {
  id: number;
  testOrderId: number;
  analysisType: "CountTest" | "Observation" | "HplcAssay" | "ElementalAssay" | "Disintegration" | "WeightVariation" | string;
  equipmentId: number | null;
  equipmentCode: string | null;
  equipmentName: string | null;
  analysedAt: string;
  unitAmount: number | null;
  sampleMatrix: SampleMatrix | null;
  conditionsJson: string | null;
  validityRecordType: string | null;
  validityRecordId: number | null;
  enteredByName: string | null;
  enteredAt: string;
  comment: string | null;
  parameterResults: ParameterResultDetail[];
}

