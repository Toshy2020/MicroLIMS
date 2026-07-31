import { apiClient } from "../../../services/apiClient";

export const PathogenService = {
  async recordObservation(testOrderId: number, stepName: string, growthObserved: boolean) {
    return (await apiClient.post("/pathogen/observation", { testOrderId, stepName, growthObserved })).data.data;
  },
  async interpret(testOrderId: number): Promise<{ result: string }> {
    return (await apiClient.get(`/pathogen/interpret/${testOrderId}`)).data.data;
  }
};
