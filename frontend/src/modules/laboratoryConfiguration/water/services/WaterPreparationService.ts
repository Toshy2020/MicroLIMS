import { apiClient } from "../../../../services/apiClient";

// Backs the Water Preparation checklist (Testing Workspace -> Prepare).
// Mirrors EMPreparationService, but a water department's checklist items
// are its sampling points directly (each already carries its own
// assignedTestCodes), so one grouped GET is enough - no per-item config
// fetch like EM's Room -> RoomTestConfiguration two-step lookup.
export const WaterPreparationService = {
  getSamplingPointsForDepartment: (waterDepartmentId: number) =>
    apiClient.get("/masterdata/water-departments").then((r) => {
      const department = r.data.data.find((d: any) => d.id === waterDepartmentId);
      return department?.samplingPoints ?? [];
    }),
  prepare: (
    sampleId: number,
    waterSamplingPointIds: number[],
    storageCondition: string,
    storageTimeHours?: number | null
  ) =>
    apiClient
      .post("/water/prepare", { sampleId, waterSamplingPointIds, storageCondition, storageTimeHours })
      .then((r) => r.data.data)
};
