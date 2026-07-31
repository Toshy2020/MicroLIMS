import { apiClient } from "../../../../services/apiClient";

// Water configuration (Sampling Points) - Laboratory Configuration >
// Water page. Separate from WaterService.ts, which is the calculation
// engine used inside the Testing Workspace dialog.
export const WaterConfigService = {
  getSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  createSamplingPoint: (code: string, location: string, assignedTestCodes: string[]) =>
    apiClient.post("/masterdata/water-sampling-points", { code, location, assignedTestCodes }).then((r) => r.data.data)
};
