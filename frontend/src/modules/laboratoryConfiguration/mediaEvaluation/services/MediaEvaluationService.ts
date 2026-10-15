import { apiClient } from "../../../../services/apiClient";
import type { MediaEvaluationDetail, RecordResultPayload, ThawedVialOption } from "../types/mediaEvaluationTypes";

export const MediaEvaluationService = {
  getAll: (status?: string): Promise<MediaEvaluationDetail[]> =>
    apiClient.get("/media-evaluations", { params: status ? { status } : {} }).then((r) => r.data.data),
  getById: (id: number): Promise<MediaEvaluationDetail> => apiClient.get(`/media-evaluations/${id}`).then((r) => r.data.data),
  getThawedVials: (challengeId: number): Promise<ThawedVialOption[]> =>
    apiClient.get(`/media-evaluations/challenges/${challengeId}/thawed-vials`).then((r) => r.data.data),
  selectThawedVial: (challengeId: number, thawEventId: number) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/thawed-vial`, { thawEventId }).then((r) => r.data.data),
  thawAndSelectVial: (challengeId: number, cryovialId: number, notes?: string) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/thaw-vial`, { cryovialId, notes: notes || null }).then((r) => r.data.data),
  selectLyophilizedDisk: (challengeId: number, materialId: number) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/lyophilized-disk`, { materialId }).then((r) => r.data.data),
  recordIncubation: (challengeId: number, incubatorEquipmentId: number) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/incubation`, { incubatorEquipmentId }).then((r) => r.data.data),
  recordResult: (challengeId: number, payload: RecordResultPayload) =>
    apiClient.post(`/media-evaluations/challenges/${challengeId}/result`, payload).then((r) => r.data.data)
};
