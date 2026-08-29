import { apiClient } from "../../../services/apiClient";
import { CurrentUserInfo, LoginResult } from "../types/authTypes";

export const authenticationService = {
  async login(username: string, password: string): Promise<LoginResult> {
    const res = await apiClient.post("/auth/login", { username, password });
    const { token, refreshToken, mustChangePassword } = res.data.data as { token: string; refreshToken: string; mustChangePassword: boolean };
    const payload = JSON.parse(atob(token.split(".")[1]));
    const role = payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? payload.role;
    // System.IdentityModel.Tokens.Jwt collapses a claim type down to a
    // plain string (not a 1-element array) when only one claim of that
    // type is present - a custom role granted exactly one permission
    // would hit this, so never assume the array shape.
    const rawPermissions = payload.permission;
    const permissions: string[] = Array.isArray(rawPermissions) ? rawPermissions : rawPermissions ? [rawPermissions] : [];
    return { token, refreshToken, role, permissions, mustChangePassword };
  },

  async refresh(refreshToken: string) {
    const res = await apiClient.post("/auth/refresh", { refreshToken });
    return res.data.data as { token: string; refreshToken: string };
  },

  async requestPasswordReset(username: string) {
    return (await apiClient.post("/auth/password-reset/request", { username })).data.data;
  },

  async confirmPasswordReset(resetToken: string, newPassword: string) {
    return (await apiClient.post("/auth/password-reset/confirm", { resetToken, newPassword })).data.data;
  },

  async confirmAdminPasswordRecovery(username: string, recoveryCode: string, newPassword: string) {
    return (await apiClient.post("/auth/admin-password-recovery/confirm", { username, recoveryCode, newPassword })).data.data;
  },

  async changePassword(currentPassword: string, newPassword: string) {
    return (await apiClient.post("/auth/change-password", { currentPassword, newPassword })).data.data;
  },

  async me(): Promise<CurrentUserInfo> {
    const res = await apiClient.get("/auth/me");
    return res.data.data as CurrentUserInfo;
  }
};
