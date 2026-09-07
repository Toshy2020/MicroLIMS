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

// Colour follows the entity, never its position in a count-sorted list.
// Keying off the array index meant a date-range change re-sorted the data and
// repainted everything - "Water is the dark one" stopped being true the moment
// the period changed - and, taken modulo the palette length, a seventh
// location reused the first one's colour.
//
// Sample categories are a closed set and the palette has exactly as many
// slots, so they get a fixed assignment: a category wears the same colour in
// every chart, in every period, forever.
const CATEGORY_COLOR_SLOT: Record<string, number> = {
  FinishedProduct: 0,
  RawMaterial: 1,
  PackagingMaterial: 2,
  Water: 3,
  EnvironmentalMonitoring: 4,
  AfterCleaning: 5,
  GPT: 6
};

function preferredSlot(key: string, slots: number): number {
  let hash = 0;
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) | 0;
  }
  return Math.abs(hash) % slots;
}

// Locations are open-ended, so they cannot have a fixed map. A bare hash is
// not good enough on its own: across the seven real sampling points it put
// three of them on the same green and two more pairs on a shared colour, which
// is a worse failure than the cycling it replaced - two slices of one donut
// reading as the same thing. So the hash only proposes a slot and we probe
// forward to the next free one, which guarantees distinct colours within a
// chart while the palette lasts and still keeps an entity on its own colour
// between periods.
function assignDistinctColors(keys: string[], palette: string[]): Map<string, string> {
  const used = new Set<number>();
  const assigned = new Map<string, string>();

  for (const key of keys) {
    let slot = preferredSlot(key, palette.length);
    for (let probe = 0; probe < palette.length && used.has(slot); probe++) {
      slot = (slot + 1) % palette.length;
    }
    used.add(slot);
    assigned.set(key, palette[slot]);
  }

  return assigned;
}

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
        color: activePalette[(CATEGORY_COLOR_SLOT[c.category] ?? 0) % activePalette.length]
      }));

      const testDistribution = (res.testDistribution ?? []).map((t) => ({
        testCode: t.testCode,
        testName: t.testName || t.testCode,
        count: t.count
      }));

      const locationColors = assignDistinctColors(
        (res.locationDistribution ?? []).map((l) => l.location || "Other"),
        activePalette
      );

      const locationDistribution = (res.locationDistribution ?? []).map((l) => ({
        location: l.location || "Other",
        count: l.count,
        percentage: l.percentage,
        color: locationColors.get(l.location || "Other") ?? activePalette[0]
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
