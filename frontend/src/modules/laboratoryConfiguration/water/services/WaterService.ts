import { apiClient } from "../../../../services/apiClient";

export interface WaterComparisonResult {
  average: number;
  status: string;
  exceededLimit: string | null;
}

export const WaterService = {
  async receive(samplingPointId: number, cause: string) {
    return (await apiClient.post("/water/receive", { samplingPointId, cause })).data.data;
  },
  async calculate(testOrderId: number, readings: number[]): Promise<WaterComparisonResult> {
    return (await apiClient.post("/water/calculate", { testOrderId, readings })).data.data;
  }
};
