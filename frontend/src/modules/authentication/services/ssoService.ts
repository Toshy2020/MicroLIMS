import { apiClient } from "../../../services/apiClient";
import { parseLoginResponse, type LoginResponseDto, type LoginResult } from "./authenticationService";

export type SsoMode = "Off" | "OpenIdConnect" | "ActiveDirectory";

interface SsoModeResponseDto {
  mode?: string | null;
}

export const ssoService = {
  async getMode(signal?: AbortSignal): Promise<SsoMode> {
    const res = await apiClient.get("/auth/sso/mode", { signal });
    const mode = (res.data.data as SsoModeResponseDto | undefined)?.mode;
    if (mode === "OpenIdConnect" || mode === "ActiveDirectory") {
      return mode;
    }
    return "Off";
  },

  startUrl(): string {
    const base = (apiClient.defaults.baseURL ?? "").replace(/\/+$/, "");
    return `${base}/auth/sso/start`;
  },

  async exchange(code: string): Promise<LoginResult> {
    const res = await apiClient.post("/auth/sso/exchange", { code });
    return parseLoginResponse(res.data.data as LoginResponseDto);
  }
};
