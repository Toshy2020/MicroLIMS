import { apiClient, ifMatch, withChangeReason } from "../../../../services/apiClient";

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: string | null;
  timestamp?: string;
}

export type EquipmentType =
  | "Incubator"
  | "Autoclave"
  | "LafCabinet"
  | "BiologicalSafetyCabinet"
  | "WaterBath"
  | "Other"
  | "Hplc"
  | "PhMeter"
  | "Balance"
  | "IcpOes"
  | "UvVis"
  | "DissolutionTester"
  | "DisintegrationTester"
  | "KarlFischer"
  | "Titrator"
  | "Viscometer"
  | "Refractometer"
  | "Polarimeter"
  | "ConductivityMeter"
  | "MeltingPoint"
  | "Oven"
  | "Furnace"
  | "Gc"
  | "Aas"
  | "DigestionMicrowave"
  | "Caliper";

export type CdsSoftware =
  | "ShimadzuLabSolutions"
  | "AgilentOpenLab"
  | "WatersEmpower3"
  | "PerkinElmerSyngistix";


// Mirrors CreateEquipmentRequest in MasterDataRequests.cs (enums travel as names).
export interface CreateEquipmentRequest {
  name: string;
  code: string;
  type: EquipmentType;
  location?: string | null;
  setPointTemperature?: number | null;
  calibrationDueDate?: string | null;
  vendor?: string | null;
  cdsSoftware?: CdsSoftware | null;
  connectionSettings?: string | null;
  sectionId?: number | null;
  equipmentInventoryId?: number | null;
}

// Mirrors UpdateEquipmentRequest in MasterDataRequests.cs (enums travel as names).
export interface UpdateEquipmentRequest {
  name: string;
  code: string;
  type: EquipmentType;
  location?: string | null;
  setPointTemperature?: number | null;
  calibrationDueDate?: string | null;
  vendor?: string | null;
  cdsSoftware?: CdsSoftware | null;
  connectionSettings?: string | null;
  sectionId?: number | null;
  equipmentInventoryId?: number | null;
}

export interface EquipmentResponse {
  id: number;
  version?: number;
  name: string;
  code: string;
  type: string;
  location: string | null;
  setPointTemperature: number | null;
  calibrationDueDate: string | null;
  vendor: string | null;
  cdsSoftware: string | null;
  connectionSettings: string | null;
  sectionId: number | null;
  section?: ConfiguredEquipmentSummary["section"];
  equipmentInventoryId?: number | null;
}

export interface ConfiguredEquipmentSummary {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
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
  vendor?: string | null;
  cdsSoftware?: string | null;
  connectionSettings?: string | null;
  sectionId?: number | null;
  section?: {
    id?: number;
    sectionId?: number;
    name?: string;
    sectionName?: string;
    code?: string;
    sectionCode?: string;
  } | null;
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
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
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

  linkInventory: async (inventoryId: number): Promise<void> => {
    await apiClient.post(`/masterdata/equipment/link-inventory/${inventoryId}`);
  },

  updateSetPoint: async (id: number, req: UpdateIncubatorSetPointRequest): Promise<void> => {
    await apiClient.put(`/masterdata/equipment/${id}/set-point`, req);
  },

  getSetPointHistory: async (id: number): Promise<IncubatorSetPointHistory[]> => {
    const res = await apiClient.get<ApiResponse<IncubatorSetPointHistory[]>>(`/masterdata/equipment/${id}/set-point-history`);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getAutoclavePrograms: async (id?: number, activeOnly?: boolean): Promise<AutoclaveProgram[]> => {
    const params: Record<string, boolean> = {};
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    const url = id ? `/masterdata/equipment/${id}/autoclave-programs` : "/masterdata/equipment/autoclave-programs/all";
    const res = await apiClient.get<ApiResponse<AutoclaveProgram[]>>(url, { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  saveAutoclaveProgram: async (equipmentId: number, req: SaveAutoclaveProgramRequest, version?: number): Promise<AutoclaveProgram> => {
    const url = req.id
      ? `/masterdata/equipment/autoclave-programs/${req.id}`
      : `/masterdata/equipment/${equipmentId}/autoclave-programs`;
    const method = req.id ? apiClient.put : apiClient.post;
    const res = await method<ApiResponse<AutoclaveProgram>>(url, req, ifMatch(version));
    return res.data?.data;
  },

  setAutoclaveProgramStatus: async (programId: number, isActive: boolean, comment: string): Promise<void> => {
    await apiClient.put(`/masterdata/equipment/autoclave-programs/${programId}/status`, { isActive, comment });
  },

  getAutoclaveProgramHistory: async (programId: number): Promise<AutoclaveProgramHistory[]> => {
    const res = await apiClient.get<ApiResponse<AutoclaveProgramHistory[]>>(`/masterdata/equipment/autoclave-programs/${programId}/history`);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getEquipmentList: async (type?: string): Promise<EquipmentResponse[]> => {
    const res = await apiClient.get<ApiResponse<EquipmentResponse[]>>("/masterdata/equipment", { params: type ? { type } : {} });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getEquipmentById: async (id: number): Promise<EquipmentResponse> => {
    const res = await apiClient.get<ApiResponse<EquipmentResponse>>(`/masterdata/equipment/${id}`);
    return res.data?.data;
  },

  createEquipment: async (req: CreateEquipmentRequest): Promise<EquipmentResponse> => {
    const res = await apiClient.post<ApiResponse<EquipmentResponse>>("/masterdata/equipment", req);
    return res.data?.data;
  },

  createEquipmentBatch: async (req: CreateEquipmentRequest[]): Promise<EquipmentResponse[]> => {
    const res = await apiClient.post<ApiResponse<EquipmentResponse[]>>("/masterdata/equipment/batch", req);
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  updateEquipment: async (id: number, req: UpdateEquipmentRequest, version?: number, reason?: string): Promise<EquipmentResponse> => {
    const res = await apiClient.put<ApiResponse<EquipmentResponse>>(`/masterdata/equipment/${id}`, req, withChangeReason(version, reason));
    return res.data?.data;
  }
};
