import { AxiosError } from "axios";
import { apiClient } from "../../../services/apiClient";
import {
  CompareResult,
  FilterOptionsResponse,
  ResultRecordItem,
  ResultRecordSearchParams,
  ResultRecordSearchResponse
} from "../types/reportingTypes";

// Extracts the filename ReportingController.ExportResults sets via
// Content-Disposition - falls back to a locally-generated one if the
// header is ever missing so the download never fails silently.
function extractFileName(contentDisposition: string | undefined): string {
  const match = contentDisposition?.match(/filename="?([^"; ]+)"?/i);
  return match?.[1] ?? `microlims-results-${new Date().toISOString().replace(/[:.]/g, "-")}.csv`;
}

export const ReportingService = {
  searchResults: (params: ResultRecordSearchParams) =>
    apiClient.get<{ success: boolean; data: ResultRecordSearchResponse }>("/reporting/results", { params }).then((r) => r.data.data),

  getFilterOptions: () =>
    apiClient.get<{ success: boolean; data: FilterOptionsResponse }>("/reporting/filter-options").then((r) => r.data.data),

  getCompletedByMonth: (months = 6) =>
    apiClient.get<{ success: boolean; data: { month: string; priorYearCount: number; currentYearCount: number }[] }>(
      "/reporting/completed-by-month", { params: { months } }
    ).then((r) => r.data.data),

  getResultById: (id: number) =>
    apiClient.get<{ success: boolean; data: ResultRecordItem }>(`/reporting/results/${id}`).then((r) => r.data.data),

  getOverview: (fromDate?: string, toDate?: string) =>
    apiClient.get<{
      success: boolean;
      data: {
        totalTests: number;
        approvedCount: number;
        pendingReviewCount: number;
        pendingApprovalCount: number;
        outOfSpecCount: number;
        alertActionCount: number;
        categoryDistribution: { category: string; count: number; percentage: number }[];
        testDistribution: { testCode: string; testName: string; count: number }[];
        locationDistribution: { location: string; count: number; percentage: number }[];
        recentResults: {
          id: number;
          referenceNumber: string;
          subjectName: string;
          subjectDetail: string | null;
          category: string;
          testCode: string;
          testDisplayName: string;
          resultEnteredAt: string;
          resultEnteredByName: string;
          sampleStatus: string;
          approvalStatus: string;
        }[];
      };
    }>("/reporting/overview", { params: { fromDate, toDate } }).then((r) => r.data.data),

  getQualitativeEvents: (params: { testCode?: string; subjectName?: string; category?: string; fromDate?: string; toDate?: string }) =>
    apiClient.get<{ success: boolean; data: import("../types/reportingTypes").QualitativeEventResult }>(
      "/reporting/qualitative-events", { params }
    ).then((r) => r.data.data),

  getCompare: (testCode: string, category: string, fromDate: string, toDate: string) =>
    apiClient.get<{ success: boolean; data: CompareResult }>(
      "/reporting/compare", { params: { testCode, category, fromDate, toDate } }
    ).then((r) => r.data.data),

  // Downloads the CSV as a blob and triggers a save via a throwaway
  // object URL + programmatic click, then revokes the URL immediately
  // after so the blob isn't kept alive in memory.
  exportCsv: async (params: ResultRecordSearchParams) => {
    try {
      const response = await apiClient.get("/reporting/results/export", { params, responseType: "blob" });
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
      // responseType "blob" means axios can't parse a JSON error body for
      // us (e.g. the 10,000-row cap message) - it arrives as a Blob, so
      // read it back out manually and rethrow a normal Error.
      const axiosErr = err as AxiosError;
      const data = axiosErr.response?.data;
      if (data instanceof Blob && data.type.includes("json")) {
        const text = await data.text();
        let message = "Export failed.";
        try {
          message = JSON.parse(text).message ?? message;
        } catch {
          // leave the generic message if the body isn't valid JSON
        }
        throw new Error(message);
      }
      throw err;
    }
  }
};
