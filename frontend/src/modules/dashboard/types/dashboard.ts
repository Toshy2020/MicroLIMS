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
  isReturned?: boolean;
  returnReason?: string | null;
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
  isReturned?: boolean;
  returnReason?: string | null;
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

export interface AnalystMetrics {
  testsCompletedToday: number;
  mediaLotsPreparedToday: number;
  activeAssignedOrders: number;
  onTimeReadingRate: number;
  trailing7DayVolume: number;
}

export interface SectionHeadAttentionItem {
  sampleId: number;
  testOrderId: number | null;
  referenceNumber: string;
  subjectName: string;
  testCode: string;
  urgency: "High" | "Medium" | "Low";
  reason: string;
  actionType: "OverdueTest" | "DelayedReview" | "DelayedApproval" | "RetestRequired" | "OOS";
  timestamp: string;
}

export interface SectionHeadReviewQueueItem {
  sampleId: number;
  testOrderId: number;
  referenceNumber: string;
  subjectName: string;
  category: string;
  testCode: string;
  analystName: string | null;
  resultEnteredAt: string;
  ageHours: number;
  resultLevel: string | null;
  reportedValue: string | null;
  unit: string | null;
}

export interface SectionHeadApprovalQueueItem {
  sampleId: number;
  testOrderId: number;
  referenceNumber: string;
  subjectName: string;
  category: string;
  testCode: string;
  reviewerName: string | null;
  reviewedAt: string;
  ageHours: number;
}

export interface SectionHeadAnalystWorkload {
  analystId: number;
  analystName: string;
  username: string;
  activeCount: number;
  overdueCount: number;
  completedTodayCount: number;
}

export interface SectionHeadDashboard {
  activeTests: number;
  incubating: number;
  readyToRead: number;
  pendingReview: number;
  pendingApproval: number;
  overdue: number;
  attentionCount: number;
  testingBottleneck: number;
  incubationBottleneck: number;
  readyToReadBottleneck: number;
  reviewBottleneck: number;
  approvalBottleneck: number;
  attentionItems: SectionHeadAttentionItem[];
  reviewQueueCount: number;
  reviewQueueOverdueCount: number;
  reviewQueueOldestHours: number;
  reviewQueueItems: SectionHeadReviewQueueItem[];
  approvalQueueCount: number;
  approvalQueueOverdueCount: number;
  approvalQueueOldestHours: number;
  approvalQueueItems: SectionHeadApprovalQueueItem[];
  incubationSummary: IncubationOverviewRow[];
  analystWorkloads: SectionHeadAnalystWorkload[];
}

export interface ReviewerQueueItem {
  sampleId: number;
  testOrderId: number;
  referenceNumber: string;
  subjectName: string;
  category: string;
  testCode: string;
  testDisplayName: string;
  analystName: string | null;
  resultEnteredAt: string;
  ageMinutes: number;
  priority: "High" | "Medium" | "Normal";
  resultLevel: string | null;
  reportedValue: string | null;
  unit: string | null;
}

export interface ReviewerRecentlyReviewed {
  sampleId: number;
  testOrderId: number;
  referenceNumber: string;
  subjectName: string;
  category: string;
  testCode: string;
  reviewedAt: string;
  status: string;
  comment: string | null;
}

export interface ReviewerAttentionItem {
  sampleId: number;
  testOrderId: number;
  referenceNumber: string;
  subjectName: string;
  testCode: string;
  urgency: string;
  reason: string;
  timestamp: string;
}

export interface ReviewerDashboard {
  pendingReviewCount: number;
  overdueReviewCount: number;
  dueTodayCount: number;
  returnedCount: number;
  completedTodayCount: number;
  reviewQueue: ReviewerQueueItem[];
  attentionItems: ReviewerAttentionItem[];
  recentlyReviewed: ReviewerRecentlyReviewed[];
}

