export type EvaluationType = "GrowthPromotion" | "IndicationInhibition" | "EnrichmentCharacteristics";
export type EvaluationOutcome = "Conform" | "NonConform";
export type ApprovalGateStatus = "PendingReview" | "Approved" | "Rejected";

export interface MediaGptSearchParams {
  search?: string;
  mediaType?: string;
  evaluationType?: EvaluationType;
  outcome?: EvaluationOutcome;
  approvalStatus?: ApprovalGateStatus;
  fromDate?: string;
  toDate?: string;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
}

export interface MediaGptListItem {
  id: number;
  lotNumber: string;
  mediaType: string;
  preparedAt: string;
  expiryDate: string;
  evaluationType: string;
  evaluationStatus: string;
  evaluationOutcome: string | null;
  approvalStatus: string;
  isReleasedForUse: boolean;
  preparedByName: string;
  approvedByName: string | null;
  approvedAt: string | null;
  challengeCount: number;
  conformedChallengeCount: number;
}

export interface MediaGptSearchResponse {
  items: MediaGptListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface MediaGptChallengeDetail {
  id: number;
  organismName: string;
  atccNumber: string | null;
  challengeRole: string | null;
  strainSource: string | null;
  initialInoculum: string;
  oldMediaCount: number | null;
  newMediaCount: number | null;
  recoveryPercent: number | null;
  expectedMinRecoveryPercent: number | null;
  expectedMaxRecoveryPercent: number | null;
  referenceMediaLot: string | null;
  growthObserved: boolean | null;
  observedDescription: string | null;
  expectedDescription: string | null;
  isTurbid: boolean | null;
  outcome: string | null;
  readByName: string | null;
  readAt: string | null;
}

export interface MediaGptDetail {
  id: number;
  lotNumber: string;
  mediaType: string;
  manufacturerName: string;
  manufacturerLot: string;
  totalWeight: number;
  totalVolume: string;
  autoclaveName: string | null;
  autoclaveProgram: string;
  loadType: string;
  temperature: number;
  cycleTime: number;
  cycleNumber: number;
  ph: number;
  preparedAt: string;
  expiryDate: string;
  preparedByName: string;
  approvalStatus: string;
  isReleasedForUse: boolean;
  approvedByName: string | null;
  approvedAt: string | null;
  evaluationType: string;
  evaluationStatus: string;
  evaluationOutcome: string | null;
  evaluationCompletedAt: string | null;
  evaluationCompletedByName: string | null;
  challenges: MediaGptChallengeDetail[];
}

export interface MediaGptSummaryItem {
  mediaType: string;
  totalLots: number;
  conformedLots: number;
  nonConformedLots: number;
  pendingLots: number;
  passRatePercent: number;
}

export interface MediaGptSummary {
  totalLots: number;
  totalConformed: number;
  totalNonConformed: number;
  totalPending: number;
  overallPassRatePercent: number;
  mediaTypes: MediaGptSummaryItem[];
}

export interface MediaGptFilterOptions {
  mediaTypes: string[];
  evaluationTypes: string[];
}
