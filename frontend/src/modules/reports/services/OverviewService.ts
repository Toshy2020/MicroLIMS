import { OverviewDashboardData, SampleCategory } from "../types/reportingTypes";
import { ReportingService } from "./ReportingService";
import { brandColors } from "../../../theme";

// Exported so other Reports tabs (e.g. AnalystKpiTab's category filter and
// "Tests by Category" donut) use the same human labels instead of each
// inventing their own.
export const CATEGORY_LABELS: Record<string, string> = {
  FinishedProduct: "Finished Product",
  RawMaterial: "Raw Material",
  PackagingMaterial: "Packaging Material",
  Water: "Water",
  EnvironmentalMonitoring: "Environmental Monitor",
  AfterCleaning: "After Cleaning"
};

const CATEGORY_COLORS: Record<string, string> = {
  FinishedProduct: brandColors.badgeProduct,
  RawMaterial: brandColors.badgeRM,
  PackagingMaterial: brandColors.badgePM,
  Water: "#0891b2",
  EnvironmentalMonitoring: "#7c3aed",
  AfterCleaning: "#be185d"
};

const LOCATION_COLORS = ["#7b2d8e", "#9b3fa8", "#2563eb", "#0891b2", "#9ca3af", "#d97706", "#16a34a"];

export const OverviewService = {
  async getOverviewData(fromDate?: string, toDate?: string): Promise<OverviewDashboardData> {
    try {
      // Query real results from backend (up to 200 items for period distribution)
      const res = await ReportingService.searchResults({ fromDate, toDate, page: 1, pageSize: 200 }).catch(() => null);
      const items = res?.items ?? [];
      const totalCount = res?.totalCount ?? items.length;

      // Real calculated metrics from ResultRecords
      const approvedCount = items.filter(
        (r) => r.sampleStatus === "Approved" || r.approvalStatus === "Approved"
      ).length;

      const pendingReviewCount = items.filter(
        (r) => r.sampleStatus === "UnderReview" || (r.approvalStatus === "Pending" && r.sampleStatus !== "UnderApproval")
      ).length;

      const pendingApprovalCount = items.filter(
        (r) => r.sampleStatus === "UnderApproval"
      ).length;

      const outOfSpecCount = items.filter(
        (r) => r.resultLevel === "OutOfSpecification"
      ).length;

      const alertActionCount = items.filter(
        (r) => r.resultLevel === "AlertLevel" || r.resultLevel === "ActionLevel"
      ).length;

      // Real category distribution
      const categoryMap = new Map<string, number>();
      items.forEach((r) => {
        const cat = r.category || "FinishedProduct";
        categoryMap.set(cat, (categoryMap.get(cat) ?? 0) + 1);
      });

      const categoryDistribution = Array.from(categoryMap.entries()).map(([cat, count]) => ({
        category: cat as SampleCategory,
        label: CATEGORY_LABELS[cat] ?? cat,
        count,
        percentage: items.length > 0 ? Math.round((count / items.length) * 100) : 0,
        color: CATEGORY_COLORS[cat] ?? "#6b7280"
      }));

      // Real test distribution
      const testMap = new Map<string, { name: string; count: number }>();
      items.forEach((r) => {
        const code = r.testCode || "OTHER";
        const name = r.testDisplayName || code;
        const current = testMap.get(code) ?? { name, count: 0 };
        testMap.set(code, { name, count: current.count + 1 });
      });

      const testDistribution = Array.from(testMap.entries())
        .map(([testCode, data]) => ({
          testCode,
          testName: data.name,
          count: data.count
        }))
        .sort((a, b) => b.count - a.count)
        .slice(0, 10);

      // Real subject / location distribution
      const locationMap = new Map<string, number>();
      items.forEach((r) => {
        const loc = r.subjectDetail || r.subjectName || "Other";
        locationMap.set(loc, (locationMap.get(loc) ?? 0) + 1);
      });

      const locationDistribution = Array.from(locationMap.entries())
        .map(([location, count], idx) => ({
          location,
          count,
          percentage: items.length > 0 ? Math.round((count / items.length) * 100) : 0,
          color: LOCATION_COLORS[idx % LOCATION_COLORS.length]
        }))
        .sort((a, b) => b.count - a.count)
        .slice(0, 7);

      // Recent activity records
      const recentReports = items.slice(0, 5).map((r, idx) => ({
        id: `REP-${r.referenceNumber || String(idx + 101).padStart(5, "0")}`,
        name: `${r.subjectName} — ${r.testDisplayName || r.testCode}`,
        type: CATEGORY_LABELS[r.category] ?? r.category,
        dateGenerated: r.resultEnteredAt ? new Date(r.resultEnteredAt).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "—",
        generatedBy: r.resultEnteredByName || "Analyst",
        status: (r.approvalStatus === "Approved" || r.sampleStatus === "Approved" ? "Final" : "Draft") as "Final" | "Draft" | "Archived"
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
        recentReports,
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
        recentReports: [],
        qualitySignals: { outOfSpecCount: 0, alertActionCount: 0, pendingReviewCount: 0, pendingApprovalCount: 0 }
      };
    }
  }
};

