import { apiClient } from "../../../../services/apiClient";

export const AfterCleaningService = {
  async receive(machinePartId: number, cause: string) {
    return (await apiClient.post("/aftercleaning/receive", { machinePartId, cause })).data.data;
  }
};
