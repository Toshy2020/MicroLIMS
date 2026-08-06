export interface DashboardSummary {
  pendingTests: number;
  delayedTests: number;
  samplesToday: number;
  reviewerQueue: number;
  approvalQueue: number;
  preparationQueue: number;
  incubatingCount: number;
  readyToReadCount: number;
}

export interface KpiDeltas {
  samplesThisMonth: number;
  samplesDeltaPercent: number;
  testsThisMonth: number;
  testsDeltaPercent: number;
  totalSamples: number;
  totalTests: number;
}

export interface MonthlyTrendPoint { month: string; samplesLodged: number; testsLodged: number }
export interface DistributionSlice { category?: string; status?: string; count: number; percent: number }

export interface NotificationItem {
  id: number | null;
  type: string;
  message: string;
  timestamp: string;
  severity: string;
  isRead: boolean;
}

export type TaskUrgency = "Overdue" | "DueSoon" | "DueToday" | "DueTomorrow";

export interface MyTask {
  taskType: string;
  title: string;
  subtitle: string;
  referenceId: string;
  dueAt: string;
  urgency: TaskUrgency;
  sampleId: number | null;
  testOrderId: number | null;
  mediaId: number | null;
}

export interface MediaExpiryLot {
  mediaId: number;
  lotNumber: string;
  mediaTypeName: string;
  expiryDate: string;
  daysRemaining: number;
  evaluationStatus: string;
}

export interface TodaysWorkTest {
  testOrderId: number;
  testCode: string;
  status: string;
  timeRemaining: string | null;
}

export interface TodaysWorkItem {
  sampleId: number;
  referenceNumber: string;
  category: string;
  displayName: string;
  receivedAt: string;
  overallStatus: string;
  nextAction: string;
  tests: TodaysWorkTest[];
}

export interface IncubationOverviewRow {
  testCode: string;
  readyToRead: number;
  incubating: number;
}
