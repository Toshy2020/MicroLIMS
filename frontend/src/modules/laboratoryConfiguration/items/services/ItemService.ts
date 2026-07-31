import { apiClient } from "../../../../services/apiClient";

export interface Item {
  id: number;
  name: string;
  code: string;
  category: string;
  sopNumber: string;
  assignedTests: { testCode: string; displayName: string }[];
  specifications: { testCode: string; alertLimit: string; actionLimit: string; specLimit: string }[];
}

export const ItemService = {
  async getAll(): Promise<Item[]> {
    return (await apiClient.get("/items")).data.data;
  },
  async create(item: { name: string; code: string; category: string; sopNumber: string; assignedTests: { testCode: string; displayName: string }[] }): Promise<Item> {
    return (await apiClient.post("/items", item)).data.data;
  }
};
