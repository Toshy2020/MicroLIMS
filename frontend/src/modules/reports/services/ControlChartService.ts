import { apiClient } from "../../../services/apiClient";
import {
  AppliedLimit,
  ApplyLimitItem,
  ControlChartCriteria,
  ControlChartResult,
  ControlChartSettings,
  LimitReviewResult,
  ReportFormat,
  ReportItem
} from "../types/controlChartTypes";
import { SampleCategory } from "../types/reportingTypes";

// Date inputs hold yyyy-mm-dd; the range is inclusive of both whole days.
const dayStart = (d: string) => (d ? `${d}T00:00:00.000Z` : undefined);
const dayEnd = (d: string) => (d ? `${d}T23:59:59.999Z` : undefined);

function settingsParams(s: ControlChartSettings) {
  return {
    method: s.method,
    alertK: s.alertK,
    actionK: s.actionK,
    alertPercentile: s.alertPercentile,
    actionPercentile: s.actionPercentile,
    movingRangeLength: s.movingRangeLength,
    rounding: s.rounding,
    minimumPoints: s.minimumPoints,
    rules: s.rules.join(","),
    excludeOutOfSpec: s.excludeOutOfSpec
  };
}

const optionalNumber = (v: string) => (v.trim() === "" ? undefined : Number(v));

function fileNameFrom(contentDisposition: string | undefined, fallback: string): string {
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  return match?.[1] ? decodeURIComponent(match[1]) : fallback;
}

export const ControlChartService = {
  getChart: (c: ControlChartCriteria, s: ControlChartSettings) =>
    apiClient.get<{ success: boolean; data: ControlChartResult }>("/reporting/control-chart", {
      params: {
        testCode: c.testCode,
        subjectName: c.subjectName,
        category: c.category,
        fromDate: dayStart(c.fromDate),
        toDate: dayEnd(c.toDate),
        calculationFrom: dayStart(c.calculationFrom),
        calculationTo: dayEnd(c.calculationTo),
        approvedOnly: c.approvedOnly,
        capabilityBasis: c.capabilityBasis,
        customLsl: c.capabilityBasis === "Custom" ? optionalNumber(c.customLsl) : undefined,
        customUsl: c.capabilityBasis === "Custom" ? optionalNumber(c.customUsl) : undefined,
        ...settingsParams(s)
      }
    }).then((r) => r.data.data),

  getLimitReview: (c: ControlChartCriteria, s: ControlChartSettings) =>
    apiClient.get<{ success: boolean; data: LimitReviewResult }>("/reporting/control-chart/limit-review", {
      params: {
        testCode: c.testCode,
        category: c.category,
        fromDate: dayStart(c.fromDate),
        toDate: dayEnd(c.toDate),
        approvedOnly: c.approvedOnly,
        ...settingsParams(s)
      }
    }).then((r) => r.data.data),

  applyLimits: (
    c: Pick<ControlChartCriteria, "testCode" | "fromDate" | "toDate" | "approvedOnly"> & { category: SampleCategory },
    s: ControlChartSettings,
    items: ApplyLimitItem[],
    reason: string,
    password: string
  ) =>
    apiClient.post<{ success: boolean; data: AppliedLimit[] }>("/reporting/control-chart/apply-limits", {
      testCode: c.testCode,
      category: c.category,
      fromDate: dayStart(c.fromDate) ?? null,
      toDate: dayEnd(c.toDate) ?? null,
      approvedOnly: c.approvedOnly,
      settings: s,
      items,
      reason,
      password
    }).then((r) => r.data.data),

  // One PDF or Word document holding every queued chart. Returns the file
  // and the name the server gave it.
  downloadReport: async (title: string, notes: string, includeSummary: boolean, format: ReportFormat, items: ReportItem[]) => {
    const res = await apiClient.post("/reporting/control-chart/report", {
      title,
      notes: notes.trim() || null,
      format,
      includeSummary,
      sections: items.map((i) => ({
        testCode: i.criteria.testCode,
        subjectName: i.subjectName,
        category: i.criteria.category,
        fromDate: dayStart(i.criteria.fromDate) ?? null,
        toDate: dayEnd(i.criteria.toDate) ?? null,
        calculationFrom: dayStart(i.criteria.calculationFrom) ?? null,
        calculationTo: dayEnd(i.criteria.calculationTo) ?? null,
        approvedOnly: i.criteria.approvedOnly,
        settings: i.settings,
        capabilityBasis: i.criteria.capabilityBasis,
        customLsl: i.criteria.capabilityBasis === "Custom" ? optionalNumber(i.criteria.customLsl) ?? null : null,
        customUsl: i.criteria.capabilityBasis === "Custom" ? optionalNumber(i.criteria.customUsl) ?? null : null,
        chartType: i.chartLabel,
        title: i.title,
        imagePng: i.image
      }))
    }, { responseType: "blob" });
    const fallback = `control_chart_report.${format === "Pdf" ? "pdf" : "docx"}`;
    return { blob: res.data as Blob, fileName: fileNameFrom(res.headers["content-disposition"], fallback) };
  }
};
