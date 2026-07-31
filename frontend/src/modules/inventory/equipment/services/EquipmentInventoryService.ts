import { apiClient } from "../../../../services/apiClient";

export const EquipmentInventoryService = {
  getAll: () => apiClient.get("/inventory/equipment").then((r) => r.data.data),
  getForPrint: () => apiClient.get("/inventory/equipment/print").then((r) => r.data.data),
  create: (payload: any) => apiClient.post("/inventory/equipment", payload).then((r) => r.data.data),
  update: (id: number, payload: any) => apiClient.put(`/inventory/equipment/${id}`, payload).then((r) => r.data.data)
};
