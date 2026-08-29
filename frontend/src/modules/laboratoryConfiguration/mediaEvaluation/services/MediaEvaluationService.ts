import { apiClient } from "../../../../services/apiClient";

export const MediaEvaluationService = {
  getAll: (status?: string) => apiClient.get("/media-evaluations", { params: status ? { status } : {} }).then((r) => r.data.data),
  getById: (id: number) => apiClient.get(`/media-evaluations/${id}`).then((r) => r.data.data),
  selectCryovial: (challengeId: number, cryovialId: number) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/cryovial`, { cryovialId }).then((r) => r.data.data),
  selectLyophilizedDisk: (challengeId: number, materialId: number) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/lyophilized-disk`, { materialId }).then((r) => r.data.data),
  recordIncubation: (challengeId: number, incubatorEquipmentId: number) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/incubation`, { incubatorEquipmentId }).then((r) => r.data.data),
  recordResult: (challengeId: number, payload: any) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/result`, payload).then((r) => r.data.data)
};
