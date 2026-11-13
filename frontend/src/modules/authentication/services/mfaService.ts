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

export const mfaService = {
  async verify(body: MfaVerifyRequest): Promise<LoginResult> {
    const res = await apiClient.post("/auth/mfa/verify", body);
    return parseLoginResponse(res.data.data);
  },

  async getOptions(): Promise<MfaOptions> {
    const res = await apiClient.get("/auth/mfa/options");
    return res.data.data as MfaOptions;
  }
};
