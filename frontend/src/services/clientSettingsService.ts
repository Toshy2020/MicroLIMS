import { apiClient } from "./apiClient";

// GET api/client-settings: the server-held values the browser may read.
// Other modules add fields here rather than creating their own endpoint.
export interface ClientSettings {
  benchDraftRetentionHours: number;
  features: string[];
}

export const clientSettingsService = {
  async get(signal?: AbortSignal): Promise<ClientSettings> {
    const res = await apiClient.get<{ data: ClientSettings }>("/client-settings", { signal });
    return res.data.data;
  }
};
