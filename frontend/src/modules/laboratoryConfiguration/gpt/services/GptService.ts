import { apiClient } from "../../../../services/apiClient";

export const GptService = {
  getAllMedia: () => apiClient.get("/gpt/media").then((r) => r.data.data),
  advanceStage: (mediaId: number) => apiClient.post(`/gpt/media/${mediaId}/advance`).then((r) => r.data.data),
  release: (mediaId: number) => apiClient.post(`/gpt/media/${mediaId}/release`).then((r) => r.data.data),
  generalAgar: (payload: any) => apiClient.post("/gpt/challenge/general-agar", payload).then((r) => r.data.data),
  generalBroth: (payload: any) => apiClient.post("/gpt/challenge/general-broth", payload).then((r) => r.data.data),
  selective: (payload: any) => apiClient.post("/gpt/challenge/selective", payload).then((r) => r.data.data)
};
