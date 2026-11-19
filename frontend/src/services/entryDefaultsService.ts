import { apiClient } from "./apiClient";

export type EntryDefaultsContext =
  | "incubation"
  | "mediaPreparation"
  | "solutionPreparation"
  | "receiving";

export interface EntryDefault {
  field: string;
  id: number | null;
  text: string | null;
  label: string;
  lastUsedAtUtc: string;
}

export interface EntryDefaultsResponse {
  defaults: EntryDefault[];
}

export interface EntryDefaultsQuery {
  context: EntryDefaultsContext;
  testOrderId?: number;
  stepMediaId?: number;
  mediaProductId?: number;
  solutionMasterId?: number;
  category?: string;
}

interface ApiEnvelope<T> {
  success: boolean;
  message: string | null;
  data: T;
  errors: string[] | null;
}

export async function getEntryDefaults(
  query: EntryDefaultsQuery,
  signal?: AbortSignal
): Promise<EntryDefaultsResponse> {
  const res = await apiClient.get<ApiEnvelope<EntryDefaultsResponse>>("/entry-defaults", {
    params: query,
    signal
  });
  return res.data?.data ?? { defaults: [] };
}

export const entryDefaultsService = {
  get: getEntryDefaults
};
