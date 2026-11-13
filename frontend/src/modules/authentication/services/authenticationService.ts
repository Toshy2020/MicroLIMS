import { apiClient } from "../../../services/apiClient";
import { CurrentUserInfo, LoginResult, SignInMethod } from "../types/authTypes";
import { readTokenClaims } from "../tokenClaims";

export type { LoginResult };

interface LoginResponseDto {
  token?: string | null;
  refreshToken?: string | null;
  mustChangePassword?: boolean;
  signingPasswordRequired?: boolean;
  roleName?: string | null;
  mfaRequired?: boolean;
  mfaEnrolmentRequired?: boolean;
  mfaToken?: string | null;
  mfaEnrolmentDueAt?: string | null;
  deviceToken?: string | null;
  recoveryCodesRemaining?: number | null;
  recoveryCodes?: string[] | null;
}

interface MeResponseDto extends Omit<CurrentUserInfo, "signInMethod" | "signingPasswordSet"> {
  signInMethod?: SignInMethod | null;
  signingPasswordSet?: boolean | null;
}

export function parseLoginResponse(data: LoginResponseDto): LoginResult {
  const result: LoginResult = {
    mustChangePassword: data.mustChangePassword ?? false,
    signingPasswordRequired: data.signingPasswordRequired ?? false,
    roleName: data.roleName ?? null,
    mfaRequired: data.mfaRequired ?? false,
    mfaEnrolmentRequired: data.mfaEnrolmentRequired ?? false,
    mfaToken: data.mfaToken ?? undefined,
    mfaEnrolmentDueAt: data.mfaEnrolmentDueAt ?? undefined,
    deviceToken: data.deviceToken ?? undefined,
    recoveryCodesRemaining: data.recoveryCodesRemaining ?? undefined,
    recoveryCodes: data.recoveryCodes ?? undefined
  };

  if (data.token) {
    const { role, permissions } = readTokenClaims(data.token);
    result.token = data.token;
    result.refreshToken = data.refreshToken ?? undefined;
    result.role = role ?? undefined;
    result.permissions = permissions;
  }

  return result;
}

export const authenticationService = {
  async login(username: string, password: string, deviceToken?: string): Promise<LoginResult> {
    const body = deviceToken ? { username, password, deviceToken } : { username, password };
    const res = await apiClient.post("/auth/login", body);
    return parseLoginResponse(res.data.data as LoginResponseDto);
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

  async setSigningPassword(newPassword: string) {
    return (await apiClient.post("/auth/signing-password", { newPassword })).data.data;
  },

  async me(): Promise<CurrentUserInfo> {
    const res = await apiClient.get("/auth/me");
    const dto = res.data.data as MeResponseDto;
    return {
      ...dto,
      signInMethod: dto.signInMethod ?? "Local",
      signingPasswordSet: dto.signingPasswordSet ?? false
    };
  }
};
