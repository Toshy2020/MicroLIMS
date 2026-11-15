import { apiClient, withChangeReason } from "../../../../services/apiClient";

export interface Item {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  name: string;
  code: string;
  category: string;
  sopNumber: string;
  isActive: boolean;
  assignedTests: { testCode: string; displayName: string }[];
  specifications: { id?: number; testCode: string; alertLimit: string; actionLimit: string; specLimit: string; dilutionFactor?: number | null; grade?: string | null }[];
  minSampleQuantity?: number | null;
  minRetestSampleQuantity?: number | null;
  minSampleQuantityUnit?: string | null;
}

export interface CopyItemOptions {
  sourceItemId: number;
  specifications: boolean;
  autoAssignedTests: boolean;
}

export interface ItemSaveRequest {
  name: string;
  code: string;
  category: string;
  sopNumber: string;
  assignedTests: { testCode: string; displayName: string }[];
  minSampleQuantity?: number | null;
  minRetestSampleQuantity?: number | null;
  minSampleQuantityUnit?: string | null;
  copyFrom?: CopyItemOptions | null;
}

export const ItemService = {
  async getAll(): Promise<Item[]> {
    return (await apiClient.get("/items")).data.data;
  },
  async create(item: ItemSaveRequest): Promise<Item> {
    return (await apiClient.post("/items", item)).data.data;
  },
  async update(id: number, item: ItemSaveRequest, version?: number, reason?: string): Promise<void> {
    await apiClient.put(`/items/${id}`, item, withChangeReason(version, reason));
  },
  async remove(id: number, reason?: string): Promise<void> {
    await apiClient.delete(`/items/${id}`, withChangeReason(undefined, reason));
  },
  async freeze(id: number, reason?: string): Promise<void> {
    await apiClient.put(`/items/${id}/freeze`, undefined, withChangeReason(undefined, reason));
  },
  async unfreeze(id: number, reason?: string): Promise<void> {
    await apiClient.put(`/items/${id}/unfreeze`, undefined, withChangeReason(undefined, reason));
  }
};
