import { apiClient } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";

export interface ChromatographyColumnDto {
  id: number;
  code: string;
  name: string;
  serialNumber?: string | null;
  sectionId: number;
  section?: {
    id?: number;
    sectionId?: number;
    name?: string;
    sectionName?: string;
    code?: string;
    sectionCode?: string;
  } | null;
  isActive: boolean;
  createdByUserId: number;
  createdAt: string;
  lastModifiedByUserId: number;
  lastModifiedAt: string;
  compatibleEquipment: Array<{
    id: number;
    name: string;
    code: string;
    type: string | number;
    location?: string | null;
    vendor?: string | null;
    cdsSoftware?: string | number | null;
    sectionId: number;
  }>;
}

export interface CreateChromatographyColumnRequest {
  code: string;
  name: string;
  serialNumber?: string | null;
  sectionId?: number | null;
  compatibleEquipmentIds?: number[] | null;
}

export interface UpdateChromatographyColumnRequest {
  code: string;
  name: string;
  serialNumber?: string | null;
  isActive?: boolean | null;
  sectionId?: number | null;
  compatibleEquipmentIds?: number[] | null;
}

export const ChromatographyColumnService = {
  getAll: async (activeOnly?: boolean): Promise<ChromatographyColumnDto[]> => {
    const params: Record<string, boolean | string | number> = {};
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    const res = await apiClient.get<ApiResponse<ChromatographyColumnDto[]>>("/masterdata/columns", { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getById: async (id: number): Promise<ChromatographyColumnDto> => {
    const res = await apiClient.get<ApiResponse<ChromatographyColumnDto>>(`/masterdata/columns/${id}`);
    return res.data?.data;
  },

  create: async (req: CreateChromatographyColumnRequest): Promise<ChromatographyColumnDto> => {
    const res = await apiClient.post<ApiResponse<ChromatographyColumnDto>>("/masterdata/columns", req);
    return res.data?.data;
  },

  update: async (id: number, req: UpdateChromatographyColumnRequest): Promise<ChromatographyColumnDto> => {
    const res = await apiClient.put<ApiResponse<ChromatographyColumnDto>>(`/masterdata/columns/${id}`, req);
    return res.data?.data;
  },

  deactivate: async (id: number): Promise<ChromatographyColumnDto> => {
    const res = await apiClient.put<ApiResponse<ChromatographyColumnDto>>(`/masterdata/columns/${id}/deactivate`);
    return res.data?.data;
  }
};
