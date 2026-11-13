import { apiClient } from "./apiClient";

export interface ServerVersion {
  version: string;
  environment: string;
}

export const systemVersionService = {
  async get(signal?: AbortSignal): Promise<ServerVersion> {
    const res = await apiClient.get<{ data: ServerVersion }>("/system/version", { signal });
    return res.data.data;
  }
};
