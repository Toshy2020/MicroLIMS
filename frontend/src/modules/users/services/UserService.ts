import { apiClient } from "../../../services/apiClient";

export interface UserRecord {
  id: number;
  fullName: string;
  username: string;
  email: string | null;
  isActive: boolean;
  role: { id: number; name: string; type: string } | null;
}

export const UserService = {
  async getAll(): Promise<UserRecord[]> {
    return (await apiClient.get("/users")).data.data;
  },
  async create(fullName: string, username: string, password: string, roleId: number, email?: string) {
    return (await apiClient.post("/users", { fullName, username, password, roleId, email: email || null })).data.data;
  },
  async deactivate(id: number) {
    return (await apiClient.put(`/users/${id}/deactivate`)).data.data;
  },
  async updateEmail(id: number, email: string | null) {
    return (await apiClient.put(`/users/${id}/email`, { email })).data.data;
  }
};
