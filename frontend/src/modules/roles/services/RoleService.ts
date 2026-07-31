import { apiClient } from "../../../services/apiClient";

export interface RoleRecord {
  id: number;
  name: string;
  type: string;
}

export const RoleService = {
  async getAll(): Promise<RoleRecord[]> {
    return (await apiClient.get("/roles")).data.data;
  }
};
