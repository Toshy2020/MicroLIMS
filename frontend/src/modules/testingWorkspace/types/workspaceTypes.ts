export interface TestOrderSummary {
  testOrderId: number;
  testCode: string;
  status: string;
}

export interface SampleCard {
  sampleId: number;
  itemName: string;
  category: string;
  batchNumber: string;
  status: string;
  assignedTests: TestOrderSummary[];
}
