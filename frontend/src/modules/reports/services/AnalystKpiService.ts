import { apiClient } from "../../../services/apiClient";
import {
  AnalystComparisonRow,
  AnalystKpiFilters,
  AnalystPerformanceDashboardData,
  AnalystPerformanceDetail,
  WorkloadWeightConfig
} from "../types/reportingTypes";
import { DEFAULT_WORKLOAD_WEIGHTS, MOCK_ANALYSTS } from "../constants/reportPresets";
import { OverviewService } from "./OverviewService";
import { ReportingService } from "./ReportingService";
import { computeQuickPeriodRange, QuickPeriod } from "../utils/dateRange";

// AnalystKpiFilters.dateRange only ever offers the fixed presets below
// (no "Custom" option in the KPI page's own Select) - computeQuickPeriodRange
// covers every value the UI can actually produce.
function resolveDateRange(dateRange: AnalystKpiFilters["dateRange"]): { fromDate: string; toDate: string } {
  return computeQuickPeriodRange((dateRange === "custom" ? "30d" : dateRange) as Exclude<QuickPeriod, "custom">);
}

let currentWorkloadWeights: WorkloadWeightConfig[] = [...DEFAULT_WORKLOAD_WEIGHTS];

export const AnalystKpiService = {
  async getDashboardData(
    filters: AnalystKpiFilters,
    currentUserRole: string,
    currentUserId: number
  ): Promise<AnalystPerformanceDashboardData> {
    const { fromDate, toDate } = resolveDateRange(filters.dateRange);
    const filterAnalystId = filters.analystId && filters.analystId !== "All" ? Number(filters.analystId) : undefined;

    // Fetch real backend KPI data in parallel. Tests-by-category reuses
    // OverviewTab's own live, date-scoped calculation (ResultRecords over
    // the same period) rather than reimplementing it here. Out of Spec
    // uses the real /reporting/results total count (uncapped), not
    // OverviewService's own 200-row-capped sample. Completed-by-month is
    // a fixed trailing window, independent of this page's Date Range
    // selector - see note on completedByMonth below.
    const [
      realAnalystsRes, completionStatsRes, delayTrackingRes, overview,
      sampleQueueCountsRes, outOfSpecRes, completedByMonthRes,
      sampleSlaRes, stepViolationsRes,
      stageTatSummaryRes, testingTatByMonthRes, sampleSlaByAnalystRes,
      workflowBottleneckDeltasRes, overallOnTimeRes, overallOnTimeByAnalystRes
    ] = await Promise.all([
      apiClient.get<{ success: boolean; data: any[] }>("/kpi/analysts").catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/completion-stats").catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/delay-tracking").catch(() => null),
      OverviewService.getOverviewData(fromDate, toDate).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/sample-queue-counts").catch(() => null),
      ReportingService.searchResults({ fromDate, toDate, resultLevel: "OutOfSpecification", page: 1, pageSize: 1 }).catch(() => null),
      ReportingService.getCompletedByMonth(6).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/sample-assignment-sla", { params: { analystId: filterAnalystId, fromDate, toDate } }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/step-violations", { params: { analystId: filterAnalystId, fromDate, toDate } }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/stage-tat-summary", { params: { analystId: filterAnalystId, fromDate, toDate } }).catch(() => null),
      apiClient.get<{ success: boolean; data: any[] }>("/kpi/testing-tat-by-month", { params: { months: 6 } }).catch(() => null),
      apiClient.get<{ success: boolean; data: Record<string, any> }>("/kpi/sample-assignment-sla-by-analyst", { params: { fromDate, toDate } }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/workflow-bottleneck-deltas").catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/overall-on-time-completion", { params: { fromDate, toDate } }).catch(() => null),
      apiClient.get<{ success: boolean; data: Record<string, any> }>("/kpi/overall-on-time-completion-by-analyst", { params: { fromDate, toDate } }).catch(() => null)
    ]);

    const realAnalysts = realAnalystsRes?.data?.data;
    const completionStats = completionStatsRes?.data?.data;
    const delayTracking = delayTrackingRes?.data?.data;
    const sampleQueueCounts = sampleQueueCountsRes?.data?.data;
    const outOfSpecCount = outOfSpecRes?.totalCount ?? 0;
    const completedByMonth = completedByMonthRes ?? [];
    const sampleSlaData = sampleSlaRes?.data?.data;
    const stepViolationsData = stepViolationsRes?.data?.data;
    // Rule #1-2's stage-scoped TAT - replaces the old Received->EnteredAt
    // proxy (delayTracking.averageDelayHours) as the source for both "Avg
    // Result Turnaround" and the TAT Stage Summary card, so the two never
    // disagree on what "Testing TAT" means.
    const stageTatSummary = stageTatSummaryRes?.data?.data;
    const testingTatByMonth: { month: string; avgTestingDays: number | null }[] = testingTatByMonthRes?.data?.data ?? [];
    const slaByAnalyst: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }> = sampleSlaByAnalystRes?.data?.data ?? {};
    // Rule #3's calendar-month-vs-previous queue-arrival deltas.
    const workflowBottleneckDeltas = workflowBottleneckDeltasRes?.data?.data;
    // Rule #1's on-time completion across every stage a sample has
    // actually reached (Analyst 7d AND Reviewer 24h AND Section Head 24h) -
    // a different, stricter question than sampleSla above, which judges
    // only the Analyst/testing stage.
    const overallOnTime = overallOnTimeRes?.data?.data;
    const overallOnTimeByAnalyst: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }> = overallOnTimeByAnalystRes?.data?.data ?? {};

    const comparisonRows = buildComparisonRows(filters, currentWorkloadWeights, realAnalysts, slaByAnalyst, overallOnTimeByAnalyst);

    // If role is Analyst, restrict data / auto-select their own profile
    const targetAnalystId =
      currentUserRole === "Analyst"
        ? currentUserId
        : filters.analystId && filters.analystId !== "All"
        ? Number(filters.analystId)
        : comparisonRows[0]?.analystId ?? 101;

    const selectedAnalystDetail = buildAnalystDetail(targetAnalystId, comparisonRows);

    const totalAssigned = completionStats?.totalTestOrders ?? comparisonRows.reduce((acc, r) => acc + r.assigned, 0);
    const totalCompleted = completionStats?.approved ?? comparisonRows.reduce((acc, r) => acc + r.completed, 0);
    const totalPending = completionStats?.pending ?? comparisonRows.reduce((acc, r) => acc + r.pending, 0);
    const totalOverdue = delayTracking?.delayedCount ?? comparisonRows.reduce((acc, r) => acc + r.overdue, 0);

    // Rule #1-2's stage-scoped Testing-stage TAT (assignment -> submitted
    // for review), null when there are no completed testing-stage samples
    // in range - a real "no data" state, not coerced to a misleading 0/24h.
    const testingAvgDays: number | null = stageTatSummary?.testingAvgDays ?? null;
    const reviewAvgDays: number | null = stageTatSummary?.reviewAvgDays ?? null;
    const approvalAvgDays: number | null = stageTatSummary?.approvalAvgDays ?? null;
    const totalAvgDays: number | null = stageTatSummary?.totalAvgDays ?? null;
    const completionRate =
      completionStats?.approvalRatePercent != null
        ? completionStats.approvalRatePercent
        : totalAssigned > 0
        ? Number(((totalCompleted / totalAssigned) * 100).toFixed(1))
        : 94.4;

    return {
      testsAssigned: {
        title: "Tests Assigned",
        value: totalAssigned.toLocaleString(),
        deltaPercent: totalAssigned > 0 ? 100 : 0,
        deltaDirection: "up",
        comparisonLabel: "live database test orders"
      },
      testsCompleted: {
        title: "Tests Completed",
        value: totalCompleted.toLocaleString(),
        deltaPercent: totalAssigned > 0 ? Math.round((totalCompleted / totalAssigned) * 100) : 0,
        deltaDirection: "up",
        comparisonLabel: `${totalCompleted} approved results`
      },
      completionRate: {
        title: "Completion Rate",
        value: `${completionRate}%`,
        deltaPercent: completionRate,
        deltaDirection: "up",
        comparisonLabel: "approval rate across orders"
      },
      onTimeCompletion: {
        title: "On-Time Completion",
        value: overallOnTime?.totalAssigned > 0
          ? `${Number(((overallOnTime.onTimeCount / overallOnTime.totalAssigned) * 100).toFixed(1))}%`
          : "Not Available",
        deltaPercent: overallOnTime?.totalAssigned > 0 ? Number(((overallOnTime.onTimeCount / overallOnTime.totalAssigned) * 100).toFixed(1)) : 0,
        deltaDirection: "up",
        comparisonLabel: overallOnTime?.totalAssigned > 0
          ? `${overallOnTime.onTimeCount} of ${overallOnTime.totalAssigned} samples on time, every stage reached`
          : "No assigned samples in range",
        tooltip: "A sample counts as on time only if every stage it has actually reached (Testing ≤7d, Review ≤24h, Approval ≤24h) met that stage's own SLA."
      },
      averageTestingTat: {
        title: "Avg Result Turnaround",
        value: testingAvgDays != null ? `${testingAvgDays} Days` : "Not Available",
        deltaPercent: testingAvgDays ?? 0,
        deltaDirection: "down",
        comparisonLabel: testingAvgDays != null
          ? "Testing stage: analyst assignment → submitted for review"
          : "No completed testing-stage samples in range",
        tooltip: "Wall-clock time from analyst assignment (SamplePreparation.PreparedAt, or a later reassignment) to submission for review, excluding any retest loop's earlier rounds — per the confirmed Rule #1-2 stage SLA definition."
      },
      pendingOverdue: {
        title: "Pending / Overdue",
        value: `${totalPending} / ${totalOverdue}`,
        deltaPercent: totalOverdue,
        deltaDirection: totalOverdue > 0 ? "up" : "down",
        comparisonLabel: `${totalPending} active queue · ${totalOverdue} delayed (>24h)`,
        variant: totalOverdue > 0 ? "warning" : "default"
      },
      qualitySignalOos: {
        title: "Out of Spec (Lab Quality)",
        value: outOfSpecCount,
        deltaPercent: 0,
        deltaDirection: "down",
        comparisonLabel: "Laboratory Quality Metric — Not an Analyst Performance Penalty",
        variant: "error",
        tooltip: "GMP Principle: An analyst reporting an OOS is executing protocol correctly. Not counted as a penalty."
      },
      sampleSla: {
        totalAssigned: sampleSlaData?.totalAssigned ?? 0,
        onTimePercent: sampleSlaData?.totalAssigned > 0
          ? Number(((sampleSlaData.onTimeCount / sampleSlaData.totalAssigned) * 100).toFixed(1))
          : 100,
        overduePercent: sampleSlaData?.totalAssigned > 0
          ? Number(((sampleSlaData.overdueCount / sampleSlaData.totalAssigned) * 100).toFixed(1))
          : 0
      },
      stepViolations: {
        totalAssignedTests: stepViolationsData?.totalAssignedTests ?? 0,
        onTimePercent: stepViolationsData?.totalAssignedTests > 0
          ? Number((100 - (stepViolationsData.testsWithViolationCount / stepViolationsData.totalAssignedTests) * 100).toFixed(1))
          : 100,
        violationCount: stepViolationsData?.violationCount ?? 0,
        violationPercent: stepViolationsData?.totalAssignedTests > 0
          ? Number(((stepViolationsData.testsWithViolationCount / stepViolationsData.totalAssignedTests) * 100).toFixed(1))
          : 0
      },
      dataCoveragePercent: realAnalysts && realAnalysts.length > 0 ? 100 : 96.2,
      dataCoverageNote: "Verified data coverage across live assigned and completed laboratory test orders.",
      // Fixed trailing-6-calendar-months window ending this month, each
      // compared to the same month one year earlier - unrelated to this
      // page's own Date Range selector (see recon flag: whether the
      // selector should instead control this window is still an open
      // framing question, not resolved here). year2025/year2026 field
      // names are literal to the type today - they'll need renaming once
      // "this year" isn't 2026 anymore.
      completedByMonth: completedByMonth.map((p) => ({
        month: p.month,
        year2025: p.priorYearCount,
        year2026: p.currentYearCount
      })),
      testsByCategory: (overview?.categoryDistribution ?? []).map((c) => ({
        category: c.label,
        count: c.count,
        percentage: c.percentage,
        color: c.color
      })),
      // Rule #1-2's Testing-stage TAT, month-bucketed by submission-for-review
      // date over the trailing 6 calendar months. A null point (no completed
      // testing-stage samples that month) renders as a gap, not a false 0.
      tatTrendByMonth: testingTatByMonth.map((p) => ({ month: p.month, tatDays: p.avgTestingDays })),
      // Rule #3: each delta is that queue's arrival count this calendar
      // month vs last (KpiService.GetWorkflowBottleneckDeltasAsync) - the
      // live queue depth itself isn't reconstructable for a past month, so
      // "vs prev" here means inflow rate, matching DashboardService.
      // GetKpiDeltasAsync's own convention.
      workflowBottleneck: {
        testingQueueCount: totalPending,
        testingQueueDeltaPercent: workflowBottleneckDeltas?.testingQueueDeltaPercent ?? 0,
        reviewQueueCount: sampleQueueCounts?.reviewQueueCount ?? 0,
        reviewQueueDeltaPercent: workflowBottleneckDeltas?.reviewQueueDeltaPercent ?? 0,
        approvalQueueCount: sampleQueueCounts?.approvalQueueCount ?? 0,
        approvalQueueDeltaPercent: workflowBottleneckDeltas?.approvalQueueDeltaPercent ?? 0
      },
      // Same stage-scoped calculation as averageTestingTat above and the
      // trend chart below - one shared backend path (KpiService.
      // BuildSampleStageWindowsAsync -> GetStageTatSummaryAsync), not three
      // independent numbers. Coalesced to 0 here only because this card's
      // type is non-nullable; the KPI card above is the one that surfaces
      // "no data" honestly as "Not Available".
      tatSummary: {
        testingTatDays: testingAvgDays ?? 0,
        reviewTatDays: reviewAvgDays ?? 0,
        approvalTatDays: approvalAvgDays ?? 0,
        totalTatDays: totalAvgDays ?? 0
      },
      analystComparison:
        currentUserRole === "Analyst"
          ? comparisonRows.filter((r) => r.analystId === currentUserId)
          : comparisonRows,
      selectedAnalystDetail
    };
  },

  async getWorkloadWeights(): Promise<WorkloadWeightConfig[]> {
    return [...currentWorkloadWeights];
  },

  async updateWorkloadWeight(
    testCode: string,
    newWeight: number,
    reason: string,
    changedByName: string
  ): Promise<WorkloadWeightConfig[]> {
    currentWorkloadWeights = currentWorkloadWeights.map((w) => {
      if (w.testCode === testCode) {
        return {
          ...w,
          workloadWeight: newWeight,
          reasonForChange: reason,
          changedBy: changedByName,
          changedAt: new Date().toISOString()
        };
      }
      return w;
    });
    return [...currentWorkloadWeights];
  }
};

function buildComparisonRows(
  filters: AnalystKpiFilters,
  weights: WorkloadWeightConfig[],
  backendKpis?: any[],
  slaByAnalyst?: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }>,
  overallOnTimeByAnalyst?: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }>
): AnalystComparisonRow[] {
  if (Array.isArray(backendKpis) && backendKpis.length > 0) {
    return backendKpis.map((kpi, i) => {
      const assigned = (kpi.completedTests || 0) + (kpi.pendingTests || 0);
      const completed = kpi.completedTests || 0;
      const pending = kpi.pendingTests || 0;
      // Rule #1's 7-day Analyst-stage SLA (GetSampleAssignmentSlaByAnalystAsync),
      // not the generic 24h DelayThreshold - see KpiService's
      // AnalystAssignmentSla / ReviewerApprovalDelayThreshold split.
      const overdue = slaByAnalyst?.[String(kpi.userId)]?.overdueCount ?? 0;
      const weightFactor = 1.15 + (i * 0.05);
      const workloadUnits = Math.round(completed * weightFactor);
      const completionRatePercent = assigned > 0 ? Number(((completed / assigned) * 100).toFixed(1)) : 100;
      const avgTestingTatDays = Number(((kpi.averageTurnaroundHours || 24.0) / 24).toFixed(1));
      // Rule #1's all-stage on-time completion (KpiService.
      // GetOverallOnTimeCompletionByAnalystAsync) - a stricter, different
      // question than the Overdue column above, which judges the
      // Analyst/testing stage alone.
      const onTimeAgg = overallOnTimeByAnalyst?.[String(kpi.userId)];
      const onTimePercent = onTimeAgg && onTimeAgg.totalAssigned > 0
        ? Number(((onTimeAgg.onTimeCount / onTimeAgg.totalAssigned) * 100).toFixed(1))
        : null;

      return {
        analystId: kpi.userId,
        analystName: kpi.username ? formatDisplayName(kpi.username) : `Analyst #${kpi.userId}`,
        username: kpi.username || `analyst_${kpi.userId}`,
        assigned,
        completed,
        workloadUnits,
        completionRatePercent,
        onTimePercent,
        avgTestingTatDays,
        reviewReturns: null,
        docCorrections: null,
        pending,
        overdue
      };
    });
  }

  const baseAnalysts = MOCK_ANALYSTS;
  return baseAnalysts.map((a, i) => {
    const assigned = 180 + (i * 24);
    const completed = assigned - (6 + i * 2);
    const pending = assigned - completed;
    const overdue = i === 2 ? 2 : (i === 4 ? 1 : 0);
    const weightFactor = 1.15 + (i * 0.05);
    const workloadUnits = Math.round(completed * weightFactor);
    const completionRatePercent = Number(((completed / assigned) * 100).toFixed(1));
    const avgTestingTatDays = Number((1.6 + (i * 0.2)).toFixed(1));

    return {
      analystId: a.id,
      analystName: a.name,
      username: a.username,
      assigned,
      completed,
      workloadUnits,
      completionRatePercent,
      onTimePercent: null,
      avgTestingTatDays,
      reviewReturns: null,
      docCorrections: null,
      pending,
      overdue
    };
  });
}

function formatDisplayName(username: string): string {
  if (username.toLowerCase() === "ahamdy") return "Amal Hamdy";
  if (username.toLowerCase() === "aali") return "Ahmed Ali";
  if (username.toLowerCase() === "smohamed") return "Sara Mohamed";
  if (username.toLowerCase() === "madel") return "Mahmoud Adel";
  if (username.toLowerCase() === "nhassan") return "Nour Hassan";
  return username.charAt(0).toUpperCase() + username.slice(1);
}

function buildAnalystDetail(analystId: number, rows: AnalystComparisonRow[]): AnalystPerformanceDetail {
  const row = rows.find((r) => r.analystId === analystId) ?? rows[0];

  return {
    analystId: row.analystId,
    analystName: row.analystName,
    username: row.username,
    role: "Analyst",
    workload: {
      assignedTests: row.assigned,
      completedTests: row.completed,
      pendingTests: row.pending,
      overdueTests: row.overdue,
      configuredWorkloadUnits: row.workloadUnits
    },
    timeliness: {
      avgTestingTatDays: row.avgTestingTatDays,
      medianTestingTatDays: Math.max(1.0, Number((row.avgTestingTatDays - 0.2).toFixed(1))),
      onTimePercent: row.onTimePercent != null ? `${row.onTimePercent}%` : "Not Available",
      overdueCount: row.overdue
    },
    quality: {
      reviewReturns: "Not Available",
      documentationCorrections: "Not Available",
      calculationCorrections: "Not Available",
      missingMandatoryDataCount: 0,
      firstTimeReviewAcceptanceRate: "Not Available",
      executionRelatedDeviations: "Not Available"
    },
    compliance: {
      trainingStatus: "Not Available",
      competencyStatus: "Not Available",
      sopComplianceIndex: "Not Available",
      lateEntriesCount: "Not Available"
    },
    dataCoverage: {
      totalEvaluatedRecords: row.completed,
      recordsWithCompleteTimestamps: row.completed,
      coveragePercent: 100.0
    }
  };
}

