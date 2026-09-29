import { apiClient, ifMatch } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";
import type { MaterialUnit } from "../../../inventory/materials/types/materialTypes";

export type MaterialMasterCategory = "Reagent" | "Indicator" | "ReferenceStandard";

export interface MaterialMasterEntry {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  sectionId: number;
  sectionName?: string;
  code: string;
  name: string;
  category: MaterialMasterCategory;
  grade?: string | null;
  source?: string | null;
  baseUnit: MaterialUnit;
  isActive: boolean;
  workingConcentration?: string | null;
  solvent?: string | null;
  transitionRangeFrom?: number | null;
  transitionRangeTo?: number | null;
  colourChange?: string | null;
  indicatorUse?: string | null;
  createdAt?: string;
  lastModifiedAt?: string;
}

export interface SaveMaterialMasterEntryRequest {
  code: string;
  name: string;
  category: MaterialMasterCategory;
  grade?: string | null;
  source?: string | null;
  baseUnit: MaterialUnit;
  sectionId?: number | null;
  workingConcentration?: string | null;
  solvent?: string | null;
  transitionRangeFrom?: number | null;
  transitionRangeTo?: number | null;
  colourChange?: string | null;
  indicatorUse?: string | null;
}

export const MaterialMasterService = {
  getAll: async (category?: MaterialMasterCategory, activeOnly?: boolean): Promise<MaterialMasterEntry[]> => {
    const params: Record<string, boolean | string> = {};
    if (category) params.category = category;
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    const res = await apiClient.get<ApiResponse<MaterialMasterEntry[]>>("/masterdata/material-masters", { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getById: async (id: number): Promise<MaterialMasterEntry> => {
    const res = await apiClient.get<ApiResponse<MaterialMasterEntry>>(`/masterdata/material-masters/${id}`);
    return res.data?.data;
  },

  create: async (req: SaveMaterialMasterEntryRequest): Promise<MaterialMasterEntry> => {
    const res = await apiClient.post<ApiResponse<MaterialMasterEntry>>("/masterdata/material-masters", req);
    return res.data?.data;
  },

  update: async (id: number, req: SaveMaterialMasterEntryRequest, version?: number): Promise<MaterialMasterEntry> => {
    const res = await apiClient.put<ApiResponse<MaterialMasterEntry>>(`/masterdata/material-masters/${id}`, req, ifMatch(version));
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean): Promise<MaterialMasterEntry> => {
    const res = await apiClient.put<ApiResponse<MaterialMasterEntry>>(`/masterdata/material-masters/${id}/active`, null, {
      params: { value }
    });
    return res.data?.data;
  }
};
