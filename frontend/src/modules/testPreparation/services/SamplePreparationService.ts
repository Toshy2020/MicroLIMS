import { apiClient } from "../../../services/apiClient";

// One item configuration's worth of samples that can be prepared together.
// Samples group by the configuration they would confirm, because that
// configuration IS what the analyst signs for - a single signature
// statement can only cover one set of steps.
export interface GroupedPreparationSample {
  sampleId: number;
  sampleReference: string;
  displayName: string;
  batchNumber: string | null;
  category: string;
  assignedAnalystId: number | null;
  assignedAnalystName: string | null;
}

export interface ExcludedPreparationSample {
  sampleId: number;
  sampleReference: string;
  displayName: string;
  reason: string;
}

export interface GroupedPreparation {
  groupKey: string;
  configurationId: number;
  itemId: number;
  itemName: string;
  approvalStatus: "PendingReview" | "Approved" | "Rejected";
  amount: number;
  technique: string;
  filtrationVolume: number | null;
  washingVolume: number | null;
  diluent: string;
  neutralizer: string;
  sampleCount: number;
  samples: GroupedPreparationSample[];
}

export interface GroupedPreparationResponse {
  groups: GroupedPreparation[];
  excludedCount: number;
  excluded: ExcludedPreparationSample[];
}

export interface BatchPreparationSuccessItem {
  sampleId: number;
  sampleReference: string;
  preparationId: number;
  message: string;
}

export interface BatchPreparationSkippedItem {
  sampleId: number;
  sampleReference: string;
  reason: string;
}

export interface BatchConfirmPreparationResponse {
  totalRequested: number;
  succeededCount: number;
  skippedCount: number;
  succeeded: BatchPreparationSuccessItem[];
  skipped: BatchPreparationSkippedItem[];
}

export const SamplePreparationService = {
  getNeedsPreparation: () => apiClient.get("/testorders").then((r) =>
    r.data.data.filter((s: any) =>
      ["FinishedProduct", "RawMaterial", "PackagingMaterial", "Water"].includes(s.category) &&
      s.preparationStatus === "NeedsPreparation")),

  // Manual entry - only reachable when the item has no configuration yet.
  // These values also become that item's standing configuration.
  prepare: (payload: {
    sampleId: number; amount: number; technique: string;
    filtrationVolume?: number; washingVolume?: number; diluent: string;
    neutralizer: string; password: string;
  }) => apiClient.post("/sample-preparation", payload).then((r) => r.data.data),

  // Confirm-only - the item's configured steps are the ones performed.
  confirm: (payload: { sampleId: number; password: string }) =>
    apiClient.post("/sample-preparation/confirm", payload).then((r) => r.data.data),

  // Grouped preparation - which of the checked samples can be confirmed
  // together, plus the ones that cannot with the reason why.
  getGroups: (sampleIds: number[]): Promise<GroupedPreparationResponse> =>
    apiClient
      .get("/sample-preparation/groups", { params: { sampleIds: sampleIds.join(",") } })
      .then((r) => r.data.data),

  // One password entry; the server writes a separate signature and
  // preparation record against every sample in the group.
  confirmBatch: (payload: {
    sampleIds: number[]; configurationId: number; password: string;
  }): Promise<BatchConfirmPreparationResponse> =>
    apiClient.post("/sample-preparation/batch-confirm", payload).then((r) => r.data.data)
};
