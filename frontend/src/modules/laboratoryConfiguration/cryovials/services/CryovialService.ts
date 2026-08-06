import { apiClient } from "../../../../services/apiClient";

export const CryovialService = {
  getAll: () => apiClient.get("/cryovials").then((r) => r.data.data),
  prepare: (payload: any) => apiClient.post("/cryovials/prepare", payload).then((r) => r.data.data),
  approve: (id: number, approved: boolean, password: string, comment?: string) =>
    apiClient.post(`/cryovials/${id}/approve`, { approved, password, comment }).then((r) => r.data.data),
  destroy: (id: number) => apiClient.post(`/cryovials/${id}/destroy`).then((r) => r.data.data),
  thawVial: (id: number, notes?: string) => apiClient.post(`/cryovials/${id}/thaw`, { notes: notes || null }).then((r) => r.data.data)
};
