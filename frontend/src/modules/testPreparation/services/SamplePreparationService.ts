import { apiClient } from "../../../services/apiClient";

export const SamplePreparationService = {
  getNeedsPreparation: () => apiClient.get("/testorders").then((r) =>
    r.data.data.filter((s: any) =>
      ["FinishedProduct", "RawMaterial", "PackagingMaterial", "Water"].includes(s.category) &&
      s.preparationStatus === "NeedsPreparation")),

  // Manual entry - only reachable when the item has no configuration yet.
  // These values also become that item's standing configuration.
  prepare: (payload: {
    sampleId: number; amount: number; unit: string; technique: string;
    filtrationVolume?: number; washingVolume?: number; diluentTypeId: number; diluentMediaId?: number;
    neutralizerId: number; password: string;
  }) => apiClient.post("/sample-preparation", payload).then((r) => r.data.data),

  // Confirm-only - the item's configured steps are the ones performed.
  confirm: (payload: { sampleId: number; password: string }) =>
    apiClient.post("/sample-preparation/confirm", payload).then((r) => r.data.data)
};
