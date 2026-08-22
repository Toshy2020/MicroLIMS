// Mirrors backend/MicroLIMS.Domain/Enums and the ReportingController /
// ReportingQueryService DTOs - the flattened ResultRecord projection is
// the single source every Record Search screen reads from.

export type ResultKind = "Quantitative" | "Qualitative";

export type ResultLevel = "WithinLimit" | "AlertLevel" | "ActionLevel" | "OutOfSpecification" | "NotApplicable";

export type SampleCategory =
  | "FinishedProduct"
  | "RawMaterial"
  | "PackagingMaterial"
  | "Water"
  | "EnvironmentalMonitoring"
  | "AfterCleaning"
  | "GPT";

export type SampleStatus =
  | "Received"
  | "InTesting"
  | "UnderReview"
  | "UnderApproval"
  | "Approved"
  | "Rejected"
  | "RetestRequested";

export type ApprovalStatus = "Approved" | "Pending" | "Rejected";

export interface ResultRecordItem {
  id: number;
  sampleId: number;
  testOrderId: number;
  sourceTable: string;
  sourceId: number;
  round: number;
  referenceNumber: string;
  category: SampleCategory;
  subjectName: string;
  subjectDetail: string | null;
  batchNumber: string | null;
  controlNumber: string | null;
  testCode: string;
  testDisplayName: string;
  resultKind: ResultKind;
  numericValue: number | null;
  reportedValue: string;
  unit: string | null;
  isBelowDetectionLimit: boolean;
  detectionLimit: number | null;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  resultLevel: ResultLevel;
  resultEnteredAt: string;
  resultEnteredByUserId: number;
  resultEnteredByName: string;
  sampleStatus: SampleStatus;
  approvalStatus: ApprovalStatus;
  approvedByUserId: number | null;
  approvedByName: string | null;
  approvedAt: string | null;
}

export interface ResultRecordSearchParams {
  search?: string;
  category?: SampleCategory;
  testCode?: string;
  resultLevel?: ResultLevel;
  sampleStatus?: SampleStatus;
  approvalStatus?: ApprovalStatus;
  fromDate?: string;
  toDate?: string;
  subjectName?: string;
  resultKind?: ResultKind;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface ResultRecordSearchResponse {
  items: ResultRecordItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TestCodeOption {
  testCode: string;
  testDisplayName: string;
}

export interface FilterOptionsResponse {
  categories: SampleCategory[];
  testCodes: TestCodeOption[];
  subjectNames: string[];
  units: string[];
}

// ---------------------------------------------------------------------------
// Overview Dashboard Types
// ---------------------------------------------------------------------------

export interface OverviewKpiCardData {
  title: string;
  value: number | string;
  deltaPercent: number;
  deltaDirection: "up" | "down" | "neutral";
  comparisonLabel: string;
  variant?: "default" | "warning" | "error" | "success";
  tooltip?: string;
}

export interface CategoryDistributionItem {
  category: SampleCategory;
  label: string;
  count: number;
  percentage: number;
  color: string;
}

export interface TestDistributionItem {
  testCode: string;
  testName: string;
  count: number;
}

export interface LocationDistributionItem {
  location: string;
  count: number;
  percentage: number;
  color: string;
}

export interface RecentReportItem {
  id: string;
  name: string;
  type: string;
  dateGenerated: string;
  generatedBy: string;
  status: "Final" | "Draft" | "Archived";
}

export interface QualitySignalSummary {
  outOfSpecCount: number;
  alertActionCount: number;
  pendingReviewCount: number;
  pendingApprovalCount: number;
}

export interface OverviewDashboardData {
  totalTests: OverviewKpiCardData;
  approvedResults: OverviewKpiCardData;
  pendingReview: OverviewKpiCardData;
  pendingApproval: OverviewKpiCardData;
  outOfSpec: OverviewKpiCardData;
  alertActionLevel: OverviewKpiCardData;
  categoryDistribution: CategoryDistributionItem[];
  testDistribution: TestDistributionItem[];
  locationDistribution: LocationDistributionItem[];
  recentReports: RecentReportItem[];
  qualitySignals: QualitySignalSummary;
}

// ---------------------------------------------------------------------------
// Report Builder Types
// ---------------------------------------------------------------------------

export type ReportType =
  | "Microbiology Results Report"
  | "Finished Product Report"
  | "Raw Material Report"
  | "Packaging Material Report"
  | "Water Monitoring Report"
  | "Environmental Monitoring Report"
  | "After Cleaning Report"
  | "Analyst Activity Report"
  | "Custom Report";

export type ReportPurpose = "Ad-Hoc Analysis" | "Operational Report" | "Controlled Report";

export type ReportFormat = "PDF (Portrait)" | "PDF (Landscape)" | "Excel (.xlsx)" | "CSV (.csv)";

export interface ReportBuilderCriteria {
  reportType: ReportType;
  reportPurpose: ReportPurpose;
  fromDate?: string;
  toDate?: string;
  category?: SampleCategory | "";
  selectedProducts: string[];
  selectedTests: string[];
  location?: string;
  resultLevel?: ResultLevel | "";
  status?: string;
  approvalStatus?: ApprovalStatus | "";
  analystId?: number | "";
  groupBy: "Product" | "Category" | "Test" | "Location" | "Date" | "None";
  sortBy: "DateAsc" | "DateDesc" | "Reference" | "ResultLevel";
}

export interface ReportBuilderOptions {
  includeSpecifications: boolean;
  includeLimits: boolean;
  includeAnalyst: boolean;
  includeReviewer: boolean;
  includeApprovalInfo: boolean;
  includeAuditInfo: boolean;
  includeSignatures: boolean;
  includePageNumbers: boolean;
  templateId: string;
  format: ReportFormat;
}

export interface ReportPreviewGroup {
  groupKey: string;
  groupTitle: string;
  items: ResultRecordItem[];
}

export interface ReportPreviewData {
  reportTitle: string;
  reportPurpose: ReportPurpose;
  reportingPeriod: string;
  generatedAt: string;
  generatedByName: string;
  totalRecords: number;
  groups: ReportPreviewGroup[];
}

// ---------------------------------------------------------------------------
// Trending & Analysis Types
// ---------------------------------------------------------------------------

export type TrendFrequency = "Monthly" | "Weekly" | "Quarterly";

export type TrendComparisonMode = "None" | "Previous Period" | "Product Comparison" | "Location Comparison";

export interface TrendingCriteria {
  testCode: string;
  subjectName: string;
  category?: SampleCategory | "";
  location?: string;
  dateRange: "7d" | "30d" | "3m" | "6m" | "12m" | "custom";
  customFrom?: string;
  customTo?: string;
  frequency: TrendFrequency;
  compareWith: TrendComparisonMode;
  showMode: "All" | "ApprovedOnly" | "OutOfTrendOnly";
}

export interface NumericTrendPoint {
  date: string;
  label: string;
  value: number | null;
  reportedValue: string;
  mean: number | null;
  upperLimit: number | null;
  lowerLimit: number | null;
  alertLevel: number | null;
  actionLevel: number | null;
  resultLevel: ResultLevel;
  referenceNumber: string;
  recordId: number;
}

export interface QualitativeTrendPoint {
  date: string;
  label: string;
  detectedCount: number;
  absentCount: number;
  totalCount: number;
  referenceNumbers: string[];
}

export interface NumericStatisticsSummary {
  numberOfResults: number;
  minimum: string;
  maximum: string;
  mean: number | string;
  median: number | string;
  standardDeviation: number | string;
  percentWithinSpec: number;
  percentAlertLevel: number;
  percentActionLevel: number;
  outOfSpecCount: number;
}

export interface QualitativeStatisticsSummary {
  numberOfResults: number;
  conformCount: number;
  nonConformCount: number;
  detectedCount: number;
  absentCount: number;
  percentConform: number;
  percentDetected: number;
}

export interface TrendAnalysisResult {
  isNumeric: boolean;
  testCode: string;
  testDisplayName: string;
  subjectName: string;
  unit: string | null;
  numericPoints?: NumericTrendPoint[];
  qualitativePoints?: QualitativeTrendPoint[];
  numericStats?: NumericStatisticsSummary;
  qualitativeStats?: QualitativeStatisticsSummary;
}

// ---------------------------------------------------------------------------
// Saved Reports Types
// ---------------------------------------------------------------------------

export interface GeneratedReport {
  id: string;
  name: string;
  type: string;
  purpose: ReportPurpose;
  period: string;
  generatedOn: string;
  generatedBy: string;
  generatedByUserId: number;
  status: "Final" | "Draft" | "Archived";
  format: "PDF" | "Excel" | "CSV";
  criteriaJson: string;
  sourceRecordIds: number[];
  templateId?: string;
  version: string;
  auditTrailRef?: string;
  sizeBytes?: number;
}

export interface SavedReportConfiguration {
  id: string;
  name: string;
  reportType: ReportType;
  purpose: ReportPurpose;
  categories: SampleCategory[];
  criteria: Partial<ReportBuilderCriteria>;
  options: Partial<ReportBuilderOptions>;
  lastModified: string;
  modifiedBy: string;
  modifiedByUserId: number;
  status: "Active" | "Inactive";
}

// ---------------------------------------------------------------------------
// Analyst KPI & Performance Types
// ---------------------------------------------------------------------------

export interface WorkloadWeightConfig {
  testCode: string;
  testName: string;
  category: SampleCategory;
  workloadWeight: number;
  effectiveDate: string;
  status: "Active" | "Inactive";
  reasonForChange?: string;
  changedBy: string;
  changedAt: string;
}

export interface AnalystKpiFilters {
  dateRange: "7d" | "30d" | "3m" | "6m" | "12m" | "custom";
  customFrom?: string;
  customTo?: string;
  analystId?: number | "All";
  category?: SampleCategory | "All";
  location?: string | "All";
  testCode?: string | "All";
}

export interface AnalystComparisonRow {
  analystId: number;
  analystName: string;
  username: string;
  assigned: number;
  completed: number;
  workloadUnits: number;
  completionRatePercent: number;
  onTimePercent: number | null;
  avgTestingTatDays: number;
  reviewReturns: number | null;
  docCorrections: number | null;
  pending: number;
  overdue: number;
}

export interface AnalystPerformanceDetail {
  analystId: number;
  analystName: string;
  username: string;
  role: string;
  // A. Workload
  workload: {
    assignedTests: number;
    completedTests: number;
    pendingTests: number;
    overdueTests: number;
    configuredWorkloadUnits: number;
  };
  // B. Timeliness
  timeliness: {
    avgTestingTatDays: number;
    medianTestingTatDays: number;
    onTimePercent: string;
    overdueCount: number;
  };
  // C. Documentation / Review Quality (Strict attribution only)
  quality: {
    reviewReturns: string | number;
    documentationCorrections: string | number;
    calculationCorrections: string | number;
    missingMandatoryDataCount: number;
    firstTimeReviewAcceptanceRate: string;
    executionRelatedDeviations: string | number;
  };
  // D. Compliance & Competency (Only actual available values, no fake metrics)
  compliance: {
    trainingStatus: string;
    competencyStatus: string;
    sopComplianceIndex: string;
    lateEntriesCount: string | number;
  };
  // Data Coverage
  dataCoverage: {
    totalEvaluatedRecords: number;
    recordsWithCompleteTimestamps: number;
    coveragePercent: number;
  };
}

export interface SectionWorkflowBottleneck {
  testingQueueCount: number;
  testingQueueDeltaPercent: number;
  reviewQueueCount: number;
  reviewQueueDeltaPercent: number;
  approvalQueueCount: number;
  approvalQueueDeltaPercent: number;
}

export interface StageTatSummary {
  testingTatDays: number;
  reviewTatDays: number;
  approvalTatDays: number;
  totalTatDays: number;
}

export interface AnalystPerformanceDashboardData {
  // Top 6 Analyst KPI Cards
  testsAssigned: OverviewKpiCardData;
  testsCompleted: OverviewKpiCardData;
  completionRate: OverviewKpiCardData;
  onTimeCompletion: OverviewKpiCardData;
  averageTestingTat: OverviewKpiCardData;
  pendingOverdue: OverviewKpiCardData;
  // Lab Quality Signal (OOS is explicitly marked as NOT an analyst penalty)
  qualitySignalOos: OverviewKpiCardData;
  // Data Coverage Indicator
  dataCoveragePercent: number;
  dataCoverageNote: string;
  // Visualizations
  completedByMonth: { month: string; year2025: number; year2026: number }[];
  testsByCategory: { category: string; count: number; percentage: number; color: string }[];
  tatTrendByMonth: { month: string; tatDays: number }[];
  // Workflow Bottleneck & TAT Breakdown
  workflowBottleneck: SectionWorkflowBottleneck;
  tatSummary: StageTatSummary;
  // Comparison Table
  analystComparison: AnalystComparisonRow[];
  // Detailed Drill-Down for Selected Analyst
  selectedAnalystDetail: AnalystPerformanceDetail | null;
}
