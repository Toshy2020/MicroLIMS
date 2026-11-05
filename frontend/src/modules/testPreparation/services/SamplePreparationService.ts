import { apiClient } from "../../../services/apiClient";
import type { PreparationMaterialSource } from "./ItemPreparationConfigurationService";

export type { PreparationMaterialSource };

// One role's (diluent or neutralizer) lot choices.
export interface PreparationLotOption {
  id: number;
  code: string;
  expiresAt: string | null;
  usable: boolean;
  reason: string | null;
}

export interface PreparationLotGroup {
  source: PreparationMaterialSource | null;
  name: string;
  lotRequired: boolean;
  options: PreparationLotOption[];
}

export interface PreparationLotOptions {
  configurationId: number;
  needsMaterialMapping: boolean;
  diluent: PreparationLotGroup;
  neutralizer: PreparationLotGroup;
}

export interface PreparationWhereUsedRow {
  sampleId: number;
  sampleNumber: string;
  itemName: string;
  role: "Diluent" | "Neutralizer";
  preparedAt: string;
  preparedByName: string | null;
}

// Ids sent with a manual first entry: a source plus exactly one master id per role.
export interface PreparationMaterialPayload {
  diluentSource: PreparationMaterialSource;
  diluentMediaProductId?: number | null;
  diluentSolutionMasterId?: number | null;
  diluentMaterialMasterEntryId?: number | null;
  neutralizerSource: PreparationMaterialSource;
  neutralizerMediaProductId?: number | null;
  neutralizerSolutionMasterId?: number | null;
  neutralizerMaterialMasterEntryId?: number | null;
}

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
  diluentSource?: PreparationMaterialSource | null;
  neutralizerSource?: PreparationMaterialSource | null;
  needsMaterialMapping?: boolean;
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
  // Manual entry - only reachable when the item has no configuration yet.
  // These values also become that item's standing configuration.
  prepare: (payload: PreparationMaterialPayload & {
    sampleId: number; amount: number; technique: string;
    filtrationVolume?: number; washingVolume?: number; password: string;
    diluentLotId?: number | null; neutralizerLotId?: number | null;
  }) => apiClient.post("/sample-preparation", payload).then((r) => r.data.data),

  // Confirm-only - the item's configured steps are the ones performed.
  confirm: (payload: {
    sampleId: number; password: string; diluentLotId?: number | null; neutralizerLotId?: number | null;
  }) =>
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
    diluentLotId?: number | null; neutralizerLotId?: number | null;
  }): Promise<BatchConfirmPreparationResponse> =>
    apiClient.post("/sample-preparation/batch-confirm", payload).then((r) => r.data.data),

  // The lots the analyst may pick, for a sample's Item or (grouped) a configuration.
  getLotOptions: (params: { sampleId: number } | { configurationId: number }): Promise<PreparationLotOptions> =>
    apiClient.get("/sample-preparation/lot-options", { params }).then((r) => r.data.data),

  // Manual first entry: the lots of the master just chosen, before a configuration exists.
  getLotOptionsForMaster: (source: PreparationMaterialSource, masterId: number): Promise<PreparationLotGroup> =>
    apiClient.get("/sample-preparation/lot-options/master", { params: { source, masterId } }).then((r) => r.data.data),

  // Every sample that used this lot as diluent or neutralizer (exactly one id).
  getWhereUsed: (
    params: { mediaId: number } | { solutionPreparationId: number } | { materialId: number }
  ): Promise<PreparationWhereUsedRow[]> =>
    apiClient.get("/sample-preparation/where-used", { params }).then((r) => r.data.data)
};
