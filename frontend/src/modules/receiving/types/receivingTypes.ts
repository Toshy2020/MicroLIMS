export interface TestOrderSummary {
  testOrderId: number;
  testCode: string;
  status: string;
}

// Mirrors backend SampleDto exactly.
export interface SampleRecord {
  sampleId: number;
  referenceNumber: string;
  category: string;
  displayName: string;
  departmentId: number | null;
  machineId: number | null;
  productionStage: string | null;
  causeOfTesting: string;
  batchNumber: string | null;
  controlNumber: string;
  status: string;
  preparationStatus: string;
  receivedAt: string;
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
  waterSamplingPointId: number;
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
