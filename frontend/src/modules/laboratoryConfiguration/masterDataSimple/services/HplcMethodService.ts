import { apiClient, ifMatch } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";

export type ElutionMode = "Isocratic" | "Gradient";
export type HplcTechnique = "Hplc" | "Gc";
export type HplcResultMode = "Assay" | "ResidualSolvents";
export type CarrierGas = "Helium" | "Nitrogen" | "Hydrogen";
export type HplcDetectorType =
  | "UV"
  | "PDA"
  | "FLD"
  | "RI"
  | "ELSD"
  | "Other"
  | "Fid"
  | "Tcd"
  | "Ecd"
  | "Ms";

export interface HplcMobilePhaseInput {
  channel: string;
  solutionMasterId: number;
  ratioPercent?: number | null;
}

export interface HplcGradientStepInput {
  timeMin: number;
  percentA: number;
  percentB: number;
  percentC: number;
  percentD: number;
}

export interface GcOvenStepInput {
  rateCPerMin?: number | null;
  temperatureC: number;
  holdMin: number;
}

export interface HplcAnalyteInput {
  id?: number | null;
  name: string;
  wavelengthNm?: number | null;
  standardEntryId: number;
  theoreticalWeightStdMg: number;
  theoreticalWeightTestMg: number;
  standardDilution?: number | null;
  standardInjections: number;
  sstMaxRsdPercent?: number | null;
  sstMinResolution?: number | null;
  sstMaxTailingFactor?: number | null;
  sstMinTheoreticalPlates?: number | null;
  sstMinRetentionFactor?: number | null;
  sstMinSignalToNoise?: number | null;
  sstMinPeakToValley?: number | null;
  standardConcentrationUgPerMl?: number | null;
}

export interface SaveHplcMethodRequest {
  name: string;
  abbreviation: string;
  effectiveDate: string;
  columnDesignation: string;
  columnLengthMm: number;
  columnInternalDiameterMm: number;
  particleSizeUm?: number | null;
  columnTemperatureC?: number | null;
  elutionMode: ElutionMode;
  flowRateMlPerMin: number;
  detectorType: HplcDetectorType;
  injectionVolumeUl: number;
  runTimeMin: number;
  diluentSolutionId: number;
  mobilePhases: HplcMobilePhaseInput[];
  gradientSteps: HplcGradientStepInput[];
  analytes: HplcAnalyteInput[];
  columnBrand?: string | null;
  columnPartNumber?: string | null;
  equilibrationMin?: number | null;
  sectionId?: number | null;
  reason?: string | null;
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
  filmThicknessUm?: number | null;
  carrierGas?: CarrierGas | null;
  splitRatio?: number | null;
  inletTemperatureC?: number | null;
  detectorTemperatureC?: number | null;
  ovenSteps?: GcOvenStepInput[];
  headspaceEnabled?: boolean;
  headspaceEquilibrationTemperatureC?: number | null;
  headspaceEquilibrationMin?: number | null;
  headspaceTransferLineTemperatureC?: number | null;
  sampleSolutionVolumeMl?: number | null;
}

export interface HplcMethodListItem {
  id: number;
  name: string;
  abbreviation: string;
  isActive: boolean;
  analyteCount: number;
  sectionName: string;
  lastModifiedAt: string;
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
}

export interface HplcMethodMobilePhaseResponse {
  id: number;
  channel: string;
  solutionMasterId: number;
  solutionMasterName: string;
  ratioPercent?: number | null;
}

export interface HplcMethodGradientStepResponse {
  id: number;
  timeMin: number;
  percentA: number;
  percentB: number;
  percentC: number;
  percentD: number;
}

export interface HplcMethodOvenStepResponse {
  id: number;
  stepNo: number;
  rateCPerMin?: number | null;
  temperatureC: number;
  holdMin: number;
}

export interface HplcMethodAnalyteResponse {
  id: number;
  displayOrder: number;
  name: string;
  wavelengthNm?: number | null;
  standardEntryId: number;
  standardEntryCode: string;
  theoreticalWeightStdMg: number;
  theoreticalWeightTestMg: number;
  standardDilution?: number | null;
  standardInjections: number;
  sstMaxRsdPercent?: number | null;
  sstMinResolution?: number | null;
  sstMaxTailingFactor?: number | null;
  sstMinTheoreticalPlates?: number | null;
  sstMinRetentionFactor?: number | null;
  sstMinSignalToNoise?: number | null;
  sstMinPeakToValley?: number | null;
  standardConcentrationUgPerMl?: number | null;
}

export interface HplcMethodResponse {
  id: number;
  version?: number;
  sectionId: number;
  sectionName?: string;
  name: string;
  abbreviation: string;
  effectiveDate: string;
  isActive: boolean;
  columnDesignation: string;
  columnLengthMm: number;
  columnInternalDiameterMm: number;
  particleSizeUm?: number | null;
  columnBrand?: string | null;
  columnPartNumber?: string | null;
  columnTemperatureC?: number | null;
  elutionMode: ElutionMode;
  equilibrationMin?: number | null;
  flowRateMlPerMin: number;
  detectorType: HplcDetectorType;
  injectionVolumeUl: number;
  runTimeMin: number;
  diluentSolutionId: number;
  diluentSolutionName?: string | null;
  mobilePhases: HplcMethodMobilePhaseResponse[];
  gradientSteps: HplcMethodGradientStepResponse[];
  analytes: HplcMethodAnalyteResponse[];
  createdByUserId?: number;
  createdAt?: string;
  lastModifiedByUserId?: number;
  lastModifiedAt?: string;
  technique?: HplcTechnique;
  resultMode?: HplcResultMode;
  filmThicknessUm?: number | null;
  carrierGas?: CarrierGas | null;
  splitRatio?: number | null;
  inletTemperatureC?: number | null;
  detectorTemperatureC?: number | null;
  ovenSteps?: HplcMethodOvenStepResponse[];
  headspaceEnabled?: boolean;
  headspaceEquilibrationTemperatureC?: number | null;
  headspaceEquilibrationMin?: number | null;
  headspaceTransferLineTemperatureC?: number | null;
  sampleSolutionVolumeMl?: number | null;
}

export interface HplcMethodHistoryEntry {
  at: string;
  userName: string;
  action: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
}

export const HplcMethodService = {
  getAll: async (activeOnly?: boolean, technique?: HplcTechnique): Promise<HplcMethodListItem[]> => {
    const params: Record<string, boolean | string> = {};
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    if (technique !== undefined) params.technique = technique;
    const res = await apiClient.get<ApiResponse<HplcMethodListItem[]>>("/masterdata/hplc-methods", { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getById: async (id: number): Promise<HplcMethodResponse> => {
    const res = await apiClient.get<ApiResponse<HplcMethodResponse>>(`/masterdata/hplc-methods/${id}`);
    return res.data?.data;
  },

  getHistory: async (id: number): Promise<HplcMethodHistoryEntry[]> => {
    const res = await apiClient.get<ApiResponse<HplcMethodHistoryEntry[]>>(`/masterdata/hplc-methods/${id}/history`);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  create: async (req: SaveHplcMethodRequest): Promise<HplcMethodResponse> => {
    const res = await apiClient.post<ApiResponse<HplcMethodResponse>>("/masterdata/hplc-methods", req);
    return res.data?.data;
  },

  update: async (id: number, req: SaveHplcMethodRequest, version?: number): Promise<HplcMethodResponse> => {
    const res = await apiClient.put<ApiResponse<HplcMethodResponse>>(
      `/masterdata/hplc-methods/${id}`,
      req,
      ifMatch(version)
    );
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean, reason: string): Promise<HplcMethodResponse> => {
    const res = await apiClient.put<ApiResponse<HplcMethodResponse>>(
      `/masterdata/hplc-methods/${id}/active`,
      { reason },
      { params: { value } }
    );
    return res.data?.data;
  }
};
