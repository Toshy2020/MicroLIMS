import { apiClient } from "../../../services/apiClient";
import { LoginResult, parseLoginResponse } from "./authenticationService";

export interface MfaVerifyRequest {
  mfaToken: string;
  code?: string;
  recoveryCode?: string;
  trustDevice: boolean;
}

export interface MfaOptions {
  trustedDeviceHours: number;
}

export interface EnrolmentStart {
  manualKey: string;
  otpAuthUri: string;
  qrSvg: string;
}

export interface EnrolmentTokenConfirmResponse {
  recoveryCodes: string[];
  login: LoginResult;
}

export interface TrustedDeviceInfo {
  id: number;
  createdAt: string;
  expiresAt: string;
  createdIp?: string | null;
  userAgent?: string | null;
}

export interface MfaStatus {
  enrolled: boolean;
  type?: string | null;
  confirmedAt?: string | null;
  recoveryCodesRemaining: number;
  required: boolean;
  enrolmentDueAt?: string | null;
  trustedDevices: TrustedDeviceInfo[];
}

export const mfaService = {
  async verify(body: MfaVerifyRequest): Promise<LoginResult> {
    const res = await apiClient.post("/auth/mfa/verify", body);
    return parseLoginResponse(res.data.data);
  },

  async getOptions(): Promise<MfaOptions> {
    const res = await apiClient.get("/auth/mfa/options");
    return res.data.data as MfaOptions;
  },

  async startEnrolmentWithToken(mfaToken: string): Promise<EnrolmentStart> {
    const res = await apiClient.post("/auth/mfa/enrol/token/start", { mfaToken });
    return res.data.data as EnrolmentStart;
  },

  async confirmEnrolmentWithToken(mfaToken: string, code: string): Promise<EnrolmentTokenConfirmResponse> {
    const res = await apiClient.post("/auth/mfa/enrol/token/confirm", { mfaToken, code });
    const raw = res.data.data as { recoveryCodes: string[]; login: Parameters<typeof parseLoginResponse>[0] };
    return {
      recoveryCodes: raw.recoveryCodes,
      login: parseLoginResponse(raw.login)
    };
  },

  async getStatus(signal?: AbortSignal): Promise<MfaStatus> {
    const res = await apiClient.get("/auth/mfa/status", { signal });
    return res.data.data as MfaStatus;
  },

  async startEnrolment(password: string): Promise<EnrolmentStart> {
    const res = await apiClient.post("/auth/mfa/enrol/start", { password });
    return res.data.data as EnrolmentStart;
  },

  async confirmEnrolment(code: string): Promise<{ recoveryCodes: string[] }> {
    const res = await apiClient.post("/auth/mfa/enrol/confirm", { code });
    return res.data.data as { recoveryCodes: string[] };
  },

  async regenerateRecoveryCodes(password: string, code: string): Promise<{ recoveryCodes: string[] }> {
    const res = await apiClient.post("/auth/mfa/recovery-codes", { password, code });
    return res.data.data as { recoveryCodes: string[] };
  },

  async removeFactor(password: string, code: string): Promise<void> {
    await apiClient.delete("/auth/mfa", { data: { password, code } });
  },

  async forgetTrustedDevice(id: number): Promise<void> {
    await apiClient.delete(`/auth/mfa/trusted-devices/${id}`);
  }
};
