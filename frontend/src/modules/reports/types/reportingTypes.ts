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
  | "GPT"; // real backend SampleCategory value (used by WorkloadWeight config), but no Sample row is ever
           // received under it and Record Search has no query path for it yet - do not wire a Quick Report
           // tile to this until that's actually built. "ReferenceStrain" was removed from this union: it
           // was never added to the backend SampleCategory enum at all, so it could never have worked.

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

export interface RecentResultItem {
  id: number;
  referenceNumber: string;
  subjectName: string;
  subjectDetail: string | null;
  category: SampleCategory;
  testCode: string;
  testDisplayName: string;
  dateEntered: string;
  enteredBy: string;
  sampleStatus: SampleStatus;
  approvalStatus: string;
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
  recentResults: RecentResultItem[];
  qualitySignals: QualitySignalSummary;
}

// ---------------------------------------------------------------------------
// Trending & Analysis Types
// ---------------------------------------------------------------------------

export type TrendComparisonMode = "None" | "Previous Period" | "Product Comparison" | "Location Comparison";

export interface TrendingCriteria {
  testCode: string;
  subjectName: string;
  category?: SampleCategory | "";
  dateRange: "7d" | "30d" | "3m" | "6m" | "12m" | "custom";
  customFrom?: string;
  customTo?: string;
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

// One product/item or location/point's aggregate stats for a single test
// code, over the Trending panel's own criteria (category + date range) -
// backs the Quick Compare dialog. meanValue is populated only for a
// numeric (CountTest) testCode; percentDetected only for a qualitative
// one - a row only ever carries the field matching CompareResult.isNumeric.
export interface CompareSubjectStat {
  subjectName: string;
  testsEvaluated: number;
  meanValue: number | null;
  percentDetected: number | null;
  alertActionCount: number;
  oosCount: number;
  compliancePercent: number;
}

export interface CompareResult {
  testCode: string;
  testDisplayName: string;
  isNumeric: boolean;
  subjects: CompareSubjectStat[];
}

export interface QualitativeEventItem {
  id: number;
  referenceNumber: string;
  category: SampleCategory;
  subjectName: string;
  subjectDetail: string | null;
  testCode: string;
  testDisplayName: string;
  reportedValue: string;
  resultEnteredAt: string;
  resultEnteredByName: string;
  sampleStatus: SampleStatus;
  approvalStatus: string;
  approvedByName: string | null;
  approvedAt: string | null;
}

export interface QualitativeEventResult {
  testCode: string;
  testDisplayName: string;
  events: QualitativeEventItem[];
}
