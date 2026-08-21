import { apiClient } from "../../../../services/apiClient";

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: string | null;
  timestamp?: string;
}

export interface ConfiguredEquipmentSummary {
  id: number;
  name: string;
  code: string;
  type: string | number;
  location?: string | null;
  setPointTemperature?: number | null;
  calibrationDueDate?: string | null;
  equipmentInventoryId?: number | null;
  inventoryStatus?: string | null;
  inventoryLocation?: string | null;
  configuredProgramCount: number;
  serialNumber?: string | null;
  manufacturerName?: string | null;
}

export interface IncubatorSetPointHistory {
  id: number;
  equipmentId: number;
  previousSetPoint: number;
  newSetPoint: number;
  reason: string;
  changedByUserId: number;
  changedByName: string;
  changedAt: string;
}

export interface AutoclaveProgram {
  id: number;
  equipmentId: number;
  autoclaveCode: string;
  autoclaveName: string;
  programCode: string;
  programName: string;
  loadType: string;
  temperature: number;
  cycleTimeMinutes: number;
  isActive: boolean;
  createdByUserId: number;
  createdAt: string;
  lastModifiedByUserId: number;
  lastModifiedAt: string;
}

export interface AutoclaveProgramHistory {
  id: number;
  autoclaveProgramId: number;
  action: string;
  programCode: string;
  previousProgramName: string;
  newProgramName: string;
  previousLoadType: string;
  newLoadType: string;
  previousTemperature: number;
  newTemperature: number;
  previousCycleTimeMinutes: number;
  newCycleTimeMinutes: number;
  previousIsActive: boolean;
  newIsActive: boolean;
  comment: string;
  changedByUserId: number;
  changedByName: string;
  changedAt: string;
}

export interface UpdateIncubatorSetPointRequest {
  newSetPoint: number;
  reason: string;
}

export interface SaveAutoclaveProgramRequest {
  id?: number | null;
  equipmentId: number;
  programCode: string;
  programName: string;
  loadType: string;
  temperature: number;
  cycleTimeMinutes: number;
  isActive: boolean;
  comment?: string | null;
}

export const EquipmentConfigurationService = {
  getConfiguredSummary: async (): Promise<ConfiguredEquipmentSummary[]> => {
    const res = await apiClient.get<ApiResponse<ConfiguredEquipmentSummary[]>>("/masterdata/equipment/configured-summary");
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  linkInventory: async (inventoryId: number): Promise<any> => {
    const res = await apiClient.post<ApiResponse<any>>(`/masterdata/equipment/link-inventory/${inventoryId}`);
    return res.data?.data;
  },

  updateSetPoint: async (id: number, req: UpdateIncubatorSetPointRequest): Promise<any> => {
    const res = await apiClient.put<ApiResponse<any>>(`/masterdata/equipment/${id}/set-point`, req);
    return res.data?.data;
  },

  getSetPointHistory: async (id: number): Promise<IncubatorSetPointHistory[]> => {
    const res = await apiClient.get<ApiResponse<IncubatorSetPointHistory[]>>(`/masterdata/equipment/${id}/set-point-history`);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getAutoclavePrograms: async (id?: number, activeOnly?: boolean): Promise<AutoclaveProgram[]> => {
    const params: Record<string, any> = {};
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    const url = id ? `/masterdata/equipment/${id}/autoclave-programs` : "/masterdata/equipment/autoclave-programs/all";
    const res = await apiClient.get<ApiResponse<AutoclaveProgram[]>>(url, { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  saveAutoclaveProgram: async (equipmentId: number, req: SaveAutoclaveProgramRequest): Promise<AutoclaveProgram> => {
    const url = req.id
      ? `/masterdata/equipment/autoclave-programs/${req.id}`
      : `/masterdata/equipment/${equipmentId}/autoclave-programs`;
    const method = req.id ? apiClient.put : apiClient.post;
    const res = await method<ApiResponse<AutoclaveProgram>>(url, req);
    return res.data?.data;
  },

  setAutoclaveProgramStatus: async (programId: number, isActive: boolean, comment: string): Promise<void> => {
    await apiClient.put(`/masterdata/equipment/autoclave-programs/${programId}/status`, { isActive, comment });
  },

  getAutoclaveProgramHistory: async (programId: number): Promise<AutoclaveProgramHistory[]> => {
    const res = await apiClient.get<ApiResponse<AutoclaveProgramHistory[]>>(`/masterdata/equipment/autoclave-programs/${programId}/history`);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  }
};
