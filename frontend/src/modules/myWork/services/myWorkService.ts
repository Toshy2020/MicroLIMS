import { apiClient } from "../../../services/apiClient";
import type { MyWorkResponse, MyWorkSummary } from "../myWorkTypes";

interface ApiEnvelope<T> {
  data: T;
}

export const myWorkService = {
  async get(lab?: string, signal?: AbortSignal): Promise<MyWorkResponse> {
    const res = await apiClient.get<ApiEnvelope<MyWorkResponse>>("/my-work", {
      params: lab ? { lab } : undefined,
      signal
    });
    return res.data.data;
  },

  async summary(signal?: AbortSignal): Promise<MyWorkSummary> {
    const res = await apiClient.get<ApiEnvelope<MyWorkSummary>>("/my-work/summary", { signal });
    return res.data.data;
  },

  async take(testOrderId: number): Promise<void> {
    await apiClient.post(`/testorders/${testOrderId}/take`);
  },

  async release(testOrderId: number): Promise<void> {
    await apiClient.post(`/testorders/${testOrderId}/release`);
  }
};
