import { apiClient } from "../../../services/apiClient";

export type RecentRecordType =
  | "sample"
  | "testOrder"
  | "item"
  | "document"
  | "equipment"
  | "material"
  | "media"
  | "solution";

export interface RecentRecordDto {
  recordType: string;
  recordId: number;
  identifier: string;
  title: string;
  // The page that opens the record, built by the server as for search results.
  route: string;
  openedAtUtc: string;
}

interface ApiEnvelope<T> {
  data: T;
}

// The caller's own recently opened records. The server decides what the user may
// still read and writes the labels; nothing here is a label source.
export const recentRecordsService = {
  async list(signal?: AbortSignal): Promise<RecentRecordDto[]> {
    const res = await apiClient.get<ApiEnvelope<RecentRecordDto[] | null>>("/me/recent", { signal });
    return res.data.data ?? [];
  },

  async touch(recordType: RecentRecordType, recordId: number): Promise<void> {
    await apiClient.post("/me/recent", { recordType, recordId });
  },

  async clear(): Promise<void> {
    await apiClient.delete("/me/recent");
  }
};
