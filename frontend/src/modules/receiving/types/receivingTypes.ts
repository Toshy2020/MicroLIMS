export interface TestOrderSummary {
  testOrderId: number;
  sectionId: number;
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

// Mirrors backend SampleAssignmentOptionsDto: each test on the sample, who
// is on it, and who the signed-in user may put on it. The backend applies
// every assignment rule (section membership, Section Heads assign only
// themselves); the dialog only offers what it returned.
export interface AssignableUser {
  id: number;
  fullName: string;
  username: string;
  roleName: string;
  roleType: string;
}

export interface AssignableTest {
  testOrderId: number;
  testCode: string;
  sectionId: number;
  sectionName: string;
  status: string;
  assignedAnalystId: number | null;
  assignedAnalystName: string | null;
  canAssign: boolean;
  eligibleUserIds: number[];
}

export interface SampleAssignmentOptions {
  sampleId: number;
  tests: AssignableTest[];
  users: AssignableUser[];
}

export interface TestAnalystAssignment {
  testOrderId: number;
  analystUserId: number | null;
}

// Mirrors backend SampleDto exactly.
export interface SampleRecord {
  sampleId: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  itemId?: number | null;
  referenceNumber: string;
  category: string;
  displayName: string;
  departmentId: number | null;
  machineId: number | null;
  waterDepartmentId: number | null;
  productionStage: string | null;
  causeOfTesting: string;
  causeOfTestingId?: number;
  batchNumber: string | null;
  controlNumber: string;
  status: string;
  preparationStatus: string;
  receivedAt: string;
  // From the server's overdue rule (same as the Overdue tile): when this open
  // sample becomes overdue (null once closed) and whether it already is.
  dueAt?: string | null;
  isOverdue?: boolean;
  sampleQuantity?: string | null;
  sampledBy: string;
  mfgDate?: string | null;
  expDate?: string | null;
  waterSamplingPointCode?: string | null;
  waterSamplingPointLocation?: string | null;
  storageCondition?: string | null;
  storageTimeHours?: number | null;
  incubationStarted?: boolean;
  // Display hints only - the backend re-checks both on every correction.
  canEditDetails?: boolean;
  canChangeItemOrLocation?: boolean;
  assignedAnalystId?: number | null;
  assignedAnalystName?: string | null;
  previousProductName?: string | null;
  previousProductBatchNumber?: string | null;
  originSampleId?: number | null;
  originReferenceNumber?: string | null;
  oosGroupCode?: string | null;
  assignedTests: TestOrderSummary[];
}

// Mirrors backend CorrectSampleRequest: the sample's full corrected details
// (a null optional field clears it) plus the reason and the signer's password.
export interface SampleCorrectionPayload {
  reason: string;
  password: string;
  controlNumber: string;
  sampledBy: string;
  causeOfTestingId: number;
  batchNumber: string | null;
  mfgDate: string | null;
  expDate: string | null;
  sampleQuantity: string | null;
  productionStage: string | null;
  previousProductName: string | null;
  previousProductBatchNumber: string | null;
  storageCondition: string | null;
  storageTimeHours: number | null;
  itemId: number | null;
  waterDepartmentId: number | null;
  departmentId: number | null;
  machineId: number | null;
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
  // Which laboratories to receive the sample for - required, at least one.
  targetSectionIds: number[];
}

// Mirrors backend ReceiptLabOptionDto (GET /samples/receipt-labs?itemId=).
export interface ReceiptLabOption {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  testCount: number;
}

// Mirrors backend AddLaboratoryRequest (POST /samples/{id}/laboratories).
export interface AddLaboratoryPayload {
  sectionId: number;
  password: string;
  reason: string;
}

export interface WaterReceiveRequest {
  waterDepartmentId: number;
  causeOfTestingId: number;
  sampleQuantity: string;
  sampledBy: string;
  controlNumber: string;
  sampledAt?: string | null;
}

export interface EMReceiveRequest {
  departmentId: number;
  causeOfTestingId: number;
  sampledBy: string;
  controlNumber: string;
  sampledAt?: string | null;
}

export interface AfterCleaningReceiveRequest {
  machineId: number;
  causeOfTestingId: number;
  sampledBy: string;
  controlNumber: string;
  previousProductName: string;
  previousProductBatchNumber: string;
  sampledAt?: string | null;
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
  previousProductName?: string;
  previousProductBatchNumber?: string;
  productionStage?: string;
  waterSamplingPointId?: number | "";
  departmentId?: number | "";
  machineId?: number | "";
  causeOfTestingId?: number | "";
  sampleQuantity?: string;
  sampledBy?: string;
  // Water / EM / After Cleaning: when the sample was taken (datetime-local value).
  sampledAt?: string;
  batchNumber?: string;
  controlNumber?: string;
  mfgDate?: string;
  expDate?: string;
  // Choose-mode only: labs picked for this row (Laboratories column).
  // undefined until the item's receipt-labs have loaded and defaulted.
  targetSectionIds?: number[];
  errors?: Record<string, string>;
}
