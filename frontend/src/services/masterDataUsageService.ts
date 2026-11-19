import { apiClient } from "./apiClient";

export type MasterRecordType =
  | "TestDefinition"
  | "Specification"
  | "Item"
  | "Equipment"
  | "MaterialMaster";

export interface UsageLine {
  key: string;
  label: string;
  count: number;
  isOpenWork: boolean;
  listRoute: string | null;
}

export interface UsageSummary {
  type: MasterRecordType;
  id: number;
  openWorkCount: number;
  historicalCount: number;
  lines: UsageLine[];
}

interface UsageApiResponse {
  success: boolean;
  data: UsageSummary;
}

export const masterDataUsageService = {
  async getUsage(
    type: MasterRecordType,
    id: number,
    signal?: AbortSignal
  ): Promise<UsageSummary> {
    const res = await apiClient.get<UsageApiResponse>("/masterdata/usage", {
      params: { type, id },
      signal,
    });
    return res.data.data;
  },
};
