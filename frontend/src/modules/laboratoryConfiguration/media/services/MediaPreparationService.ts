import { apiClient } from "../../../../services/apiClient";

export const MediaPreparationService = {
  getAll: () => apiClient.get("/media").then((r) => r.data.data),
  prepare: (payload: any) => apiClient.post("/media", payload).then((r) => r.data.data),

  // Lots that passed evaluation and are waiting on a Section Head's
  // release signature.
  getAwaitingApproval: () => apiClient.get("/media/awaiting-approval").then((r) => r.data.data),

  decideRelease: (mediaId: number, password: string, approved: boolean, comment?: string) =>
    apiClient.post(`/media/${mediaId}/release`, { password, approved, comment }).then((r) => r.data.data),

  getStorage: (mediaId: number) => apiClient.get(`/media/${mediaId}/storage`).then((r) => r.data.data),
  addStorage: (mediaId: number, payload: any) => apiClient.post(`/media/${mediaId}/storage`, payload).then((r) => r.data.data),
  moveStorage: (mediaId: number, entryId: number, payload: any) =>
    apiClient.post(`/media/${mediaId}/storage/${entryId}/move`, payload).then((r) => r.data.data)
};
