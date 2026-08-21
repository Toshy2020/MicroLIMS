import { apiClient } from "../../../../services/apiClient";
import type {
  EquipmentDocument,
  EquipmentDocumentType,
  EquipmentStatusHistoryItem
} from "../types/equipmentTypes";

export interface ActiveEquipmentDto {
  id: number;
  code: string;
  instrumentType: string;
  manufacturerName: string;
  location: string;
  status: string;
  calibrationDueDate: string | null;
  setPointTemperature: number | null;
  primaryActivityCategory: string;
  activeItemCount: number;
}

export interface EquipmentActivityDto {
  activityId: number;
  itemName: string;
  itemCode: string;
  activityType: string;
  mediaDescription: string;
  startedOn: string;
  startedBy: string;
  expectedCompletion: string | null;
  completedOn: string | null;
  isActive: boolean;
  entityId: number | null;
  entityType: string | null;
}

export interface HistoricalLocationDto {
  equipmentCode: string;
  equipmentName: string;
  activityType: string;
  startedOn: string;
  completedOn: string | null;
  performedBy: string;
}

export interface WhereIsItResultDto {
  searchTerm: string;
  currentActivity: EquipmentActivityDto | null;
  currentEquipmentCode: string | null;
  currentEquipmentName: string | null;
  history: HistoricalLocationDto[];
}

export const EquipmentInventoryService = {
  getAll: () => apiClient.get("/inventory/equipment").then((r) => r.data.data),
  getById: (id: number) => apiClient.get(`/inventory/equipment/${id}`).then((r) => r.data.data),
  getForPrint: () => apiClient.get("/inventory/equipment/print").then((r) => r.data.data),
  getActiveEquipment: (): Promise<ActiveEquipmentDto[]> => apiClient.get("/inventory/equipment/active").then((r) => r.data.data),
  getActiveActivities: (equipmentId: number): Promise<EquipmentActivityDto[]> =>
    apiClient.get(`/inventory/equipment/${equipmentId}/activities`).then((r) => r.data.data),
  getHistory: (equipmentId: number, params?: { itemCode?: string; fromDate?: string; toDate?: string }): Promise<EquipmentActivityDto[]> =>
    apiClient.get(`/inventory/equipment/${equipmentId}/history`, { params }).then((r) => r.data.data),
  whereIsIt: (query: string): Promise<WhereIsItResultDto> =>
    apiClient.get("/inventory/equipment/where-is-it", { params: { query } }).then((r) => r.data.data),
  create: (payload: any) => apiClient.post("/inventory/equipment", payload).then((r) => r.data.data),
  update: (id: number, payload: any) => apiClient.put(`/inventory/equipment/${id}`, payload).then((r) => r.data.data),

  getStatusHistory: (equipmentId: number): Promise<EquipmentStatusHistoryItem[]> =>
    apiClient.get(`/inventory/equipment/${equipmentId}/status-history`).then((r) => r.data.data),

  getDocuments: (equipmentId: number): Promise<EquipmentDocument[]> =>
    apiClient.get(`/inventory/equipment/${equipmentId}/documents`).then((r) => r.data.data),

  getDocumentContent: (documentId: number, equipmentId: number): Promise<Blob> =>
    apiClient
      .get(`/inventory/equipment-documents/${documentId}/content`, {
        params: { equipmentId },
        responseType: "blob"
      })
      .then((r) => r.data),

  uploadDocument: (equipmentId: number, documentType: EquipmentDocumentType, file: File): Promise<EquipmentDocument> => {
    const fd = new FormData();
    fd.append("documentType", documentType);
    fd.append("file", file);
    return apiClient
      .post(`/inventory/equipment/${equipmentId}/documents`, fd)
      .then((r) => r.data.data);
  },

  supersedeDocument: (
    documentId: number,
    equipmentId: number,
    file: File,
    reason: string
  ): Promise<EquipmentDocument> => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("reason", reason);
    return apiClient
      .post(`/inventory/equipment-documents/${documentId}/supersede`, fd, {
        params: { equipmentId }
      })
      .then((r) => r.data.data);
  },

  voidDocument: (documentId: number, equipmentId: number, reason: string): Promise<EquipmentDocument> =>
    apiClient
      .post(
        `/inventory/equipment-documents/${documentId}/void`,
        { reason },
        { params: { equipmentId } }
      )
      .then((r) => r.data.data)
};
