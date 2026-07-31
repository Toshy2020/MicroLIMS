import { apiClient } from "../../../../services/apiClient";

export const MediaPreparationService = {
  getAll: () => apiClient.get("/media").then((r) => r.data.data),
  prepare: (payload: any) => apiClient.post("/media", payload).then((r) => r.data.data)
};
