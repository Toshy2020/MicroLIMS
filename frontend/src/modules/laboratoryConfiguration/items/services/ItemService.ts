import { apiClient } from "../../../../services/apiClient";

export interface Item {
  id: number;
  name: string;
  code: string;
  category: string;
  sopNumber: string;
  isActive: boolean;
  assignedTests: { testCode: string; displayName: string }[];
  specifications: { testCode: string; alertLimit: string; actionLimit: string; specLimit: string }[];
}

export interface ItemSaveRequest {
  name: string;
  code: string;
  category: string;
  sopNumber: string;
  assignedTests: { testCode: string; displayName: string }[];
}

export const ItemService = {
  async getAll(): Promise<Item[]> {
    return (await apiClient.get("/items")).data.data;
  },
  async create(item: ItemSaveRequest): Promise<Item> {
    return (await apiClient.post("/items", item)).data.data;
  },
  async update(id: number, item: ItemSaveRequest): Promise<void> {
    await apiClient.put(`/items/${id}`, item);
  },
  async remove(id: number): Promise<void> {
    await apiClient.delete(`/items/${id}`);
  },
  async freeze(id: number): Promise<void> {
    await apiClient.put(`/items/${id}/freeze`);
  },
  async unfreeze(id: number): Promise<void> {
    await apiClient.put(`/items/${id}/unfreeze`);
  }
};
