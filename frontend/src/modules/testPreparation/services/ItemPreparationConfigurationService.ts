import { apiClient } from "../../../services/apiClient";

// Per-item preparation protocol. Read by the analyst's confirm dialogue,
// written from Laboratory Configuration -> Items. Diluent/Neutralizer are
// picked from master data (a source plus one id); the server resolves the
// display names. Unit was removed as of the 2026-09 Preparation
// Configuration simplification - the reporting unit lives on Specification
// per test instead.
export type PreparationMaterialSource =
  | "MediaProduct" | "SolutionRecipe" | "MaterialEntry" | "ContainedInMedium";

// The source plus exactly one master id matching it, for one role.
export interface PreparationMaterialRefs {
  source: PreparationMaterialSource | null;
  mediaProductId: number | null;
  solutionMasterId: number | null;
  materialMasterEntryId: number | null;
}

export interface ItemPreparationConfiguration {
  id: number;
  itemId: number;
  amount: number;
  technique: string;
  filtrationVolume: number | null;
  washingVolume: number | null;
  diluent: string;
  neutralizer: string;
  diluentSource: PreparationMaterialSource | null;
  diluentMediaProductId: number | null;
  diluentSolutionMasterId: number | null;
  diluentMaterialMasterEntryId: number | null;
  neutralizerSource: PreparationMaterialSource | null;
  neutralizerMediaProductId: number | null;
  neutralizerSolutionMasterId: number | null;
  neutralizerMaterialMasterEntryId: number | null;
  // Legacy text with no master data link yet: no lot can be recorded.
  needsMaterialMapping?: boolean;
  itemName?: string;
  approvalStatus: "PendingReview" | "Approved" | "Rejected";
  createdByUserId: number;
  createdByName: string | null;
  createdAt: string;
  approvedByUserId: number | null;
  approvedByName: string | null;
  approvedAt: string | null;
}

export interface PreparationConfigurationSaveRequest {
  amount: number;
  technique: string;
  filtrationVolume?: number | null;
  washingVolume?: number | null;
  diluentSource: PreparationMaterialSource;
  diluentMediaProductId?: number | null;
  diluentSolutionMasterId?: number | null;
  diluentMaterialMasterEntryId?: number | null;
  neutralizerSource: PreparationMaterialSource;
  neutralizerMediaProductId?: number | null;
  neutralizerSolutionMasterId?: number | null;
  neutralizerMaterialMasterEntryId?: number | null;
}

export const ItemPreparationConfigurationService = {
  // Null when the item has no configuration yet - the caller falls back to
  // manual entry, which then seeds one.
  async get(itemId: number): Promise<ItemPreparationConfiguration | null> {
    return (await apiClient.get(`/items/${itemId}/preparation-configuration`)).data.data ?? null;
  },
  async save(itemId: number, payload: PreparationConfigurationSaveRequest): Promise<ItemPreparationConfiguration> {
    return (await apiClient.put(`/items/${itemId}/preparation-configuration`, payload)).data.data;
  },
  async approve(itemId: number): Promise<ItemPreparationConfiguration> {
    return (await apiClient.post(`/items/${itemId}/preparation-configuration/approve`)).data.data;
  },
  async getPending(): Promise<ItemPreparationConfiguration[]> {
    return (await apiClient.get("/preparation-configurations/pending")).data.data;
  },
  // Configurations whose diluent/neutralizer is still legacy text (Items.Manage).
  async getUnmapped(): Promise<ItemPreparationConfiguration[]> {
    return (await apiClient.get("/preparation-configurations/unmapped")).data.data;
  }
};
