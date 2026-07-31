import { apiClient } from "../../../services/apiClient";

export interface UserRecord {
  id: number;
  fullName: string;
  username: string;
  isActive: boolean;
  role: { id: number; name: string; type: string } | null;
}

export const UserService = {
  async getAll(): Promise<UserRecord[]> {
    return (await apiClient.get("/users")).data.data;
  },
  async create(fullName: string, username: string, password: string, roleId: number) {
    return (await apiClient.post("/users", { fullName, username, password, roleId })).data.data;
  },
  async deactivate(id: number) {
    return (await apiClient.put(`/users/${id}/deactivate`)).data.data;
  }
};
