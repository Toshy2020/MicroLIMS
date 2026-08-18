import { apiClient } from "../../../../services/apiClient";
import type {
  EquipmentDocument,
  EquipmentDocumentType,
  EquipmentStatusHistoryItem
} from "../types/equipmentTypes";

export const EquipmentInventoryService = {
  getAll: () => apiClient.get("/inventory/equipment").then((r) => r.data.data),
  getById: (id: number) => apiClient.get(`/inventory/equipment/${id}`).then((r) => r.data.data),
  getForPrint: () => apiClient.get("/inventory/equipment/print").then((r) => r.data.data),
  create: (payload: any) => apiClient.post("/inventory/equipment", payload).then((r) => r.data.data),
  update: (id: number, payload: any) => apiClient.put(`/inventory/equipment/${id}`, payload).then((r) => r.data.data),

  // Status history (immutable, append-only)
  getStatusHistory: (equipmentId: number): Promise<EquipmentStatusHistoryItem[]> =>
    apiClient.get(`/inventory/equipment/${equipmentId}/status-history`).then((r) => r.data.data),

  // Equipment controlled documents
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
