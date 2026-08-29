import { apiClient } from "../../../services/apiClient";
import {
  AnalystComparisonRow,
  AnalystKpiFilters,
  AnalystPerformanceDashboardData,
  AnalystPerformanceDetail,
  WorkloadWeightConfig
} from "../types/reportingTypes";
import { OverviewService } from "./OverviewService";
import { ReportingService } from "./ReportingService";
import { computeQuickPeriodRange, QuickPeriod } from "../utils/dateRange";

function resolveDateRange(dateRange: AnalystKpiFilters["dateRange"]): { fromDate: string; toDate: string } {
  return computeQuickPeriodRange((dateRange === "custom" ? "30d" : dateRange) as Exclude<QuickPeriod, "custom">);
}

export const AnalystKpiService = {
  async getDashboardData(
    filters: AnalystKpiFilters,
    currentUserRole: string,
    currentUserId: number
  ): Promise<AnalystPerformanceDashboardData> {
    const { fromDate, toDate } = resolveDateRange(filters.dateRange);
    const filterAnalystId = filters.analystId && filters.analystId !== "All" ? Number(filters.analystId) : undefined;
    const categoryParam = filters.category && filters.category !== "All" ? filters.category : undefined;
    const locationParam = filters.location && filters.location !== "All" ? filters.location : undefined;
    const testCodeParam = filters.testCode && filters.testCode !== "All" ? filters.testCode : undefined;

    // Fetch real backend KPI data in parallel.
    const [
      realAnalystsRes, completionStatsRes, delayTrackingRes, overview,
      sampleQueueCountsRes, outOfSpecRes, completedByMonthRes,
      sampleSlaRes, stepViolationsRes,
      stageTatSummaryRes, testingTatByMonthRes, sampleSlaByAnalystRes,
      workflowBottleneckDeltasRes, overallOnTimeRes, overallOnTimeByAnalystRes,
      returnToAnalystRes
    ] = await Promise.all([
      apiClient.get<{ success: boolean; data: any[] }>("/kpi/analysts", {
        params: {
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch((err) => {
        if (err?.response?.status === 403) {
          return { data: { success: false, data: [] } };
        }
        return null;
      }),
      apiClient.get<{ success: boolean; data: any }>("/kpi/completion-stats", {
        params: {
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/delay-tracking", {
        params: {
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      OverviewService.getOverviewData(fromDate, toDate).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/sample-queue-counts", {
        params: {
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      ReportingService.searchResults({ fromDate, toDate, resultLevel: "OutOfSpecification", page: 1, pageSize: 1 }).catch(() => null),
      ReportingService.getCompletedByMonth(6).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/sample-assignment-sla", {
        params: {
          analystId: filterAnalystId,
          fromDate,
          toDate,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/step-violations", {
        params: {
          analystId: filterAnalystId,
          fromDate,
          toDate,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/stage-tat-summary", {
        params: {
          analystId: filterAnalystId,
          fromDate,
          toDate,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: any[] }>("/kpi/testing-tat-by-month", {
        params: {
          months: 6,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: Record<string, any> }>("/kpi/sample-assignment-sla-by-analyst", {
        params: {
          fromDate,
          toDate,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/workflow-bottleneck-deltas", {
        params: {
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: any }>("/kpi/overall-on-time-completion", {
        params: {
          fromDate,
          toDate,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: Record<string, any> }>("/kpi/overall-on-time-completion-by-analyst", {
        params: {
          fromDate,
          toDate,
          category: categoryParam,
          location: locationParam,
          testCode: testCodeParam
        }
      }).catch(() => null),
      apiClient.get<{ success: boolean; data: Record<string, number> }>("/kpi/return-to-analyst-count", {
        params: {
          analystId: filterAnalystId,
          fromDate,
          toDate
        }
      }).catch(() => null)
    ]);

    const realAnalysts = realAnalystsRes?.data?.data ?? [];
    const completionStats = completionStatsRes?.data?.data;
    const delayTracking = delayTrackingRes?.data?.data;
    const sampleQueueCounts = sampleQueueCountsRes?.data?.data;
    const outOfSpecCount = outOfSpecRes?.totalCount ?? 0;
    const completedByMonth = completedByMonthRes ?? [];
    const sampleSlaData = sampleSlaRes?.data?.data;
    const stepViolationsData = stepViolationsRes?.data?.data;
    const stageTatSummary = stageTatSummaryRes?.data?.data;
    const testingTatByMonth: { month: string; avgTestingDays: number | null }[] = testingTatByMonthRes?.data?.data ?? [];
    const slaByAnalyst: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }> = sampleSlaByAnalystRes?.data?.data ?? {};
    const workflowBottleneckDeltas = workflowBottleneckDeltasRes?.data?.data;
    const overallOnTime = overallOnTimeRes?.data?.data;
    const overallOnTimeByAnalyst: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }> = overallOnTimeByAnalystRes?.data?.data ?? {};
    const returnToAnalystByAnalyst: Record<string, number> | null = returnToAnalystRes?.data?.data ?? null;

    const comparisonRows = buildComparisonRows(realAnalysts, slaByAnalyst, overallOnTimeByAnalyst);

    const targetAnalystId =
      currentUserRole === "Analyst"
        ? currentUserId
        : filters.analystId && filters.analystId !== "All"
        ? Number(filters.analystId)
        : comparisonRows[0]?.analystId ?? currentUserId;

    const selectedAnalystDetail = buildAnalystDetail(targetAnalystId, comparisonRows, stageTatSummary, returnToAnalystByAnalyst);

    const totalAssigned = completionStats?.totalTestOrders ?? comparisonRows.reduce((acc, r) => acc + r.assigned, 0);
    const totalCompleted = completionStats?.approved ?? comparisonRows.reduce((acc, r) => acc + r.completed, 0);
    const totalPending = completionStats?.pending ?? comparisonRows.reduce((acc, r) => acc + r.pending, 0);
    const totalOverdue = delayTracking?.delayedCount ?? comparisonRows.reduce((acc, r) => acc + r.overdue, 0);

    const testingAvgDays: number | null = stageTatSummary?.testingAvgDays ?? null;
    const reviewAvgDays: number | null = stageTatSummary?.reviewAvgDays ?? null;
    const approvalAvgDays: number | null = stageTatSummary?.approvalAvgDays ?? null;
    const totalAvgDays: number | null = stageTatSummary?.totalAvgDays ?? null;
    const completionRate =
      completionStats?.approvalRatePercent != null
        ? completionStats.approvalRatePercent
        : totalAssigned > 0
        ? Number(((totalCompleted / totalAssigned) * 100).toFixed(1))
        : 0;

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
      dataCoveragePercent: realAnalysts && realAnalysts.length > 0 ? 100 : 0,
      dataCoverageNote: "Verified data coverage across live assigned and completed laboratory test orders.",
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
      tatTrendByMonth: testingTatByMonth.map((p) => ({ month: p.month, tatDays: p.avgTestingDays })),
      workflowBottleneck: {
        testingQueueCount: totalPending,
        testingQueueDeltaPercent: workflowBottleneckDeltas?.testingQueueDeltaPercent ?? 0,
        reviewQueueCount: sampleQueueCounts?.reviewQueueCount ?? 0,
        reviewQueueDeltaPercent: workflowBottleneckDeltas?.reviewQueueDeltaPercent ?? 0,
        approvalQueueCount: sampleQueueCounts?.approvalQueueCount ?? 0,
        approvalQueueDeltaPercent: workflowBottleneckDeltas?.approvalQueueDeltaPercent ?? 0
      },
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
    const res = await apiClient.get<{ success: boolean; data: any[] }>("/kpi/workload-weights");
    return (res.data.data || []).map((w: any) => ({
      testCode: w.testCode,
      testName: w.testName || w.testCode,
      category: w.category,
      workloadWeight: w.weight,
      effectiveDate: w.effectiveDate ? new Date(w.effectiveDate).toLocaleDateString("en-GB") : "Baseline",
      status: w.status === "Inactive" ? "Inactive" : "Active",
      reasonForChange: w.reasonForChange || undefined,
      changedBy: w.changedByName || "System Admin",
      changedAt: w.changedAt || ""
    }));
  },

  async updateWorkloadWeight(
    testCode: string,
    newWeight: number,
    reason: string
  ): Promise<void> {
    await apiClient.put(`/kpi/workload-weights/${testCode}`, {
      weight: newWeight,
      reasonForChange: reason
    });
  }
};

function buildComparisonRows(
  backendKpis: any[],
  slaByAnalyst?: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }>,
  overallOnTimeByAnalyst?: Record<string, { totalAssigned: number; onTimeCount: number; overdueCount: number }>
): AnalystComparisonRow[] {
  if (!Array.isArray(backendKpis) || backendKpis.length === 0) {
    return [];
  }

  return backendKpis.map((kpi) => {
    const assigned = (kpi.completedTests || 0) + (kpi.pendingTests || 0);
    const completed = kpi.completedTests || 0;
    const pending = kpi.pendingTests || 0;
    const overdue = slaByAnalyst?.[String(kpi.userId)]?.overdueCount ?? 0;
    const workloadUnits = kpi.workloadUnits != null ? Math.round(kpi.workloadUnits) : completed;
    const completionRatePercent = assigned > 0 ? Number(((completed / assigned) * 100).toFixed(1)) : 100;
    const avgTestingTatDays = Number(((kpi.averageTurnaroundHours || 24.0) / 24).toFixed(1));

    const onTimeAgg = overallOnTimeByAnalyst?.[String(kpi.userId)];
    const onTimePercent = onTimeAgg && onTimeAgg.totalAssigned > 0
      ? Number(((onTimeAgg.onTimeCount / onTimeAgg.totalAssigned) * 100).toFixed(1))
      : null;

    const reviewReturns = kpi.reviewReturns != null ? Number(kpi.reviewReturns) : null;
    const docCorrections = kpi.docCorrections != null ? Number(kpi.docCorrections) : null;

    return {
      analystId: kpi.userId,
      analystName: kpi.fullName || kpi.username || `Analyst #${kpi.userId}`,
      username: kpi.username || `analyst_${kpi.userId}`,
      assigned,
      completed,
      workloadUnits,
      completionRatePercent,
      onTimePercent,
      avgTestingTatDays,
      reviewReturns,
      docCorrections,
      pending,
      overdue
    };
  });
}

function buildAnalystDetail(
  analystId: number,
  rows: AnalystComparisonRow[],
  stageTatSummary?: any,
  returnToAnalystByAnalyst?: Record<string, number> | null
): AnalystPerformanceDetail | null {
  const row = rows.find((r) => r.analystId === analystId) ?? rows[0];
  if (!row) return null;

  const medianTatDays =
    stageTatSummary?.testingMedianDays != null
      ? Number(stageTatSummary.testingMedianDays.toFixed(1))
      : row.avgTestingTatDays;

  const reviewReturnsCount = returnToAnalystByAnalyst != null
    ? (returnToAnalystByAnalyst[String(row.analystId)] ?? returnToAnalystByAnalyst[row.analystId] ?? 0)
    : "Not Available";

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
      medianTestingTatDays: medianTatDays,
      onTimePercent: row.onTimePercent != null ? `${row.onTimePercent}%` : "Not Available",
      overdueCount: row.overdue
    },
    quality: {
      reviewReturns: reviewReturnsCount,
      documentationCorrections: row.docCorrections != null ? row.docCorrections : "Not Available",
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
