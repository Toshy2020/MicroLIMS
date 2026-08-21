import { apiClient } from "../../../services/apiClient";

export interface UserRecord {
  id: number;
  fullName: string;
  username: string;
  email: string | null;
  roleId: number;
  role: { id: number; name: string; type: string } | null;
  isActive: boolean;
  isLocked: boolean;
  lockedUntil: string | null;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  passwordChangedAt: string | null;
}

export const UserService = {
  async getAll(): Promise<UserRecord[]> {
    return (await apiClient.get("/users")).data.data;
  },
  async getById(id: number): Promise<UserRecord> {
    return (await apiClient.get(`/users/${id}`)).data.data;
  },
  async create(fullName: string, username: string, password: string, roleId: number, email?: string) {
    return (await apiClient.post("/users", { fullName, username, password, roleId, email: email || null })).data.data;
  },
  async updateProfile(id: number, fullName: string, username: string, email: string | null) {
    return (await apiClient.put(`/users/${id}`, { fullName, username, email })).data.data;
  },
  async changeRole(id: number, roleId: number, reason: string) {
    return (await apiClient.put(`/users/${id}/role`, { roleId, reason })).data.data;
  },
  async setStatus(id: number, isActive: boolean, reason?: string) {
    return (await apiClient.put(`/users/${id}/status`, { isActive, reason: reason || null })).data.data;
  },
  async unlock(id: number, reason?: string) {
    return (await apiClient.put(`/users/${id}/unlock`, { reason: reason || null })).data.data;
  },
  async initiatePasswordReset(id: number, reason?: string) {
    return (await apiClient.post(`/users/${id}/password-reset`, { reason: reason || null })).data.data;
  },
  async adminPasswordRecovery(id: number, reason: string): Promise<{ recoveryCode: string; expiresAt: string }> {
    return (await apiClient.post(`/users/${id}/admin-password-recovery`, { reason })).data.data;
  },
  async forcePasswordChange(id: number) {
    return (await apiClient.put(`/users/${id}/force-password-change`)).data.data;
  }
};
