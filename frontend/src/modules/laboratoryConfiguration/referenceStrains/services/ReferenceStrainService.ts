import { apiClient } from "../../../../services/apiClient";

export const ReferenceStrainService = {
  getAll: () => apiClient.get("/reference-strains").then((r) => r.data.data),
  receive: (payload: any) => apiClient.post("/reference-strains", payload).then((r) => r.data.data),
  approve: (id: number, approved: boolean) => apiClient.post(`/reference-strains/${id}/approve`, { approved }).then((r) => r.data.data),
  prepareCryovials: (payload: any) => apiClient.post("/reference-strains/cryovials", payload).then((r) => r.data.data),
  approveCryovial: (id: number, approved: boolean) => apiClient.post(`/reference-strains/cryovials/${id}/approve`, { approved }).then((r) => r.data.data),
  destroyCryovial: (id: number) => apiClient.post(`/reference-strains/cryovials/${id}/destroy`).then((r) => r.data.data),
  thawCryovial: (id: number) => apiClient.post(`/reference-strains/cryovials/${id}/thaw`).then((r) => r.data.data)
};
