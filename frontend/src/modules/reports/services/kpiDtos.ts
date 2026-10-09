// Wire shapes of /api/kpi/* (backend KpiService.cs records), camelCase JSON.
import type { SampleCategory } from "../types/reportingTypes";
export interface AnalystKpiDto {
  userId: number;
  username: string;
  fullName: string;
  completedTests: number;
  pendingTests: number;
  averageTurnaroundHours: number;
  medianTurnaroundHours: number;
  workloadUnits: number;
  reviewReturns: number;
  docCorrections: number;
}

export interface CompletionStatsDto {
  totalTestOrders: number;
  approved: number;
  rejected: number;
  pending: number;
  approvalRatePercent: number;
}

export interface DelayTrackingDto {
  delayedCount: number;
  averageDelayHours: number;
}

export interface SampleQueueCountsDto {
  reviewQueueCount: number;
  approvalQueueCount: number;
}

export interface SampleSlaOutcomeDto {
  totalAssigned: number;
  onTimeCount: number;
  overdueCount: number;
}

export type OverallOnTimeOutcomeDto = SampleSlaOutcomeDto;

export interface StepViolationOutcomeDto {
  totalAssignedTests: number;
  violationCount: number;
  testsWithViolationCount: number;
}

export interface StageTatSummaryDto {
  testingAvgDays: number | null;
  testingMedianDays: number | null;
  reviewAvgDays: number | null;
  approvalAvgDays: number | null;
  totalAvgDays: number | null;
}

export interface MonthlyTatPoint {
  month: string;
  avgTestingDays: number | null;
}

export interface WorkflowBottleneckDeltaDto {
  testingQueueDeltaPercent: number;
  reviewQueueDeltaPercent: number;
  approvalQueueDeltaPercent: number;
}

export interface WorkloadWeightDto {
  testCode: string;
  testName: string;
  category: SampleCategory;
  workloadWeight: number;
  effectiveDate: string;
  status: string;
  reasonForChange: string | null;
  changedBy: string;
  changedAt: string;
}
