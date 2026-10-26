import { apiClient, ifMatch } from "../../../services/apiClient";

export interface RoleRecord {
  id: number;
  name: string;
  description: string | null;
  type: string;
  isSystemRole: boolean;
  isActive: boolean;
  // Administration + GxP pairs the role holds today ("A + B"); empty when it is clean.
  sodConflicts?: string[];
}

export interface RoleDetail {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  name: string;
  description: string | null;
  type: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissionCodes: string[];
  sodConflicts?: string[];
}

// The segregation-of-duties rule the server enforces: no role may hold a
// code from each list.
export interface SodRules {
  administrationCodes: string[];
  gxpExecutionAndApprovalCodes: string[];
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
  async update(id: number, name: string, description: string | null, version?: number): Promise<RoleDetail> {
    return (await apiClient.put(`/roles/${id}`, { name, description }, ifMatch(version))).data.data;
  },
  async remove(id: number): Promise<void> {
    await apiClient.delete(`/roles/${id}`);
  },
  async updatePermissions(id: number, permissionCodes: string[]): Promise<RoleDetail> {
    return (await apiClient.put(`/roles/${id}/permissions`, { permissionCodes })).data.data;
  },
  async getAllPermissions(): Promise<PermissionRecord[]> {
    return (await apiClient.get("/permissions")).data.data;
  },
  async getSodRules(): Promise<SodRules> {
    return (await apiClient.get("/roles/sod-rules")).data.data;
  }
};
