import { apiClient } from "./apiClient";

// The caller's own interface preferences (tour seen, last release note, ...).
// The server always acts on the signed-in user; there is no user id here.
export type UiStateMap = Record<string, string>;

export const uiStateService = {
  async getAll(signal?: AbortSignal): Promise<UiStateMap> {
    const res = await apiClient.get<{ data: UiStateMap | null }>("/me/ui-state", { signal });
    return res.data.data ?? {};
  },

  async set(key: string, value: string): Promise<void> {
    await apiClient.put(`/me/ui-state/${encodeURIComponent(key)}`, { value });
  },

  async remove(key: string): Promise<void> {
    await apiClient.delete(`/me/ui-state/${encodeURIComponent(key)}`);
  }
};
