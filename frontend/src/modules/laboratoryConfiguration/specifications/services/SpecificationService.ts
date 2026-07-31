import { apiClient } from "../../../../services/apiClient";

export const SpecificationService = {
  getForItem: (itemId: number) => apiClient.get(`/masterdata/specifications?itemId=${itemId}`).then((r) => r.data.data),
  create: (itemId: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.post("/masterdata/specifications", { itemId, testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data)
};
