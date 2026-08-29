import { AxiosError } from "axios";
import { apiClient } from "../../../services/apiClient";
import {
  MediaGptDetail,
  MediaGptFilterOptions,
  MediaGptSearchParams,
  MediaGptSearchResponse,
  MediaGptSummary
} from "../types/mediaGptTypes";

function extractFileName(contentDisposition: string | undefined): string {
  const match = contentDisposition?.match(/filename="?([^"; ]+)"?/i);
  return match?.[1] ?? `microlims-media-gpt-${new Date().toISOString().replace(/[:.]/g, "-")}.csv`;
}

export const MediaGptReportService = {
  search: (params: MediaGptSearchParams) =>
    apiClient.get<{ success: boolean; data: MediaGptSearchResponse }>("/reporting/media-gpt", { params })
      .then((r) => r.data.data),

  getById: (id: number) =>
    apiClient.get<{ success: boolean; data: MediaGptDetail }>(`/reporting/media-gpt/${id}`)
      .then((r) => r.data.data),

  getSummary: (fromDate?: string, toDate?: string, mediaType?: string) =>
    apiClient.get<{ success: boolean; data: MediaGptSummary }>("/reporting/media-gpt/summary", {
      params: { fromDate, toDate, mediaType }
    }).then((r) => r.data.data),

  getFilterOptions: () =>
    apiClient.get<{ success: boolean; data: MediaGptFilterOptions }>("/reporting/media-gpt/filter-options")
      .then((r) => r.data.data),

  exportCsv: async (params: MediaGptSearchParams) => {
    try {
      const response = await apiClient.get("/reporting/media-gpt/export", { params, responseType: "blob" });
      const fileName = extractFileName(response.headers["content-disposition"]);

      const url = window.URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      const axiosErr = err as AxiosError;
      const data = axiosErr.response?.data;
      if (data instanceof Blob && data.type.includes("json")) {
        const text = await data.text();
        let message = "Export failed.";
        try {
          message = JSON.parse(text).message ?? message;
        } catch {
          // ignore parsing error
        }
        throw new Error(message);
      }
      throw err;
    }
  }
};
