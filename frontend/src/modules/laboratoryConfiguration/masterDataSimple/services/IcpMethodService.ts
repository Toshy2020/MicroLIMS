import { apiClient, ifMatch } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";
import type { HplcMethodHistoryEntry } from "./HplcMethodService";

// ICP-OES method master (spec 2026-10-03 §4.1). Mirrors HplcMethodService.
export type IcpMethodMode = "MineralAssay" | "ElementalImpurities";
export type IcpAnalyteView = "Axial" | "Radial";

export interface IcpElementInput {
  id?: number | null;
  symbol: string;
  wavelengthNm: number;
  view: IcpAnalyteView;
  conversionFactor: number;
}

export interface IcpMethodElementResponse {
  id: number;
  displayOrder: number;
  symbol: string;
  wavelengthNm: number;
  view: IcpAnalyteView;
  conversionFactor: number;
}

interface IcpMethodFields {
  name: string;
  abbreviation: string;
  effectiveDate: string;
  mode: IcpMethodMode;
  standardLevelsMgPerL: string;
  calibrationStandardEntryId: number;
  minCorrelation: number;
  sampleVolumeMl: number;
  dilutionFactor: number;
  maxCalibrationAgeHours: number;
  requireBlank: boolean;
  blankMaxMgPerL?: number | null;
  requireIcv: boolean;
  icvStandardEntryId?: number | null;
  icvNominalMgPerL?: number | null;
  icvRecoveryLowPercent?: number | null;
  icvRecoveryHighPercent?: number | null;
  requireCcv: boolean;
  ccvNominalMgPerL?: number | null;
  ccvRecoveryLowPercent?: number | null;
  ccvRecoveryHighPercent?: number | null;
}

export interface SaveIcpMethodRequest extends IcpMethodFields {
  elements: IcpElementInput[];
  sectionId?: number | null;
  reason?: string | null; // required on update
}

export interface IcpMethodResponse extends IcpMethodFields {
  id: number;
  version: number;
  sectionId: number;
  sectionName?: string | null;
  isActive: boolean;
  calibrationStandardEntryCode?: string | null;
  icvStandardEntryCode?: string | null;
  elements: IcpMethodElementResponse[];
  createdAt: string;
  lastModifiedAt: string;
}

export interface IcpMethodListItem {
  id: number;
  name: string;
  abbreviation: string;
  mode: IcpMethodMode;
  isActive: boolean;
  elementCount: number;
  sectionName: string;
  lastModifiedAt: string;
}

export type IcpMethodHistoryEntry = HplcMethodHistoryEntry;

const BASE = "/masterdata/icp-methods";

export const IcpMethodService = {
  getAll: async (activeOnly?: boolean): Promise<IcpMethodListItem[]> => {
    const params = activeOnly !== undefined ? { activeOnly } : {};
    const res = await apiClient.get<ApiResponse<IcpMethodListItem[]>>(BASE, { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getById: async (id: number): Promise<IcpMethodResponse> => {
    const res = await apiClient.get<ApiResponse<IcpMethodResponse>>(`${BASE}/${id}`);
    return res.data?.data;
  },

  getHistory: async (id: number): Promise<IcpMethodHistoryEntry[]> => {
    const res = await apiClient.get<ApiResponse<IcpMethodHistoryEntry[]>>(`${BASE}/${id}/history`);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  create: async (req: SaveIcpMethodRequest): Promise<IcpMethodResponse> => {
    const res = await apiClient.post<ApiResponse<IcpMethodResponse>>(BASE, req);
    return res.data?.data;
  },

  update: async (id: number, req: SaveIcpMethodRequest, version?: number): Promise<IcpMethodResponse> => {
    const res = await apiClient.put<ApiResponse<IcpMethodResponse>>(`${BASE}/${id}`, req, ifMatch(version));
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean, reason: string): Promise<IcpMethodResponse> => {
    const res = await apiClient.put<ApiResponse<IcpMethodResponse>>(`${BASE}/${id}/active`, { reason }, { params: { value } });
    return res.data?.data;
  }
};
