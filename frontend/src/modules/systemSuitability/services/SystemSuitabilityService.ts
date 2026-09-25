import { apiClient } from "../../../services/apiClient";
import type { SignatureLike } from "../../testingWorkspace/reportPrimitives";

// Mirrors backend SystemSuitabilityStandardResponseDto - one replicate
// standard response (peak area for HPLC, titre in mL for titration).
export interface SystemSuitabilityStandardResponseDto {
  id: number;
  index: number;
  response: number;
}

// Mirrors backend SystemSuitabilityRunAnalyteView (SystemSuitabilityDtos.cs) -
// one row per analyte for a StandardComparison run.
export interface SystemSuitabilityRunAnalyteView {
  id: number;
  systemSuitabilityRunId: number;
  testAnalyteId: number;
  analyteName: string;
  wavelengthNm: number;
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
  passed: boolean;
  failureReasons?: string | null;
  // SC-5a: Th.Wt.std, MC, weigh-in deviation/justification, computed RSD from
  // replicate responses, and (titration only) the blank titre. See backend
  // SystemSuitabilityService.CreateAsync / StandardWeighInTolerancePercent.
  theoreticalWeightMg?: number | null;
  moisturePercent?: number | null;
  standardWeighInDeviationPercent?: number | null;
  standardWeighInOutOfWindow?: boolean;
  weighInJustification?: string | null;
  computedRsdPercent?: number | null;
  responses?: SystemSuitabilityStandardResponseDto[] | null;
  blankTitreMl?: number | null;
}

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
  // Null for a titration run (SC-5a) - titration doesn't use a column.
  chromatographyColumnId: number | null;
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
  // StandardComparison only - one row per active analyte. Null/empty
  // for non-analyte-based runs; the run-level standard fields above are
  // then just the first analyte's snapshot and not meaningful.
  analytes?: SystemSuitabilityRunAnalyteView[] | null;
  theoreticalWeightMg?: number | null;
  moisturePercent?: number | null;
  standardWeighInDeviationPercent?: number | null;
  standardWeighInOutOfWindow?: boolean;
  weighInJustification?: string | null;
  computedRsdPercent?: number | null;
}

// One analyte's standard + CDS values for CreateSystemSuitabilityRunPayload.analytes.
export interface CreateSystemSuitabilityRunAnalytePayload {
  testAnalyteId: number;
  referenceStandardMaterialId: number;
  standardWeightMg: number;
  standardDilution: number;
  standardMeanArea: number;
  rsdPercent?: number | null;
  resolution?: number | null;
  tailingFactor?: number | null;
  theoreticalPlates?: number | null;
  // SC-5a additions - see backend CreateSystemSuitabilityRunAnalyteRequest.
  theoreticalWeightMg?: number | null;
  moisturePercent?: number | null;
  weighInJustification?: string | null;
  // Standard replicate responses (peak areas or titres). When supplied the
  // backend computes mean + RSD itself and that RSD drives pass/fail.
  responses?: number[] | null;
  // Titration only.
  blankTitreMl?: number | null;
}

export interface CreateSystemSuitabilityRunPayload {
  testDefinitionId: number;
  equipmentId: number;
  // Null for a titration run - titration runs don't use a chromatography column.
  chromatographyColumnId: number | null;
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
  // StandardComparison only - one row per active analyte of the test, exactly
  // once each. The run-level standard fields above are then ignored by the
  // caller's UI but still required non-null by the backend request type.
  analytes?: CreateSystemSuitabilityRunAnalytePayload[];
}

export interface SuitabilityRunLinkedTest {
  testOrderId: number;
  sampleId: number;
  sampleReferenceNumber: string;
  itemName: string | null;
  batchNumber: string | null;
  testCode: string;
  reportedResult: string | null;
  resultStatus: string | null;
  resultEnteredAt: string | null;
}

// Mirrors backend SuitabilityRunReportAnalyteDto - one row per vitamin/
// analyte, result vs its own criterion (null criterion = not checked).
export interface SuitabilityRunReportAnalyteDto {
  testAnalyteId: number;
  analyteName: string;
  wavelengthNm: number;
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
  sstMaxRsdPercent?: number | null;
  sstMinResolution?: number | null;
  sstMaxTailingFactor?: number | null;
  sstMinTheoreticalPlates?: number | null;
  passed: boolean;
  failureReasons?: string | null;
  theoreticalWeightMg?: number | null;
  moisturePercent?: number | null;
  standardWeighInDeviationPercent?: number | null;
  standardWeighInOutOfWindow?: boolean;
  weighInJustification?: string | null;
  computedRsdPercent?: number | null;
  responses?: SystemSuitabilityStandardResponseDto[] | null;
  blankTitreMl?: number | null;
}

// Mirrors backend SuitabilityRunReportDetailsDto + the run view.
export interface SuitabilityRunReport {
  run: SystemSuitabilityRun;
  details: {
    sstMaxRsdPercent: number | null;
    sstMinResolution: number | null;
    sstMaxTailingFactor: number | null;
    sstMinTheoreticalPlates: number | null;
    equipmentVendor: string | null;
    cdsSoftware: string | null;
    columnSerialNumber: string | null;
    signature: SignatureLike | null;
    linkedTests: SuitabilityRunLinkedTest[];
    // StandardComparison only - one row per analyte snapshot on the run.
    analytes?: SuitabilityRunReportAnalyteDto[] | null;
  };
}

export const SystemSuitabilityService = {
  getReport: (id: number): Promise<SuitabilityRunReport> =>
    apiClient.get(`/system-suitability-runs/${id}/report`).then((r) => r.data.data),
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
