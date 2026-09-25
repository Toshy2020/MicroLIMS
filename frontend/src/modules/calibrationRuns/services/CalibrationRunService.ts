import { apiClient } from "../../../services/apiClient";

export type CalibrationCheckType = "Blank" | "Icv" | "Ccv" | "InternalStandard";
export type CorrelationType = "R" | "RSquared";
export type AnalyteView = "Axial" | "Radial";
export type CalibrationRunStatus = "Active" | "Withdrawn";

export interface CreateCalibrationRunCheckRequest {
  checkType: CalibrationCheckType;
  sequencePosition: number;
  nominalMgPerL: number | null;
  measuredMgPerL: number;
}

export interface CreateCalibrationRunAnalyteRequest {
  testAnalyteId: number;
  correlationValue: number;
  correlationType: CorrelationType;
  numberOfStandards: number;
  lowestStandardMgPerL: number;
  highestStandardMgPerL: number;
  checks?: CreateCalibrationRunCheckRequest[];
}

export interface CreateCalibrationRunRequest {
  testDefinitionId: number;
  equipmentId: number;
  calibrationStandardMaterialId: number;
  icvStandardMaterialId?: number | null;
  calibrationAt: string;
  password?: string;
  comment?: string | null;
  analytes?: CreateCalibrationRunAnalyteRequest[];
}

export interface PreviewCalibrationRunRequest {
  testDefinitionId: number;
  equipmentId: number;
  calibrationStandardMaterialId: number;
  icvStandardMaterialId?: number | null;
  calibrationAt: string;
  comment?: string | null;
  analytes?: CreateCalibrationRunAnalyteRequest[];
}

export interface WithdrawCalibrationRunRequest {
  reason: string;
  password?: string;
}

export interface CalibrationRunFilter {
  testDefinitionId?: number;
  testCode?: string;
  passed?: boolean;
  status?: string;
  runStatus?: CalibrationRunStatus;
  date?: string;
  fromDate?: string;
  toDate?: string;
}

export interface CalibrationRunCheckView {
  id: number;
  calibrationRunAnalyteId: number;
  checkType: CalibrationCheckType;
  sequencePosition: number;
  nominalMgPerL: number | null;
  measuredMgPerL: number;
  recoveryPercent: number | null;
  passed: boolean;
}

export interface CalibrationRunAnalyteView {
  id: number;
  calibrationRunId: number;
  testAnalyteId: number;
  element: string;
  wavelengthNm: number;
  view: AnalyteView;
  correlationValue: number;
  correlationType: CorrelationType;
  numberOfStandards: number;
  lowestStandardMgPerL: number;
  highestStandardMgPerL: number;
  passed: boolean;
  failureReasons: string | null;
  isUsable: boolean;
  checks: CalibrationRunCheckView[];
}

export interface CalibrationRunDocumentView {
  id: number;
  calibrationRunId: number;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  contentSha256: string;
  uploadedByUserId: number;
  uploadedByName: string | null;
  uploadedAt: string;
}

export interface CalibrationRunView {
  id: number;
  code: string;
  testDefinitionId: number;
  testCode: string | null;
  testDisplayName: string | null;
  methodAbbreviation: string | null;
  sectionId: number;
  sectionName: string | null;
  equipmentId: number;
  equipmentCode: string | null;
  equipmentName: string | null;
  calibrationStandardMaterialId: number;
  calibrationStandardMaterialName: string | null;
  calibrationStandardBatchNumber: string | null;
  calibrationStandardExpiryDate: string | null;
  icvStandardMaterialId: number | null;
  icvStandardMaterialName: string | null;
  icvStandardBatchNumber: string | null;
  icvStandardExpiryDate: string | null;
  calibrationAt: string;
  performedByUserId: number;
  performedByName: string | null;
  performedAt: string;
  signatureId: number;
  comment: string | null;
  analytesPassed: number;
  analytesTotal: number;
  passed: boolean;
  status: CalibrationRunStatus;
  withdrawnAt: string | null;
  withdrawnByUserId: number | null;
  withdrawnByName: string | null;
  withdrawalReason: string | null;
  withdrawalSignatureId: number | null;
  document: CalibrationRunDocumentView | null;
  analytes: CalibrationRunAnalyteView[];
}

export interface CalibrationRunReportDetailsDto {
  calibrationEntryMode: string | null;
  calMinCorrelation: number | null;
  calCorrelationType: CorrelationType | null;
  calMinStandards: number | null;
  calCheckRecoveryLowPercent: number | null;
  calCheckRecoveryHighPercent: number | null;
  calBlankMax: number | null;
  calIsRecoveryLowPercent: number | null;
  calIsRecoveryHighPercent: number | null;
  calRequireBlank: boolean | null;
  calRequireIcv: boolean | null;
  calRequireCcv: boolean | null;
  calRequireInternalStandard: boolean | null;
  reportedConcentrationBasis: string | null;
  calMaxRunAgeHours: number | null;
  calInstrumentType: "IcpOes" | "Aas" | null;
  calStandardLevelsMgPerL: string | null;
  calibrationAt: string;
  status: CalibrationRunStatus;
  withdrawnAt: string | null;
  withdrawnByName: string | null;
  withdrawalReason: string | null;
  withdrawalSignature?: { userFullNameSnapshot?: string; signedAt?: string; meaning?: string } | null;
  equipmentVendor: string | null;
  cdsSoftware: string | null;
  signature?: { userFullNameSnapshot?: string; signedAt?: string; meaning?: string } | null;
  document: CalibrationRunDocumentView | null;
  analytes: CalibrationRunAnalyteView[];
}

export interface CalibrationRunReportResponse {
  run: CalibrationRunView;
  details: CalibrationRunReportDetailsDto;
}

export interface CalibrationRunPreviewCheckResult {
  checkType: CalibrationCheckType;
  sequencePosition: number;
  nominalMgPerL: number | null;
  measuredMgPerL: number;
  recoveryPercent: number | null;
  passed: boolean;
}

export interface CalibrationRunPreviewAnalyteResult {
  testAnalyteId: number;
  element: string;
  wavelengthNm: number;
  view: AnalyteView;
  correlationValue: number;
  correlationType: CorrelationType;
  numberOfStandards: number;
  lowestStandardMgPerL: number;
  highestStandardMgPerL: number;
  passed: boolean;
  failureReasons: string | null;
  isUsable: boolean;
  checks: CalibrationRunPreviewCheckResult[];
}

export interface CalibrationRunPreviewResult {
  testDefinitionId: number;
  equipmentId: number;
  calibrationStandardMaterialId: number;
  icvStandardMaterialId: number | null;
  calibrationAt: string;
  standardsExpiredOrMissing: boolean;
  standardsExpiryFailureReason: string | null;
  analytesPassed: number;
  analytesTotal: number;
  passed: boolean;
  analytes: CalibrationRunPreviewAnalyteResult[];
}

export const CalibrationRunService = {
  getAll: (filter?: CalibrationRunFilter): Promise<CalibrationRunView[]> =>
    apiClient.get("/calibration-runs", { params: filter ?? {} }).then((r) => r.data.data),

  getById: (id: number): Promise<CalibrationRunView> =>
    apiClient.get(`/calibration-runs/${id}`).then((r) => r.data.data),

  preview: (payload: PreviewCalibrationRunRequest): Promise<CalibrationRunPreviewResult> =>
    apiClient.post("/calibration-runs/preview", payload).then((r) => r.data.data),

  create: (payload: CreateCalibrationRunRequest, reportFile: File): Promise<CalibrationRunView> => {
    const formData = new FormData();
    formData.append("payload", JSON.stringify(payload));
    formData.append("report", reportFile);
    return apiClient
      .post("/calibration-runs", formData, {
        headers: { "Content-Type": "multipart/form-data" }
      })
      .then((r) => r.data.data);
  },

  withdraw: (id: number, request: WithdrawCalibrationRunRequest): Promise<CalibrationRunView> =>
    apiClient.post(`/calibration-runs/${id}/withdraw`, request).then((r) => r.data.data),

  getReport: (id: number): Promise<CalibrationRunReportResponse> =>
    apiClient.get(`/calibration-runs/${id}/report`).then((r) => r.data.data),

  downloadDocument: async (id: number, filename?: string): Promise<void> => {
    const response = await apiClient.get(`/calibration-runs/${id}/document`, {
      responseType: "blob"
    });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", filename || `calibration_report_${id}.pdf`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  }
};
