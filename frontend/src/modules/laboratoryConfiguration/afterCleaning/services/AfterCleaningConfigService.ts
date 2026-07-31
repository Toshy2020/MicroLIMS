import { apiClient } from "../../../../services/apiClient";

export const AfterCleaningConfigService = {
  getMachines: () => apiClient.get("/masterdata/machines").then((r) => r.data.data),
  createMachine: (name: string) => apiClient.post("/masterdata/machines", { name }).then((r) => r.data.data),
  createMachinePart: (name: string, machineId: number) =>
    apiClient.post("/masterdata/machine-parts", { name, machineId }).then((r) => r.data.data),
  createPartConfiguration: (machinePartId: number, testType: string, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, isPathogenTest: boolean) =>
    apiClient.post("/masterdata/machine-part-configurations", { machinePartId, testType, testCode, alertLimit, actionLimit, specLimit, isPathogenTest }).then((r) => r.data.data)
};
