import { apiClient } from "../../../../services/apiClient";
import type { MaterialDocumentType } from "../types/materialTypes";

export interface SaveMaterialPayload {
  materialType: string;
  materialName: string;
  manufacturerName: string;
  batchNumber: string;
  receivingDate: string;
  expiryDate: string | null;
  code: string | null;
  location: string;
  quantityReceived: number;
  unit: string;
  minimumStockLevel: number | null;
  atccNumber: string | null;
  organismId: number | null;
  mediaProductId: number | null;
  sectionId?: number | null;
  purity?: number | null;
}

export const MaterialService = {
  // ---- Materials Stock ----
  getAll: (materialType?: string) =>
    apiClient.get("/inventory/materials", { params: materialType ? { type: materialType } : {} }).then((r) => r.data.data),
  getUsableReferenceStandards: () =>
    apiClient.get("/inventory/materials/usable-reference-standards").then((r) => r.data.data),
  getForPrint: () => apiClient.get("/inventory/materials/print").then((r) => r.data.data),
  getDefaultUnit: (materialType: string) =>
    apiClient.get("/inventory/materials/default-unit", { params: { materialType } }).then((r) => r.data.data.unit),
  create: (payload: SaveMaterialPayload) => apiClient.post("/inventory/materials", payload).then((r) => r.data.data),
  update: (id: number, payload: SaveMaterialPayload) => apiClient.put(`/inventory/materials/${id}`, payload).then((r) => r.data.data),

  // ---- Lot Documents ----
  getDocuments: (materialId: number) =>
    apiClient.get(`/inventory/materials/${materialId}/documents`).then((r) => r.data.data),

  uploadDocument: (materialId: number, documentType: MaterialDocumentType, file: File) => {
    const fd = new FormData();
    fd.append("documentType", documentType);
    fd.append("file", file);
    return apiClient.post(`/inventory/materials/${materialId}/documents`, fd, {
      headers: { "Content-Type": "multipart/form-data" }
    }).then((r) => r.data.data);
  },

  getDocumentContent: (documentId: number, materialId: number) =>
    apiClient.get(`/inventory/material-documents/${documentId}/content`, {
      params: { materialId },
      responseType: "blob"
    }).then((r) => r.data),

  supersedeDocument: (documentId: number, materialId: number, file: File, reason: string) => {
    const fd = new FormData();
    fd.append("materialId", String(materialId));
    fd.append("reason", reason);
    fd.append("file", file);
    return apiClient.post(`/inventory/material-documents/${documentId}/supersede`, fd, {
      headers: { "Content-Type": "multipart/form-data" }
    }).then((r) => r.data.data);
  },

  voidDocument: (documentId: number, materialId: number, reason: string) =>
    apiClient.post(
      `/inventory/material-documents/${documentId}/void`,
      { reason },
      { params: { materialId } }
    ).then((r) => r.data.data),

  getCOAEligibility: (materialId: number) =>
    apiClient.get(`/inventory/materials/${materialId}/coa-eligibility`).then((r) => r.data.data)
};

