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
