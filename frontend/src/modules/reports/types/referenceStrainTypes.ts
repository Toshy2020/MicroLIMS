export interface ReferenceStrainSearchParams {
  search?: string;
  organismId?: number;
  approvalStatus?: string;
  isDestroyed?: boolean;
  receiptFromDate?: string;
  receiptToDate?: string;
  usageFromDate?: string;
  usageToDate?: string;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
}

export interface ReferenceStrainListItem {
  id: number;
  strainName: string;
  atccNumber: string | null;
  cryovialCode: string;
  manufacturerName: string;
  sourceMaterialName: string;
  sourceMaterialBatchNumber: string;
  receiptDate: string;
  preparedAt: string;
  expiryDate: string;
  numberOfVialsPrepared: number;
  vialsRemaining: number;
  storageCondition: string;
  approvalStatus: string;
  isDestroyed: boolean;
  preparedByName: string;
  approvedByName: string | null;
  approvedAt: string | null;
  directUsageCount: number;
}

export interface ReferenceStrainSearchResponse {
  items: ReferenceStrainListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReferenceStrainIdentityConfirmation {
  id: number;
  mediaLotNumber: string | null;
  mediaName: string | null;
  incubatorName: string | null;
  incubationStart: string;
  incubationEnd: string;
  observationText: string;
}

export interface ReferenceStrainThawEvent {
  id: number;
  thawedAt: string;
  thawedByName: string;
  notes: string | null;
}

export interface ReferenceStrainDirectUsage {
  challengeId: number;
  mediaId: number;
  mediaLotNumber: string;
  mediaType: string;
  evaluationType: string;
  challengeRole: string | null;
  outcome: string | null;
  readByName: string | null;
  readAt: string | null;
  evaluationStatus: string;
}

export interface ReferenceStrainDetail {
  id: number;
  cryovialCode: string;
  strainName: string;
  atccNumber: string | null;
  manufacturerName: string;
  sourceMaterialName: string;
  sourceMaterialBatchNumber: string;
  sourceMaterialReceivingDate: string;
  sourceMaterialQuantityReceived: number;
  preparedAt: string;
  expiryDate: string;
  numberOfVialsPrepared: number;
  vialsRemaining: number;
  storageCondition: string;
  physicalCheckConfirmed: boolean;
  physicalCheckText: string;
  approvalStatus: string;
  isDestroyed: boolean;
  preparedByName: string;
  approvedByName: string | null;
  approvedAt: string | null;
  identityConfirmations: ReferenceStrainIdentityConfirmation[];
  thawHistory: ReferenceStrainThawEvent[];
  directUsageLog: ReferenceStrainDirectUsage[];
  distinctQualifiedMediaLotsCount: number;
  indirectTestOrdersCount: number;
  indirectUsageSummary: string;
}

export interface OrganismOption {
  id: number;
  scientificName: string;
  atccNumber: string | null;
}

export interface ReferenceStrainFilterOptions {
  organisms: OrganismOption[];
}
