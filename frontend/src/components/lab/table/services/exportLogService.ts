import { apiClient } from "../../../../services/apiClient";
import type { ApiResponse } from "../../../../services/apiTypes";

export interface ExportLogRequest {
  tableKey: string;
  filterJson: string;
  columns: string[];
  rowCount: number;
  recordIds: string[];
}

export interface ExportLogResponse {
  exportId: number;
  serverTimeUtc: string;
  timeZoneId: string;
  exportedBy: string;
}

export const exportLogService = {
  async log(body: ExportLogRequest): Promise<ExportLogResponse> {
    const res = await apiClient.post<ApiResponse<ExportLogResponse>>(
      "/exports/log",
      body
    );
    if (!res.data.data) {
      throw new Error(res.data.message ?? "Failed to log export.");
    }
    return res.data.data;
  },
};
