import { apiClient } from "../../../services/apiClient";
import {
  NumericStatisticsSummary,
  NumericTrendPoint,
  QualitativeEventItem,
  QualitativeEventResult,
  TrendAnalysisResult,
  TrendingCriteria
} from "../types/reportingTypes";
import { computeQuickPeriodRange, QuickPeriod } from "../utils/dateRange";
import { ReportingService } from "./ReportingService";

// TrendingCriteria.dateRange only ever offers the fixed presets the
// Trending tab's own Select shows (30d/3m/6m/12m) - custom/7d fall back to
// the criteria's own default ("12m") rather than guessing a range.
export function resolveDateRange(criteria: TrendingCriteria): { fromDate: string; toDate: string } {
  if (criteria.dateRange === "custom" && criteria.customFrom && criteria.customTo) {
    return { fromDate: criteria.customFrom, toDate: criteria.customTo };
  }
  const preset = criteria.dateRange === "custom" ? "12m" : criteria.dateRange;
  return computeQuickPeriodRange(preset as Exclude<QuickPeriod, "custom">);
}

function calculateMedian(values: number[]): string {
  if (values.length === 0) return "—";
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  const median = sorted.length % 2 !== 0 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
  return Number(median.toFixed(1)).toString();
}

function parseLimitNumber(limitStr: string): number | null {
  const match = limitStr.match(/\d+(\.\d+)?/);
  return match ? parseFloat(match[0]) : null;
}

function formatMonthLabel(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleDateString("en-GB", { month: "short", year: "numeric" });
  } catch {
    return dateStr;
  }
}

export const TrendingService = {
  async getQualitativeEvents(criteria: TrendingCriteria): Promise<QualitativeEventResult> {
    const { fromDate, toDate } = resolveDateRange(criteria);
    return ReportingService.getQualitativeEvents({
      testCode: criteria.testCode || undefined,
      subjectName: criteria.subjectName || undefined,
      category: criteria.category || undefined,
      fromDate,
      toDate
    });
  },

  async getAnalysis(criteria: TrendingCriteria): Promise<TrendAnalysisResult & { qualitativeEvents?: QualitativeEventItem[] }> {
    const isQualitative =
      criteria.testCode.toUpperCase().includes("PATHOGEN") ||
      criteria.testCode.toUpperCase().includes("ECOLI") ||
      criteria.testCode.toUpperCase().includes("SALM") ||
      criteria.testCode.toUpperCase().includes("PAERUG") ||
      criteria.testCode.toUpperCase().includes("SAUREUS");

    if (isQualitative) {
      try {
        const eventsRes = await this.getQualitativeEvents(criteria);
        return {
          isNumeric: false,
          testCode: eventsRes.testCode,
          testDisplayName: eventsRes.testDisplayName,
          subjectName: criteria.subjectName || "All Subjects",
          unit: null,
          qualitativeEvents: eventsRes.events
        };
      } catch {
        return {
          isNumeric: false,
          testCode: criteria.testCode,
          testDisplayName: criteria.testCode,
          subjectName: criteria.subjectName || "All Subjects",
          unit: null,
          qualitativeEvents: []
        };
      }
    }

    // Call real backend trend endpoint for numeric results
    try {
      const { fromDate, toDate } = resolveDateRange(criteria);
      const res = await apiClient.get<{ success: boolean; data: any }>("/reporting/trend", {
        params: {
          testCode: criteria.testCode || "TAMC",
          subjectName: criteria.subjectName || undefined,
          fromDate,
          toDate
        }
      }).catch(() => null);

      if (res?.data?.data) {
        const backendData = res.data.data;
        const pts: NumericTrendPoint[] = (backendData.points || []).map((p: any) => ({
          date: p.date,
          label: formatMonthLabel(p.date),
          value: p.numericValue,
          reportedValue: p.reportedValue,
          mean: backendData.statistics?.mean ?? 0,
          upperLimit: p.specLimit ? parseLimitNumber(p.specLimit) : null,
          lowerLimit: 0,
          alertLevel: p.alertLimit ? parseLimitNumber(p.alertLimit) : null,
          actionLevel: p.actionLimit ? parseLimitNumber(p.actionLimit) : null,
          resultLevel: p.resultLevel ?? "WithinLimit",
          referenceNumber: p.referenceNumber,
          recordId: p.recordId
        }));

        const numericValues = pts.map((p) => p.value).filter((v): v is number => v != null);
        const count = pts.length;
        const oosCount = pts.filter((p) => p.resultLevel === "OutOfSpecification").length;
        const alertCount = pts.filter((p) => p.resultLevel === "AlertLevel").length;
        const actionCount = pts.filter((p) => p.resultLevel === "ActionLevel").length;
        const withinSpecCount = pts.filter((p) => p.resultLevel === "WithinLimit").length;

        const stats: NumericStatisticsSummary = {
          numberOfResults: count,
          minimum: backendData.statistics?.min != null ? `${backendData.statistics.min}` : (count > 0 ? "< 1" : "—"),
          maximum: backendData.statistics?.max != null ? `${backendData.statistics.max}` : (count > 0 ? "—" : "—"),
          mean: backendData.statistics?.mean != null ? Number(backendData.statistics.mean).toFixed(1) : "—",
          median: calculateMedian(numericValues),
          standardDeviation: backendData.statistics?.standardDeviation != null ? Number(backendData.statistics.standardDeviation).toFixed(1) : "—",
          percentWithinSpec: count > 0 ? Number(((withinSpecCount / count) * 100).toFixed(1)) : 0,
          percentAlertLevel: count > 0 ? Number(((alertCount / count) * 100).toFixed(1)) : 0,
          percentActionLevel: count > 0 ? Number(((actionCount / count) * 100).toFixed(1)) : 0,
          outOfSpecCount: oosCount
        };

        return {
          isNumeric: true,
          testCode: backendData.testCode,
          testDisplayName: backendData.testDisplayName || backendData.testCode,
          subjectName: backendData.subjectName,
          unit: backendData.unit ?? "CFU/g",
          numericPoints: pts,
          numericStats: stats
        };
      }
    } catch {
      // Return empty real result
    }

    return {
      isNumeric: true,
      testCode: criteria.testCode || "TAMC",
      testDisplayName: criteria.testCode || "TAMC",
      subjectName: criteria.subjectName || "",
      unit: "CFU/g",
      numericPoints: [],
      numericStats: {
        numberOfResults: 0,
        minimum: "—",
        maximum: "—",
        mean: "—",
        median: "—",
        standardDeviation: "—",
        percentWithinSpec: 0,
        percentAlertLevel: 0,
        percentActionLevel: 0,
        outOfSpecCount: 0
      }
    };
  }
};
