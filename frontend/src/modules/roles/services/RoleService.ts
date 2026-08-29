import { apiClient } from "../../../services/apiClient";

export interface RoleRecord {
  id: number;
  name: string;
  description: string | null;
  type: string;
  isSystemRole: boolean;
  isActive: boolean;
}

export interface RoleDetail {
  id: number;
  name: string;
  description: string | null;
  type: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissionCodes: string[];
}

export interface PermissionRecord {
  code: string;
  description: string;
  isEnforced: boolean;
}

export const RoleService = {
  async getAll(): Promise<RoleRecord[]> {
    return (await apiClient.get("/roles")).data.data;
  },
  async getById(id: number): Promise<RoleDetail> {
    return (await apiClient.get(`/roles/${id}`)).data.data;
  },
  async create(name: string, description: string | null, baseType: string): Promise<RoleDetail> {
    return (await apiClient.post("/roles", { name, description, baseType })).data.data;
  },
  async update(id: number, name: string, description: string | null): Promise<RoleDetail> {
    return (await apiClient.put(`/roles/${id}`, { name, description })).data.data;
  },
  async remove(id: number): Promise<void> {
    await apiClient.delete(`/roles/${id}`);
  },
  async updatePermissions(id: number, permissionCodes: string[]): Promise<RoleDetail> {
    return (await apiClient.put(`/roles/${id}/permissions`, { permissionCodes })).data.data;
  },
  async getAllPermissions(): Promise<PermissionRecord[]> {
    return (await apiClient.get("/permissions")).data.data;
  }
};
