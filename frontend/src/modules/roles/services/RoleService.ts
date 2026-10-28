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
  // Every change carries the administrator's reason (required by the server).
  async create(name: string, description: string | null, baseType: string, reason: string): Promise<RoleDetail> {
    return (await apiClient.post("/roles", { name, description, baseType, reason })).data.data;
  },
  async update(id: number, name: string, description: string | null, reason: string, version?: number): Promise<RoleDetail> {
    return (await apiClient.put(`/roles/${id}`, { name, description, reason }, ifMatch(version))).data.data;
  },
  // Electronically signed: password is the administrator's own, re-verified by the server.
  async remove(id: number, reason: string, password: string): Promise<void> {
    await apiClient.delete(`/roles/${id}`, { data: { reason, password } });
  },
  // Electronically signed (password re-entry) with a reason.
  async updatePermissions(id: number, permissionCodes: string[], reason: string, password: string): Promise<RoleDetail> {
    return (await apiClient.put(`/roles/${id}/permissions`, { permissionCodes, reason, password })).data.data;
  },
  async getAllPermissions(): Promise<PermissionRecord[]> {
    return (await apiClient.get("/permissions")).data.data;
  },
  async getSodRules(): Promise<SodRules> {
    return (await apiClient.get("/roles/sod-rules")).data.data;
  }
};
