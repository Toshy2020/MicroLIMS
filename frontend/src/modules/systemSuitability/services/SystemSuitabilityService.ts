import { apiClient } from "../../../services/apiClient";

// Mirrors backend SystemSuitabilityRunView (SystemSuitabilityController.cs).
export interface SystemSuitabilityRun {
  id: number;
  code: string;
  passed: boolean;
  failureReasons?: string | null;
  testDefinitionId: number;
  testCode?: string | null;
  testName?: string | null;
  methodAbbreviation?: string | null;
  sectionId: number;
  sectionName?: string | null;
  equipmentId: number;
  equipmentCode?: string | null;
  equipmentName?: string | null;
  chromatographyColumnId: number;
  columnCode?: string | null;
  columnName?: string | null;
  referenceStandardMaterialId: number;
  referenceStandardName?: string | null;
  referenceStandardBatch?: string | null;
  standardPurityPercent: number;
  standardWeightMg: number;
  standardDilution: number;
  standardMeanArea: number;
  rsdPercent?: number | null;
  resolution?: number | null;
  tailingFactor?: number | null;
  theoreticalPlates?: number | null;
  performedByUserId: number;
  performedByName?: string | null;
  performedAt: string;
  comment?: string | null;
}

export interface CreateSystemSuitabilityRunPayload {
  testDefinitionId: number;
  equipmentId: number;
  chromatographyColumnId: number;
  referenceStandardMaterialId: number;
  standardWeightMg: number;
  standardDilution: number;
  standardMeanArea: number;
  rsdPercent: number | null;
  resolution: number | null;
  tailingFactor: number | null;
  theoreticalPlates: number | null;
  password: string;
  comment?: string | null;
}

export const SystemSuitabilityService = {
  getAll: (filter?: { testDefinitionId?: number; passed?: boolean }): Promise<SystemSuitabilityRun[]> =>
    apiClient.get("/system-suitability-runs", { params: filter ?? {} }).then((r) => r.data.data),
  getById: (id: number): Promise<SystemSuitabilityRun> =>
    apiClient.get(`/system-suitability-runs/${id}`).then((r) => r.data.data),
  create: (payload: CreateSystemSuitabilityRunPayload): Promise<SystemSuitabilityRun> =>
    apiClient.post("/system-suitability-runs", payload).then((r) => r.data.data),
  // Passed runs of the order's own method and section.
  getSelectableForTestOrder: (testOrderId: number): Promise<SystemSuitabilityRun[]> =>
    apiClient.get("/system-suitability-runs/selectable", { params: { testOrderId } }).then((r) => r.data.data),
  // The run this test order is linked to, or null.
  getLinkedForTestOrder: (testOrderId: number): Promise<SystemSuitabilityRun | null> =>
    apiClient.get("/system-suitability-runs/linked", { params: { testOrderId } }).then((r) => r.data.data ?? null),
  linkTestOrders: (runId: number, testOrderIds: number[]): Promise<void> =>
    apiClient.post(`/system-suitability-runs/${runId}/link`, { testOrderIds }).then(() => undefined)
};
