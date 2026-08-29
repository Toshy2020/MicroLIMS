import { AxiosError } from "axios";
import { apiClient } from "../../../services/apiClient";
import {
  ReferenceStrainDetail,
  ReferenceStrainFilterOptions,
  ReferenceStrainSearchParams,
  ReferenceStrainSearchResponse
} from "../types/referenceStrainTypes";

function extractFileName(contentDisposition: string | undefined): string {
  const match = contentDisposition?.match(/filename="?([^"; ]+)"?/i);
  return match?.[1] ?? `microlims-reference-strains-${new Date().toISOString().replace(/[:.]/g, "-")}.csv`;
}

export const ReferenceStrainReportService = {
  search: (params: ReferenceStrainSearchParams) =>
    apiClient.get<{ success: boolean; data: ReferenceStrainSearchResponse }>("/reporting/reference-strains", { params })
      .then((r) => r.data.data),

  getById: (id: number) =>
    apiClient.get<{ success: boolean; data: ReferenceStrainDetail }>(`/reporting/reference-strains/${id}`)
      .then((r) => r.data.data),

  getFilterOptions: () =>
    apiClient.get<{ success: boolean; data: ReferenceStrainFilterOptions }>("/reporting/reference-strains/filter-options")
      .then((r) => r.data.data),

  exportCsv: async (params: ReferenceStrainSearchParams) => {
    try {
      const response = await apiClient.get("/reporting/reference-strains/export", { params, responseType: "blob" });
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
