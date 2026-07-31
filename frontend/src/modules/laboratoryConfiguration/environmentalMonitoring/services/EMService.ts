import { apiClient } from "../../../../services/apiClient";

export const EMService = {
  async startStep1(testOrderId: number) {
    return (await apiClient.post(`/em/step1/start/${testOrderId}`)).data.data;
  },
  async completeStep1(testOrderId: number, count: number) {
    return (await apiClient.post("/em/step1/complete", { testOrderId, count })).data.data;
  },
  async startStep2(testOrderId: number) {
    return (await apiClient.post(`/em/step2/start/${testOrderId}`)).data.data;
  },
  async completeStep2(testOrderId: number, count: number, actionLimit: number) {
    return (await apiClient.post("/em/step2/complete", { testOrderId, count, actionLimit })).data.data;
  }
};
