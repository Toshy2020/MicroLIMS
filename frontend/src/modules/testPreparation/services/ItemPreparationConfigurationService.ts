import { apiClient } from "../../../services/apiClient";

// Per-item preparation protocol. Read by the analyst's confirm dialogue,
// written from Laboratory Configuration -> Items.
export interface ItemPreparationConfiguration {
  id: number;
  itemId: number;
  amount: number;
  unit: string;
  technique: string;
  filtrationVolume: number | null;
  washingVolume: number | null;
  diluentTypeId: number;
  diluentTypeName: string;
  diluentMediaId: number | null;
  diluentMediaLotNumber: string | null;
  neutralizerId: number;
  neutralizerName: string;
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
  unit: string;
  technique: string;
  filtrationVolume?: number | null;
  washingVolume?: number | null;
  diluentTypeId: number;
  diluentMediaId?: number | null;
  neutralizerId: number;
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
  }
};
