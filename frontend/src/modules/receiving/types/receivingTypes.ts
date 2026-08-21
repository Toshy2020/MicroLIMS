export interface TestOrderSummary {
  testOrderId: number;
  testCode: string;
  status: string;
  workflowStatus?:
    | "Pending"
    | "InProgress"
    | "ReadyToRead"
    | "EnterResult"
    | "PendingReview"
    | "Completed"
    | string
    | null;
  locationCount: number;
  assignedAnalystId: number | null;
  assignedAnalystName: string | null;
}

// Mirrors backend SampleDto exactly.
export interface SampleRecord {
  sampleId: number;
  itemId?: number | null;
  referenceNumber: string;
  category: string;
  displayName: string;
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
  sampleQuantity?: string | null;
  sampledBy: string;
  mfgDate?: string | null;
  expDate?: string | null;
  waterSamplingPointCode?: string | null;
  waterSamplingPointLocation?: string | null;
  storageCondition?: string | null;
  storageTimeHours?: number | null;
  incubationStarted?: boolean;
  assignedTests: TestOrderSummary[];
}

// Mirrors backend ItemBasedReceiveRequest (Product/RM/PM).
export interface ItemBasedReceiveRequest {
  itemId: number;
  causeOfTestingId: number;
  sampleQuantity: string;
  sampledBy: string;
  batchNumber: string;
  controlNumber: string;
  mfgDate: string | null;
  expDate: string | null;
  productionStage?: string | null;
}

export interface WaterReceiveRequest {
  waterDepartmentId: number;
  causeOfTestingId: number;
  sampleQuantity: string;
  sampledBy: string;
  controlNumber: string;
}

export interface EMReceiveRequest {
  departmentId: number;
  causeOfTestingId: number;
  sampledBy: string;
  controlNumber: string;
}

export interface AfterCleaningReceiveRequest {
  machineId: number;
  causeOfTestingId: number;
  sampledBy: string;
  controlNumber: string;
}

export type SampleCategoryKey = "product" | "rm" | "pm" | "water" | "em" | "ac";

export interface CategoryDefinition {
  key: SampleCategoryKey;
  label: string;
  apiCategory: string | null;
  backendCategoryName: string;
  description: string;
}

export interface SampleKpiCounts {
  total: number;
  underTesting: number;
  pendingReview: number;
  approved: number;
  rejected: number;
  cancelledVoided: number;
}

export interface ReceiveRowItem {
  id: string; // unique client-side key for row rendering
  itemId?: number | "";
  productionStage?: string;
  waterSamplingPointId?: number | "";
  departmentId?: number | "";
  machineId?: number | "";
  causeOfTestingId?: number | "";
  sampleQuantity?: string;
  sampledBy?: string;
  batchNumber?: string;
  controlNumber?: string;
  mfgDate?: string;
  expDate?: string;
  errors?: Record<string, string>;
}
