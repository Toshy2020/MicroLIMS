import { apiClient, ifMatch } from "../../../../services/apiClient";

// Water configuration - Laboratory Configuration > Water page. Mirrors
// EMConfigService: Water Departments -> Sample Locations (sampling
// points) -> per-count-test limits (SamplingConfiguration). Separate
// from WaterService.ts, which is the calculation engine used inside the
// Testing Workspace dialog.
export const WaterConfigService = {
  getWaterDepartments: () => apiClient.get("/masterdata/water-departments").then((r) => r.data.data),
  createWaterDepartment: (name: string) =>
    apiClient.post("/masterdata/water-departments", { name }).then((r) => r.data.data),
  updateWaterDepartment: (id: number, name: string, version?: number) =>
    apiClient.put(`/masterdata/water-departments/${id}`, { name }, ifMatch(version)).then((r) => r.data.data),
  deleteWaterDepartment: (id: number) => apiClient.delete(`/masterdata/water-departments/${id}`),

  getSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  createSamplingPoint: (code: string, location: string, testingFrequency: string, assignedTestCodes: string[], waterDepartmentId: number, frequency?: object) =>
    apiClient.post("/masterdata/water-sampling-points", { code, location, testingFrequency, assignedTestCodes, waterDepartmentId, frequency }).then((r) => r.data.data),
  updateSamplingPoint: (id: number, code: string, location: string, testingFrequency: string, assignedTestCodes: string[], waterDepartmentId: number, version?: number, frequency?: object) =>
    apiClient.put(`/masterdata/water-sampling-points/${id}`, { code, location, testingFrequency, assignedTestCodes, waterDepartmentId, frequency }, ifMatch(version)).then((r) => r.data.data),
  deleteSamplingPoint: (id: number) => apiClient.delete(`/masterdata/water-sampling-points/${id}`),

  getSamplingConfigurations: (pointId: number) =>
    apiClient.get("/masterdata/water-sampling-configurations", { params: { pointId } }).then((r) => r.data.data),
  createSamplingConfiguration: (
    waterSamplingPointId: number,
    testCode: string,
    alertLimit?: string | null,
    actionLimit?: string | null,
    specLimit?: string | null,
    unit?: string | null,
    limitType?: "Range" | "NotMoreThan" | "NotLessThan" | "Qualitative" | null,
    lowerLimit?: number | null,
    upperLimit?: number | null,
    lowerInclusive?: boolean,
    upperInclusive?: boolean,
    expectedResultText?: string | null
  ) =>
    apiClient
      .post("/masterdata/water-sampling-configurations", {
        waterSamplingPointId,
        testCode,
        alertLimit: alertLimit ?? null,
        actionLimit: actionLimit ?? null,
        specLimit: specLimit ?? null,
        unit: unit ?? null,
        limitType: limitType ?? null,
        lowerLimit: lowerLimit ?? null,
        upperLimit: upperLimit ?? null,
        lowerInclusive: lowerInclusive ?? true,
        upperInclusive: upperInclusive ?? true,
        expectedResultText: expectedResultText ?? null
      })
      .then((r) => r.data.data),
  updateSamplingConfiguration: (
    id: number,
    testCode: string,
    alertLimit?: string | null,
    actionLimit?: string | null,
    specLimit?: string | null,
    unit?: string | null,
    version?: number,
    limitType?: "Range" | "NotMoreThan" | "NotLessThan" | "Qualitative" | null,
    lowerLimit?: number | null,
    upperLimit?: number | null,
    lowerInclusive?: boolean,
    upperInclusive?: boolean,
    expectedResultText?: string | null
  ) =>
    apiClient
      .put(
        `/masterdata/water-sampling-configurations/${id}`,
        {
          testCode,
          alertLimit: alertLimit ?? null,
          actionLimit: actionLimit ?? null,
          specLimit: specLimit ?? null,
          unit: unit ?? null,
          limitType: limitType ?? null,
          lowerLimit: lowerLimit ?? null,
          upperLimit: upperLimit ?? null,
          lowerInclusive: lowerInclusive ?? true,
          upperInclusive: upperInclusive ?? true,
          expectedResultText: expectedResultText ?? null
        },
        ifMatch(version)
      )
      .then((r) => r.data.data),
  deleteSamplingConfiguration: (id: number) => apiClient.delete(`/masterdata/water-sampling-configurations/${id}`)
};
