import { apiClient } from "../../../../services/apiClient";

export const AfterCleaningConfigService = {
  getMachines: () => apiClient.get("/masterdata/machines").then((r) => r.data.data),
  createMachine: (name: string) => apiClient.post("/masterdata/machines", { name }).then((r) => r.data.data),
  updateMachine: (id: number, name: string) => apiClient.put(`/masterdata/machines/${id}`, { name }).then((r) => r.data.data),
  deleteMachine: (id: number) => apiClient.delete(`/masterdata/machines/${id}`),

  createMachinePart: (name: string, machineId: number) =>
    apiClient.post("/masterdata/machine-parts", { name, machineId }).then((r) => r.data.data),
  updateMachinePart: (id: number, name: string, machineId: number) =>
    apiClient.put(`/masterdata/machine-parts/${id}`, { name, machineId }).then((r) => r.data.data),
  deleteMachinePart: (id: number) => apiClient.delete(`/masterdata/machine-parts/${id}`),

  getPartConfigurations: (machinePartId: number) =>
    apiClient.get("/masterdata/machine-part-configurations", { params: { machinePartId } }).then((r) => r.data.data),
  createPartConfiguration: (machinePartId: number, testType: string, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, isPathogenTest: boolean) =>
    apiClient.post("/masterdata/machine-part-configurations", { machinePartId, testType, testCode, alertLimit, actionLimit, specLimit, isPathogenTest }).then((r) => r.data.data),
  updatePartConfiguration: (id: number, testType: string, testCode: string, alertLimit: string, actionLimit: string, specLimit: string, isPathogenTest: boolean) =>
    apiClient.put(`/masterdata/machine-part-configurations/${id}`, { testType, testCode, alertLimit, actionLimit, specLimit, isPathogenTest }).then((r) => r.data.data),
  deletePartConfiguration: (id: number) => apiClient.delete(`/masterdata/machine-part-configurations/${id}`)
};
