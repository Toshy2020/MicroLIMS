import { apiClient } from "../../../../services/apiClient";

export interface SpecificationDto {
  id?: number;
  itemId?: number;
  testCode: string;
  alertLimit?: string | null;
  actionLimit?: string | null;
  specLimit: string;
  unit?: string | null;
  // Only meaningful for count-type tests (TAMC/TYMC) - null/undefined for
  // pathogen presence/absence tests.
  dilutionFactor?: number | null;
}

export const SpecificationService = {
  getForItem: (itemId: number) => apiClient.get(`/masterdata/specifications?itemId=${itemId}`).then((r) => r.data.data),
  create: (itemId: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, unit?: string, dilutionFactor?: number | null) =>
    apiClient.post("/masterdata/specifications", { itemId, testCode, alertLimit, actionLimit, specLimit, unit, dilutionFactor }).then((r) => r.data.data),
  update: (id: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, unit?: string, dilutionFactor?: number | null) =>
    apiClient.put(`/masterdata/specifications/${id}`, { testCode, alertLimit, actionLimit, specLimit, unit, dilutionFactor }).then((r) => r.data.data),
  remove: (id: number) => apiClient.delete(`/masterdata/specifications/${id}`),
  delete: (id: number) => apiClient.delete(`/masterdata/specifications/${id}`)
};
