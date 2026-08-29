export interface TestOrderSummary {
  testOrderId: number;
  testCode: string;
  status: string;
  currentStep?: string;
  workflowState?: string;
  workflowStateDisplay?: string;
  workflowStatus?:
    | "Pending"
    | "InProgress"
    | "ReadyToRead"
    | "EnterResult"
    | "PendingReview"
    | "Completed"
    | string
    | null;
  usesSharedTsb?: boolean;
  isWorkflowLocked?: boolean;
  isResultEntryAllowed?: boolean;
  resultLockReason?: string | null;
  locationCount: number;
  assignedAnalystId: number | null;
  assignedAnalystName: string | null;
}

// Mirrors backend SampleDto (backend/MicroLIMS.Application/DTOs/SampleDto.cs)
export interface SampleCard {
  sampleId: number;
  itemId?: number | null;
  referenceNumber: string;
  displayName: string; // Item name, or Sampling Point/Department/Machine name
  category: string;
  departmentId: number | null;
  machineId: number | null;
  waterDepartmentId: number | null;
  productionStage: string | null;
  causeOfTesting: string;
  batchNumber: string | null;
  controlNumber: string;
  status: string;
  preparationStatus: string;
  receivedAt: string;
  sampleQuantity: string | null;
  sampledBy: string;
  mfgDate: string | null;
  expDate: string | null;
  waterSamplingPointCode: string | null;
  waterSamplingPointLocation: string | null;
  storageCondition: string | null;
  storageTimeHours: number | null;
  incubationStarted: boolean;
  assignedAnalystId?: number | null;
  assignedAnalystName?: string | null;
  previousProductName?: string | null;
  previousProductBatchNumber?: string | null;
  assignedTests: TestOrderSummary[];
}
