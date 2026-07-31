import { apiClient } from "../../../services/apiClient";

export const SamplePreparationService = {
  getNeedsPreparation: () => apiClient.get("/testorders").then((r) =>
    r.data.data.filter((s: any) =>
      ["FinishedProduct", "RawMaterial", "PackagingMaterial", "Water"].includes(s.category))),
  prepare: (payload: {
    sampleId: number; amount: number; unit: string; technique: string;
    filtrationVolume?: number; washingVolume?: number; diluentTypeId: number; diluentMediaId?: number;
    neutralizerId: number; storageCondition?: string; storageTimeHours?: number;
  }) => apiClient.post("/sample-preparation", payload).then((r) => r.data.data)
};
