import { apiClient } from "../../../../services/apiClient";

export const AfterCleaningPreparationService = {
  getPartsForMachine: (machineId: number) =>
    apiClient.get("/masterdata/machines").then((r) => r.data.data.find((m: any) => m.id === machineId)?.parts ?? []),
  getPartConfigurations: (machinePartId: number) =>
    apiClient.get(`/masterdata/machine-part-configurations?machinePartId=${machinePartId}`).then((r) => r.data.data),
  prepare: (sampleId: number, machinePartConfigurationIds: number[]) =>
    apiClient.post("/aftercleaning/prepare", { sampleId, machinePartConfigurationIds }).then((r) => r.data.data)
};
