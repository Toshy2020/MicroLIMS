import { apiClient, ifMatch } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";
import type { MaterialUnit } from "../../../inventory/materials/types/materialTypes";

export type MaterialMasterCategory = "Reagent" | "Indicator" | "ReferenceStandard" | "PrimaryStandard";

export interface MaterialMasterEntry {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  // Owner lab: the only lab that can edit the entry; every lab can see and use it.
  sectionId: number;
  sectionName?: string;
  sectionCode?: string;
  // Decided by the server from the user's configurable sections.
  canEdit?: boolean;
  // Any stock lot is linked: the code is frozen and changes only with a reason and signature.
  hasStock?: boolean;
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

// Enforced on create and on code change; older codes are grandfathered.
export const MATERIAL_MASTER_CODE_PREFIX: Record<MaterialMasterCategory, string> = {
  Reagent: "RG-",
  Indicator: "IND-",
  ReferenceStandard: "RS-",
  PrimaryStandard: "PS-"
};

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

  changeCode: async (id: number, code: string, reason: string, password: string): Promise<MaterialMasterEntry> => {
    const res = await apiClient.put<ApiResponse<MaterialMasterEntry>>(`/masterdata/material-masters/${id}/code`, { code, reason, password });
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean): Promise<MaterialMasterEntry> => {
    const res = await apiClient.put<ApiResponse<MaterialMasterEntry>>(`/masterdata/material-masters/${id}/active`, null, {
      params: { value }
    });
    return res.data?.data;
  }
};
