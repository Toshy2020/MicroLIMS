import { apiClient } from "../../../../services/apiClient";

// Backs the Items Master's category-dependent dynamic forms, mirroring
// MasterDataController exactly.
export const MasterDataService = {
  // Water
  getWaterSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  createWaterSamplingPoint: (code: string, location: string, assignedTestCodes: string[]) =>
    apiClient.post("/masterdata/water-sampling-points", { code, location, assignedTestCodes }).then((r) => r.data.data),

  // EM
  getDepartments: () => apiClient.get("/masterdata/departments").then((r) => r.data.data),
  createDepartment: (name: string) => apiClient.post("/masterdata/departments", { name }).then((r) => r.data.data),
  getRooms: () => apiClient.get("/masterdata/rooms").then((r) => r.data.data),
  createRoom: (name: string, departmentId: number, gradeClassification: string) =>
    apiClient.post("/masterdata/rooms", { name, departmentId, gradeClassification }).then((r) => r.data.data),

  // After Cleaning
  getMachines: () => apiClient.get("/masterdata/machines").then((r) => r.data.data),
  createMachine: (name: string) => apiClient.post("/masterdata/machines", { name }).then((r) => r.data.data),
  createMachinePart: (name: string, machineId: number) =>
    apiClient.post("/masterdata/machine-parts", { name, machineId }).then((r) => r.data.data),

  // Product
  getSpecifications: (itemId: number) => apiClient.get(`/masterdata/specifications?itemId=${itemId}`).then((r) => r.data.data),
  createSpecification: (itemId: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.post("/masterdata/specifications", { itemId, testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data)
};
