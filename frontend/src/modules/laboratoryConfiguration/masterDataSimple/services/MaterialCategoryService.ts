import { apiClient } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";
import type { MaterialUnit } from "../../../inventory/materials/types/materialTypes";

// A category of the Other Materials group. It carries the rules the server applies to every
// master entry and stock lot of the category.
export interface MaterialCategory {
  id: number;
  version?: number;
  name: string;
  codePrefix: string;
  defaultUnit: MaterialUnit;
  coaRequired: boolean;
  expiryRequired: boolean;
  quantityTracked: boolean;
  defaultStorageCondition?: string | null;
  approvalBeforeUse: boolean;
  labelRequired: boolean;
  isActive: boolean;
  // Empty = available to every section.
  sectionIds: number[];
  // Any master entry uses the category: the code prefix is frozen.
  hasEntries: boolean;
}

export interface SaveMaterialCategoryRequest {
  name: string;
  codePrefix: string;
  defaultUnit: MaterialUnit;
  coaRequired: boolean;
  expiryRequired: boolean;
  quantityTracked: boolean;
  defaultStorageCondition: string | null;
  approvalBeforeUse: boolean;
  labelRequired: boolean;
  // null = every section.
  sectionIds: number[] | null;
  reason: string;
  password: string;
}

export const MaterialCategoryService = {
  getAll: async (activeOnly?: boolean, sectionId?: number): Promise<MaterialCategory[]> => {
    const params: Record<string, boolean | number> = {};
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    if (sectionId !== undefined) params.sectionId = sectionId;
    const res = await apiClient.get<ApiResponse<MaterialCategory[]>>("/masterdata/material-categories", { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  create: async (req: SaveMaterialCategoryRequest): Promise<MaterialCategory> => {
    const res = await apiClient.post<ApiResponse<MaterialCategory>>("/masterdata/material-categories", req);
    return res.data?.data;
  },

  update: async (id: number, req: SaveMaterialCategoryRequest): Promise<MaterialCategory> => {
    const res = await apiClient.put<ApiResponse<MaterialCategory>>(`/masterdata/material-categories/${id}`, req);
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean): Promise<MaterialCategory> => {
    const res = await apiClient.put<ApiResponse<MaterialCategory>>(`/masterdata/material-categories/${id}/active`, null, {
      params: { value }
    });
    return res.data?.data;
  }
};
