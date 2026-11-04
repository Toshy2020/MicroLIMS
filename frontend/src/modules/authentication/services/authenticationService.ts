import { apiClient } from "../../../services/apiClient";
import { CurrentUserInfo, LoginResult } from "../types/authTypes";
import { readTokenClaims } from "../tokenClaims";

export const authenticationService = {
  async login(username: string, password: string): Promise<LoginResult> {
    const res = await apiClient.post("/auth/login", { username, password });
    const { token, refreshToken, mustChangePassword, roleName } = res.data.data as {
      token: string; refreshToken: string; mustChangePassword: boolean; roleName?: string | null;
    };
    const { role, permissions } = readTokenClaims(token);
    return { token, refreshToken, role: role!, permissions, mustChangePassword, roleName };
  },

  async refresh(refreshToken: string) {
    const res = await apiClient.post("/auth/refresh", { refreshToken });
    return res.data.data as { token: string; refreshToken: string; roleName?: string | null };
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
