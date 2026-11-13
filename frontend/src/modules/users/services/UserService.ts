import { apiClient, ifMatch } from "../../../services/apiClient";
import type { TrustedDeviceInfo } from "../../authentication/services/mfaService";
import type { SignInMethod } from "../../authentication/types/authTypes";

export type { TrustedDeviceInfo, SignInMethod };

export type DirectoryLinkState = "NotLinked" | "LinkedNeverSignedIn" | "Linked" | "SignInRefused";

export interface CreateUserSsoOptions {
  signInMethod?: SignInMethod;
  directoryUsername?: string | null;
  isEmergencyAccount?: boolean;
}

export interface AssignHardwareTokenPayload {
  serialNumber: string;
  seed: string;
  seedFormat: "base32" | "hex";
  stepSeconds: 30 | 60;
  reason: string;
}

export interface UserRecord {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  fullName: string;
  username: string;
  email: string | null;
  jobTitle?: string | null;
  roleId: number;
  role: { id: number; name: string; type: string } | null;
  isActive: boolean;
  isLocked: boolean;
  lockedUntil: string | null;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  passwordChangedAt: string | null;
  mfaState?: string;
  signInMethod?: SignInMethod;
  linkState?: DirectoryLinkState;
  directoryUsername?: string | null;
  isEmergencyAccount?: boolean;
}

export interface UserSession {
  id: number;
  createdAt: string;
  createdIp: string | null;
  userAgent: string | null;
  lastUsedAt: string | null;
  expiresAt: string;
  mfaVerified?: boolean;
}

export const UserService = {
  async getSessions(id: number): Promise<UserSession[]> {
    return (await apiClient.get(`/admin/users/${id}/sessions`)).data.data;
  },
  async revokeSessions(id: number, reason: string): Promise<{ revokedCount: number }> {
    return (await apiClient.post(`/admin/users/${id}/sessions/revoke`, { reason })).data.data;
  },
  async resetMfa(id: number, reason: string): Promise<void> {
    await apiClient.post(`/admin/users/${id}/mfa/reset`, { reason });
  },
  async assignHardwareToken(id: number, payload: AssignHardwareTokenPayload): Promise<void> {
    await apiClient.post(`/admin/users/${id}/mfa/hardware-token`, payload);
  },
  async getTrustedDevices(id: number, signal?: AbortSignal): Promise<TrustedDeviceInfo[]> {
    return (await apiClient.get(`/admin/users/${id}/trusted-devices`, { signal })).data.data;
  },
  async revokeTrustedDevice(id: number, deviceId: number): Promise<void> {
    await apiClient.delete(`/admin/users/${id}/trusted-devices/${deviceId}`);
  },
  // status: "active" (default) or "inactive". "all" is only for screens that
  // resolve a stored user id to a name (historical attribution); never feed it to a picker.
  async getAll(status: "active" | "inactive" | "all" = "active"): Promise<UserRecord[]> {
    return (await apiClient.get("/users", { params: { status } })).data.data;
  },
  async getEligibleAnalysts(): Promise<UserRecord[]> {
    return (await apiClient.get("/users/analysts")).data.data;
  },
  async getById(id: number): Promise<UserRecord> {
    return (await apiClient.get(`/users/${id}`)).data.data;
  },
  // reason is required by the server for every administrative change.
  async create(
    fullName: string,
    username: string,
    password: string,
    roleId: number,
    reason: string,
    email?: string,
    jobTitle?: string,
    sso?: CreateUserSsoOptions
  ) {
    return (
      await apiClient.post("/users", {
        fullName,
        username,
        password,
        roleId,
        email: email || null,
        jobTitle: jobTitle || null,
        reason,
        ...(sso ? sso : {})
      })
    ).data.data;
  },
  async relinkDirectory(id: number, reason: string): Promise<void> {
    await apiClient.post(`/admin/users/${id}/directory-relink`, { reason });
  },
  // jobTitle is required so every caller states it: the backend treats a missing value as "clear it".
  async updateProfile(id: number, fullName: string, username: string, email: string | null, jobTitle: string | null, reason: string, version?: number, sso?: CreateUserSsoOptions) {
    return (await apiClient.put(`/users/${id}`, { fullName, username, email, jobTitle: jobTitle || null, reason, ...(sso ?? {}) }, ifMatch(version))).data.data;
  },
  // Electronically signed: password is the administrator's own, re-verified by the server.
  async changeRole(id: number, roleId: number, reason: string, password: string) {
    return (await apiClient.put(`/users/${id}/role`, { roleId, reason, password })).data.data;
  },
  async setStatus(id: number, isActive: boolean, reason: string) {
    return (await apiClient.put(`/users/${id}/status`, { isActive, reason })).data.data;
  },
  async unlock(id: number, reason: string) {
    return (await apiClient.put(`/users/${id}/unlock`, { reason })).data.data;
  },
  async initiatePasswordReset(id: number, reason: string) {
    return (await apiClient.post(`/users/${id}/password-reset`, { reason })).data.data;
  },
  async adminPasswordRecovery(id: number, reason: string): Promise<{ recoveryCode: string; expiresAt: string }> {
    return (await apiClient.post(`/users/${id}/admin-password-recovery`, { reason })).data.data;
  },
  async forcePasswordChange(id: number, reason: string) {
    return (await apiClient.put(`/users/${id}/force-password-change`, { reason })).data.data;
  },
  async updateEmail(id: number, email: string | null, reason: string) {
    return (await apiClient.put(`/users/${id}/email`, { email, reason })).data.data;
  }
};
