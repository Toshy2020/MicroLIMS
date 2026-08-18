import { apiClient } from "../../../../services/apiClient";

// Water configuration - Laboratory Configuration > Water page. Mirrors
// EMConfigService: Water Departments -> Sample Locations (sampling
// points) -> per-count-test limits (SamplingConfiguration). Separate
// from WaterService.ts, which is the calculation engine used inside the
// Testing Workspace dialog.
export const WaterConfigService = {
  getWaterDepartments: () => apiClient.get("/masterdata/water-departments").then((r) => r.data.data),
  createWaterDepartment: (name: string) =>
    apiClient.post("/masterdata/water-departments", { name }).then((r) => r.data.data),
  updateWaterDepartment: (id: number, name: string) =>
    apiClient.put(`/masterdata/water-departments/${id}`, { name }).then((r) => r.data.data),
  deleteWaterDepartment: (id: number) => apiClient.delete(`/masterdata/water-departments/${id}`),

  getSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  createSamplingPoint: (code: string, location: string, testingFrequency: string, assignedTestCodes: string[], waterDepartmentId: number) =>
    apiClient.post("/masterdata/water-sampling-points", { code, location, testingFrequency, assignedTestCodes, waterDepartmentId }).then((r) => r.data.data),
  updateSamplingPoint: (id: number, code: string, location: string, testingFrequency: string, assignedTestCodes: string[], waterDepartmentId: number) =>
    apiClient.put(`/masterdata/water-sampling-points/${id}`, { code, location, testingFrequency, assignedTestCodes, waterDepartmentId }).then((r) => r.data.data),
  deleteSamplingPoint: (id: number) => apiClient.delete(`/masterdata/water-sampling-points/${id}`),

  getSamplingConfigurations: (pointId: number) =>
    apiClient.get("/masterdata/water-sampling-configurations", { params: { pointId } }).then((r) => r.data.data),
  createSamplingConfiguration: (waterSamplingPointId: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.post("/masterdata/water-sampling-configurations", { waterSamplingPointId, testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data),
  updateSamplingConfiguration: (id: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.put(`/masterdata/water-sampling-configurations/${id}`, { testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data),
  deleteSamplingConfiguration: (id: number) => apiClient.delete(`/masterdata/water-sampling-configurations/${id}`)
};
