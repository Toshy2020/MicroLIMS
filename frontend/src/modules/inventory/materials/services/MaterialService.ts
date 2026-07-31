import { apiClient } from "../../../../services/apiClient";

export const MaterialService = {
  getAll: (materialType?: string) =>
    apiClient.get("/inventory/materials", { params: materialType ? { type: materialType } : {} }).then((r) => r.data.data),
  getForPrint: () => apiClient.get("/inventory/materials/print").then((r) => r.data.data),
  getDefaultUnit: (materialType: string) =>
    apiClient.get("/inventory/materials/default-unit", { params: { materialType } }).then((r) => r.data.data.unit),
  create: (payload: any) => apiClient.post("/inventory/materials", payload).then((r) => r.data.data),
  update: (id: number, payload: any) => apiClient.put(`/inventory/materials/${id}`, payload).then((r) => r.data.data)
};
