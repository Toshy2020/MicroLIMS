import { OverviewDashboardData, SampleCategory } from "../types/reportingTypes";
import { ReportingService } from "./ReportingService";
import { chartPalette as defaultChartPalette } from "../../../theme";

// Exported so other Reports tabs (e.g. AnalystKpiTab's category filter and
// "Tests by Category" donut) use the same human labels instead of each
// inventing their own.
export const CATEGORY_LABELS: Record<string, string> = {
  FinishedProduct: "Finished Product",
  RawMaterial: "Raw Material",
  PackagingMaterial: "Packaging Material",
  Water: "Water",
  EnvironmentalMonitoring: "Environmental Monitor",
  AfterCleaning: "After Cleaning",
  GPT: "Media / GPT",
  ReferenceStrain: "Reference Strain"
};

export const OverviewService = {
  async getOverviewData(fromDate?: string, toDate?: string, palette?: string[]): Promise<OverviewDashboardData> {
    const activePalette = palette && palette.length > 0 ? palette : defaultChartPalette;
    try {
      // Query SQL-level aggregates across all matching records from the backend
      const res = await ReportingService.getOverview(fromDate, toDate);

      const totalCount = res.totalTests;
      const approvedCount = res.approvedCount;
      const pendingReviewCount = res.pendingReviewCount;
      const pendingApprovalCount = res.pendingApprovalCount;
      const outOfSpecCount = res.outOfSpecCount;
      const alertActionCount = res.alertActionCount;

      const categoryDistribution = (res.categoryDistribution ?? []).map((c, idx) => ({
        category: c.category as SampleCategory,
        label: CATEGORY_LABELS[c.category] ?? c.category,
        count: c.count,
        percentage: c.percentage,
        color: activePalette[idx % activePalette.length]
      }));

      const testDistribution = (res.testDistribution ?? []).map((t) => ({
        testCode: t.testCode,
        testName: t.testName || t.testCode,
        count: t.count
      }));

      const locationDistribution = (res.locationDistribution ?? []).map((l, idx) => ({
        location: l.location || "Other",
        count: l.count,
        percentage: l.percentage,
        color: activePalette[idx % activePalette.length]
      }));

      const recentResults = (res.recentResults ?? []).map((r) => ({
        id: r.id,
        referenceNumber: r.referenceNumber,
        subjectName: r.subjectName,
        subjectDetail: r.subjectDetail,
        category: r.category as SampleCategory,
        testCode: r.testCode,
        testDisplayName: r.testDisplayName || r.testCode,
        dateEntered: r.resultEnteredAt
          ? new Date(r.resultEnteredAt).toLocaleDateString("en-GB", {
              day: "2-digit",
              month: "short",
              year: "numeric",
              hour: "2-digit",
              minute: "2-digit"
            })
          : "—",
        enteredBy: r.resultEnteredByName || "Analyst",
        sampleStatus: r.sampleStatus as any,
        approvalStatus: r.approvalStatus
      }));

      return {
        totalTests: {
          title: "Total Tests",
          value: totalCount.toLocaleString(),
          deltaPercent: totalCount > 0 ? 100 : 0,
          deltaDirection: "up",
          comparisonLabel: "live database records"
        },
        approvedResults: {
          title: "Approved Results",
          value: approvedCount.toLocaleString(),
          deltaPercent: totalCount > 0 ? Math.round((approvedCount / totalCount) * 100) : 0,
          deltaDirection: "up",
          comparisonLabel: `${approvedCount} of ${totalCount} approved`
        },
        pendingReview: {
          title: "Pending Review",
          value: pendingReviewCount,
          deltaPercent: pendingReviewCount > 0 ? 100 : 0,
          deltaDirection: pendingReviewCount > 0 ? "up" : "down",
          comparisonLabel: "active in testing/review queue"
        },
        pendingApproval: {
          title: "Pending Approval",
          value: pendingApprovalCount,
          deltaPercent: pendingApprovalCount > 0 ? 100 : 0,
          deltaDirection: pendingApprovalCount > 0 ? "up" : "down",
          comparisonLabel: "awaiting section head release"
        },
        outOfSpec: {
          title: "Out of Spec",
          value: outOfSpecCount,
          deltaPercent: outOfSpecCount,
          deltaDirection: outOfSpecCount > 0 ? "up" : "down",
          comparisonLabel: "quality signal (independent of analyst)",
          variant: outOfSpecCount > 0 ? "error" : "default",
          tooltip: "Laboratory Quality Metric — Evaluated independently of analyst performance"
        },
        alertActionLevel: {
          title: "Alert / Action Level",
          value: alertActionCount,
          deltaPercent: alertActionCount,
          deltaDirection: alertActionCount > 0 ? "up" : "down",
          comparisonLabel: "exceeded alert or action thresholds",
          variant: alertActionCount > 0 ? "warning" : "default"
        },
        categoryDistribution,
        testDistribution,
        locationDistribution,
        recentResults,
        qualitySignals: {
          outOfSpecCount,
          alertActionCount,
          pendingReviewCount,
          pendingApprovalCount
        }
      };
    } catch {
      return {
        totalTests: { title: "Total Tests", value: "0", deltaPercent: 0, deltaDirection: "up", comparisonLabel: "no data" },
        approvedResults: { title: "Approved Results", value: "0", deltaPercent: 0, deltaDirection: "up", comparisonLabel: "no data" },
        pendingReview: { title: "Pending Review", value: 0, deltaPercent: 0, deltaDirection: "down", comparisonLabel: "no data" },
        pendingApproval: { title: "Pending Approval", value: 0, deltaPercent: 0, deltaDirection: "down", comparisonLabel: "no data" },
        outOfSpec: { title: "Out of Spec", value: 0, deltaPercent: 0, deltaDirection: "down", comparisonLabel: "no data", variant: "error" },
        alertActionLevel: { title: "Alert / Action Level", value: 0, deltaPercent: 0, deltaDirection: "down", comparisonLabel: "no data", variant: "warning" },
        categoryDistribution: [],
        testDistribution: [],
        locationDistribution: [],
        recentResults: [],
        qualitySignals: { outOfSpecCount: 0, alertActionCount: 0, pendingReviewCount: 0, pendingApprovalCount: 0 }
      };
    }
  }
};
